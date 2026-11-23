using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Grpc;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Repositories;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Booster.Auth.Idp.Features.Principals.Grpc;

[AllowAnonymous]
public sealed class PrincipalLookupGrpcService(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IRequestContextAccessor requestContextAccessor,
    Booster.Auth.Idp.Managers.UserManagers.EndUserFactory endUserFactory)
    : PrincipalLookupService.PrincipalLookupServiceBase
{
    /// <summary>Limit kontroluyle AYNI sayim (EndUserFactory.CountLicensedUsersAsync).</summary>
    public override async Task<CountLicensedUsersResponse> CountLicensedUsers(CountLicensedUsersRequest request, ServerCallContext context)
        => new() { Count = await endUserFactory.CountLicensedUsersAsync(context.CancellationToken) };

    public override async Task<FindByIdentifierResponse> FindByIdentifier(
        FindByIdentifierRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.IsolationId, out var isolation) || isolation == Guid.Empty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "isolation_id geçersiz."));

        if (string.IsNullOrWhiteSpace(request.Identifier)
            || !Enum.IsDefined(typeof(IdentifierType), request.IdentifierType))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Tanımlayıcı geçersiz."));

        var type = (IdentifierType)request.IdentifierType;
        var identifier = request.Identifier;

        using var _ = requestContextAccessor.BeginSystemContext(isolation);

        var principal = await principalRepository.GetAsync(
            p => p.Type == PrincipalType.User
                 && p.Status == PrincipalStatus.Active
                 && (type == IdentifierType.Email ? p.Email == identifier
                     : type == IdentifierType.Phone ? p.PhoneNumber == identifier
                     : p.UserName == identifier),
            enableTracking: false,
            cancellationToken: context.CancellationToken);

        return principal is null
            ? new FindByIdentifierResponse { Found = false }
            : new FindByIdentifierResponse { Found = true, PrincipalId = principal.Id.ToString() };
    }
}
