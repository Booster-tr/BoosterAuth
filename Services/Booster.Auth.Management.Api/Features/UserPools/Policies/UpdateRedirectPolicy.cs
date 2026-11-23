using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Licensing.Domains;
using FastEndpoints;
using FluentValidation;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.UserPools.Policies;

/// <summary>
/// Havuzun redirect domain politikasini gunceller. Mevcut client'lar ETKILENMEZ (girisleri kesilmez); yanit, politikaya
/// uymayan mevcut adresleri listeler ki panel uyari gostersin.
/// </summary>
public class UpdateRedirectPolicyEndpoint : Endpoint<UpdateRedirectPolicyCommand, UpdateRedirectPolicyResponse>
{
    public override void Configure()
    {
        Put("policy/redirect");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "Havuzun izin verilen redirect domain politikasini gunceller");
    }

    public override async Task HandleAsync(UpdateRedirectPolicyCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateRedirectPolicyCommand(
    Guid UserPoolId,
    bool Enabled,
    List<string> AllowedDomains,
    bool AllowTestHosts,
    bool AllowCustomSchemes) : ICommand<UpdateRedirectPolicyResponse>;

/// <param name="NonCompliantClients">Politikaya uymayan adresi olan mevcut client'lar (yalnizca uyari).</param>
public sealed record UpdateRedirectPolicyResponse(Guid Id, IReadOnlyList<RedirectPolicyViolation> NonCompliantClients);

public sealed record RedirectPolicyViolation(Guid ClientId, string ClientKey, IReadOnlyList<string> Uris);

public class UpdateRedirectPolicyCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    AuthNDbContext db)
    : ICommandHandler<UpdateRedirectPolicyCommand, UpdateRedirectPolicyResponse>
{
    public async Task<UpdateRedirectPolicyResponse> ExecuteAsync(UpdateRedirectPolicyCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var policy = pool!.Policy.Adapt<UserPoolPolicy>();
        policy.Redirect = new RedirectPolicy
        {
            Enabled = command.Enabled,
            AllowedDomains = command.AllowedDomains
                .Select(DomainRules.NormalizeHost).OfType<string>().Distinct(StringComparer.Ordinal).ToList(),
            AllowTestHosts = command.AllowTestHosts,
            AllowCustomSchemes = command.AllowCustomSchemes,
        };
        pool.Policy = policy;

        await userPoolRepository.SaveChangesAsync(ct);

        // Politika IdP'ye gitmez (yalnizca yapilandirma aninda Management'ta kullanilir); onbellek gecersizlestirmeye gerek yok.
        var clients = await db.Set<Client>().AsNoTracking().Where(c => c.UserPoolId == pool.Id)
            .Select(c => new { c.Id, c.ClientKey, c.RedirectUris, c.PostLogoutRedirectUris }).ToListAsync(ct);

        var violations = clients
            .Select(c => new RedirectPolicyViolation(c.Id, c.ClientKey,
                RedirectPolicyChecker.Violations(policy.Redirect, c.RedirectUris.Concat(c.PostLogoutRedirectUris))))
            .Where(v => v.Uris.Count > 0)
            .ToList();

        return new UpdateRedirectPolicyResponse(pool.Id, violations);
    }
}

public class UpdateRedirectPolicyValidator : Validator<UpdateRedirectPolicyCommand>
{
    public UpdateRedirectPolicyValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.AllowedDomains).NotNull().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.AllowedDomains.Count).LessThanOrEqualTo(100).WithMessage(ValidationErrors.MaxLengthError);
        RuleForEach(x => x.AllowedDomains)
            .Must(d => DomainRules.NormalizeHost(d) is { } h && h.Contains('.') && !DomainRules.IsTestHost(h))
            .WithMessage(AuthNMessageKeys.RedirectDomainInvalid);
        // Acik politika + bos liste + test/ozel sema kapali = hicbir adres eklenemez; bunu bilerek isteyen olmaz.
        RuleFor(x => x)
            .Must(x => !x.Enabled || x.AllowedDomains.Count > 0 || x.AllowTestHosts || x.AllowCustomSchemes)
            .WithName(nameof(UpdateRedirectPolicyCommand.AllowedDomains))
            .WithMessage(AuthNMessageKeys.RedirectDomainsRequired);
    }
}
