using Booster.Hashing.OneWays.Argon;
using Booster.Hashing.OneWays.Simple;
using Booster.Hashing.Options;
using Booster.Hashing.TwoWays.Aes;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Hashing.Extensions;

public static class HasingExtensions
{

    public static IServiceCollection AddHashing(
    this IServiceCollection services,
    HashingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Pepper))
            throw new InvalidOperationException("Pepper değeri boş olamaz.");

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ISimpleHasher, SimpleHasher>();
        services.AddSingleton<ISecretProtector>(new AesGcmSecretProtector(options.EncryptionOptions));
        return services;
    }

}
