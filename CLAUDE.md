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
- **Musteri alani (`/hesap`, `AccountController`):** e-postasi dogrulanmis her kullanici erisebilir.
  - Islemler: firma bilgileri, kurulum ekleme, ucretsiz deneme, lisanslarim, aktarim.
  - Kayitlar her zaman giris yapanin firmasina baglidir. Baska firmanin kaydi 404 doner.
  - Kurallar `CustomerPortalService` ve `SelfServiceRules` icindedir:
    - Bir kurulum GUID'i tek firmaya ait olabilir.
    - Deneme: ucretsiz e-posta saglayicisi kabul edilmez; e-posta domaini kurulum domainiyle uyusmali; ayni domaine (alt alan dahil) tek deneme; IP basina gunde 3 istek (`CF-Connecting-IP`).
    - Aktarim: yalnizca ucretli ve suresi dolmamis lisans; ayni bitis tarihi ve limitler; zincirin ilk lisansi (`OriginLicenseId`) basina yilda 3.
    - Cevrimdisi model: eski kurulumdaki lisans o sunucuda suresi dolana kadar calismaya devam eder.
- **Siparis (`OrderService`):** akis siparis -> havale/EFT -> admin "Odeme alindi" -> lisans otomatik uretilir.
  - Plan ve fatura bilgileri siparise KOPYALANIR; plan sonradan degisse de siparis etkilenmez.
  - Referans kodu `BST-yyyy-NNNNNN` bicimindedir ve veritabani dizisinden (`OrderReferenceNumbers`) gelir.
  - **Onay:** `ExecuteUpdate ... WHERE Status = Pending` ile ve transaction icinde yapilir. Cift tiklama ya da es zamanli onay TEK lisans uretir.
  - **Yenileme / yukseltme:** kurulumda gecerli ve aktarilmamis ucretli lisans varsa yeni sure onun bitisine eklenir; yoksa bugunden baslar.
  - Planlar admin ekranindan yonetilir; ilk acilista ornek planlar fiyatsiz eklenir.
  - Havale bilgileri `LicensingPortalOptions__Bank__{BankName,AccountName,Iban}` ayarlarindan okunur (gizli degil).
- **Portal e-postalari (`PortalNotifier`):** Notification servisi gonderir; sablonlar `license_*` anahtarlariyla `NotificationBootstrapper`'da (tr/en).
  - Akislar: deneme, siparis alindi, lisans hazir, aktarim, yenileme hatirlatmasi. Alici firmanin fatura e-postasi.
  - Gonderim commit'ten SONRA ve en-iyi-cabadir: Kafka hatasi loglanir, islem bozulmaz.
  - Baglantilar `LicensingPortalOptions__PublicUrl` ile uretilir.
- **Yenileme hatirlatmasi (`RenewalReminderService`):** acilista ve 6 saatte bir calisir; admin Siparisler ekranindan elle de tetiklenebilir.
  - Bitise 30 ve 7 gun kala, lisans ve tur basina BIR KEZ (`LicenseReminders` tekil index'i).
  - Ayni kurulumda daha gec biten ucretli lisans varsa (yenilenmis) gonderilmez.
- **Notification sablonlari:** e-posta govdesine yerlestirilen degerler HTML kacislidir (firma adi gibi kullanici girdileri e-postaya HTML enjekte edemez). Konu ve SMS duz metindir.
- **Uretim imzalama anahtari:** kid `k2026p`; public key `LicensePublicKeys`'te gomulu. Private key yalnizca cluster secret'inda (`demo/licensing-portal`, `signing-private-key`).
  - Portal ilk acilista DB'de anahtar yoksa `LicensingPortalOptions__InitialSigningKey__{Kid,PrivateKey}` ile ice aktarir; anahtar varsa bu ayar yok sayilir.
  - Rotasyon: portalda yeni anahtar uret, public key'i `LicensePublicKeys`'e EKLE (eskisini silme), urunu yayinla.
- **Tanitim sitesi (portal icinde):** `/` urun, `/paketler` aktif planlar + deneme, `/indir` kurulum paketi (`SiteController`).
  - "Lisans al" -> `/hesap/satin-al?plan=<kod>` (`deneme` = ucretsiz deneme). Eksik adimi sirayla tamamlatir ve her adimdan sonra geri doner: firma -> kurulum -> (birden cok kurulumda secim) -> siparis.
  - Panelin "Lisans al" butonu `/?installationId=` ile gelir: giris -> kurulum formu dolu (kurulum zaten kayitliysa Kurulumlarim).
- **Musteri kurulum paketi:** `deploy/compose/` (docker-compose, `.env.example`, `init/`, `kurulum.sh|ps1`, `BENIOKU.md`).
  - Portala gomulu kaynak olarak derlenir; `/indir/booster-auth.zip` zip'i calisma aninda uretir (`InstallPackage`; `.sh` 0755).
  - `.dockerignore` `.env.*` ve `docker-compose*` dosyalarini dislar; paket dosyalari icin `!deploy/compose/...` istisnalari VAR, silme.
  - Image'lar `ghcr.io/booster-tr/*` (public, portal haric). Paket WalkToWin ve lisans portali ayarlarini ICERMEZ (yalnizca Booster'in kendi kurulumu).
  - Paket dosyalari LF olmali (`.gitattributes`); betikler ayrica `tr -d '\r'` ile calistirilir.
- **Razor:** `action` ozniteligi acikca yazilan POST formlarina `asp-antiforgery="true"` EKLE. Tag helper o durumda CSRF token'ini kendiliginden koymaz; form 400 doner.

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
  - Self-servis kayitta son kullaniciya teknik detay gosterilmez; genel bir "kayit su an kapali" mesaji cikar (`LICENSE_REGISTRATION_CLOSED`).
  - **Uygulama:** `CommonInternal/.../Licensing/LicenseLimitService`.
    - Cagiran ACIK bir transaction icinde olmalidir. Servis `pg_advisory_xact_lock` alir, sayar ve doluysa reddeder; ekleme + commit kilit altinda yapilir. Boylece ayni anda iki istek son yeri alamaz.
    - Kullanicilar icin `EndUserFactory.RunWithinUserLimitAsync`; pool ve client icin `CreateUserPool` / `CreateClient` ornektir.
  - **Kontrol eden yollar:**
    - Kullanici: self-servis kayit, federasyon ilk girisi, panelden ekleme, CSV ice aktarma.
    - Pool: `POST userpools`. Client: `POST clients`.
    - Muaf olanlar: bootstrapper'lar, admin seeder ve servis hesaplari (kullanici sayilmaz).
  - **Sayim:** yonetim havuzu ve silinmisler HARIC; pasif ve askidakiler DAHIL.
  - Gecerli lisans yokken deneme varsayilanlari uygulanir: 100 / 1 / 3.
  - Durum bilinemiyorsa (Management'a ulasilamiyor) limit UYGULANMAZ.
  - Kullanim: `GET management/system/license/usage`. 30 sn onbellek yalnizca gosterim icindir.
- **Client redirect URI'leri lisansla kisitlanmaz.** Bunun yerine pool ayarlarinda ayri bir "izin verilen redirect domain'leri" politikasi bulunur (varsayilan kapali).
  - Ayar: `UserPoolPolicy.Redirect` (Enabled, AllowedDomains, AllowTestHosts, AllowCustomSchemes); uc `PUT userpools/policy/redirect`.
  - Kontrol: `RedirectPolicyChecker` / `RedirectPolicyGuard`. Client olusturmada tum adresler, guncellemede yalnizca YENI eklenen adresler kontrol edilir.
  - Politika sonradan acilirsa mevcut client'lar calismaya devam eder; uymayan adresleri client detayinda (`redirectPolicyViolations`) ve politika kaydinin yanitinda uyari olarak doner.
  - Giris anindaki redirect kontrolu (birebir eslesme) degismedi.

### Lisans calisma zamani (nerede ne var)
- **Sahip Management.**
  - `authndb.Installations` tek satirdir: kurulum GUID'i, aktif token ve saat icin gorulen en ileri zaman.
  - Durum `Licensing/LicenseService` tarafindan hesaplanir. En fazla 1 dk eskiyebilir; saat basi ve acilista yeniden hesaplanir.
  - Uclar:
    - `GET management/system/license`
    - `POST management/system/license/activate`: gecersiz token KAYDEDILMEZ.
- **IdP ve AuthZ** durumu `LicenseStatusService` gRPC'si ile alir (`GrpcLicenseStatusProvider`, 1 dk onbellek).
  - Management'a ulasilamazsa her sey ACIK kalir.
  - AuthZ'de `ManagementSettings__GrpcAddress` verilmezse kontrol yapilmaz.
- **Panel kisitlamasi:** `CommonInternal/.../Licensing/LicenseGuard` (FastEndpoints global on-isleyici).
  - Yalnizca panel scope'lu ve okuma olmayan istekleri reddeder (`LICENSE_RESTRICTED`, 403).
  - POST ile cagrilan ama bir sey degistirmeyen uclar `[AllowWhenLicenseRestricted]` ile isaretlenir. Yeni bir okuma ucu POST ise isaretlemeyi UNUTMA; yoksa kisitliyken panel ekrani cizemez.
- **Deneme lisansi:** `Idps/.../Managers/LicenseManagers/EndUserLicenseGate`, authorize ve token'da calisir.
  - Yalnizca deneme lisansi Restricted iken kapatir (`LICENSE_SERVICE_UNAVAILABLE`).
  - Yonetim havuzu her zaman aciktir, cunku yonetici yeni lisansi yapistirabilmeli.
- **Hata kodu:** is kurali hatalari ProblemDetails'te `code` alaniyla da doner. Bu alan mesaj anahtaridir; panel metne degil koda bakar.

## Kullanici ekleme ve davet (IdP)
- **Tek olusturma noktasi:** `Managers/UserManagers/EndUserFactory` (Principal + varsayilan rol/kapsam + FGA `end_user`).
  - Panelden ekleme (`POST pools/users`) ve CSV ice aktarma (`POST pools/users/import`) bunu kullanir.
  - Self-servis kayit ve federasyon ilk girisi kendi kayitlarini olusturur ama limit kontrolunu `EndUserFactory.RunWithinUserLimitAsync` ile yapar.
  - Yeni bir kullanici olusturma yolu yazarken BU SINIFI KULLAN; aksi halde yol lisans limitini atlar.
- **Davet:** `InvitationManager`.
  - Baglanti `/idp/ui/invite?token=`; 1 gun gecerli ve tek kullanimlik.
  - Yeniden gonderilen davet (`POST pools/users/invite`) eskisini gecersiz kilar.
  - Kabul edilince parola belirlenir ve e-posta dogrulanmis sayilir.
- Havuz politikasi istemcisiz akislarda `PoolDataReader` (Management `GetPoolData` gRPC) ile okunur.
- **OLAYLAR OUTBOX'A YAZILMAZ (2026-11 yerelde dogrulandi):** `IEventBus.PublishAsync` -> Kafka rider `ITopicProducer`, olayi HEMEN gonderir.
  - `UseBusOutbox` rider ureticilerini kapsamiyor; `OutboxMessage` tablolarina (IdP, Management, portal) hic satir yazilmiyor.
  - Sonuc: transaction geri alinsa da olay gitmis olur; Kafka kapaliysa olay kaybolur.
  - Kural: olayi is islemi KAYDEDILDIKTEN/commit edildikten SONRA yayinla. Yinelenmemesi gereken islerde once DB'de sahiplen (`ON CONFLICT DO NOTHING`), sonra yayinla (bkz. portal `RenewalReminderService`).

## Komutlar
```bash
dotnet build Booster.Auth.slnx -v q
docker compose up -d    # lokal ortam: ayri `booster_docker` klasorunde (docker-compose.yml)
```
