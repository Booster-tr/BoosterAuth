namespace Booster.Auth.Manager.RequestContexts;

public static class RequestContextAccessorExtensions
{
    public static RequestContext Context(this IRequestContextAccessor accessor)
        => accessor.RequestContext ?? RequestContext.Empty;

    public static IDisposable BeginSystemContext(
        this IRequestContextAccessor accessor,
        Guid isolation = default)
    {
        var previous = accessor.RequestContext;

        accessor.RequestContext = RequestContext.ForSystem(isolation);

        return new ContextScope(accessor, previous);
    }

    public static IDisposable BeginIsolationScope(
    this IRequestContextAccessor accessor,
    Guid isolation)
    {
        var previous = accessor.RequestContext;
        var current = previous ?? RequestContext.Empty;
        accessor.RequestContext = current.WithIsolation(isolation);
        return new ContextScope(accessor, previous);
    }

    private sealed class ContextScope(IRequestContextAccessor accessor, RequestContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            accessor.RequestContext = previous;
        }
    }
}

