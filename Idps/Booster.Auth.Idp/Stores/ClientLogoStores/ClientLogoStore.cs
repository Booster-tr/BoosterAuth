namespace Booster.Auth.Idp.Stores.ClientLogoStores;

public sealed class ClientLogoStore : IClientLogoStore
{
    /// <summary>wwwroot altındaki klasör adı; aynı zamanda servis edildiği yol.</summary>
    public const string FolderName = "client-logos";

    // Aynı istemciye eşzamanlı iki yükleme "sil + yaz" adımlarını iç içe geçirip
    // iki farklı uzantıyla iki dosya bırakabilirdi. Yükleme seyrek; tek kilit yeter.
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public string Directory { get; }

    public ClientLogoStore(IWebHostEnvironment environment)
    {
        var webRoot = string.IsNullOrEmpty(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;

        Directory = Path.Combine(webRoot, FolderName);
        System.IO.Directory.CreateDirectory(Directory);
    }

    public async Task SaveAsync(Guid clientId, ClientLogoFormat format, Stream content, CancellationToken ct)
    {
        var target = PathOf(clientId, format);
        var temp = Path.Combine(Directory, $"{clientId:N}.{Guid.NewGuid():N}.tmp");

        try
        {
            // Önce geçici dosyaya yazılır: yazma yarıda kalırsa eski logo yerinde durur.
            await using (var file = File.Create(temp))
                await content.CopyToAsync(file, ct);

            await WriteLock.WaitAsync(ct);
            try
            {
                foreach (var existing in ClientLogoFormat.All.Select(f => PathOf(clientId, f)))
                    File.Delete(existing);

                File.Move(temp, target);
            }
            finally
            {
                WriteLock.Release();
            }
        }
        finally
        {
            // Taşındıysa dosya yoktur; File.Delete yok olan dosyada hata vermez.
            File.Delete(temp);
        }
    }

    public ClientLogoFile? Find(Guid clientId)
    {
        foreach (var format in ClientLogoFormat.All)
        {
            var info = new FileInfo(PathOf(clientId, format));
            if (!info.Exists)
                continue;

            // Sürüm damgası: logo değiştiğinde tarayıcı önbelleği eski görseli göstermez.
            var url = $"/{FolderName}/{info.Name}?v={info.LastWriteTimeUtc.Ticks}";
            return new ClientLogoFile(info.FullName, url, format.ContentType);
        }

        return null;
    }

    private string PathOf(Guid clientId, ClientLogoFormat format)
        => Path.Combine(Directory, $"{clientId}.{format.Extension}");
}
