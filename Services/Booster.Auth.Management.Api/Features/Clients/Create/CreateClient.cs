using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Database.Repositories;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using FluentValidation;
using Mapster;
using Booster.Auth.Management.Api.Constants;

namespace Booster.Auth.Management.Api.Features.Clients.Create;

public class CreateClientEndpoint : Endpoint<CreateClientCommand, CreateClientResponse>
{
    public override void Configure()
    {
        Post("");
        Group<ClientsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));
        Summary(s =>
        {
            s.Summary = "Yeni OAuth client oluşturur";
            s.Description = "Verilen havuz altında bir uygulama kaydeder. Client anahtarı istekten gelir " +
                            "ve sistem genelinde benzersizdir. Secret tutulmaz.";
        });
    }

    public override async Task HandleAsync(CreateClientCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateClientCommand(
    Guid UserPoolId,
    string ClientKey,
    string DisplayName,
    OAuthGrantType AllowedGrantTypes,
    List<string> RedirectUris,
    List<string> PostLogoutRedirectUris,
    List<string> AllowedAudiences,
    bool RequireConsent) : ICommand<CreateClientResponse>;

public readonly record struct CreateClientResponse(Guid Id, string ClientKey);

public class CreateClientCommandHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore,
    IAccessManager accessManager)
    : ICommandHandler<CreateClientCommand, CreateClientResponse>
{
    public async Task<CreateClientResponse> ExecuteAsync(CreateClientCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(
            p => p.Id == command.UserPoolId, enableTracking: false, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        if (pool!.Status != EntityStatus.Active)
            throw new BusinessException(AuthNMessageKeys.ParentNotActive);

        command = command with { ClientKey = command.ClientKey.Trim().ToLowerInvariant() };

        var keyExists = await clientRepository.AnyAsync(
            c => c.ClientKey == command.ClientKey, cancellationToken: ct);

        if (keyExists)
            throw new BusinessException(AuthNMessageKeys.ClientKeyAlreadyExists);

        var client = command.Adapt<Client>();
        client.SetId();
        client.Status = EntityStatus.Active;

        await clientRepository.AddAsync(client, ct);
        await clientRepository.SaveChangesAsync(ct);

        await accessManager.LinkParentAsync(
            AuthConstants.ClientType, client.Id, AuthConstants.PoolType, client.UserPoolId, ct);

        await clientDataCacheStore.InvalidateClientAsync(client.ClientKey, ct);

        return new CreateClientResponse(client.Id, client.ClientKey);
    }
}

public class CreateClientValidator : Validator<CreateClientCommand>
{
    public CreateClientValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.ClientKey)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(200).WithMessage(ValidationErrors.MaxLengthError)
            .Matches("^[a-z0-9-]+$").WithMessage(AuthNMessageKeys.ClientKeyInvalidFormat);

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
