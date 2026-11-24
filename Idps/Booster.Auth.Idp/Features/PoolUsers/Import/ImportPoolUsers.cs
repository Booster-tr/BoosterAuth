using System.Net.Mail;
using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.UserManagers;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

namespace Booster.Auth.Idp.Features.PoolUsers.Import;

/// <summary>
/// CSV ile toplu kullanici ekleme. Panel dosyayi metin olarak gonderir (BFF proxy'si JSON tasir).
/// Bicim: ilk satir baslik; <c>email,ad,soyad</c> (veya <c>email,first_name,last_name</c>); ayrac virgul ya da noktali
/// virgul (Turkce Excel noktali virgul kullanir). Her satirin sonucu ayri doner; hatali satir digerlerini durdurmaz.
/// </summary>
public class ImportPoolUsersEndpoint : Endpoint<ImportPoolUsersCommand, ImportPoolUsersResponse>
{
    public override void Configure()
    {
        Post("pools/users/import");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "CSV ile havuza toplu kullanici ekler ve davet gonderir");
    }

    public override async Task HandleAsync(ImportPoolUsersCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record ImportPoolUsersCommand(Guid UserPoolId, string Csv, bool SendInvitations = true) : ICommand<ImportPoolUsersResponse>;

/// <summary>LimitReached: lisansin kullanici limiti doldu; bu ve sonraki satirlar eklenmedi.</summary>
public enum ImportRowStatus { Created = 1, AlreadyExists = 2, Duplicate = 3, Invalid = 4, LimitReached = 5 }

public sealed record ImportRowResult(int Line, string? Email, string? DisplayName, ImportRowStatus Status, Guid? PrincipalId);

public sealed record ImportPoolUsersResponse(int Created, int Skipped, IReadOnlyList<ImportRowResult> Rows);

public sealed class ImportPoolUsersValidator : Validator<ImportPoolUsersCommand>
{
    public ImportPoolUsersValidator()
    {
        // ~1000 satir icin bol; cok buyuk istekleri ayristirmaya baslamadan reddeder.
        RuleFor(x => x.Csv).NotEmpty().WithMessage(IdpMessageKeys.ImportCsvInvalid)
            .MaximumLength(1_000_000).WithMessage(IdpMessageKeys.ImportFileTooLarge);
    }
}

public class ImportPoolUsersCommandHandler(
    PoolDataReader poolDataReader,
    EndUserFactory factory,
    InvitationManager invitations,
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<ImportPoolUsersCommand, ImportPoolUsersResponse>
{
    public const int MaxRows = 1000;

    public async Task<ImportPoolUsersResponse> ExecuteAsync(ImportPoolUsersCommand cmd, CancellationToken ct)
    {
        var rows = PoolUserCsv.Parse(cmd.Csv);
        if (rows.Count > MaxRows)
            // Ceviri yer tutucu doldurmaz; sinir metnin icinde yazilidir (eski anahtar "{Max}" gosteriyordu).
            throw new ValidationException([new ValidationFailure(nameof(cmd.Csv), IdpMessageKeys.ImportRowLimitExceeded)]);

        var pool = await poolDataReader.GetAsync(cmd.UserPoolId, ct)
                   ?? throw new BusinessException(IdpMessageKeys.PoolNotFound);

        using var _ = requestContextAccessor.BeginSystemContext(cmd.UserPoolId);

        var results = new List<ImportRowResult>(rows.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var limitReached = false;

        foreach (var row in rows)
        {
            var email = string.IsNullOrWhiteSpace(row.Email) ? null : EndUserFactory.NormalizeEmail(row.Email);
            var name = row.DisplayName;

            if (email is null || !IsEmail(email) || string.IsNullOrWhiteSpace(name) || name.Length > 200)
            {
                results.Add(new(row.Line, row.Email, name, ImportRowStatus.Invalid, null));
                continue;
            }

            if (limitReached)
            {
                results.Add(new(row.Line, email, name, ImportRowStatus.LimitReached, null));
                continue;
            }

            if (!seen.Add(email))
            {
                results.Add(new(row.Line, email, name, ImportRowStatus.Duplicate, null));
                continue;
            }

            if (await factory.EmailExistsAsync(email, ct))
            {
                results.Add(new(row.Line, email, name, ImportRowStatus.AlreadyExists, null));
                continue;
            }

            // Satir satir kaydedilir: bir hata, onceki satirlarin kullanicilarini ve davetlerini (outbox) geri almaz.
            Guid? createdId = null;
            try
            {
                await factory.RunWithinUserLimitAsync(async c =>
                {
                    var principal = await factory.CreateInvitedAsync(pool, name, email, c);
                    if (cmd.SendInvitations)
                        await invitations.SendAsync(principal, cmd.UserPoolId, c);
                    await principalRepository.SaveChangesAsync(c);
                    createdId = principal.Id;
                }, ct);
            }
            catch (BusinessException e) when (e.Message == LicenseMessageKeys.LimitUsers)
            {
                // Limit doldu: kalan satirlar da eklenmez, raporda gosterilir (mevcut kullanicilar etkilenmez).
                limitReached = true;
                results.Add(new(row.Line, email, name, ImportRowStatus.LimitReached, null));
                continue;
            }

            results.Add(new(row.Line, email, name, ImportRowStatus.Created, createdId));
        }

        var created = results.Count(r => r.Status == ImportRowStatus.Created);
        return new ImportPoolUsersResponse(created, results.Count - created, results);
    }

    private static bool IsEmail(string value) =>
        value.Length <= 320 && MailAddress.TryCreate(value, out var a) && a.Address == value && value.Contains('.');
}

/// <summary>CSV ayristirici: tirnakli alanlari, virgul / noktali virgul ayraci ve BOM'u destekler.</summary>
public static class PoolUserCsv
{
    public sealed record Row(int Line, string? Email, string DisplayName);

    private static readonly string[] EmailHeaders = ["email", "e-posta", "eposta", "mail"];
    private static readonly string[] FirstNameHeaders = ["ad", "adi", "first_name", "firstname", "first name", "isim"];
    private static readonly string[] LastNameHeaders = ["soyad", "soyadi", "last_name", "lastname", "last name"];
    private static readonly string[] FullNameHeaders = ["ad soyad", "adsoyad", "name", "display_name", "displayname", "full_name"];

    public static List<Row> Parse(string csv)
    {
        var lines = csv.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var headerIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (headerIndex < 0) throw Invalid();

        var header = lines[headerIndex];
        var sep = header.Count(c => c == ';') > header.Count(c => c == ',') ? ';' : ',';
        var columns = Split(header, sep).Select(h => h.Trim().ToLowerInvariant()).ToList();

        var email = columns.FindIndex(c => EmailHeaders.Contains(c));
        if (email < 0) throw Invalid();
        var first = columns.FindIndex(c => FirstNameHeaders.Contains(c));
        var last = columns.FindIndex(c => LastNameHeaders.Contains(c));
        var full = columns.FindIndex(c => FullNameHeaders.Contains(c));

        var rows = new List<Row>();
        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var f = Split(lines[i], sep);
            string At(int idx) => idx >= 0 && idx < f.Count ? f[idx].Trim() : string.Empty;

            var name = full >= 0 ? At(full) : $"{At(first)} {At(last)}".Trim();
            rows.Add(new Row(i + 1, At(email), name));
        }
        return rows;
    }

    private static List<string> Split(string line, char sep)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == sep) { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    private static ValidationException Invalid() =>
        new([new ValidationFailure(nameof(ImportPoolUsersCommand.Csv), IdpMessageKeys.ImportCsvInvalid)]);
}
