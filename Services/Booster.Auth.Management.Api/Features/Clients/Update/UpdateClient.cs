using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Clients.Update;

public class UpdateClientEndpoint : Endpoint<UpdateClientCommand, UpdateClientResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}");
        Group<ClientsEndpointGroup>();
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
    Guid Id,
    string DisplayName,
    string? LogoUri,
    OAuthGrantType AllowedGrantTypes,
    List<string> RedirectUris,
    List<string> PostLogoutRedirectUris,
    List<string> AllowedAudiences,
    bool RequireConsent) : ICommand<UpdateClientResponse>;

public readonly record struct UpdateClientResponse(Guid Id);

public class UpdateClientCommandHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdateClientCommand, UpdateClientResponse>
{
    public async Task<UpdateClientResponse> ExecuteAsync(UpdateClientCommand command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(c => c.Id == command.Id, cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

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
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.LogoUri).MaximumLength(2000).WithMessage(ValidationErrors.MaxLengthError);

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
