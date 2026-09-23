/* ==========================================================================
   Booster Auth - auth.js
   Vanilla JS, bagimliliksiz, tek IIFE. `defer` ile yuklenir.

   Ilerlemeli iyilestirme sozlesmesi (pazarlik disi):
   JS'in yaptigi hicbir sey formun gonderilebilmesi icin gerekli DEGILDIR.
   JS yalnizca (a) sunucuda `hidden` render edilmis kontrolleri acar,
   (b) girdiyi normalize eder, (c) cift gonderimi engeller.

   DOGRULAMA BURADA YAPILMAZ.
   Kurallar istemciye gore degisir (parola politikasi havuzdan gelir) ve
   mesajlar dile baglidir; ikisini de yalnizca sunucu bilir. Istemci tarafi
   yalnizca tarayicinin kendi `required` kontrolunu kullanir -- metnini
   tarayici kullanicinin dilinde uretir, bizim cevirmemiz gerekmez.

   Parola gucu gostergesi de KALDIRILDI: "guclu" olcutu havuz politikasina
   gore degisirken sabit bir puanlama kullaniciyi yaniltirdi.

   Moduller
     util            yardimcilar
     enhance         gizli kontrollerin acilmasi
     passwordToggle  parola goster/gizle
     capsLock        Caps Lock uyarisi
     submitGuard     yukleniyor durumu + cift gonderim kilidi
     phoneInput      telefon normalizasyonu
     userNameInput   kullanici adi normalizasyonu
     initialFocus    sayfa acilis odagi
     logoFallback    bozuk istemci logosu -> monogram
   ========================================================================== */

(function () {
    "use strict";

    /* ======================================================================
       util
       ====================================================================== */

    var $ = function (sel, root) { return (root || document).querySelector(sel); };
    var $$ = function (sel, root) {
        return Array.prototype.slice.call((root || document).querySelectorAll(sel));
    };

    /** Nazik canli bolgeye metin yazar (role="status"). Bos metin duyuru yapmaz. */
    function announce(regionId, text) {
        var region = document.getElementById(regionId);
        if (region) region.textContent = text;
    }

    /* ======================================================================
       enhance  -- ilk calisan modul
       ====================================================================== */

    function enhance() {
        // Sunucuda `hidden` render edilen, yalnizca JS ile anlamli kontrol.
        $$("[data-pw-toggle]").forEach(function (el) { el.hidden = false; });

        // noValidate KAPATILMAZ: tarayicinin kendi `required` uyarisi kullanilir.
        // Eskiden kapatilip yerine JS dogrulama konuluyordu; o dogrulama blur
        // aninda hata satiri ekleyip cikararak duzeni kaydiriyor, dugme imlecin
        // altindan kaciyor ve gonderim icin IKI tiklama gerekiyordu.
    }

    function passwordToggle() {
        // mousedown'da odagin input'tan kacmasini engelle -> caret yerinde kalir.
        document.addEventListener("mousedown", function (e) {
            var btn = e.target.closest && e.target.closest("[data-pw-toggle]");
            if (btn) e.preventDefault();
        });

        document.addEventListener("click", function (e) {
            var btn = e.target.closest && e.target.closest("[data-pw-toggle]");
            if (!btn) return;

            var input = document.getElementById(btn.getAttribute("aria-controls"));
            if (!input) return;

            var visible = input.type === "password";
            var start = input.selectionStart;
            var end = input.selectionEnd;
            var wasFocused = document.activeElement === input;

            // Node yeniden olusturulmaz; yalnizca tip degisir (parola yoneticileri bozulmasin).
            input.type = visible ? "text" : "password";

            if (wasFocused) {
                input.focus();
                try { input.setSelectionRange(start, end); } catch (err) { /* type=text disi durum */ }
            }

            btn.dataset.visible = String(visible);
            var label = visible ? btn.dataset.labelHide : btn.dataset.labelShow;
            btn.setAttribute("aria-label", label);
            btn.setAttribute("title", label);

            // Parolanin KENDISI asla duyurulmaz; yalnizca mod bildirilir.
            announce("form-live", visible ? "Parola görünür durumda." : "Parola gizlendi.");
        });
    }

    function capsLock() {
        function update(e) {
            var input = e.target;
            if (!input.matches || !input.matches("[data-caps-warn]")) return;

            var note = document.getElementById(input.id + "-caps");
            if (!note) return;

            var on = typeof e.getModifierState === "function"
                && e.getModifierState("CapsLock");
            note.hidden = !on;
        }

        document.addEventListener("keydown", update, true);
        document.addEventListener("keyup", update, true);

        document.addEventListener("blur", function (e) {
            if (e.target.matches && e.target.matches("[data-caps-warn]")) {
                var note = document.getElementById(e.target.id + "-caps");
                if (note) note.hidden = true;
            }
        }, true);
    }

    /* ======================================================================
       submitGuard
       `disabled` KULLANILMAZ: odak kaybolur, ekran okuyucu yeni etiketi
       duyurmaz ve buton name/value gondermez (izin ekraninda hayati).
       Bunun yerine aria-disabled + form uzerinde tekrar-gonderim kilidi.
       ====================================================================== */

    var GUARD_TIMEOUT_MS = 12000;

    function resetGuard(form) {
        delete form.dataset.submitting;
        form.removeAttribute("aria-busy");
        $$("[data-submit-guard]", form).forEach(function (btn) {
            btn.removeAttribute("data-state");
            btn.removeAttribute("aria-disabled");
            var label = $(".auth-btn__label", btn);
            if (label && btn.dataset.labelIdle) label.textContent = btn.dataset.labelIdle;
        });
    }

    function submitGuard() {
        $$("form[data-auth-form]").forEach(function (form) {
            form.addEventListener("submit", function (e) {
                if (form.dataset.submitting === "1") {
                    e.preventDefault();
                    return;
                }
                form.dataset.submitting = "1";
                form.setAttribute("aria-busy", "true");

                // Birden fazla submit butonu varsa (izin ekrani) yalnizca
                // basilan buton "yukleniyor" olur, digeri sadece kilitlenir.
                var pressed = e.submitter || document.activeElement;

                $$("[data-submit-guard]", form).forEach(function (btn) {
                    btn.setAttribute("aria-disabled", "true");

                    if (btn !== pressed) return;

                    var label = $(".auth-btn__label", btn);
                    if (label) {
                        btn.dataset.labelIdle = label.textContent;
                        if (btn.dataset.labelBusy) label.textContent = btn.dataset.labelBusy;
                    }
                    btn.dataset.state = "loading";
                    announce("form-live", (btn.dataset.labelBusy || "Gönderiliyor")
                        + ", lütfen bekleyin.");
                });

                // Emniyet supabi: gezinme gerceklesmezse kilit acilir.
                // Sessizce acilmaz -- "Giris yapiliyor, lutfen bekleyin" duyurusunu
                // duymus bir kullanici bir daha hicbir sey duymadan beklemesin.
                setTimeout(function () {
                    if (form.dataset.submitting !== "1") return;
                    resetGuard(form);
                    announce("form-live",
                        "İşlem beklenenden uzun sürdü. Lütfen tekrar deneyin.");
                }, GUARD_TIMEOUT_MS);
            });
        });

        // Geri tusuyla bfcache'ten donen kullanici donmus spinner gormemeli.
        window.addEventListener("pageshow", function (e) {
            if (e.persisted) $$("form[data-auth-form]").forEach(resetGuard);
        });
    }
    function digitsOnly(value, max) {
        var d = value.replace(/\D/g, "");
        return max ? d.slice(0, max) : d;
    }

    function phoneInput() {
        var country = $("[data-phone-country]");
        var number = $("[data-phone-number]");
        if (!number) return;

        if (country) {
            country.addEventListener("input", function () {
                // "+90" ve "0090" -> "90"
                var value = country.value.replace(/^\+/, "").replace(/^00/, "");
                country.value = digitsOnly(value, 4);
            });
            country.addEventListener("blur", function () {
                if (!country.value) country.value = "90";
            });
        }

        number.addEventListener("input", function () {
            var caret = number.selectionStart;
            var before = number.value;
            var digits = digitsOnly(before, 15);

            // Ulke kodu 90 iken kullanicinin yazdigi bastaki 0 atilir (0532... -> 532...)
            if (country && country.value === "90") digits = digits.replace(/^0+/, "");

            if (digits !== before) {
                number.value = digits;
                var removed = before.length - digits.length;
                try { number.setSelectionRange(Math.max(0, caret - removed), Math.max(0, caret - removed)); }
                catch (err) { /* yoksay */ }
            }
        });

        // Akilli yapistirma: "+90 532 111 22 33" -> kod 90, numara 5321112233
        number.addEventListener("paste", function (e) {
            var text = (e.clipboardData || window.clipboardData).getData("text");
            if (!text) return;

            var international = /^\s*(\+|00)/.test(text);
            var digits = digitsOnly(text);
            if (!digits) return;

            e.preventDefault();

            if (international && country && digits.length > 10) {
                // Ulke kodunu bastan ayir (genel kural: son 10 hane numaradir).
                // Programatik atama `input` olayini tetiklemedigi icin "00" onekini
                // burada da temizlemek zorunludur; aksi hâlde PhoneCountry="0090" gider.
                country.value = digitsOnly(digits.slice(0, digits.length - 10).replace(/^00/, ""), 4);
                digits = digits.slice(-10);
            } else if (country && country.value === "90") {
                digits = digits.replace(/^0+/, "");
            }

            number.value = digits.slice(0, 15);

            // Dogrulama, eslesme notu ve guc gostergesi `input` olayini dinler;
            // programatik atama sonrasi elle tetiklenmezse hicbiri calismaz.
            number.dispatchEvent(new Event("input", { bubbles: true }));
            if (country) country.dispatchEvent(new Event("input", { bubbles: true }));
        });
    }

    /* ======================================================================
       userNameInput
       Gecersiz karakterler sessizce silinir; ilk seferinde bir kez neden
       silindigi soylenir (sessiz silme tek basina kafa karistiricidir).
       ====================================================================== */

    function userNameInput() {
        var input = $("[data-username]");
        if (!input) return;

        input.addEventListener("input", function () {
            var caret = input.selectionStart;
            var before = input.value;
            var cleaned = before.replace(/[^a-zA-Z0-9._-]/g, "");

            if (cleaned !== before) {
                input.value = cleaned;
                var removed = before.length - cleaned.length;
                try { input.setSelectionRange(caret - removed, caret - removed); }
                catch (err) { /* yoksay */ }

                // Rengi degistirmek tek basina yeterli degildir (SC 1.4.1):
                // ne oldugu METINLE soylenir, bir kez.
                var hint = document.getElementById(input.id + "-hint");
                if (hint && !hint.dataset.warned) {
                    hint.dataset.warned = "1";
                    hint.classList.add("auth-note--warn");
                    hint.textContent = "Desteklenmeyen karakterler kaldırıldı. "
                        + "Yalnızca harf, rakam, nokta, alt çizgi ve tire kullanabilirsiniz.";
                }
            }
        });

        input.addEventListener("blur", function () {
            var before = input.value;
            input.value = input.value.replace(/^[._-]+/, "").replace(/[._-]+$/, "");

            // Belge duzeyindeki blur dinleyicisi (capture) bu kirpmadan ONCE calisir,
            // yani kirpilmamis degeri dogrular. Deger degistiyse yeniden degerlendir.
            if (input.value !== before) evaluateField(input);
        });
    }

    /* ======================================================================
       initialFocus
       Tek karar noktasi. HTML `autofocus` niteligi KULLANILMAZ; o nitelik bu
       koddan once tetiklenir ve hata varken cift odak sicramasi olusturur.
       ====================================================================== */

    function initialFocus() {
        // 1) Hata ozeti varsa odak oradadir: kullanici once kac sorun oldugunu duyar.
        //    Alan bazli odak, bos anahtarli ModelState hatalarini ("Bilgiler
        //    eslesmedi") tamamen sessizlestirirdi.
        var summary = $("[data-autofocus-summary]");
        if (summary) {
            summary.focus();
            return;
        }

        // 2) Temiz sayfada yalnizca isaretli alan ve yalnizca masaustunde.
        //    Dar ekranda sanal klavye formu ortmemeli.
        if (window.matchMedia && !window.matchMedia("(min-width: 640px)").matches) return;

        var first = $("[data-initial-focus]");
        if (first && !first.value) first.focus();
    }

    /* ======================================================================
       logoFallback
       ====================================================================== */

    function logoFallback() {
        $$("[data-logo-fallback]").forEach(function (img) {
            function fail() {
                img.hidden = true;
                var mono = img.parentNode.querySelector(".auth-appmark__mono");
                if (mono) mono.hidden = false;
            }
            img.addEventListener("error", fail);
            // Onbellekten gelen bozuk gorsel: dinleyici gec baglanmis olabilir.
            if (img.complete && img.naturalWidth === 0) fail();
        });
    }

    /* ======================================================================
       init
       ====================================================================== */

    enhance();
    passwordToggle();
    capsLock();
    submitGuard();
    phoneInput();
    userNameInput();
    logoFallback();
    initialFocus();
})();
