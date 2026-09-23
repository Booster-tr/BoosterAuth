using Booster.Database.Repositories;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Amqp.Contexts;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Logging.BoosterAmqp;
using Booster.LoggingService.Features._base;
using Booster.LoggingService.Features.Logs._base.Entities;

namespace Booster.LoggingService.Amqp.Consumers;

/// <summary>
/// <c>log-create-topic</c>'i dinleyip gelen logu DOĞRUDAN DB'ye yazar (ayrı bir command/event akışı yok).
/// Booster.Logging.BoosterAmqp'nin abstract <see cref="LogEventConsumer"/>'ını inherit eder; kayıt host
/// MassTransit kurulumunda <c>AddLogConsumer</c> / <c>AddLogConsumerEndpoint</c> ile yapılır.
/// </summary>
public sealed class LogPersistConsumer(
    IAsyncRepository<Log, LoggerDbContext> repository,
    ICorrelationContext correlationContext)
    : LogEventConsumer
{
    protected override async Task HandleAsync(LogCreateEvent log, CancellationToken cancellationToken)
    {
        var props = log.Properties;

        // Generic event → Log entity. Çekirdek alanlar doğrudan; bağlam (user/ip/service) opak Properties'ten.
        // Log izolasyon eksenine bağlı değildir: olay AMQP'den gelir, tenant/client bağlamı taşımaz.
        var entity = new Log
        {
            Level = log.Level,
            Message = log.Message,
            Exception = string.IsNullOrEmpty(log.Exception) ? nameof(BusinessException) : log.Exception,
            Timestamp = log.Timestamp,
            UserId = GetGuid(props, "UserId"),
            IpAddress = GetString(props, "IpAddress") ?? "unknown",
            ServiceName = GetString(props, "ServiceName") ?? string.Empty,
            CorrelationId = correlationContext.CorrelationId
        };
        entity.SetId();

        await repository.AddAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string? GetString(IReadOnlyDictionary<string, string> props, string key)
        => props.TryGetValue(key, out var v) ? v : null;

    private static Guid? GetGuid(IReadOnlyDictionary<string, string> props, string key)
        => props.TryGetValue(key, out var v) && Guid.TryParse(v, out var g) ? g : null;
}
