// Booster Auth tanitim sitesi: kaydirmayla beliren bolumler, mobil menu, kod kopyalama, dokumantasyon icindekiler.
// Kutuphane yok. JS kapaliyken icerik tam gorunur: .reveal yalnizca <html class="js"> iken gizlenir ve asla gizli kalmaz
// (ekrandakiler hemen, digerleri gorununce, en gec 2,5 sn sonra hepsi; reduced-motion'da hic gizlenmez).
(function () {
    "use strict";
    var doc = document.documentElement;
    var reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var lang = doc.lang === "en" ? "en" : "tr";
    var t = lang === "en" ? { copy: "Copy", copied: "Copied" } : { copy: "Kopyala", copied: "Kopyalandı" };

    // --- kaydirinca beliren ogeler
    var items = Array.prototype.slice.call(document.querySelectorAll(".reveal"));
    if (items.length && "IntersectionObserver" in window && !reduce) {
        var vh = window.innerHeight || 800;
        // Ekranda olanlari isaretle, SONRA gizleme sinifini ekle: ilk boyamada gorunen icerik hic kaybolmaz.
        items.forEach(function (el) { if (el.getBoundingClientRect().top < vh) el.classList.add("in"); });
        doc.classList.remove("no-js");
        doc.classList.add("js");
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (e) {
                if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
            });
        }, { rootMargin: "0px 0px -6% 0px", threshold: 0.01 });
        items.forEach(function (el) { if (!el.classList.contains("in")) io.observe(el); });
        setTimeout(function () { items.forEach(function (el) { el.classList.add("in"); }); }, 2500);
    } else {
        doc.classList.remove("no-js");
    }

    // --- mobil menu: baglantiya basinca kapat, disari tiklayinca kapat
    var menu = document.querySelector("[data-menu]");
    if (menu) {
        menu.addEventListener("click", function (e) { if (e.target.tagName === "A") menu.removeAttribute("open"); });
        document.addEventListener("click", function (e) { if (!menu.contains(e.target)) menu.removeAttribute("open"); });
        document.addEventListener("keydown", function (e) { if (e.key === "Escape") menu.removeAttribute("open"); });
    }

    // --- dokumantasyon yan menusu: dar ekranda kapali baslar
    var docsNav = document.querySelector("[data-docs-nav]");
    if (docsNav && window.matchMedia("(max-width: 860px)").matches) docsNav.removeAttribute("open");

    // --- kod bloklari: kopyala butonu
    document.querySelectorAll(".code").forEach(function (block) {
        var pre = block.querySelector("pre");
        if (!pre || !navigator.clipboard) return;
        var btn = document.createElement("button");
        btn.type = "button";
        btn.className = "copy";
        btn.textContent = t.copy;
        btn.addEventListener("click", function () {
            navigator.clipboard.writeText(pre.innerText.replace(/\n$/, "")).then(function () {
                btn.textContent = t.copied;
                setTimeout(function () { btn.textContent = t.copy; }, 1600);
            });
        });
        block.appendChild(btn);
    });

    // --- dokumantasyon: sayfa ici icindekiler (h2) ve aktif baslik
    var toc = document.querySelector("[data-toc]");
    var heads = document.querySelectorAll(".doc h2[id]");
    if (toc && heads.length) {
        var links = [];
        heads.forEach(function (h) {
            var a = document.createElement("a");
            a.href = "#" + h.id;
            a.textContent = h.textContent;
            toc.appendChild(a);
            links.push(a);
        });
        if ("IntersectionObserver" in window) {
            var hio = new IntersectionObserver(function (entries) {
                entries.forEach(function (e) {
                    if (!e.isIntersecting) return;
                    links.forEach(function (l) { l.classList.toggle("on", l.getAttribute("href") === "#" + e.target.id); });
                });
            }, { rootMargin: "-80px 0px -70% 0px" });
            heads.forEach(function (h) { hio.observe(h); });
        }
    } else if (toc) {
        toc.parentElement.style.visibility = "hidden";
    }
})();
