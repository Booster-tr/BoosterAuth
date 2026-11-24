title: KVKK Aydınlatma Metni
summary: Oturum açma ve hesap hizmeti kapsamında kişisel verilerinizin nasıl işlendiğine dair 6698 sayılı Kanun md.10 uyarınca bilgilendirme.
date: 2026-11-24
---
*Bu metin bir şablondur: {{organization}} olarak kendi işleyişinize göre düzenleyin ve yayınlamadan önce hukuk danışmanınıza kontrol ettirin. Düzenledikten sonra bu satırı silin.*

Bu metin, {{organization}} tarafından sunulan oturum açma, kayıt ve hesap yönetimi hizmetini kullanırken kişisel verilerinizin nasıl işlendiğini açıklar. 6698 sayılı Kişisel Verilerin Korunması Kanunu (KVKK) md.10 kapsamında bilgilendirme amacı taşır. Bu metni onaylamanız gerekmez.

## Veri sorumlusu

Kişisel verileriniz bakımından veri sorumlusu **{{organization}}**'dır.

| Bilgi | Değer |
|---|---|
| Unvan | {{organization}} |
| Sicil / MERSİS | {{registry}} |
| Adres | {{address}} |
| E-posta | {{email}} |
| KEP adresi | {{kep}} |
| Telefon | {{phone}} |
| Web sitesi | {{website}} |

## İşlenen kişisel veri kategorileri

Hangi verilerin işlendiği, hangi giriş yöntemlerini kullandığınıza ve {{organization}}'ın hangi özellikleri açtığına göre değişir.

- **Kimlik:** görünen ad, kullanıcı adı, sistemin size verdiği benzersiz kullanıcı numarası.
- **İletişim:** e-posta adresi, telefon numarası ve bunların doğrulanıp doğrulanmadığı bilgisi.
- **Giriş bilgileri:** parolanızın geri çevrilemez özeti (hash), parolanın yeniden kullanılmasını önlemek için önceki parola özetleri, parola değişiklik tarihi, hatalı giriş sayısı ve geçici hesap kilidi bilgisi. Parolanızın kendisi saklanmaz.
- **Doğrulama kodları:** e-posta veya SMS ile gönderilen tek kullanımlık kodlar. Kodlar kısa süre geçerlidir.
- **Oturum ve güvenlik kayıtları:** oturum ve yenileme anahtarlarının özetleri, oturumun açıldığı IP adresi, tarayıcı ve cihaz bilgisi (user agent), kullanılan giriş yöntemi, oturum açılış, son etkinlik ve bitiş zamanları.
- **Harici hesap bağlantıları:** harici bir sağlayıcıyla (örneğin Google, Microsoft, Apple, LinkedIn, Facebook, GitHub, X veya kurumsal OIDC/SAML sağlayıcısı) giriş yaparsanız, o sağlayıcıdaki kullanıcı kimliğiniz ve son giriş zamanı; sağlayıcının bize ilettiği ad ve e-posta gibi bilgiler.
- **Kurumsal dizin bilgileri:** kurum dizini (LDAP / Active Directory) ile giriş yapıyorsanız, dizinden alınan kullanıcı adı, ad ve iletişim bilgileri.
- **Yetki bilgileri:** size atanan roller ve yetkiler; hangi uygulamaya hangi bilgilerinizin paylaşılmasına izin verdiğiniz.
- **Davet bilgileri:** bir yönetici sizi davet ettiyse, davet için kullanılan e-posta adresi ve davetin durumu.

Bu hizmet özel nitelikli kişisel veri (örneğin sağlık veya biyometrik veri) işlemez. [Kurumunuz bu tür veri işliyorsa bu cümleyi değiştirin ve KVKK md.6 kapsamındaki işleme şartını belirtin.]

## İşleme amaçları ve hukuki sebepler

Her faaliyet için hukuki sebep KVKK md.5/2'deki ilgili bent belirtilerek gösterilmiştir.

| Faaliyet | İşlenen veriler | Amaç | Hukuki sebep |
|---|---|---|---|
| Hesap oluşturma, davet kabulü, hesap bilgilerinin güncellenmesi | Kimlik, iletişim, giriş bilgileri | Hesabınızı açmak ve yönetmek | md.5/2-c: sözleşmenin kurulması ve ifası için gerekli olması |
| Oturum açma ve oturumun sürdürülmesi (tek oturum açma dahil) | Kimlik, giriş bilgileri, oturum kayıtları | Kimliğinizi doğrulamak ve bağlı uygulamalara erişiminizi sağlamak | md.5/2-c |
| E-posta veya SMS ile doğrulama kodu, parolasız giriş, iki aşamalı doğrulama | İletişim, doğrulama kodları | Girişin size ait olduğunu doğrulamak, e-posta ve telefonunuzu doğrulamak | md.5/2-c; hesabın ele geçirilmesini önleme yönüyle md.5/2-f: meşru menfaat |
| Parola sıfırlama | İletişim, doğrulama kodları, IP adresi | Hesabınıza yeniden erişmenizi güvenli şekilde sağlamak | md.5/2-c; kötüye kullanımı önleme yönüyle md.5/2-f |
| Harici sağlayıcı veya kurum dizini ile giriş | Harici hesap bağlantıları, kurumsal dizin bilgileri | Seçtiğiniz yöntemle giriş yapabilmeniz | md.5/2-c |
| Güvenlik kayıtları, hatalı giriş takibi, geçici kilit, parola geçmişi | Oturum ve güvenlik kayıtları, giriş bilgileri | Yetkisiz erişimi, deneme-yanılma saldırılarını ve hesap ele geçirmeyi önlemek; güvenlik olaylarını incelemek | md.5/2-f: hesapların ve sistemlerin güvenliğini sağlamadaki meşru menfaat (temel hak ve özgürlüklerinize zarar vermemek kaydıyla) |
| Bağlı uygulamalara bilgi iletimi | Kullanıcı numarası, ad, e-posta ve doğrulanma durumu, roller ve yetkiler | Giriş yaptığınız uygulamanın sizi tanıması | md.5/2-c. Hangi bilgilerin paylaşılacağı uygulama izin ekranında gösterilir. Bu ekrandaki onay, KVKK anlamında açık rıza değil, hizmetin sizin talebinizle sunulmasıdır. |
| Yetkili makamlara bilgi verilmesi | Talebin kapsamına giren veriler | Yasal yükümlülüklere uymak | md.5/2-ç: hukuki yükümlülük. [Tabi olduğunuz sektörel mevzuatı yazın, örneğin 5651, BDDK, SPK düzenlemeleri; yoksa bu satırı genel bırakın.] |
| Uyuşmazlıkların çözümü | İlgili kayıtlar | Hakların tesisi, kullanılması veya korunması | md.5/2-e |

[Hizmet kurum çalışanlarına sunuluyorsa: "Çalışanlarımız bakımından hesap ve giriş işlemleri iş sözleşmesinin ifası (md.5/2-c) kapsamındadır." cümlesini ekleyin.]

Yukarıdaki faaliyetler için açık rızanız istenmez. Açık rızaya dayanan isteğe bağlı bir işleme varsa, bu ayrı bir metinle ve ayrı olarak sorulur; hizmeti kullanmanız buna bağlı değildir.

## Verilerin toplanma yöntemi

- Kayıt, giriş, parola ve hesap sayfalarındaki elektronik formlara girdiğiniz bilgilerden.
- Giriş sırasında tarayıcınızdan otomatik olarak (IP adresi, tarayıcı bilgisi, çerezler). Çerezler için [Çerez Politikası](/cookies) sayfasına bakın.
- Giriş için seçtiğiniz harici kimlik sağlayıcısından veya kurum dizininden (LDAP / Active Directory).
- Sizi davet eden ya da hesabınızı oluşturan yöneticiden.

## SMS ve e-posta doğrulama kodları

Doğrulama kodları yalnızca giriş, kayıt, parola sıfırlama veya iletişim bilgisi doğrulama amacıyla gönderilir. Kodu kimseyle paylaşmayın: kodu ele geçiren kişi hesabınıza sizin adınıza erişebilir. Bu kodlar pazarlama izni veya veri işleme rızası almak için kullanılmaz.

## Verilerin aktarıldığı kişiler ve amaçları

| Alıcı grubu | Aktarılan veriler | Amaç |
|---|---|---|
| Barındırma ve altyapı hizmeti sağlayıcısı [Sağlayıcı adını yazın veya kendi sunucularımızda barındırılıyorsa bu satırı silin] | Hizmette işlenen tüm veriler (veri işleyen sıfatıyla) | Hizmetin çalıştırılması |
| E-posta gönderim sağlayıcısı [Sağlayıcı adını yazın] | E-posta adresi, ad, kod veya bağlantı içeren ileti | Doğrulama kodu, davet ve bildirim e-postalarının iletilmesi |
| SMS sağlayıcısı [Sağlayıcı adını yazın; SMS kullanılmıyorsa silin] | Telefon numarası, kod içeren ileti | Doğrulama kodlarının iletilmesi |
| Giriş için seçtiğiniz harici kimlik sağlayıcısı | Yönlendirme sırasında giriş isteğine ait teknik bilgiler | Kimliğinizi o sağlayıcıda doğrulamanız. Sağlayıcı verilerinizi kendi gizlilik politikasına göre işler. |
| Giriş yaptığınız bağlı uygulamalar | Kullanıcı numarası ve izin ekranında gösterilen bilgiler (ad, e-posta, roller, yetkiler) | Uygulamaya erişiminizin sağlanması |
| Yetkili kamu kurum ve kuruluşları, mahkemeler | Talebin kapsamına giren veriler | Hukuki yükümlülüklerin yerine getirilmesi |
| [Hukuk ve denetim danışmanları; yoksa silin] | [Gerekli olan veriler] | [Amaç] |

## Yurt dışına aktarım

[Barındırma, e-posta veya SMS sağlayıcınız Türkiye dışında ise bu bölümü doldurun; yurt dışına aktarım yoksa bu bölümü tamamen silin.]

Kişisel verileriniz, [sağlayıcı türü, örneğin barındırma sağlayıcımız] aracılığıyla [Barındırma ülkesini yazın] ülkesine aktarılmaktadır. Bu aktarım KVKK md.9 uyarınca [Kişisel Verileri Koruma Kurulu'nun ilan ettiği standart sözleşme imzalanarak ve Kurum'a bildirilerek / ilgili yeterlilik kararına dayanarak] yapılır. Sürekli yapılan bu tür aktarımlar için açık rıza yoluna başvurulmaz.

Harici bir kimlik sağlayıcısıyla giriş yapmayı seçerseniz, bu sağlayıcıların bir kısmı yurt dışında yerleşiktir. [Bu durumun değerlendirmesini hukuk danışmanınızla yapın ve sonucu buraya yazın.]

## Saklama süreleri

| Veri | Süre |
|---|---|
| Hesap bilgileri | Hesap açık kaldığı sürece; hesap silindiğinde kimlik ve iletişim bilgileri silinir veya anonim hale getirilir. [Ek süre varsa yazın] |
| Doğrulama kodları | Kodun geçerlilik süresi kadar (dakikalar mertebesinde) |
| Oturum kayıtları (IP adresi, tarayıcı bilgisi dahil) | [Süreyi yazın, örneğin oturum bitiminden sonra 1 yıl] |
| Güvenlik ve olay kayıtları | [Süreyi yazın, örneğin 2 yıl; sektörel mevzuat daha uzun süre öngörüyorsa onu yazın] |
| Parola geçmişi | [Süreyi veya kayıt sayısını yazın] |
| Uygulama izinleri | İzin geri alınana, süresi dolana veya hesap silinene kadar |

Süre dolduğunda veriler silinir, yok edilir veya anonim hale getirilir. [Kişisel Veri Saklama ve İmha Politikanız varsa ona bağlantı verin.]

## KVKK md.11 kapsamındaki haklarınız

{{organization}}'a başvurarak:

- Kişisel verilerinizin işlenip işlenmediğini öğrenebilir, işlendiyse buna ilişkin bilgi isteyebilirsiniz.
- Verilerinizin hangi amaçla işlendiğini ve bu amaca uygun kullanılıp kullanılmadığını öğrenebilirsiniz.
- Verilerinizin yurt içinde veya yurt dışında kimlere aktarıldığını öğrenebilirsiniz.
- Eksik veya yanlış işlenmiş verilerin düzeltilmesini isteyebilirsiniz.
- İşlenme sebepleri ortadan kalkan verilerin silinmesini veya yok edilmesini isteyebilirsiniz.
- Düzeltme ve silme işlemlerinin, verilerin aktarıldığı kişilere bildirilmesini isteyebilirsiniz.
- Yalnızca otomatik sistemlerle yapılan analiz sonucunda aleyhinize bir sonuç çıkmasına itiraz edebilirsiniz.
- Hukuka aykırı işleme nedeniyle zarara uğradıysanız zararın giderilmesini talep edebilirsiniz.

Hesap sayfanız üzerinden bilgilerinizi güncelleyebilir, açık oturumlarınızı kapatabilir ve {{organization}} bu özelliği açtıysa hesabınızı silebilirsiniz.

## Başvuru yöntemi

Başvurunuzu Türkçe olarak aşağıdaki yollardan biriyle iletebilirsiniz:

- **Yazılı olarak:** ıslak imzalı dilekçe ile {{address}} adresine elden veya posta yoluyla.
- **KEP ile:** {{kep}} adresine.
- **E-posta ile:** sistemimizde kayıtlı e-posta adresinizden {{email}} adresine.
- **Güvenli elektronik imza veya mobil imza ile:** imzalı başvurunuzu {{email}} adresine gönderebilirsiniz.

Başvurunuzda adınız soyadınız, başvuru yazılıysa imzanız, T.C. kimlik numaranız (yabancıysanız uyruğunuz ve pasaport numaranız), tebligata esas adresiniz, varsa e-posta adresiniz veya telefon numaranız ve talebinizin konusu yer almalıdır.

Başvurunuz en geç **30 gün** içinde sonuçlandırılır. Kural olarak ücret alınmaz. İşlem ayrıca bir maliyet gerektirirse, Kişisel Verileri Koruma Kurulu'nun belirlediği tarifeye göre ücret alınabilir.

## Değişiklikler

Bu metin, işleme faaliyetlerimiz değiştiğinde güncellenir. Güncel sürüm ve yürürlük tarihi bu sayfada yayımlanır. Amaçta esaslı bir değişiklik olursa, yeni işlemeden önce ayrıca bilgilendirilirsiniz.

Ayrıntılı bilgi için [Gizlilik Politikası](/privacy) ve [Çerez Politikası](/cookies) sayfalarına bakabilirsiniz.
