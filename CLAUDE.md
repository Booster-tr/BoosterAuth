# Booster Auth

Cok kiracili (multi-tenant) OIDC/OAuth2 kimlik saglayici (IdP) ve yetki platformu (OpenFGA).
Booster'in tum urunleri kimlik ve yetkiyi buradan alir. Yonetim paneli ayri bir repodadir (`booster-management`).
Urun, musteri sunucusunda on-prem calisir ve **cevrimdisi lisansla** acilir. Lisanslar bu solution icindeki lisans portalinda (`licensing.appnuraymind.com`) uretilir.

## Calisma tarzi
- **KISS, YAGNI.** Iletisim ve kod yorumlari **Turkce**. Yorum NEDEN'i anlatir.
- Her gorevde once kisa plan yaz, onay al, sonra kod yaz. Fazlarin sonunda dur ve ozetle.
- Tahmin etme: once koda bak. Belirsizlikte varsayilan yoksa sor.
- Test projesi YOK (karar). Dogrulama derleme (0 hata) ve calisan sistemde curl/smoke kontrolu ile yapilir. Dogrulanamayani "dogrulanmadi" diye belirt.

## Altin kurallar
1. **`master`'a dogrudan commit/push YOK.** `claude/<konu>` branch -> PR; kullanici merge eder. Merge = canliya cikis (CI -> GHCR -> gitops -> ArgoCD).
   **Lisanslama isi istisna:** tum PR'lar `feature/lisanslama` dalina gider. CI yalnizca `master` PR'larinda calistigi icin bu dala merge canliya CIKMAZ. Sistem tamamlaninca dal tek PR ile `master`'a alinir.
2. **Secret repoya girmez:** kod, log, commit. Cluster Secret'lari elle olusturulur.
3. Hata metni yazilmaz; mesaj anahtari firlatilir (`BusinessException(...)`). Metinler tr + en olarak SystemMessage seed'inde tutulur.
4. Id'ler route'ta degil query string'de tasinir (FGA secicileri `query:XId` okur). Silme soft yapilir. Audit alanlari elle set edilmez.
5. Servis surumu `appsettings.json` `"Version"` alaninda tutulur. Davranis degisikligi = surum artisi.

## Yapi
| Host | Gorev | DbContext |
|---|---|---|
| `Idps/Booster.Auth.Idp` | Son kullanici akislari (`authorize`, `token`, `me`, `logout`, `/ui/*`, `/register`, federasyon) + panel uclari (`pools/users`, `service-accounts`, `sessions`, `consents`) | `IdpDbContext` |
| `Services/Booster.Auth.Management.Api` | Tenant, pool, client, rol, scope, IdP tanimlari | `AuthNDbContext` |
| `Services/Booster.Auth.Z` | Yetki (OpenFGA) | `AuthZDbContext` |
| `Gateways/Booster.Auth.YarpGateway` | `/idp`, `/management`, `/authz` | |
| `Services/Booster.Licensing.Portal` | Lisans portali (MVC, `licensing.appnuraymind.com`): musteri girisi Booster Auth ile, admin ekranlari, lisans uretme. YALNIZCA Booster'in kendi kurulumunda calisir | `LicensingDbContext` (`licensingdb`) |
| `Services/Booster.{Logging,Notification,SystemMessage}Service` | Yalnizca Booster'in kendi yardimci servisleri | |

Ortak kutuphaneler `Common/` ve `CommonInternal/` altinda. Diger urunlere kopya-yapistir ile gecer.

## Lisans kurallari (degismez)
Ayri repo YOK; hepsi bu solution icindedir.

| Proje | Kim kullanir | Icerik |
|---|---|---|
| `Common/Licensing/Booster.Licensing` | Urun ve portal | Token bicimi, `LicensePublicKeys` (gomulu), `LicenseValidator`, `LicenseValidationResult` (`PanelWritable`, `EndUserFlowsAllowed`), `DomainRules`, `LicenseClock` + `ILicenseClockStore` |
| `Common/Licensing/Booster.Licensing.Signing` | YALNIZCA portal | `LicenseSigningKey` (uretme, imzalama). Urun projeleri bu projeye referans VERMEZ |

Portal imzalama anahtarini ilk acilista kendisi uretir ve `licensingdb`'ye `ISecretProtector` ile SIFRELI yazar. Elle anahtar islemi yoktur.
Public key portalin Genel sayfasinda gorunur; `LicensePublicKeys`'e eklenir.

**Portal ayarlari:**
- Musteri havuzu ve portal client'i `LicensingBootstrapper` (Management + AuthZ) ile kurulur. Varsayilan KAPALI; acmak icin `BootstrapOptions__Licensing__Enabled=true` ve `RedirectUris`. Musteri kurulumlarinda kapali kalir.
- Admin erisimi `LicensingPortalOptions__AdminEmails__N` listesiyle verilir. E-posta Booster Auth'ta dogrulanmis olmalidir (`email_verified`).

- **Token:** `BSTR1.<payload>.<imza>`.
  - Iki parca da base64url; imza Ed25519 (BouncyCastle) ile atilir ve `"BSTR1." + payload` metnini kapsar.
  - Payload alanlari: `v, kid, licenseId, customer{id,name}, plan, type(trial|paid), limits{users,pools,clients}, allowedDomains[], issuedAt, expiresAt, graceDays, installationId`.
- **Public key'ler KODA GOMULUDUR** (`kid` -> key). Config veya env ile eklenemez ya da degistirilemez.
- Urunde imzalama kodu (`Booster.Licensing.Signing`) ve private key **bulunmaz**.
- **Tamamen cevrimdisi:** urun lisans sunucusuna HIC istek atmaz.
- **installationId:**
  - Ilk acilista rastgele bir GUID uretilir ve veritabanina yazilir.
  - Kurulum yasadigi surece degismez. Dosyaya yazilmaz.
  - Baska bir GUID icin uretilmis token gecersizdir.
- **Saat geri alma korumasi:** urun gordugu en ileri zamani saklar ve onu kullanir.
- **Durumlar:** Active -> Warning (bitise 14 gun kala) -> Grace (bitisten sonra graceDays boyunca) -> Restricted. Imza hatasinda Invalid.
- **EN KRITIK KURAL:**
  - Ucretli lisansta son kullanici login/token/refresh akislari HICBIR durumda kesilmez.
  - Kisitlama yalnizca yonetim paneline uygulanir: Restricted ve Invalid durumunda panel salt okunur olur, yalnizca lisans ekrani calisir.
  - Deneme lisansinda ek sure bitince son kullanici girisleri de durur.
  - Lisans filtresi IdP'nin son kullanici uclarini (`authorize`, `token`, `me`, `logout`, `/ui/*`, `/register`, `.well-known/*`) ACIKCA disarida birakir.
- **Domain:**
  - Kontrol ana domain (eTLD+1) uzerinden yapilir ve global issuer (`IdpOptions.Issuer`) ile UI `BaseUrl`'e uygulanir.
  - Uymazsa panelde uyari verilir; giris kesilmez.
  - localhost, `*.local`, `*.internal` ve IP adresleri her zaman serbesttir; panelde "test ortami" etiketiyle gosterilir.
- **Limitler:**
  - Kontrol tek merkezi servisle yapilir: `ILicenseLimitService`. Kontrol BACKEND'dedir; UI yalnizca gosterir.
  - Kullanici, pool ve client olusturan TUM yollar bu kontrolden gecer.
  - Hata kodlari: `LICENSE_LIMIT_USERS`, `LICENSE_LIMIT_POOLS`, `LICENSE_LIMIT_CLIENTS`.
  - Mevcut kayitlar limit asimindan ASLA etkilenmez.
  - Self-servis kayitta son kullaniciya teknik detay gosterilmez; genel bir "kayit su an kapali" mesaji cikar.
- **Client redirect URI'leri lisansla kisitlanmaz.** Bunun yerine pool ayarlarinda ayri bir "izin verilen redirect domain'leri" politikasi bulunur (varsayilan kapali).

## Komutlar
```bash
dotnet build Booster.Auth.slnx -v q
docker compose up -d    # lokal ortam: ayri `booster_docker` klasorunde (docker-compose.yml)
```
