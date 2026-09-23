// Mesaj anahtarları TEK KAYNAKTA: Booster.Auth.Common.Internal.Constants.Messages.
// Hem hatayı fırlatan servis hem metni seed eden bu servis aynı sınıfları kullanır.
// Kopya tutulsaydı bir anahtar değiştiğinde seed edilen metin sessizce eşleşmeyi
// bırakır, kullanıcı çevrilmemiş ham anahtar görürdü.
global using Booster.Auth.Common.Internal.Constants.Messages;
