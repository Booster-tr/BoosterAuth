/* ==========================================================================
   Booster Auth - auth.js
   Vanilla JS, bagimliliksiz, tek IIFE. `defer` ile yuklenir.

   Ilerlemeli iyilestirme sozlesmesi (pazarlik disi):
   JS'in yaptigi hicbir sey formun gonderilebilmesi icin gerekli DEGILDIR.
   JS yalnizca (a) sunucuda `hidden` render edilmis kontrolleri acar,
   (b) hatayi erken gosterir, (c) girdiyi normalize eder.
   JS kapaliyken parola goster/gizle dugmesi ve guc gostergesi hic gorunmez;
   olu kontrol kalmaz. Dogrulamanin tek gercek kaynagi her zaman sunucudur.

   Moduller
     util            yardimcilar
     enhance         .js sinifi, gizli kontrollerin acilmasi
     passwordToggle  parola goster/gizle
     capsLock        Caps Lock uyarisi
     strengthMeter   parola gucu + kosul listesi
     fieldValidation blur sonrasi istemci dogrulamasi + hata temizleme
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

    var prefersReducedMotion = window.matchMedia
        && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    /** Nazik canli bolgeye metin yazar (role="status"). Bos metin duyuru yapmaz. */
    function announce(regionId, text) {
        var region = document.getElementById(regionId);
        if (region) region.textContent = text;
    }

    function debounce(fn, wait) {
        var timer;
        return function () {
            var args = arguments, self = this;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(self, args); }, wait);
        };
    }

    /** Bir input'un ait oldugu .auth-field kabugu. */
    function fieldOf(input) {
        return input.closest ? input.closest(".auth-field") : null;
    }

    /**
     * Alan hatasini yazar veya temizler.
     * Hata elementi DOM'da her zaman durur (aria-describedby id'si cozulsun);
     * yalnizca icerigi degisir. Bos oldugunda CSS gizler ve erisilebilir
     * aciklamaya hicbir sey eklemez.
     */
    function setFieldError(input, message) {
        var field = fieldOf(input);
        if (!field) return;

        var box = document.getElementById(input.id + "-error");
        if (!box) return;

        if (message) {
            // Ekran okuyucu hatayi ipucundan ayirt edebilsin diye gorsel gizli onek.
            box.innerHTML = "";
            var prefix = document.createElement("span");
            prefix.className = "u-visually-hidden";
            prefix.textContent = "Hata: ";
            var text = document.createElement("span");
            text.textContent = message;
            box.appendChild(prefix);
            box.appendChild(text);

            field.classList.add("auth-field--invalid");
            input.setAttribute("aria-invalid", "true");
        } else {
            box.textContent = "";
            field.classList.remove("auth-field--invalid");
            input.removeAttribute("aria-invalid");
        }
    }

    /* ======================================================================
       enhance  -- ilk calisan modul
       ====================================================================== */

    function enhance() {
        // Sunucuda `hidden` render edilen, yalnizca JS ile anlamli kontrol.
        $$("[data-pw-toggle]").forEach(function (el) { el.hidden = false; });

        // Guc gostergesi burada ACILMAZ: parola alanina ilk odaklanmada acilir
        // (bkz. strengthMeter/reveal). Boylece bos formda ekran sakin durur ve
        // aria-describedby hicbir an gizli bir kaba isaret etmez.

        // Tarayicinin stillenemeyen, zamanla kaybolan dogrulama balonlarini kapat.
        // `required` niteligi DOM'da kalir -> durum yardimci teknolojiye acik kalir.
        $$("form[data-auth-form]").forEach(function (form) { form.noValidate = true; });
    }

    /* ======================================================================
       passwordToggle
       Erisilebilir ad DEGISIR ("Parolayi goster" <-> "Parolayi gizle");
       aria-pressed KULLANILMAZ -- "goster, basili" celiskili okunur.
       ====================================================================== */

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

    /* ======================================================================
       capsLock
       Parola alani maskeli oldugu icin kullanici Caps Lock'u fark edemez.
       Bir hata degil, bir ipucudur: canli bolge kullanilmaz (klavye durumu
       zaten isletim sistemi duzeyinde duyurulur).
       ====================================================================== */

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
       strengthMeter
       ====================================================================== */

    var STRENGTH_LABELS = ["", "Zayıf", "Orta", "Güçlü", "Çok güçlü"];

    // Turkce alfabe, guc gostergesi ve gonderim dogrulamasi tarafindan ORTAK
    // kullanilir. Ikisi ayrisirsa "Ş" buyuk harf kosulunu yesil yapar ama
    // gonderimde reddedilir: kullanici tum kosullari saglamis gorunen bir
    // listenin altinda hata mesaji gorur.
    var RE_UPPER = /[A-ZÇĞİÖŞÜ]/;
    var RE_LOWER = /[a-zçğıöşü]/;
    var RE_SYMBOL = /[^A-Za-z0-9ÇĞİÖŞÜçğıöşü]/;

    var COMMON_PASSWORDS = {};
    ["123456", "12345678", "123456789", "1234567890", "111111", "000000",
        "qwerty", "qwertyuiop", "password", "password1", "passw0rd", "admin",
        "letmein", "welcome", "iloveyou", "abc123", "dragon", "monkey", "master",
        "secret", "sunshine", "1q2w3e4r", "1q2w3e", "qazwsx", "zxcvbnm", "asdasd",
        "asdfgh", "sifre", "sifre123", "parola", "parola123", "deneme", "deneme123",
        "test123", "merhaba", "turkiye", "istanbul", "ankara", "izmir",
        "galatasaray", "fenerbahce", "besiktas", "trabzonspor", "canim", "askim"
    ].forEach(function (p) { COMMON_PASSWORDS[p] = true; });

    // Hem TR-Q hem US klavye siralari ve rakam sirasi
    var KEYBOARD_ROWS = [
        "qwertyuiopğü", "asdfghjklşi", "zxcvbnmöç",
        "qwertyuiop", "asdfghjkl", "zxcvbnm", "1234567890"
    ];

    /** abcd / 4321 gibi n uzunlugunda artan/azalan dizi var mi? */
    function hasRun(value, n) {
        var s = value.toLowerCase();
        for (var i = 0; i + n <= s.length; i++) {
            var step = s.charCodeAt(i + 1) - s.charCodeAt(i);
            if (step !== 1 && step !== -1) continue;
            var ok = true;
            for (var k = 1; k < n - 1; k++) {
                if (s.charCodeAt(i + k + 1) - s.charCodeAt(i + k) !== step) { ok = false; break; }
            }
            if (ok) return true;
        }
        return false;
    }

    /** qwer / asdf gibi klavye siralari */
    function hasKeyboardRun(value, n) {
        var s = value.toLowerCase();
        var r = s.split("").reverse().join("");
        for (var i = 0; i < KEYBOARD_ROWS.length; i++) {
            for (var j = 0; j + n <= s.length; j++) {
                if (KEYBOARD_ROWS[i].indexOf(s.substr(j, n)) !== -1) return true;
                if (KEYBOARD_ROWS[i].indexOf(r.substr(j, n)) !== -1) return true;
            }
        }
        return false;
    }

    /**
     * Deterministik skorlama. Sunucu politikasi saglanmiyorsa seviye tavani
     * "Orta"dir -- gosterge sunucunun reddedecegi bir parolaya "Guclu" demez.
     */
    function evaluatePassword(pw, context) {
        var checks = {
            len: pw.length >= 8,
            upper: RE_UPPER.test(pw),
            lower: RE_LOWER.test(pw),
            digit: /[0-9]/.test(pw),
            nospace: !/\s/.test(pw)
        };

        if (!pw) return { score: 0, level: 0, checks: checks, policyOk: false };

        var symbol = RE_SYMBOL.test(pw);
        var classes = (checks.lower ? 1 : 0) + (checks.upper ? 1 : 0)
            + (checks.digit ? 1 : 0) + (symbol ? 1 : 0);

        var score = Math.min(pw.length, 20) * 3 + classes * 8;
        if (pw.length >= 12) score += 8;
        if (pw.length >= 16) score += 6;

        if (classes === 1) score -= 18;
        if (/^\d+$/.test(pw)) score -= 15;
        if (/(.)\1{2,}/.test(pw)) score -= 12;
        if (hasRun(pw, 4)) score -= 12;
        if (hasKeyboardRun(pw, 4)) score -= 12;
        if (!checks.nospace) score -= 10;

        var lower = pw.toLowerCase();
        for (var i = 0; i < context.length; i++) {
            var token = (context[i] || "").toLowerCase();
            if (token.length >= 4 && lower.indexOf(token) !== -1) { score -= 20; break; }
        }

        var stripped = lower.replace(/[0-9!@#$%^&*._\-+]+$/, "");
        if (COMMON_PASSWORDS[lower] || COMMON_PASSWORDS[stripped]) score = Math.min(score, 12);

        score = Math.max(0, Math.min(100, score));

        var policyOk = checks.len && checks.upper && checks.lower && checks.digit && checks.nospace;
        var level = score >= 85 ? 4 : score >= 60 ? 3 : score >= 35 ? 2 : 1;
        if (!policyOk) level = Math.min(level, 2);

        return { score: score, level: level, checks: checks, policyOk: policyOk };
    }

    function strengthMeter() {
        $$("[data-strength-for]").forEach(function (meter) {
            var input = document.getElementById(meter.dataset.strengthFor);
            if (!input) return;

            var levelEl = $(".auth-meter__level", meter);
            var items = $$(".auth-reqs__item", meter);
            var liveId = input.id + "-live";
            var reqsId = input.id + "-reqs";
            var lastLevel = null;

            /**
             * Gostergeyi ilk kez acar ve ayni anda erisilebilir aciklamayi devreder:
             * JS kapaliyken politikayi anlatan statik ipucu kaldirilir, yerine
             * canli kosul listesinin id'si AYNI SIRADA gecer.
             *
             * Ikisi tek anda yapilmalidir. Ipucu erken kaldirilirsa aciklama
             * cozulemeyen bir id'ye isaret eder; kosul listesi erken baglanirsa
             * gizli bir kaptan dort tane ", eksik" parcasi okunur.
             */
            var hint = document.getElementById(input.id + "-hint");

            function reveal() {
                if (!meter.hidden) return;
                meter.hidden = false;

                if (!hint || !hint.hasAttribute("data-nojs-hint")) return;

                var ids = (input.getAttribute("aria-describedby") || "")
                    .split(/\s+/)
                    .filter(Boolean)
                    .map(function (id) { return id === hint.id ? reqsId : id; });

                input.setAttribute("aria-describedby", ids.join(" "));
                hint.remove();
            }

            var contextFields = (meter.dataset.context || "")
                .split(",")
                .map(function (id) { return id.trim(); })
                .filter(Boolean);

            function contextValues() {
                return contextFields.map(function (id) {
                    var el = document.getElementById(id);
                    if (!el) return "";
                    // E-postada yalnizca yerel kisim anlamli
                    return el.value.indexOf("@") !== -1 ? el.value.split("@")[0] : el.value;
                });
            }

            // Duyuru: 500 ms debounce + YALNIZCA seviye etiketi degistiginde.
            // 20 karakterlik bir parola en fazla 3-4 duyuru uretir, 20 degil.
            var announceLevel = debounce(function (level, metCount) {
                if (level === lastLevel) return;
                lastLevel = level;
                if (!level) { announce(liveId, ""); return; }
                announce(liveId, "Parola gücü: " + STRENGTH_LABELS[level]
                    + ". 4 koşuldan " + metCount + " tanesi sağlandı.");
            }, 500);

            function render() {
                var result = evaluatePassword(input.value, contextValues());

                // Gorsel geri bildirim debounce EDILMEZ; yalnizca duyuru gecikir.
                meter.dataset.level = String(result.level);
                if (levelEl) levelEl.textContent = STRENGTH_LABELS[result.level];

                var met = 0;
                items.forEach(function (item) {
                    var ok = !!result.checks[item.dataset.req];
                    if (ok) met++;
                    item.dataset.met = String(ok);
                    var state = $(".auth-reqs__state", item);
                    if (state) state.textContent = ok ? ", tamamlandı" : ", eksik";
                });

                if (!input.value) lastLevel = null;
                announceLevel(result.level, met);
            }

            input.addEventListener("focus", reveal);
            input.addEventListener("input", function () {
                reveal();
                render();
            });

            contextFields.forEach(function (id) {
                var el = document.getElementById(id);
                if (el) el.addEventListener("input", render);
            });

            // Sunucu hatasi nedeniyle alan dolu geldiyse gosterge hemen acilir.
            if (input.value) {
                reveal();
                render();
            }
        });
    }

    /* ======================================================================
       fieldValidation
       "Tembel dogrulama, hevesli yeniden dogrulama": alan ilk kez blur olana
       kadar hicbir hata gosterilmez; touched olduktan sonra her tus vurusunda
       yeniden degerlendirilir. Mesajlar view'daki data-msg-* niteliklerinden
       okunur -> metinlerin tek kaynagi Razor view'dur.
       ====================================================================== */

    var RULES = {
        required: function (v) { return v.trim().length > 0; },
        email: function (v) { return !v || /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(v); },
        username: function (v) { return !v || /^[a-zA-Z0-9._-]{3,100}$/.test(v); },
        password: function (v) {
            return v.length >= 8 && RE_UPPER.test(v) && RE_LOWER.test(v)
                && /[0-9]/.test(v) && !/\s/.test(v);
        },
        phone: function (v) { return !v || /^[0-9]{6,15}$/.test(v); }
    };

    function validateInput(input) {
        var value = input.value;

        if (input.hasAttribute("required") && !RULES.required(value)) {
            return input.dataset.msgRequired || null;
        }

        var rule = input.dataset.rule;
        if (rule && RULES[rule] && !RULES[rule](value)) {
            return input.dataset.msgRule || null;
        }

        if (input.dataset.matchFor) {
            var source = document.getElementById(input.dataset.matchFor);
            if (source && value && source.value !== value) {
                return input.dataset.msgMatch || null;
            }
        }

        return null;
    }

    function refreshMatchNote(input) {
        var note = document.getElementById(input.id + "-ok");
        if (!note) return;
        var source = document.getElementById(input.dataset.matchFor);
        var matches = source && input.value.length > 0 && source.value === input.value;
        note.hidden = !(input.dataset.touched === "1" && matches);
    }

    function evaluateField(input) {
        setFieldError(input, validateInput(input));
        if (input.dataset.matchFor) refreshMatchNote(input);
    }

    function fieldValidation() {
        document.addEventListener("blur", function (e) {
            var input = e.target;
            if (!input.matches || !input.matches("[data-validate]")) return;
            input.dataset.touched = "1";
            evaluateField(input);
        }, true);

        document.addEventListener("input", function (e) {
            var input = e.target;
            if (!input.matches || !input.matches("[data-validate]")) return;

            if (input.dataset.touched === "1") {
                evaluateField(input);
            } else {
                // Sunucudan gelmis hata dahil, kullanici yazmaya baslayinca temizlenir.
                setFieldError(input, null);
            }

            // Kaynak parola degisirse, dokunulmus tekrar alani yeniden degerlendirilir.
            $$('[data-match-for="' + input.id + '"]').forEach(function (confirmInput) {
                if (confirmInput.dataset.touched === "1") evaluateField(confirmInput);
            });
        }, true);

        // Gonderimde tum alanlar bir kez dogrulanir; ilk hatali alana odaklanilir.
        $$("form[data-auth-form]").forEach(function (form) {
            form.addEventListener("submit", function (e) {
                var invalid = null;

                $$("[data-validate]", form).forEach(function (input) {
                    input.dataset.touched = "1";
                    var message = validateInput(input);
                    setFieldError(input, message);
                    if (message && !invalid) invalid = input;
                });

                if (invalid) {
                    e.preventDefault();
                    e.stopPropagation();
                    // focus() kendi basina kaydirir; once bunu bastirip tek bir
                    // kontrollu kaydirma yapilir, yoksa sayfa iki kez ziplar.
                    invalid.focus({ preventScroll: true });
                    invalid.scrollIntoView({
                        block: "center",
                        behavior: prefersReducedMotion ? "auto" : "smooth"
                    });
                }
            }, true); // capture: submitGuard'dan once calisir
        });
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

    /* ======================================================================
       phoneInput
       Canli bicimlendirme (532 111 22 33) YOK: caret sicramasi ve
       "ekranda gordugum ile gonderilen deger farkli" riski tasir.
       Beklenen sekil placeholder ile gosterilir.
       ====================================================================== */

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
    strengthMeter();
    fieldValidation();
    submitGuard();
    phoneInput();
    userNameInput();
    logoFallback();
    initialFocus();
})();
