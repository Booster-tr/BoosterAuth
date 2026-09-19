using Booster.Amqp.Bus;
using Booster.Amqp.Constants;
using Booster.Amqp.Contexts;
using Booster.Amqp.Middlewares;
using Booster.Amqp.Providers.ProducerProvider;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Amqp.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddKafkaBus<TContext>(this IServiceCollection services, AmqpSettings amqpSettings,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IRiderRegistrationConfigurator>? configureRider = null,
        Action<IKafkaFactoryConfigurator, IRiderRegistrationContext>? configureKafka = null)
        where TContext : DbContext
    {
        services.AddScoped<IKafkaProducerProvider, KafkaProducerProvider>();

        services.AddScoped<IEventBus, MassTransitEventBus>();

        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.AddEntityFrameworkOutbox<TContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.AddRider(rider =>
            {
                configureRider?.Invoke(rider);

                rider.UsingKafka((context, cfg) =>
                {

                    cfg.Host(amqpSettings.BootstrapServers);

                    configureKafka?.Invoke(cfg, context);
                });
            });

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddOptions<MassTransitHostOptions>()
            .Configure(options =>
            {
                options.WaitUntilStarted = true;
            });

        
        return services;
    }

    public static IServiceCollection AddBoosterCorrelationContext(this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationContext, CorrelationContext>();
        return services;
    }

    public static WebApplication UseBoosterCorrelationContext(this WebApplication app)
    {
        app.UseMiddleware<CorrelationMiddleware>();
        return app;
    }
}

