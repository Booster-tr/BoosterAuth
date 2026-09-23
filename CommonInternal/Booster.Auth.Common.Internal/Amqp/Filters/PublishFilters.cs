using Booster.Amqp.Contexts;
using Booster.Auth.Manager.RequestContexts;
using MassTransit;

namespace Booster.Auth.Common.Internal.Amqp.Filters;

/// <summary>
/// Yayınlanan her mesaja isteğin correlation id'sini başlık olarak ekler.
/// <para>
/// Olmazsa kuyruğun öbür ucundaki kayıt, onu tetikleyen HTTP isteğiyle
/// ilişkilendirilemez; tek bir kullanıcı işleminin izi servis sınırında kopar.
/// </para>
/// </summary>
public class CorrelationPublishFilter<T>(ICorrelationContext correlationContext) : IFilter<SendContext<T>>
    where T : class
{
    public const string HeaderKey = "CorrelationId";

    public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        var correlationId = correlationContext.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            context.Headers.Set(HeaderKey, correlationId);

            if (Guid.TryParse(correlationId, out var parsed))
                context.CorrelationId = parsed;
        }

        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("correlationPublish");
}

/// <summary>
/// Yayınlanan mesaja izolasyon ve kimlik bağlamını başlık olarak ekler.
/// <para>
/// Tüketici ayrı bir süreçtir ve <see cref="RequestContext"/>'i göremez; bu
/// bilgi mesajla birlikte taşınmazsa tüketici hangi havuz adına çalıştığını
/// bilemez ve izolasyonlu yazma yapamaz.
/// </para>
/// </summary>
public class RequestContextPublishFilter<T>(IRequestContextAccessor requestContextAccessor) : IFilter<SendContext<T>>
    where T : class
{
    public const string IsolationHeaderKey = "Booster-Isolation";
    public const string PrincipalHeaderKey = "Booster-Principal";
    public const string LanguageHeaderKey = "Booster-Accept-Language";

    public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        var requestContext = requestContextAccessor.RequestContext;

        if (requestContext is not null)
        {
            if (requestContext.Isolation != Guid.Empty)
                context.Headers.Set(IsolationHeaderKey, requestContext.Isolation.ToString());

            if (requestContext.PrincipalId != Guid.Empty)
                context.Headers.Set(PrincipalHeaderKey, requestContext.PrincipalId.ToString());

            context.Headers.Set(LanguageHeaderKey, requestContext.AcceptLanguage);
        }

        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("requestContextPublish");
}
