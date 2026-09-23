using MassTransit;

namespace Booster.Logging.BoosterAmqp;

public abstract class LogEventConsumer : IConsumer<LogCreateEvent>
{
    protected ConsumeContext<LogCreateEvent>? Context;
    public async Task Consume(ConsumeContext<LogCreateEvent> context)
    {
        Context = context;
        await HandleAsync(context.Message, context.CancellationToken);
        
    }

    protected abstract Task HandleAsync(LogCreateEvent log, CancellationToken cancellationToken);
}
