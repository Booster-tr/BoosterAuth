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
3. Hata metni yazilmaz; mesaj anahtari firlatilir (`BusinessException(...)`). Metinler tr + en olarak SystemMessage seed'inde tutulur. Seed YALNIZCA eksik anahtari ekler (panelden duzeltilen metin ezilmesin): var olan bir metni degistirmek kurulu sistemlere YANSIMAZ; anlam degisiyorsa yeni anahtar ac.
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
- Musteri havuzu ve portal client'i `LicensingBootstrapper` (Management + AuthZ) ile kurulur. Varsayilan KAPALI; acmak icin `BootstrapOptions__Licensing__Enabled=true` ve `RedirectUris`. Cikis adresi (`.../signout-callback-oidc`) her `.../signin-oidc` adresinden turetilir; ayrica yazmak gerekmez. Musteri kurulumlarinda kapali kalir.
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
- **Tanitim sitesi (portal icinde, tr + en):** `/{tr|en}`, `/features`, `/pricing`, `/download`, `/docs/...` (`SiteController`).
  - Sayfa kaydi `Site/SiteCatalog.cs` (menu, dokumantasyon yan menusu, sitemap, hreflang); gorunumler `Views/Site/{tr|en}/`. Yeni sayfa: katalog + iki dilde gorunum.
  - `/` tarayici diline gore `/tr` ya da `/en`'e (302). Eski `/paketler`, `/indir` 301. SEO: canonical, hreflang, OG, JSON-LD, `/sitemap.xml`, `/robots.txt`.
  - Dokumantasyon KODA dayanir; panelde gorunup calismayan seceneklerden (LDAP, TOTP, token politikasi) bahsetme.
  - Animasyon: `.reveal` + `wwwroot/js/site.js` (IntersectionObserver; 2,5 sn emniyet; reduced-motion). Hesap/admin ekranlari `_Layout` ile, yalnizca Turkce.
  - "Lisans al" -> `/hesap/satin-al?plan=<kod>` (`deneme` = ucretsiz deneme). Eksik adimi sirayla tamamlatir ve her adimdan sonra geri doner: firma -> kurulum -> (birden cok kurulumda secim) -> siparis.
  - Panelin "Lisans al" butonu `/?installationId=` ile gelir: giris -> kurulum formu dolu (kurulum zaten kayitliysa Kurulumlarim).
- **Musteri kurulum paketi:** dil basina `deploy/compose/tr/` (`kurulum.sh|ps1`, `BENIOKU.md`) ve `deploy/compose/en/` (`setup.sh|ps1`, `README.md`); ikisinde de docker-compose, `.env.example`, `init/`.
  - Portala gomulu kaynak olarak derlenir; `/{tr|en}/download/booster-auth.zip` zip'i sitenin dilinde calisma aninda uretir (`InstallPackage`; `.sh` 0755).
  - Bir dosyayi degistirince DIGER dildekini de degistir. `docker-compose.yml` yorumlar ve zorunlu degisken mesajlari (`${X:?mesaj}`) disinda iki dilde ayni olmak ZORUNDA; degilse zip uretilmez (`EnsureSameCompose`).
  - `.dockerignore` `.env.*` ve `docker-compose*` dosyalarini dislar; paket dosyalari icin `!deploy/compose/...` istisnalari VAR, silme.
  - Image'lar `ghcr.io/booster-tr/*` (public, portal haric). Paket lisans portali ayarlarini ICERMEZ (yalnizca Booster'in kendi kurulumu).
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
  - Yeni kayit limitte reddedilir. Mevcut kayitlarin son kullanici girisleri limit asimindan ASLA etkilenmez.
  - Self-servis kayitta son kullaniciya teknik detay gosterilmez; genel bir "kayit su an kapali" mesaji cikar (`LICENSE_REGISTRATION_CLOSED`).
  - **Uygulama:** `CommonInternal/.../Licensing/LicenseLimitService`.
    - Cagiran ACIK bir transaction icinde olmalidir. Servis `pg_advisory_xact_lock` alir, sayar ve doluysa reddeder; ekleme + commit kilit altinda yapilir. Boylece ayni anda iki istek son yeri alamaz.
    - Kullanicilar icin `EndUserFactory.RunWithinUserLimitAsync`; pool ve client icin `CreateUserPool` / `CreateClient` ornektir.
  - **Kontrol eden yollar:**
    - Kullanici: self-servis kayit, federasyon ilk girisi, panelden ekleme, CSV ice aktarma.
    - Pool: `POST userpools`. Client: `POST clients`.
    - Muaf olanlar: bootstrapper'lar, admin seeder ve servis hesaplari (kullanici sayilmaz).
  - **Sayim:** silinmisler HARIC; pasif ve askidakiler DAHIL. Kullanicilarda yonetim havuzu (panel yoneticileri) haric; uygulamalarda yalnizca panelin kendi uygulamasi (`ManagementClientId`) haric.
  - **Yonetim havuzu:** uygulama olusturma / guncelleme / izinli kapsam degistirme REDDEDILIR (`authn.management_pool_clients_reserved`); panel uygulamasi silinemez. Panel uygulamasini yalnizca bootstrapper yonetir. Aksi halde limit disi havuzda sinirsiz uygulama acilabilirdi.
  - **Limit asimi (`LicenseService.IsOverLimitAsync`, her degerlendirmede):** kullanim limitin USTUNDEYSE (veritabanina dogrudan ekleme ya da kucuk pakete gecis) panel salt okunur, yalnizca DELETE serbest (`DeletesAllowed`, `LICENSE_OVER_LIMIT`). Limitin altina inince kendiliginden acilir. Kullanici sayisi IdP gRPC'den; alinamazsa son bilinen deger. Panelden kullanici silme: `DELETE idp/pools/users` (`AccountService.DeletePrincipalAsync`).
  - Gecerli lisans yokken deneme varsayilanlari uygulanir: 100 / 1 / 3.
  - Durum bilinemiyorsa (Management'a ulasilamiyor) yeni kayit REDDEDILIR (`LICENSE_STATUS_UNAVAILABLE`; son kullaniciya `rejectWith`). Girisler etkilenmez. `GrpcLicenseStatusProvider` acilista ilk sorguyu bekletir, boylece acilista gereksiz ret olmaz.
  - Kullanim: `GET management/system/license/usage`. 30 sn onbellek yalnizca gosterim icindir.
- **Client redirect URI'leri lisansla kisitlanmaz.** Bunun yerine pool ayarlarinda ayri bir "izin verilen redirect domain'leri" politikasi bulunur (varsayilan kapali).
  - Ayar: `UserPoolPolicy.Redirect` (Enabled, AllowedDomains, AllowTestHosts, AllowCustomSchemes); uc `PUT userpools/policy/redirect`.
  - Kontrol: `RedirectPolicyChecker` / `RedirectPolicyGuard`. Client olusturmada tum adresler, guncellemede yalnizca YENI eklenen adresler kontrol edilir.
  - Politika sonradan acilirsa mevcut client'lar calismaya devam eder; uymayan adresleri client detayinda (`redirectPolicyViolations`) ve politika kaydinin yanitinda uyari olarak doner.
  - Giris anindaki redirect kontrolu (birebir eslesme) degismedi.

### Lisans calisma zamani (nerede ne var)
- **Sahip Management.**
  - `authndb.Installations` tek satirdir: kurulum GUID'i, aktif token ve saat icin gorulen en ileri zaman.
  - En ileri zaman `ClockMarkProtected`: `ISecretProtector` (AES-GCM, HASHING_ENCRYPTION_KEY) ile sifreli, kurulum GUID'ine bagli. Satirla birlikte yazilir. Bos / cozulemeyen / baska kuruluma ait = `LicenseClockTamperedException`: lisans Restricted sayilir ve yeniden etkinlestirme reddedilir (`LICENSE_CLOCK_TAMPERED`). Cozum: kurulum satirini yenileyip (yeni GUID) lisansi portalda aktarmak.
  - Eski sifresiz `ClockHighWaterMark` yalnizca surum gecisinde bir kez tasinip bosaltilir. Sinir: anahtar musterinin `.env`'inde; bu bir zorlastirma, kesin koruma degil. Eski sutun ve gecis yolu sonraki surumlerde kaldirilabilir.
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

## Guvenlik kurallari (2026-11 E2E turu)
- **Panel uclari yalnizca yonetim havuzunun token'i:** panel scope'u (`booster:panel`) isteyen her uc `actor` = yonetim havuzu ister (`CommonInternal/.../Authorization/PanelActorHandler`, FGA'dan ONCE calisir, ret sonrasi diger handler'lar calismaz). Rol/scope kontrolu yalnizca token'daki ifadeye baktigi ve FGA deposunu `actor`'dan sectigi icin musteri havuzu kendi `admin` rolu / `pool` tipiyle yonetim uclarina ulasabiliyordu.
- **AuthZ sorgu uclari** (`check`, `batch-check`, `lookup/*`): govdedeki `isolationId` token'i veren havuz olmali; yonetim havuzu token'i (panel kullanicisi, servis hesabi) o havuzda `viewer` olmali (`QueryCallerGuard`, `authz.query_not_allowed`).
- **Token exchange:** konu token'i istemcinin havuzundan (`actor`) ve bu istemciye verilmis (`azp`) olmali; servis hesabi token'inda hesap aktif olmali. Yeni token konu token'indan uzun yasamaz; istemcide izinli olmayan kapsamlar tasinmaz.
- **Uygulama adresleri** (`ClientUriRules`): yonlendirme adresi mutlak, `#` yok, `javascript:` / `data:` / `file:` vb. yasak; mobil ozel sema serbest. Audience tam http(s) adresi.
- **Parolasiz giris + MFA:** kodun gittigi kanal MFA'da kullanilmaz; diger kanal yoksa giris reddedilir (`idp.mfa_second_channel_unavailable`).
- **Federasyon state'i tarayiciya bagli:** challenge `booster_fed_binding` cerezini (etkilesim anahtari, SameSite=None) yazar; donuste esit degilse `federation_state_invalid`.
- **Hesabim mevcut parola:** girisle ayni sayac ve kilit (havuz `MaxFailedAttempts` / `LockoutSeconds`).
- **Istemci IP'si:** gateway basliga yalnizca `ClientIp:TrustedNetworks` icindeki proxy'den gelirse guvenir (`ClientIpResolver`; compose `TRUSTED_PROXY_NETWORK`, `CLIENT_IP_HEADER`) ve arka servislere `X-Forwarded-For`'u **Set** eder. Arka servisler ozel ag araliklarindan gelen `X-Forwarded-For/Proto`'yu uygular (`UseBoosterServices`). **Booster'in kendi kurulumu (Cloudflare):** `ClientIp__Header=CF-Connecting-IP` ve `ClientIp__TrustedNetworks__0=<cloudflared pod araligi>` verilmezse tum istekler tek IP'den gorunur.
- **IdP guvenlik basliklari:** CSP (`'self'`, Swagger haric; `form-action` YOK cunku giris formu redirect_uri'ye 302 ile gider), nosniff, Referrer-Policy; HSTS `X-Forwarded-Proto` sayesinde calisir. Gorunumlerde satir ici `style=` / `<script>` YAZMA.

## IdP akis kurallari (2026-11 E2E turu)
- **Token kapsami** (`TokenGrantRules.BuildScope`): istenen standart OIDC kapsamlari + kullaniciya verilen kapsamlar + rollerinin kapsamlari. Ozel kapsami istemek yetmez. Yonetim havuzunda `booster:panel` varsayilan kapsamdir; admin icin `AdminBootstrapper` eksikse ekler.
- **Audience** (`TokenGrantRules.ResolveAudience`): token istegindeki `resource`, yoksa authorize'daki, o da yoksa issuer (`/idp`). Issuer disi her audience uygulamanin `AllowedAudiences`'inda olmali (`invalid_target`).
- **Refresh:** rol / kapsam her yenilemede guncel kayittan okunur; aktif olmayan kullanici yenileyemez.
- **prompt=none / max_age / auth_time:** `AuthorizeEndpoint`; `auth_time` = oturumun `CreatedTime`'i (kod verisinde `AuthTime`). ID token yalnizca `openid` ile; `name` / `email` kapsama bagli.
- **Token hatalari:** `/token` yaniti RFC 6749 `error` / `error_description` (+ `code`) tasir (`OAuthErrors`). `/authorize` dogrulama hatasi hata sayfasina gider.
- **Izin ekrani:** `ConsentController` (`/ui/consent`); kapsam etkilesimden okunur, "allow" disi her POST ret (`access_denied`).
- **Kodlar:** her etkilesim kendi kodunu alir; kullanici basina 15 dakikada en fazla 5 gonderim (parolasiz giris, sifre sifirlama). Onbellek kod suresinden 10 dk uzun tutulur (suresi doldu mesaji icin). Kod yakilinca `login_code_attempts_exceeded`.
- **Kimlik bicimi** (`IdentifierNormalizer`): e-posta ve kullanici adi kucuk harf; telefon havuzun `DefaultCountryCode`'u ile E.164. Kayit, arama, sifre sifirlama ayni kurali kullanir.
- **Dil:** `BeginSystemContext` istegin dilini korur (bildirimler secili dilde gider). Sayfa hata dili `CurrentUICulture`. Dogrulama hatalari tum servislerde istisna isleyicisinden gecer (cevrilir).
- **Yasal metin baglantilari:** kok-goreli baglantiya `IdpUiOptions.PathPrefix` eklenir (`/kvkk` -> `/idp/kvkk`).
- **Cikis:** `logout_all` yalnizca kullaniciya ait `id_token_hint` ile ya da POST ile uygulanir.

## Yonetim kurallari (2026-11 E2E turu)
- **Kullanici durumu:** `PUT idp/pools/users/status` (Active / Suspended). Askiya alma oturum ve yenileme token'larini hemen kapatir; kayit, rol ve iliskiler kalir.
- **Panel kullanicilari** (yonetim havuzu) silinebilir / askiya alinabilir; kisi kendini ve son `admin`'i silemez / askiya alamaz.
- **Silinen kullanicinin iliskileri:** AuthZ `ReadRelations` yalnizca kullaniciyla cagrilirsa modeldeki her tip icin okur (OpenFGA tip ister). Silme `PrincipalDeletedEvent` (`auth-principal-deleted-topic`) yayinlar; Notification o kullanicinin bildirim kayitlarini siler.
- **FGA yazma / silme idempotent** (`FgaRelationManager`: on_duplicate / on_missing = ignore).
- **Sema:** tip icermeyen sema canliya alinamaz (`no_types`).
- **Sayfalama:** `PageSize` 1..500 (`common.page_size_invalid`); sorgu dizesinden `-raw` filtresi kabul edilmez, alan adlari tanimlayici olmali.
- **Rol kapsamlari token'a girer:** Management uygulama verisiyle (gRPC `role_scopes`) havuzun rol -> kapsam eslemesini gonderir; `TokenGrantRules.BuildScope` kullanicinin rollerinin kapsamlarini ekler. Rol / kapsam kaydedilince havuz onbellegi temizlenir, yeni token'lara hemen yansir.

## Kullanici ekleme ve davet (IdP)
- **Tek olusturma noktasi:** `Managers/UserManagers/EndUserFactory` (Principal + varsayilan rol/kapsam + FGA `end_user`).
  - Panelden ekleme (`POST pools/users`) ve CSV ice aktarma (`POST pools/users/import`) bunu kullanir.
  - Self-servis kayit ve federasyon ilk girisi kendi kayitlarini olusturur ama limit kontrolunu `EndUserFactory.RunWithinUserLimitAsync` ile yapar.
  - Yeni bir kullanici olusturma yolu yazarken BU SINIFI KULLAN; aksi halde yol lisans limitini atlar.
- **LDAP / Active Directory:** havuzda `ldap` turunde kimlik saglayici (`IdentityProviders`; bind parolasi `ClientSecretEncrypted`) + giris politikasinda `LoginProviderType.Ldap`. Havuz basina TEK LDAP.
  - Baglanti kodu `CommonInternal/Booster.Auth.Ldap` (`LdapDirectory`): search-then-bind; BOS PAROLA reddedilir (anonim bind), filtre RFC 4515 ile kacislanir, coklu eslesme reddedilir. LDAPS/StartTLS sertifikasi OS guven deposuyla (kurum CA'si `EXTRA_CA_CERTS`).
  - Giris (`LdapLoginService`, `CredentialLoginHandler`): yerel parolasi OLMAYAN kullanici parola formunda dizinle dogrulanir; eslesme `PrincipalIdentity` (saglayici + objectGUID/entryUUID). Ilk giriste hesap (`AllowFederatedJit` + lisans limiti), ad/e-posta her giriste dizinden. Yerel parolali hesaba otomatik BAGLANMAZ. Hatali deneme kilidi onbellekte (havuz + kullanici adi). MFA uygulanir.
  - LDAP kullanicisina sifre sifirlama kodu gonderilmez (dizini atlayamasin). Giris ekraninda LDAP butonu yoktur; LDAP acikken kimlik alani kullanici adi da kabul eder.
  - Panel "Baglantiyi test et": `POST identity-providers/ldap/test`. Yerel test: `bitnamilegacy/openldap` (ag adi `openldap`, 1389/1636).
- **Hesabim (son kullanici self-servis):** IdP `/ui/account?client_id=..&return_url=..` (`AccountController` + `AccountService`). Kimlik IdP oturum cerezinden; oturum yoksa `InteractionStateData.AccountReturnUrl` ile giris acilir, `AuthorizeEndpoint` giristen sonra kod uretmeden sayfaya doner.
  - `return_url` yalnizca uygulamanin kayitli redirect / post-logout adresleriyle AYNI kokende (scheme+host+port) kabul edilir (acik yonlendirme yok).
  - Ad; e-posta / telefon degisikligi yeni adrese kodla (`email_change` e-posta sablonu, telefon `phone_verification` SMS); parola (diger oturumlar kapanir); oturum listesi + digerlerinden cikis; hesap silme.
  - Hassas islemler (e-posta / telefon / silme) parolasi olan kullanicida mevcut parolayi yeniden ister. LDAP kullanicisinda ad / e-posta / parola degistirilemez.
  - Hesap silme: havuz kayit politikasi `AllowSelfDeletion` (varsayilan kapali; yonetim havuzunda her zaman kapali). Kayit anonimlestirilip soft-delete; parola, kimlikler, izinler, roller, oturumlar ve FGA iliskileri silinir.
- **Davet:** `InvitationManager`.
  - Baglanti `/idp/ui/invite?token=`; 1 gun gecerli ve tek kullanimlik.
  - Yeniden gonderilen davet (`POST pools/users/invite`) eskisini gecersiz kilar.
  - Kabul edilince parola belirlenir ve e-posta dogrulanmis sayilir.
- Havuz politikasi istemcisiz akislarda `PoolDataReader` (Management `GetPoolData` gRPC) ile okunur.
- **OLAYLAR OUTBOX'A YAZILMAZ (2026-11 yerelde dogrulandi):** `IEventBus.PublishAsync` -> Kafka rider `ITopicProducer`, olayi HEMEN gonderir.
  - `UseBusOutbox` rider ureticilerini kapsamiyor; `OutboxMessage` tablolarina (IdP, Management, portal) hic satir yazilmiyor.
  - Sonuc: transaction geri alinsa da olay gitmis olur; Kafka kapaliysa olay kaybolur.
  - Kural: olayi is islemi KAYDEDILDIKTEN/commit edildikten SONRA yayinla. Yinelenmemesi gereken islerde once DB'de sahiplen (`ON CONFLICT DO NOTHING`), sonra yayinla (bkz. portal `RenewalReminderService`).
- **Rol / kapsam ad senkronu:** IdP `PrincipalScope.Scope` ve `PrincipalRole.RoleName` Management tanimlarinin kopyasidir (token bunlardan uretilir).
  - Management kapsam ad degisikligi / silme -> `ScopeDefinitionChangedEvent`; rol anahtar degisikligi / silme -> `RoleChangedEvent` (`PoolDefinitionTopics`). IdP tuketicileri kopyalari gunceller / siler.
  - `PublishOrLogAsync`: Kafka hatasi loglanir; olay kaybolursa kopya bayat kalir. Verilmis token'lar suresi dolana kadar eski haliyle gecerlidir.
  - `RoleScope` IdP'ye tasinmaz; rolun kapsam listesi degisikligi senkron gerektirmez.
- **Havuz API'leri (`Idps/Booster.Auth.Idp/Apis/`, `/idp/apis/*`):** musteri uygulamasinin backend'i icin; panel uclari (`pools/...`, `/authz/relations`) panel scope'u ister, bunlar istemez.
  - Cagiran YALNIZCA panelde acilan havuz servis hesabi: YONETIM havuzunda Machine principal (`OwnerPoolId` = hedef havuz), token `client_id=management` + `resource=<PUBLIC_URL>/idp`. Havuz `OwnerPoolId`'den; istekte havuz alinmaz (`PoolApiCaller`).
  - Yonetim grafigindeki iliski uygulanir (panel uclariyla ayni): okuma + `check` -> viewer, rol/kapsam grant/revoke -> manager, iliski yazma/silme -> grant_manager. Askiya alinan hesabin token'i hemen 403.
  - Kullanici rol/kapsam: `GET .../access`, `POST .../access/grant|revoke` (havuz tanimlariyla dogrulanir). Iliskiler: `POST apis/relations`, `/delete`, `/check` (AuthZ gRPC, havuzun kendi store'u).
  - Panel scope'u tasimadigi icin `LicenseGuard` bu uclari kisitlamaz.
- **`hidden_providers`** (`/authorize`): giris ekraninda gizlenecek saglayici tipi / anahtari ya da `email_code` / `phone_code`. `InteractionStateData.HiddenProviders`; yalnizca ekran, yetki kurali degil.
- **Apple:** panel `.p8` anahtarini `client_secret` alaninda saklar; `AppleProvider` once `private_key_pem`, yoksa `client_secret` okur. Ad yalnizca ilk giriste form `user` JSON'unda gelir.
- **Cikis:** `/idp/logout` istemciyi `client_id`'den, yoksa imzasi dogrulanan `id_token_hint`'in `aud`'undan bulur.
- **AI dosyalari:** portal `wwwroot/llms.txt` ve `wwwroot/ai/booster-auth/SKILL.md`. Entegrasyon davranisi degisirse (uc, parametre, token) bunlari da guncelle.

## Komutlar
```bash
dotnet build Booster.Auth.slnx -v q
docker compose up -d    # lokal ortam: ayri `booster_docker` klasorunde (docker-compose.yml)
```

### Yasal metinler
- **IdP (urun, musterinin metinleri):** gizlilik / kullanim kosullari / KVKK / acik riza / cerez metinleri `LegalDocument` + `LegalDocumentTranslation` (ITranslation deseni) tablolarinda; varsayilanlar `Idps/Booster.Auth.Idp/LegalTemplates/{anahtar}.{dil}.md` (gomulu, `WithCulture=false`). `LegalBootstrapper` eksikleri ekler, DEGISTIRILMEMIS metni yeni sablona gunceller, degistirilmise dokunmaz.
  - Metinler UYGULAMA SAHIBININDIR (veri sorumlusu): sablonlarda satici (Booster) adi GECMEZ. Kurum bilgileri `LegalProfile` -> `{{organization}}` vb. yer tutucular.
  - Markdown `LegalMarkdown`: ham HTML kapali, yalnizca http(s)/mailto/tel/ic baglanti; yer tutucu degerleri encode edilir.
  - Ekran yazilari: `UiText` (resx anahtarini kurulum genelinde ezer; `OverridableStringLocalizer`). `<`/`>` yasak, `{0}` yer tutuculari korunmali.
  - Panel uclari: `/idp/system/legal/*`, `/idp/system/ui-texts` (panel scope + admin rolu).
  - KVKK 2026/347: aydinlatma / gizlilik metni "kabul" ettirilmez (kayit ekraninda yalnizca kullanim kosullari kabul edilir); acik riza hizmet sartina baglanmaz.
- **Lisans sitesi (bizim metinlerimiz):** `/{dil}/legal/*` (kunye, site kosullari, lisans sozlesmesi, satis kosullari, KVKK/gizlilik, cerez, destek + veri isleme eki, ucuncu taraf bildirimleri). Sirket bilgileri `appsettings` `LegalInfo` (bos/"[...]" iken sayfalarda taslak uyarisi). Siparis ve deneme formlarinda isaretsiz zorunlu iki onay (B2B beyani + kosullar); kabul surumu/zaman/IP siparise yazilir. Yayindan once avukat incelemesi sart.
