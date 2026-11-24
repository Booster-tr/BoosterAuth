using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Booster.Observability;

/// <summary>Yalnizca kosula uyan kayitlari icteki isleyiciye (disa aktarici) iletir: denetim / isletim akislarini ayirir.</summary>
internal sealed class FilteringLogProcessor(BaseProcessor<LogRecord> inner, Func<LogRecord, bool> predicate)
    : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        if (predicate(data))
            inner.OnEnd(data);
    }

    protected override bool OnForceFlush(int timeoutMilliseconds) => inner.ForceFlush(timeoutMilliseconds);

    protected override bool OnShutdown(int timeoutMilliseconds) => inner.Shutdown(timeoutMilliseconds);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }
}
