using Booster.Database.Repositories;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.NotificationService.Features._base.Entities;
using Booster.NotificationService.Infrastructures.Templating;

namespace Booster.NotificationService.Features._base;

public class NotificationBootstrapper(
    IAsyncRepository<NotificationTemplate, NotificationDbContext> templateRepository)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var added = false;
        foreach (var item in Catalog)
        {
            var exists = await templateRepository.AnyAsync(
                x => x.Key == item.Key && x.Channel == item.Channel && x.UserPoolId == null,
                cancellationToken: ct);
            if (exists)
                continue;

            var template = new NotificationTemplate
            {
                Key = item.Key,
                Channel = item.Channel,
                UserPoolId = null,
                IsActive = true
            };
            template.SetId();
            template.Translations =
            [
                NewTranslation(template.Id, "tr-tr", item.TrSubject, item.TrBody),
                NewTranslation(template.Id, "en-us", item.EnSubject, item.EnBody),
            ];

            await templateRepository.AddAsync(template, ct);
            added = true;
        }

        if (added)
            await templateRepository.SaveChangesAsync(ct);
    }

    private static NotificationTemplateTranslation NewTranslation(Guid templateId, string lang, string subject, string body)
    {
        var translation = new NotificationTemplateTranslation
        {
            NotificationTemplateId = templateId,
            LanguageCode = lang,
            Subject = subject,
            Body = body
        };
        translation.SetId();
        return translation;
    }

    private readonly record struct Template(
        string Key,
        NotificationChannel Channel,
        string TrSubject,
        string TrBody,
        string EnSubject,
        string EnBody);

    private static string CodeBody(string preheader, string heading, string intro, string footer) =>
        EmailLayout.Wrap(preheader, heading,
            EmailLayout.Paragraph(intro) +
            EmailLayout.CodeBox("{{Code}}"),
            footer);


    private static string InvitationBody(
        string preheader, string heading, string intro, string button, string fallback, string footer) =>
        EmailLayout.Wrap(preheader, heading,
            EmailLayout.Paragraph(intro) +
            EmailLayout.Button(button, "{{InvitationUrl}}") +
            EmailLayout.LinkFallback(fallback, "{{InvitationUrl}}"),
            footer);

    private static readonly Template[] Catalog =
    [
        new(NotificationTemplateKeys.UserInvitation, NotificationChannel.Email,
            "Hesabınız oluşturuldu",
            InvitationBody(
                preheader: "Hesabınız hazır; parolanızı belirlemek için 1 gününüz var.",
                heading: "Hesabınız oluşturuldu",
                intro: "Merhaba {{DisplayName}}, sizin için bir hesap oluşturuldu. Kullanmaya başlamak için aşağıdaki düğmeden parolanızı belirleyin.",
                button: "Parolamı belirle",
                fallback: "Düğme çalışmıyorsa bu bağlantıyı tarayıcınıza yapıştırın:",
                footer: "Bağlantı 1 gün geçerlidir. Bu daveti beklemiyorsanız bu e-postayı yok sayabilirsiniz."),
            "Your account has been created",
            InvitationBody(
                preheader: "Your account is ready — you have 1 day to set your password.",
                heading: "Your account has been created",
                intro: "Hi {{DisplayName}}, an account has been created for you. Use the button below to set your password and get started.",
                button: "Set my password",
                fallback: "If the button does not work, paste this link into your browser:",
                footer: "The link is valid for 1 day. If you were not expecting this invitation, you can ignore this email.")),

        new(NotificationTemplateKeys.EmailLoginCode, NotificationChannel.Email,
            "Giriş kodunuz",
            CodeBody(
                preheader: "Giriş kodunuz 5 dakika geçerli.",
                heading: "Giriş kodunuz",
                intro: "Merhaba {{DisplayName}}, hesabınıza giriş yapmak için aşağıdaki kodu giriş ekranına girin.",
                footer: "Kod 5 dakika geçerlidir ve yalnız girişi başlattığınız tarayıcıda çalışır. Bu girişi siz başlatmadıysanız bu e-postayı yok sayabilirsiniz; hesabınıza erişilmez."),
            "Your sign-in code",
            CodeBody(
                preheader: "Your sign-in code is valid for 5 minutes.",
                heading: "Your sign-in code",
                intro: "Hi {{DisplayName}}, enter the code below on the sign-in screen to access your account.",
                footer: "The code is valid for 5 minutes and only works in the browser where you started signing in. If you did not start this, you can ignore this email; nobody gains access to your account.")),

        new(NotificationTemplateKeys.PhoneLoginCode, NotificationChannel.Sms,
            "Giriş kodu",
            "Booster giriş kodun: {{Code}}. Kodu kimseyle paylaşma.",
            "Sign-in code",
            "Your Booster sign-in code: {{Code}}. Do not share it with anyone."),

        new(NotificationTemplateKeys.PasswordReset, NotificationChannel.Email,
            "Parola sıfırlama kodunuz",
            CodeBody(
                preheader: "Parola sıfırlama kodunuz 15 dakika geçerli.",
                heading: "Parola sıfırlama kodunuz",
                intro: "Hesabınız için parola sıfırlama talebinde bulunuldu. Yeni parolanızı belirlemek için aşağıdaki kodu sıfırlama ekranına girin.",
                footer: "Kod 15 dakika geçerlidir ve yalnız sıfırlamayı başlattığınız tarayıcıda çalışır. Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz; parolanız değişmez."),
            "Your password reset code",
            CodeBody(
                preheader: "Your password reset code is valid for 15 minutes.",
                heading: "Your password reset code",
                intro: "A password reset was requested for your account. Enter the code below on the reset screen to choose a new password.",
                footer: "The code is valid for 15 minutes and only works in the browser where you started the reset. If you did not request this, you can ignore this email; your password stays unchanged.")),

        new(NotificationTemplateKeys.EmailChange, NotificationChannel.Email,
            "E-posta değişikliği kodunuz",
            CodeBody(
                preheader: "Yeni e-posta adresinizi doğrulamak için kodunuz.",
                heading: "E-posta değişikliği kodunuz",
                intro: "Hesabınızın e-posta adresini bu adresle değiştirmek istediniz. Değişikliği tamamlamak için aşağıdaki kodu Hesabım ekranına girin.",
                footer: "Kod 15 dakika geçerlidir. Bu talebi siz yapmadıysanız bu e-postayı yok sayabilirsiniz; hesabınızda bir değişiklik olmaz."),
            "Your email change code",
            CodeBody(
                preheader: "Your code to verify your new email address.",
                heading: "Your email change code",
                intro: "You asked to change your account's email address to this one. Enter the code below on the My account screen to finish.",
                footer: "The code is valid for 15 minutes. If you did not request this, you can ignore this email; nothing changes on your account.")),

        new(NotificationTemplateKeys.PasswordReset, NotificationChannel.Sms,
            "Parola sıfırlama kodu",
            "Booster parola sıfırlama kodun: {{Code}}. Kodu kimseyle paylaşma.",
            "Password reset code",
            "Your Booster password reset code: {{Code}}. Do not share it with anyone."),

        new(NotificationTemplateKeys.EmailVerification, NotificationChannel.Email,
            "E-posta doğrulama kodunuz",
            CodeBody(
                preheader: "E-posta adresinizi doğrulamak için kodunuz.",
                heading: "E-posta doğrulama kodunuz",
                intro: "E-posta adresinizin size ait olduğunu doğrulamak için aşağıdaki kodu giriş ekranına girin.",
                footer: "Kod 5 dakika geçerlidir. Bu hesabı siz oluşturmadıysanız bu e-postayı yok sayabilirsiniz."),
            "Your email verification code",
            CodeBody(
                preheader: "Your code to verify your email address.",
                heading: "Your email verification code",
                intro: "Enter the code below on the sign-in screen to confirm this email address belongs to you.",
                footer: "The code is valid for 5 minutes. If you did not create this account, you can safely ignore this email.")),

        new(NotificationTemplateKeys.MfaCode, NotificationChannel.Email,
            "Giriş doğrulama kodunuz",
            CodeBody(
                preheader: "Giriş için doğrulama kodunuz.",
                heading: "Giriş doğrulama kodunuz",
                intro: "Girişi tamamlamak için aşağıdaki kodu doğrulama ekranına girin.",
                footer: "Kodu siz istemediyseniz birisi parolanızı biliyor olabilir; en kısa sürede parolanızı değiştirin."),
            "Your sign-in verification code",
            CodeBody(
                preheader: "Your verification code for signing in.",
                heading: "Your sign-in verification code",
                intro: "Enter the code below on the verification screen to finish signing in.",
                footer: "If you did not request this code, someone may know your password; change it as soon as possible.")),

        new(NotificationTemplateKeys.MfaCode, NotificationChannel.Sms,
            "Giriş doğrulama kodu",
            "Booster giriş kodun: {{Code}}. Kodu kimseyle paylaşma.",
            "Sign-in verification code",
            "Your Booster sign-in code: {{Code}}. Do not share it with anyone."),

        new(NotificationTemplateKeys.PhoneVerification, NotificationChannel.Sms,
            "Telefon doğrulama kodu",
            "Booster telefon doğrulama kodun: {{Code}}. Kodu kimseyle paylaşma.",
            "Phone verification code",
            "Your Booster phone verification code: {{Code}}. Do not share it with anyone."),

        // --- Lisans portali (yalnizca Booster'in kendi musteri iletisimi) -------------------
        new(NotificationTemplateKeys.LicenseTrialIssued, NotificationChannel.Email,
            "Booster Auth deneme lisansınız",
            EmailLayout.Wrap("Deneme lisansınız hazır; yönetim paneline yapıştırmanız yeterli.", "Deneme lisansınız hazır",
                EmailLayout.Paragraph("Merhaba {{CustomerName}}, <b>{{InstallationId}}</b> kurulumunuz için deneme lisansınız üretildi. Bitiş: <b>{{ExpiresAt}}</b>.") +
                EmailLayout.Paragraph("Booster Auth yönetim panelinde <b>Sistem &gt; Lisans</b> ekranını açın ve aşağıdaki anahtarı yapıştırın:") +
                EmailLayout.TokenBox("{{Token}}") +
                EmailLayout.Button("Lisanslarımı görüntüle", "{{PortalUrl}}"),
                "Deneme süresi bitmeden lisans satın alarak kesintisiz devam edebilirsiniz."),
            "Your Booster Auth trial license",
            EmailLayout.Wrap("Your trial license is ready; just paste it into the management panel.", "Your trial license is ready",
                EmailLayout.Paragraph("Hi {{CustomerName}}, a trial license has been issued for installation <b>{{InstallationId}}</b>. Expires: <b>{{ExpiresAt}}</b>.") +
                EmailLayout.Paragraph("Open <b>System &gt; License</b> in the Booster Auth management panel and paste the key below:") +
                EmailLayout.TokenBox("{{Token}}") +
                EmailLayout.Button("View my licenses", "{{PortalUrl}}"),
                "Buy a license before the trial ends to continue without interruption.")),

        new(NotificationTemplateKeys.LicenseOrderReceived, NotificationChannel.Email,
            "Siparişiniz alındı: {{ReferenceCode}}",
            EmailLayout.Wrap("Siparişiniz alındı; havale/EFT ile ödeyebilirsiniz.", "Siparişiniz alındı",
                EmailLayout.Paragraph("Merhaba {{CustomerName}}, siparişiniz alındı. Ödemeniz onaylandığında lisansınız hazırlanır ve size e-postayla gönderilir.") +
                EmailLayout.Facts(("Sipariş", "{{ReferenceCode}}"), ("Plan", "{{PlanName}}"), ("Tutar", "{{Amount}}"),
                    ("Banka", "{{BankName}}"), ("Alıcı", "{{AccountName}}"), ("IBAN", "{{Iban}}")) +
                EmailLayout.Paragraph("Havale/EFT açıklamasına mutlaka <b>{{ReferenceCode}}</b> yazın.") +
                EmailLayout.Button("Siparişi görüntüle", "{{OrderUrl}}"),
                "Bu siparişi siz vermediyseniz bu e-postayı yok sayabilirsiniz."),
            "Order received: {{ReferenceCode}}",
            EmailLayout.Wrap("Your order has been received; you can pay by bank transfer.", "Your order has been received",
                EmailLayout.Paragraph("Hi {{CustomerName}}, your order has been received. Once your payment is confirmed, your license will be issued and emailed to you.") +
                EmailLayout.Facts(("Order", "{{ReferenceCode}}"), ("Plan", "{{PlanName}}"), ("Amount", "{{Amount}}"),
                    ("Bank", "{{BankName}}"), ("Recipient", "{{AccountName}}"), ("IBAN", "{{Iban}}")) +
                EmailLayout.Paragraph("Please write <b>{{ReferenceCode}}</b> in the transfer description.") +
                EmailLayout.Button("View order", "{{OrderUrl}}"),
                "If you did not place this order, you can ignore this email.")),

        new(NotificationTemplateKeys.LicenseIssued, NotificationChannel.Email,
            "Lisansınız hazır: {{ReferenceCode}}",
            EmailLayout.Wrap("Ödemeniz onaylandı; lisansınız hazır.", "Lisansınız hazır",
                EmailLayout.Paragraph("Merhaba {{CustomerName}}, <b>{{ReferenceCode}}</b> siparişinizin ödemesi onaylandı. <b>{{PlanName}}</b> lisansınız <b>{{ExpiresAt}}</b> tarihine kadar geçerlidir.") +
                EmailLayout.Paragraph("Booster Auth yönetim panelinde <b>Sistem &gt; Lisans</b> ekranına aşağıdaki anahtarı yapıştırın; servisler yeniden başlatılmadan geçerli olur:") +
                EmailLayout.TokenBox("{{Token}}") +
                EmailLayout.Button("Lisansı görüntüle", "{{LicenseUrl}}"),
                "Teşekkür ederiz."),
            "Your license is ready: {{ReferenceCode}}",
            EmailLayout.Wrap("Your payment is confirmed; your license is ready.", "Your license is ready",
                EmailLayout.Paragraph("Hi {{CustomerName}}, payment for order <b>{{ReferenceCode}}</b> is confirmed. Your <b>{{PlanName}}</b> license is valid until <b>{{ExpiresAt}}</b>.") +
                EmailLayout.Paragraph("Paste the key below into <b>System &gt; License</b> in the Booster Auth management panel; it takes effect without restarting the services:") +
                EmailLayout.TokenBox("{{Token}}") +
                EmailLayout.Button("View license", "{{LicenseUrl}}"),
                "Thank you.")),

        new(NotificationTemplateKeys.LicenseRenewalReminder, NotificationChannel.Email,
            "Lisansınızın bitmesine {{DaysLeft}} gün kaldı",
            EmailLayout.Wrap("Lisansınızı yenileyerek kesintisiz devam edin.", "Lisansınızın bitmesine {{DaysLeft}} gün kaldı",
                EmailLayout.Paragraph("Merhaba {{CustomerName}}, <b>{{InstallationId}}</b> kurulumunuzdaki <b>{{PlanName}}</b> lisansı <b>{{ExpiresAt}}</b> tarihinde bitiyor.") +
                EmailLayout.Paragraph("Bitiş tarihinden sonra 14 günlük ek süre içinde yönetim paneli çalışmaya devam eder; ardından panel salt okunur olur. Son kullanıcı girişleri etkilenmez.") +
                EmailLayout.Button("Lisansı yenile", "{{RenewUrl}}"),
                "Erken yenilemede kalan günleriniz yanmaz; yeni süre mevcut bitiş tarihine eklenir."),
            "Your license expires in {{DaysLeft}} days",
            EmailLayout.Wrap("Renew your license to continue without interruption.", "Your license expires in {{DaysLeft}} days",
                EmailLayout.Paragraph("Hi {{CustomerName}}, the <b>{{PlanName}}</b> license on installation <b>{{InstallationId}}</b> expires on <b>{{ExpiresAt}}</b>.") +
                EmailLayout.Paragraph("After that date the management panel keeps working during a 14-day grace period, then becomes read-only. End-user sign-ins are not affected.") +
                EmailLayout.Button("Renew license", "{{RenewUrl}}"),
                "Renewing early does not waste remaining days; the new period is added to the current end date.")),

        new(NotificationTemplateKeys.LicenseTransferred, NotificationChannel.Email,
            "Lisansınız başka bir kuruluma aktarıldı",
            EmailLayout.Wrap("Lisans aktarımı yapıldı.", "Lisansınız aktarıldı",
                EmailLayout.Paragraph("Merhaba {{CustomerName}}, <b>{{PlanName}}</b> lisansınız aktarıldı.") +
                EmailLayout.Facts(("Eski kurulum", "{{FromInstallationId}}"), ("Yeni kurulum", "{{ToInstallationId}}"), ("Yapan", "{{RequestedBy}}"), ("Tarih", "{{TransferredAt}}")) +
                EmailLayout.Paragraph("Yeni kurulumun yönetim paneline yeni lisansı yapıştırın:") +
                EmailLayout.TokenBox("{{Token}}") +
                EmailLayout.Button("Lisansı görüntüle", "{{LicenseUrl}}"),
                "Bu aktarımı siz yapmadıysanız hemen bizimle iletişime geçin."),
            "Your license has been transferred to another installation",
            EmailLayout.Wrap("A license transfer was made.", "Your license has been transferred",
                EmailLayout.Paragraph("Hi {{CustomerName}}, your <b>{{PlanName}}</b> license has been transferred.") +
                EmailLayout.Facts(("Previous installation", "{{FromInstallationId}}"), ("New installation", "{{ToInstallationId}}"), ("By", "{{RequestedBy}}"), ("Date", "{{TransferredAt}}")) +
                EmailLayout.Paragraph("Paste the new license into the management panel of the new installation:") +
                EmailLayout.TokenBox("{{Token}}") +
                EmailLayout.Button("View license", "{{LicenseUrl}}"),
                "If you did not make this transfer, contact us immediately.")),
    ];
}
