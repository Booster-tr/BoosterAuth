using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;

namespace Booster.Authorization.Fga.Grpc;

internal sealed class BearerTokenForwardingInterceptor(IHttpContextAccessor httpContextAccessor)
    : Interceptor
{
    private const string AuthorizationHeader = "Authorization";

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var token = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(token))
            return continuation(request, context);

        var headers = new Metadata();

        if (context.Options.Headers is { } existing)
        {
            foreach (var entry in existing)
                headers.Add(entry);
        }

        if (headers.Get(AuthorizationHeader) is null)
            headers.Add(AuthorizationHeader, token);

        var forwarded = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));

        return continuation(request, forwarded);
    }
}
