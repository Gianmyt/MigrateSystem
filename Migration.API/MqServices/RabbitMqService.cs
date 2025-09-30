using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Migration.API.MqServices
{
    public class RabbitMqService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ConnectionFactory _factory;
        private readonly string _queueName = "migration_queue";
        private IConnection? _connection;
        private IChannel? _channel;
        public RabbitMqService(IServiceScopeFactory scopeFactory, IConnection? connection)
        {
            _scopeFactory = scopeFactory;
            _connection = connection;
            InitRabbitMQ();

        }

        private void InitRabbitMQ()
        {
            //var factory = new ConnectionFactory { HostName = "rabbitmq", UserName = "admin", Password = "admin" };
            //_connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            //_connection=_
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
            _channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        }

        public async Task SendMessageAsync<T>(T message)
        {

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            await _channel.BasicPublishAsync(exchange: "", routingKey: _queueName, body: body);
        }
    }
}

