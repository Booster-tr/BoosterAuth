using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.Principals.GetDisplayNames;

public class GetDisplayNamesEndpoint : Endpoint<GetDisplayNamesQuery, GetDisplayNamesResponse>
{
    public override void Configure()
    {
        Post("principals/display-names");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope)));

        Summary(s =>
        {
            s.Summary = "Kullanıcı kimliklerinden görünen adları döner";
            s.Description = "Bulunamayan kimlikler yanıtta yer almaz. Tek istekte en fazla 100 kimlik.";
        });
    }

    public override async Task HandleAsync(GetDisplayNamesQuery req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record GetDisplayNamesQuery(IReadOnlyList<Guid> Ids) : ICommand<GetDisplayNamesResponse>;

public record GetDisplayNamesResponse(IReadOnlyList<PrincipalDisplayNameResponse> Items);

public readonly record struct PrincipalDisplayNameResponse(Guid Id, string DisplayName);

public class GetDisplayNamesQueryHandler(IAsyncRepository<Principal, IdpDbContext> principalRepository)
    : ICommandHandler<GetDisplayNamesQuery, GetDisplayNamesResponse>
{
    public async Task<GetDisplayNamesResponse> ExecuteAsync(GetDisplayNamesQuery command, CancellationToken ct)
    {
        var ids = command.Ids.Distinct().ToList();

        var items = await principalRepository.Query()
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ProjectToType<PrincipalDisplayNameResponse>()
            .ToListAsync(ct);

        return new GetDisplayNamesResponse(items);
    }
}

public class GetDisplayNamesValidator : Validator<GetDisplayNamesQuery>
{
    public const int MaxIds = 100;

    public GetDisplayNamesValidator()
    {
        RuleFor(x => x.Ids)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Must(ids => ids.Count <= MaxIds).WithMessage(ValidationErrors.MaxLengthError);
    }
}
