// Mesaj anahtarları TEK KAYNAKTA: Booster.Auth.Common.Internal.Constants.Messages.
// Hem hatayı fırlatan servis hem metni seed eden SystemMessage servisi aynı
// sınıfları kullanır; kopya tutulmaz. Kopya tutulsaydı bir anahtar değiştiğinde
// seed edilen metin sessizce eşleşmeyi bırakır, kullanıcı çevrilmemiş kod görürdü.
global using Booster.Auth.Common.Internal.Constants.Messages;
