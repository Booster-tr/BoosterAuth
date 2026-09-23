using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Booster.Auth.Database.DbContextBase;

/// <summary>
/// <c>dotnet ef</c> komutlarının DbContext'i oluşturmak için kullandığı fabrika.
/// <para>
/// Fabrika olmasaydı EF, migration üretmek için uygulamanın host'unu kurmak
/// zorunda kalırdı; o da Kafka/Redis gibi bağımlılıkların kayıtlı olmasını
/// gerektirir. Migration üretmek için altyapının ayakta olması saçma bir
/// koşuldur, bu yüzden bağlantı dizesi doğrudan appsettings'ten okunur.
/// </para>
/// </summary>
public abstract class BaseDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["PostgresSettings:ConnectionString"]
            ?? throw new InvalidOperationException(
                "PostgresSettings:ConnectionString bulunamadı. Komutu proje klasöründen çalıştırın.");

        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseNpgsql(connectionString);

        return CreateNewInstance(builder.Options);
    }

    /// <summary>
    /// Türetilen sınıf context'i kendi kurucu imzasıyla oluşturur
    /// (çoğu context ayrıca <c>IRequestContextAccessor</c> ister).
    /// </summary>
    protected abstract TContext CreateNewInstance(DbContextOptions<TContext> options);
}
