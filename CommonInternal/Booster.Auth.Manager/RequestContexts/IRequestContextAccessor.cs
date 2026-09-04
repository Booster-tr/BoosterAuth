namespace Booster.Auth.Manager.RequestContexts;

public interface IRequestContextAccessor
{
    RequestContext? RequestContext { get; set; }
}

public class RequestContextAccessor : IRequestContextAccessor
{
    private static readonly AsyncLocal<RequestContextHolder> _requestContextCurrent = new();

    public RequestContext? RequestContext
    {
        get => _requestContextCurrent.Value?.Context;
        set
        {
            var holder = _requestContextCurrent.Value;
            if (holder != null)
                holder.Context = null;

            if (value != null)
                _requestContextCurrent.Value = new RequestContextHolder { Context = value };
        }
    }

    private sealed class RequestContextHolder
    {
        public RequestContext? Context;
    }
}

public class DummyRequestContextAccessor : IRequestContextAccessor
{
    public RequestContext? RequestContext { get; set; } = RequestContext.ForSystem();
}
