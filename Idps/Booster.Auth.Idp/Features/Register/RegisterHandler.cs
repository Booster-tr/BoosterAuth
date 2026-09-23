using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Database.Models;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.Hashing.OneWays.Argon;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

namespace Booster.Auth.Idp.Features.Register;

public sealed class RegisterHandler(
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<LocalCredential, IdpDbContext> credentialRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    IPasswordHasher passwordHasher,
    IRequestContextAccessor requestContextAccessor) : ICommandHandler<RegisterCommand, RegisterResult>
{
    public async Task<RegisterResult> ExecuteAsync(RegisterCommand cmd, CancellationToken ct)
    {
        var interaction = await interactionStore.GetAsync(cmd.InteractionKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);

        var client = await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        if (client.Registration.Mode != RegistrationMode.SelfSignup
            || !client.Login.AllowedCredentialProviders.HasFlag(CredentialProviderType.Local))
            throw Invalid(IdpMessageKeys.RegistrationNotAllowed);

        if (!string.Equals(cmd.Password, cmd.PasswordConfirm, StringComparison.Ordinal))
            throw Invalid(nameof(RegisterCommand.PasswordConfirm), IdpMessageKeys.PasswordsDoNotMatch);

        var userName = string.IsNullOrWhiteSpace(cmd.UserName) ? null : cmd.UserName.Trim();
        var email = string.IsNullOrWhiteSpace(cmd.Email) ? null : cmd.Email.Trim().ToLowerInvariant();
        var phone = CombinePhone(cmd.PhoneCountry, cmd.Phone);

        ValidateRequiredIdentifiers(client.Login, userName, email, phone);
        ValidatePasswordPolicy(client.Password, cmd.Password);

        Guid principalId;
        using (requestContextAccessor.BeginSystemContext(client.IsolationId))
        {
            await EnsureIdentifiersAreUniqueAsync(userName, email, phone, ct);

            var principal = new Principal
            {
                Id = EntityId.New(),
                DisplayName = cmd.DisplayName.Trim(),
                UserName = userName,
                Email = email,
                PhoneNumber = phone,
                Type = PrincipalType.User,
                Status = PrincipalStatus.Active,
                CreatedTime = DateTimeOffset.UtcNow
            };

            var credential = new LocalCredential
            {
                Id = EntityId.New(),
                PrincipalId = principal.Id,
                PasswordHash = passwordHasher.Hash(cmd.Password),
                PasswordChangedAt = DateTimeOffset.UtcNow,
                CreatedTime = DateTimeOffset.UtcNow
            };

            await principalRepository.AddAsync(principal, ct);
            await credentialRepository.AddAsync(credential, ct);
            await AssignDefaultsAsync(principal.Id, client, ct);
            await principalRepository.SaveChangesAsync(ct);

            principalId = principal.Id;
        }

        return new RegisterResult { PrincipalId = principalId };
    }

    private async Task AssignDefaultsAsync(Guid principalId, ClientData client, CancellationToken ct)
    {
        foreach (var role in client.DefaultRoles)
        {
            await principalRoleRepository.AddAsync(new PrincipalRole
            {
                Id = EntityId.New(),
                PrincipalId = principalId,
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                CreatedTime = DateTimeOffset.UtcNow
            }, ct);
        }

        foreach (var scope in client.DefaultScopes)
        {
            await principalScopeRepository.AddAsync(new PrincipalScope
            {
                Id = EntityId.New(),
                PrincipalId = principalId,
                Scope = scope,
                CreatedTime = DateTimeOffset.UtcNow
            }, ct);
        }
    }

    private static void ValidateRequiredIdentifiers(LoginPolicyData login, string? userName, string? email, string? phone)
    {
        var allowed = login.AllowedIdentifiers;

        if (allowed.Contains(IdentifierType.Email) && email is null)
            throw Invalid(nameof(RegisterCommand.Email), IdpMessageKeys.EmailRequired);

        if (allowed.Contains(IdentifierType.Username) && userName is null)
            throw Invalid(nameof(RegisterCommand.UserName), IdpMessageKeys.UserNameRequired);

        if (allowed.Contains(IdentifierType.Phone) && phone is null)
            throw Invalid(nameof(RegisterCommand.Phone), IdpMessageKeys.PhoneRequired);

        if (email is null && userName is null && phone is null)
            throw Invalid(IdpMessageKeys.IdentifierRequired);
    }

    private static void ValidatePasswordPolicy(PasswordPolicyData policy, string password)
    {
        if (password.Length < policy.MinLength)
            throw Invalid(nameof(RegisterCommand.Password), IdpMessageKeys.PasswordTooShort);
        if (policy.RequireUppercase && !password.Any(char.IsUpper))
            throw Invalid(nameof(RegisterCommand.Password), IdpMessageKeys.PasswordRequiresUppercase);
        if (policy.RequireLowercase && !password.Any(char.IsLower))
            throw Invalid(nameof(RegisterCommand.Password), IdpMessageKeys.PasswordRequiresLowercase);
        if (policy.RequireDigit && !password.Any(char.IsDigit))
            throw Invalid(nameof(RegisterCommand.Password), IdpMessageKeys.PasswordRequiresDigit);
        if (policy.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            throw Invalid(nameof(RegisterCommand.Password), IdpMessageKeys.PasswordRequiresNonAlphanumeric);
    }

    private async Task EnsureIdentifiersAreUniqueAsync(string? userName, string? email, string? phone, CancellationToken ct)
    {
        var exists = await principalRepository.AnyAsync(
            p => (email != null && p.Email == email)
              || (userName != null && p.UserName == userName)
              || (phone != null && p.PhoneNumber == phone),
            cancellationToken: ct);

        if (exists)
            throw Invalid(IdpMessageKeys.IdentifierAlreadyExists);
    }

    private static string? CombinePhone(string? country, string? number)
    {
        if (string.IsNullOrWhiteSpace(number)) return null;
        var c = new string((country ?? string.Empty).Where(char.IsDigit).ToArray());
        var n = new string(number.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(n)) return null;
        return string.IsNullOrEmpty(c) ? $"+{n}" : $"+{c}{n}";
    }

    private static ValidationException Invalid(string messageKey)
        => new([new ValidationFailure(string.Empty, messageKey)]);

    private static ValidationException Invalid(string property, string messageKey)
        => new([new ValidationFailure(property, messageKey)]);
}


public sealed class RegisterCommand : ICommand<RegisterResult>
{
    public string InteractionKey { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneCountry { get; set; }
    public string? Phone { get; set; }
    public string Password { get; set; } = default!;
    public string PasswordConfirm { get; set; } = default!;
}


public sealed class RegisterResult
{
    public Guid PrincipalId { get; init; }
}

public sealed class RegisterValidator : Validator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(300).WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Email)
            .MaximumLength(320).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.UserName)
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9._-]{3,100}$").WithMessage(IdpMessageKeys.UserNameInvalid)
            .When(x => !string.IsNullOrWhiteSpace(x.UserName));

        RuleFor(x => x.PhoneCountry).MaximumLength(6);
        RuleFor(x => x.Phone).MaximumLength(20);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(@"^\S+$").WithMessage(IdpMessageKeys.PasswordContainsWhitespace);

        RuleFor(x => x.PasswordConfirm).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
    }
}
