using Infrastructure;
using Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Migration.Infrastructure.models;
using Migration.Infrastructure.services;
using Migration.Infrastructure.Utilities;
using Migration.Worker.services;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

using Xunit;

namespace TestProject
{
    public class MigrationWorkerServiceTests
    {
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
        private readonly Mock<IServiceScope> _scopeMock = new();
        private readonly Mock<IServiceProvider> _providerMock = new();
        private readonly Mock<MigrationDbContext> _dbMock = new();
        private readonly Mock<IAuditLogService> _auditLogMock = new();
        private readonly Mock<ICollection> _connectionMock = new();
        private readonly Mock<IChannel> _channelMock = new();

        private MigrationWorkerServiceTests CreateService()
        {
            _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
            _scopeMock.Setup(s => s.ServiceProvider).Returns(_providerMock.Object);
            _providerMock.Setup(p => p.GetService(typeof(MigrationDbContext))).Returns(_dbMock.Object);
            _providerMock.Setup(p => p.GetService(typeof(IAuditLogService))).Returns(_auditLogMock.Object);

            _connectionMock.Setup(c => c.CreateChannelAsync()).ReturnsAsync(_channelMock.Object);
            _channelMock.Setup(c => c.QueueDeclareAsync(It.IsAny<string>(), true, false, false, null));

            return new MigrationWorkerService(_scopeFactoryMock.Object, _connectionMock.Object);
        }

        private static BasicDeliverEventArgs CreateEventArgs(object mqModel)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(mqModel));
            return new BasicDeliverEventArgs
            {
                Body = new ReadOnlyMemory<byte>(body),
                DeliveryTag = 1
            };
        }

        [Fact]
        public async Task ExecuteAsync_NullUser_AcksAndReturns()
        {
            var service = CreateService();
            var mqModel = new MqModel { OldUser = null };
            var eventArgs = CreateEventArgs(mqModel);

            AsyncEventHandler<BasicDeliverEventArgs> handler = null;
            _channelMock.Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), false, It.IsAny<IBasicConsumer>()))
                .Callback<string, bool, IBasicConsumer>((q, a, cons) =>
                {
                    handler = ((AsyncEventingBasicConsumer)cons).ReceivedAsync;
                })
                .Returns(Task.CompletedTask);

            await service.StartAsync(CancellationToken.None);
            Assert.NotNull(handler);

            await handler.Invoke(null, eventArgs);

            _channelMock.Verify(c => c.BasicAckAsync(1, false), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_SlotUnavailable_NacksAndLogs()
        {
            var service = CreateService();
            var oldUser = new OldUser { Id = 42 };
            var mqModel = new MqModel { OldUser = oldUser, Forced = false, SlotId = 99 };
            var eventArgs = CreateEventArgs(mqModel);

            var slots = new TestAsyncEnumerable<MigrationSlot>(Array.Empty<MigrationSlot>());
            _dbMock.Setup(d => d.MigrationSlots.FromSqlRaw(It.IsAny<string>(), It.IsAny<object[]>()))
                .Returns(slots);

            AsyncEventHandler<BasicDeliverEventArgs> handler = null;
            _channelMock.Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), false, It.IsAny<IBasicConsumer>()))
                .Callback<string, bool, IBasicConsumer>((q, a, cons) =>
                {
                    handler = ((AsyncEventingBasicConsumer)cons).ReceivedAsync;
                })
                .Returns(Task.CompletedTask);

            await service.StartAsync(CancellationToken.None);
            Assert.NotNull(handler);

            await handler.Invoke(null, eventArgs);

            _auditLogMock.Verify(a => a.LogAsync("SlotUnavailable", "WorkerService", $"User={oldUser.Id}", "Failed"), Times.Once);
            _channelMock.Verify(c => c.BasicNackAsync(1, false, true), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_AlreadyMigrated_RollsBackAcksAndLogs()
        {
            var service = CreateService();
            var oldUser = new OldUser { Id = 7 };
            var mqModel = new MqModel { OldUser = oldUser, Forced = true };
            var eventArgs = CreateEventArgs(mqModel);

            var slot = new MigrationSlot { Id = 1, IsOccupied = false };
            var slots = new TestAsyncEnumerable<MigrationSlot>(new[] { slot });
            _dbMock.Setup(d => d.MigrationSlots.FromSqlRaw(It.IsAny<string>())).Returns(slots);

            _dbMock.Setup(d => d.UserMigrations.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<UserMigration, bool>>>(), default))
                .ReturnsAsync(true);

            var txMock = new Mock<IDbContextTransaction>();
            _dbMock.Setup(d => d.Database.BeginTransactionAsync(default)).ReturnsAsync(txMock.Object);

            AsyncEventHandler<BasicDeliverEventArgs> handler = null;
            _channelMock.Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), false, It.IsAny<IBasicConsumer>()))
                .Callback<string, bool, IBasicConsumer>((q, a, cons) =>
                {
                    handler = ((AsyncEventingBasicConsumer)cons).ReceivedAsync;
                })
                .Returns(Task.CompletedTask);

            await service.StartAsync(CancellationToken.None);
            Assert.NotNull(handler);

            await handler.Invoke(null, eventArgs);

            _auditLogMock.Verify(a => a.LogAsync("MigrationSkipped", "WorkerService", $"UserId={oldUser.Id}", "AlreadyMigrated"), Times.Once);
            txMock.Verify(t => t.RollbackAsync(default), Times.Once);
            _channelMock.Verify(c => c.BasicAckAsync(1, false), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ArgumentExceptionDuringMapping_AcksAndLogs()
        {
            var service = CreateService();
            var oldUser = new OldUser { Id = 8 };
            var mqModel = new MqModel { OldUser = oldUser, Forced = true };
            var eventArgs = CreateEventArgs(mqModel);

            var slot = new MigrationSlot { Id = 2, IsOccupied = false };
            var slots = new TestAsyncEnumerable<MigrationSlot>(new[] { slot });
            _dbMock.Setup(d => d.MigrationSlots.FromSqlRaw(It.IsAny<string>())).Returns(slots);

            _dbMock.Setup(d => d.UserMigrations.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<UserMigration, bool>>>(), default))
                .ReturnsAsync(false);

            var txMock = new Mock<IDbContextTransaction>();
            _dbMock.Setup(d => d.Database.BeginTransactionAsync(default)).ReturnsAsync(txMock.Object);

            // Simulate UserMapper.Map throws
            UserMapper.Map = _ => throw new ArgumentException("bad user");

            AsyncEventHandler<BasicDeliverEventArgs> handler = null;
            _channelMock.Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), false, It.IsAny<IBasicConsumer>()))
                .Callback<string, bool, IBasicConsumer>((q, a, cons) =>
                {
                    handler = ((AsyncEventingBasicConsumer)cons).ReceivedAsync;
                })
                .Returns(Task.CompletedTask);

            await service.StartAsync(CancellationToken.None);
            Assert.NotNull(handler);

            await handler.Invoke(null, eventArgs);

            _auditLogMock.Verify(a => a.LogAsync("ValidationFailed", "WorkerService",
                $"UserId={oldUser.Id}, Error=bad user", "Failed"), Times.Once);
            _channelMock.Verify(c => c.BasicAckAsync(1, false), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_SuccessfulMigration_CommitsAcksAndLogs()
        {
            var service = CreateService();
            var oldUser = new OldUser { Id = 9 };
            var mqModel = new MqModel { OldUser = oldUser, Forced = true };
            var eventArgs = CreateEventArgs(mqModel);

            var slot = new MigrationSlot { Id = 3, IsOccupied = false };
            var slots = new TestAsyncEnumerable<MigrationSlot>(new[] { slot });
            _dbMock.Setup(d => d.MigrationSlots.FromSqlRaw(It.IsAny<string>())).Returns(slots);

            _dbMock.Setup(d => d.UserMigrations.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<UserMigration, bool>>>(), default))
                .ReturnsAsync(false);

            var txMock = new Mock<IDbContextTransaction>();
            _dbMock.Setup(d => d.Database.BeginTransactionAsync(default)).ReturnsAsync(txMock.Object);

            UserMapper.Map = u => new NewUser(); // Simulate mapping

            AsyncEventHandler<BasicDeliverEventArgs> handler = null;
            _channelMock.Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), false, It.IsAny<IBasicConsumer>()))
                .Callback<string, bool, IBasicConsumer>((q, a, cons) =>
                {
                    handler = ((AsyncEventingBasicConsumer)cons).ReceivedAsync;
                })
                .Returns(Task.CompletedTask);

            await service.StartAsync(CancellationToken.None);
            Assert.NotNull(handler);

            await handler.Invoke(null, eventArgs);

            txMock.Verify(t => t.CommitAsync(default), Times.Once);
            _channelMock.Verify(c => c.BasicAckAsync(1, false), Times.Once);
            _auditLogMock.Verify(a => a.LogAsync("MigrationSuccess", "WorkerService", $"UserId={oldUser.Id}", "Success"), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ExceptionDuringMigration_NacksAndLogs()
        {
            var service = CreateService();
            var oldUser = new OldUser { Id = 10 };
            var mqModel = new MqModel { OldUser = oldUser, Forced = true };
            var eventArgs = CreateEventArgs(mqModel);

            var slot = new MigrationSlot { Id = 4, IsOccupied = false };
            var slots = new TestAsyncEnumerable<MigrationSlot>(new[] { slot });
            _dbMock.Setup(d => d.MigrationSlots.FromSqlRaw(It.IsAny<string>())).Returns(slots);

            _dbMock.Setup(d => d.UserMigrations.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<UserMigration, bool>>>(), default))
                .ThrowsAsync(new Exception("db error"));

            var txMock = new Mock<IDbContextTransaction>();
            _dbMock.Setup(d => d.Database.BeginTransactionAsync(default)).ReturnsAsync(txMock.Object);

            AsyncEventHandler<BasicDeliverEventArgs> handler = null;
            _channelMock.Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), false, It.IsAny<IBasicConsumer>()))
                .Callback<string, bool, IBasicConsumer>((q, a, cons) =>
                {
                    handler = ((AsyncEventingBasicConsumer)cons).ReceivedAsync;
                })
                .Returns(Task.CompletedTask);

            await service.StartAsync(CancellationToken.None);
            Assert.NotNull(handler);

            await handler.Invoke(null, eventArgs);

            _auditLogMock.Verify(a => a.LogAsync("MigrationFailed", "WorkerService",
                $"UserId={oldUser.Id}, Error=db error", "Failed"), Times.Once);
            _channelMock.Verify(c => c.BasicNackAsync(1, false, true), Times.Once);
        }

        // Helper for async queryable
        private class TestAsyncEnumerable<T> : IAsyncEnumerable<T>, IQueryable<T>
        {
            private readonly IEnumerable<T> _data;
            public TestAsyncEnumerable(IEnumerable<T> data) => _data = data;
            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                => _data.ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
            public Type ElementType => typeof(T);
            public System.Linq.Expressions.Expression Expression => _data.AsQueryable().Expression;
            public IQueryProvider Provider => _data.AsQueryable().Provider;
            public IEnumerator<T> GetEnumerator() => _data.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}