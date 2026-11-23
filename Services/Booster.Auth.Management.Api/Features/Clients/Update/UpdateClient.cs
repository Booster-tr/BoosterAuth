using Microsoft.EntityFrameworkCore;
using Booster.Auth.Management.Api.Features.UserPools.Policies;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using FluentValidation;
using Mapster;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.Clients.Update;

public class UpdateClientEndpoint : Endpoint<UpdateClientCommand, UpdateClientResponse>
{
    public override void Configure()
    {
        Put("");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.ClientType, ManagementPolicies.ClientTarget, AuthConstants.ManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Client bilgilerini günceller";
            s.Description = "Görünen ad, grant tipleri ve yönlendirme adreslerini günceller. " +
                            "ClientKey ve havuz değiştirilemez.";
        });
    }

    public override async Task HandleAsync(UpdateClientCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateClientCommand(
    Guid ClientId,
    string DisplayName,
    OAuthGrantType AllowedGrantTypes,
    List<string> RedirectUris,
    List<string> PostLogoutRedirectUris,
    List<string> AllowedAudiences,
    bool RequireConsent) : ICommand<UpdateClientResponse>;

public readonly record struct UpdateClientResponse(Guid Id);

public class UpdateClientCommandHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IClientDataCacheStore clientDataCacheStore,
    AuthNDbContext db)
    : ICommandHandler<UpdateClientCommand, UpdateClientResponse>
{
    public async Task<UpdateClientResponse> ExecuteAsync(UpdateClientCommand command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(c => c.Id == command.ClientId, cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

        // Redirect politikasi yalnizca YENI eklenen adreslere uygulanir: politika sonradan acildiysa mevcut
        // (uymayan) adresler client'in baska alanlarini guncellemeyi engellemesin.
        var existing = client!.RedirectUris.Concat(client.PostLogoutRedirectUris).ToHashSet(StringComparer.Ordinal);
        var added = command.RedirectUris.Concat(command.PostLogoutRedirectUris).Where(u => !existing.Contains(u));
        var redirectPolicy = await db.Set<UserPool>().AsNoTracking().Where(p => p.Id == client.UserPoolId)
            .Select(p => p.Policy.Redirect).FirstOrDefaultAsync(ct);
        RedirectPolicyGuard.EnsureAllowed(redirectPolicy, added);

        command.Adapt(client);

        await clientRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidateClientAsync(client!.ClientKey, ct);

        return new UpdateClientResponse(client!.Id);
    }
}

public class UpdateClientValidator : Validator<UpdateClientCommand>
{
    public UpdateClientValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.AllowedGrantTypes)
            .NotEqual(OAuthGrantType.None).WithMessage(AuthNMessageKeys.ClientGrantTypeRequired);

        RuleFor(x => x.RedirectUris)
            .NotEmpty()
            .When(x => x.AllowedGrantTypes.HasFlag(OAuthGrantType.AuthorizationCode))
            .WithMessage(AuthNMessageKeys.ClientRedirectUriRequired);

        RuleForEach(x => x.RedirectUris)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage(AuthNMessageKeys.ClientRedirectUriInvalid);

        RuleForEach(x => x.PostLogoutRedirectUris)
            .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
            .WithMessage(AuthNMessageKeys.ClientRedirectUriInvalid);
    }
}
