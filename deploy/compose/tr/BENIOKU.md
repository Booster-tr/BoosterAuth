# Booster Auth kurulumu (Docker Compose)

Booster Auth'u tek bir sunucuda (ya da kendi bilgisayarınızda) birkaç dakikada çalıştırırsınız.

## Gereksinimler

- Docker Engine 24+ ve Docker Compose v2 (Windows/macOS: Docker Desktop)
- En az 4 GB boş bellek, 10 GB disk
- İnternet erişimi (image'lar `ghcr.io/booster-tr` adresinden indirilir)

## 1. Kurulum

Paketi açtığınız klasörde:

```bash
# Linux / macOS
./kurulum.sh

# Windows (PowerShell)
powershell -ExecutionPolicy Bypass -File .\kurulum.ps1
```

Betik şunları sorar:

| Soru | Örnek | Açıklama |
| --- | --- | --- |
| Booster Auth adresi | `auth.firma.com` | Uygulamalarınızın giriş için yönleneceği adres. **Lisansınızdaki domain'lerden biri olmalı.** |
| HTTPS portu | `443` | Başka bir servis 443'ü kullanıyorsa örn. `8443` |
| Panel portu ve adresi | `3000`, `http://localhost:3000` | Yönetim paneli |
| Yönetici e-postası | `it@firma.com` | 2FA açacaksanız gerçek bir posta kutusu girin |

Parolalar ve anahtarlar rastgele üretilip `.env` dosyasına yazılır. **`.env` dosyasını yedekleyin ve paylaşmayın.**

> **Yerel deneme:** Adres henüz DNS'te yoksa `hosts` dosyasına `127.0.0.1 auth.firma.com` satırı ekleyin
> (Windows: `C:\Windows\System32\drivers\etc\hosts`, Linux/macOS: `/etc/hosts`).

## 2. Çalıştırma

```bash
docker compose up -d
```

İlk açılış image'ların indirilmesiyle birkaç dakika sürer. Durumu görmek için: `docker compose ps`.

- **Yönetim paneli:** kurulumda verdiğiniz adres (varsayılan `http://localhost:3000`)
- **Booster Auth (IdP):** `https://<adres>/idp` - OIDC keşif: `https://<adres>/idp/.well-known/openid-configuration`

Panele **`admin`** kullanıcı adı ve kurulum betiğinin gösterdiği ilk parolayla girin; ilk girişte yeni parola belirlemeniz istenir.

### HTTPS sertifikası

İlk açılışta kendi yerel sertifika yetkiliniz (CA) ve sunucu sertifikası otomatik üretilir (`pki/`). Tarayıcı bu
sertifikayı tanımadığı için uyarı verir. İki seçenek:

- **Deneme:** `pki/trust/ca.crt` dosyasını işletim sisteminize "güvenilir kök sertifika" olarak ekleyin.
- **Üretim:** Önüne kendi sertifikanızla bir reverse proxy (nginx, Traefik, Cloudflare Tunnel vb.) koyun ya da
  `pki/tls/server.crt` ve `server.key` dosyalarını kendi sertifikanızla değiştirin.

## 3. Lisans

Kurulum ilk açılışta **lisanssız** çalışır: son kullanıcı girişleri çalışır, yönetim paneli **salt okunurdur**.

1. Panelde **Sistem > Lisans** ekranını açın. Kurulum kimliğiniz burada görünür.
2. **Lisans al** butonuna basın; lisans portalı (`licensing.appnuraymind.com`) kurulum kimliğiniz dolu olarak açılır.
3. Portalda hesap açın, firma ve kurulum bilgilerinizi girin. **Ücretsiz deneme** alın ya da bir paket satın alın.
4. Portalın verdiği lisans anahtarını panelde **Sistem > Lisans** ekranına yapıştırın. Servisleri yeniden başlatmanız gerekmez.

Lisans çevrimdışı doğrulanır; Booster Auth lisans sunucusuna bağlanmaz.

## E-posta

Kayıt doğrulama, davet ve şifre sıfırlama e-postaları için `.env` dosyasındaki `MAIL_*` alanlarını doldurup
`docker compose up -d` çalıştırın. Boş bırakılırsa e-posta gönderilmez.

## Günlük işlemler

| İşlem | Komut |
| --- | --- |
| Durum | `docker compose ps` |
| Loglar | `docker compose logs -f booster-idp` |
| Güncelleme | `docker compose pull && docker compose up -d` |
| Durdurma | `docker compose down` (veri silinmez) |
| Yedek | `docker compose down` sonra `data/`, `pki/`, `secrets/` ve `.env` klasör/dosyalarını yedekleyin |

Belirli bir sürüme sabitlemek için `.env` içinde `BOOSTER_VERSION` değerini değiştirin (varsayılan `latest`).

## Klasörler

```
.env               ayarlar ve gizli değerler (kurulum betiği üretir)
docker-compose.yml servisler
init/              ilk açılış betikleri (sertifika, veritabanları)
data/              KALICI VERİ (veritabanı, IdP anahtarları)
pki/               yerel CA ve HTTPS sertifikası
secrets/           IdP imzalama anahtarı
```
