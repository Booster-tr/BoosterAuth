using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Clients.SetTokenScopePolicy;

public class SetClientTokenScopePolicyEndpoint : Endpoint<SetClientTokenScopePolicyCommand, SetClientTokenScopePolicyResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}/token-scope-policy");
        Group<ClientsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Client token scope policy listesini değiştirir";
            s.Description = "Listenin TAMAMI verilenle değiştirilir (replace); kısmi güncelleme yoktur. " +
                            "En fazla 5 kalem olabilir. Yetki kontrolü isteyen bir kalemde gerekli izinler zorunludur.";
        });
    }

    public override async Task HandleAsync(SetClientTokenScopePolicyCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record SetClientTokenScopePolicyCommand(
    Guid Id,
    List<TokenScopePolicyItem> TokenScopePolicy) : ICommand<SetClientTokenScopePolicyResponse>;

public readonly record struct SetClientTokenScopePolicyResponse(Guid Id);

public class SetClientTokenScopePolicyCommandHandler(
    IAsyncRepository<Client, AuthNDbContext> clientRepository)
    : ICommandHandler<SetClientTokenScopePolicyCommand, SetClientTokenScopePolicyResponse>
{
    public async Task<SetClientTokenScopePolicyResponse> ExecuteAsync(
        SetClientTokenScopePolicyCommand command, CancellationToken ct)
    {
        var client = await clientRepository.GetAsync(c => c.Id == command.Id, cancellationToken: ct);
        client.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ClientNotFound);

        client!.TokenScopePolicy = command.TokenScopePolicy;

        await clientRepository.SaveChangesAsync(ct);

        return new SetClientTokenScopePolicyResponse(client.Id);
    }
}

public class SetClientTokenScopePolicyValidator : Validator<SetClientTokenScopePolicyCommand>
{
    public SetClientTokenScopePolicyValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.TokenScopePolicy)
            .NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError)
            .Must(p => p.Count <= 5).WithMessage(AuthNMessageKeys.TokenScopePolicyMax);

        RuleForEach(x => x.TokenScopePolicy).ChildRules(item =>
        {
            item.RuleFor(i => i.Key)
                .NotEmpty().WithMessage(AuthNMessageKeys.TokenScopePolicyKeyRequired);

            // Kontrol isteniyorsa neyin aranacağı belirtilmek zorundadır.
            item.RuleFor(i => i.RequiredPermissions)
                .Must(list => list is { Count: > 0 })
                .When(i => i.SpiceDbCheckRequired)
                .WithMessage(AuthNMessageKeys.TokenScopePolicyRequiredPermissionsRequired);

            item.RuleFor(i => i.RequiredPermissions)
                .Must(BeCleanList).WithMessage(AuthNMessageKeys.TokenScopePolicyPermissionEmpty)
                .Must(BeDistinctList).WithMessage(AuthNMessageKeys.TokenScopePolicyPermissionDuplicate);

            item.RuleFor(i => i.GrantedPermissions)
                .Must(BeCleanList).WithMessage(AuthNMessageKeys.TokenScopePolicyPermissionEmpty)
                .Must(BeDistinctList).WithMessage(AuthNMessageKeys.TokenScopePolicyPermissionDuplicate);

            // Kontrol istenmiyorsa izin listeleri anlamsızdır; sessizce yok sayılmak
            // yerine reddedilir, yoksa yapılandırma yanıltıcı görünür.
            item.RuleFor(i => i)
                .Must(i => (i.RequiredPermissions is null || i.RequiredPermissions.Count == 0)
                           && (i.GrantedPermissions is null || i.GrantedPermissions.Count == 0))
                .When(i => !i.SpiceDbCheckRequired)
                .WithMessage(AuthNMessageKeys.TokenScopePolicyPermissionsNotAllowed);
        });
    }

    private static bool BeCleanList(List<string> list)
        => list is null || list.All(p => !string.IsNullOrWhiteSpace(p));

    private static bool BeDistinctList(List<string> list)
    {
        if (list is null || list.Count == 0)
            return true;

        return list.Select(p => p?.Trim() ?? string.Empty)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .Count() == list.Count;
    }
}
