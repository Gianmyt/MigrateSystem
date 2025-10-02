using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Migration.Infrastructure.services;
using Migration.Worker.services;
using RabbitMQ.Client;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<MigrationDbContext>(options =>
            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddSingleton<IConnection>(sp =>
        {

            var config = sp.GetRequiredService<IConfiguration>();
            
            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:HostName"],
                UserName = config["RabbitMQ:UserName"],
                Password = config["RabbitMQ:Password"]
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddHostedService<MigrationWorkerService>();
        services.AddHostedService<ReservationCleanupService>();
    })
    .Build();



await host.RunAsync();