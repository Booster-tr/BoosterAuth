using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Booster.Auth.Common.Internal.Extensions;

/// <summary>
/// <c>PostgresSettings.ApplyMigration</c> açıksa servis açılışında şemayı günceller.
/// <para>
/// Container ortamında <c>dotnet ef database update</c> çalıştıracak biri yoktur;
/// bootstrapper'lar da boş veritabanında patlar. Bu yüzden şema, bootstrapper'lardan
/// ÖNCE, <see cref="ServiceExtensions.UseBoosterServices"/> içinde hazırlanır.
/// </para>
/// </summary>
public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken ct);
}

internal sealed class DatabaseMigrator<TContext>(
    TContext db,
    ILogger<DatabaseMigrator<TContext>> logger) : IDatabaseMigrator
    where TContext : DbContext
{
    private const int MaxAttempts = 10;

    public async Task MigrateAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (db.Database.GetMigrations().Any())
                {
                    await db.Database.MigrateAsync(ct);
                }
                else
                {
                    // Migration'ı hiç üretilmemiş context'ler (ör. Logging, Notification)
                    // için şema modelden oluşturulur. Tablolar zaten varsa işlem yapılmaz.
                    logger.LogWarning(
                        "{Context} için migration bulunamadı; şema EnsureCreated ile oluşturuluyor.",
                        typeof(TContext).Name);
                    await db.Database.EnsureCreatedAsync(ct);
                }

                logger.LogInformation("{Context} veritabanı şeması güncel.", typeof(TContext).Name);
                return;
            }
            catch (NpgsqlException ex) when (attempt < MaxAttempts && ex.IsTransient)
            {
                // Postgres container'ı henüz bağlantı kabul etmiyor olabilir.
                var delay = TimeSpan.FromSeconds(Math.Min(30, attempt * 3));
                logger.LogWarning(ex,
                    "{Context} migration denemesi {Attempt}/{Max} başarısız; {Delay} sonra tekrar denenecek.",
                    typeof(TContext).Name, attempt, MaxAttempts, delay);
                await Task.Delay(delay, ct);
            }
        }
    }
}
