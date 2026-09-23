using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.UserPools.GetById;

public class GetByIdUserPoolEndpoint : EndpointWithoutRequest<GetByIdUserPoolResponse>
{
    public override void Configure()
    {
        Get("{id:guid}");
        Group<UserPoolsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Kullanıcı havuzunu getirir";
            s.Description = "Havuzun detayını politikaları ve varsayılanları ile birlikte döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdUserPoolQuery(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record GetByIdUserPoolQuery(Guid Id) : ICommand<GetByIdUserPoolResponse>;

public record GetByIdUserPoolResponse(
    Guid Id,
    string Name,
    EntityStatus Status,
    UserPoolPolicy Policy,
    List<string> DefaultScopes,
    List<Guid> DefaultRoleIds,
    Guid TenantId)
{
    public string TenantName { get; init; } = default!;
}

public class GetByIdUserPoolQueryHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository)
    : ICommandHandler<GetByIdUserPoolQuery, GetByIdUserPoolResponse>
{
    public async Task<GetByIdUserPoolResponse> ExecuteAsync(GetByIdUserPoolQuery command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(
            p => p.Id == command.Id,
            include: q => q.Include(p => p.Tenant),
            enableTracking: false,
            cancellationToken: ct);

        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        return pool.Adapt<GetByIdUserPoolResponse>() with { TenantName = pool!.Tenant?.Name ?? string.Empty };
    }
}
