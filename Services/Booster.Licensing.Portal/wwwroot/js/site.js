// Booster Auth tanitim sitesi: kaydirmayla beliren bolumler, mobil menu, kod kopyalama, dokumantasyon icindekiler.
// Kutuphane yok; JS kapaliyken de icerik tam gorunur (.reveal yalnizca JS varken gizlenir).
(function () {
    "use strict";
    var doc = document.documentElement;
    doc.classList.remove("no-js");
    var lang = doc.lang === "en" ? "en" : "tr";
    var t = lang === "en" ? { copy: "Copy", copied: "Copied" } : { copy: "Kopyala", copied: "Kopyalandı" };

    // --- kaydirinca beliren ogeler
    var reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var items = document.querySelectorAll(".reveal");
    var showAll = function () { items.forEach(function (el) { el.classList.add("in"); }); };
    if ("IntersectionObserver" in window && !reduce) {
        // Acilista ekranda olanlar hemen (bir kare sonra, gecis animasyonuyla) gosterilir.
        var vh = window.innerHeight || 800;
        requestAnimationFrame(function () {
            items.forEach(function (el) { if (el.getBoundingClientRect().top < vh) el.classList.add("in"); });
        });
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (e) {
                if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
            });
        }, { rootMargin: "0px 0px -8% 0px", threshold: 0.01 });
        items.forEach(function (el) { io.observe(el); });
        // Emniyet: gozlemci bir sebeple tetiklenmezse icerik asla gizli kalmasin.
        setTimeout(showAll, 2500);
    } else {
        showAll();
    }

    // --- ust bar golgesi
    var header = document.querySelector("[data-header]");
    var onScroll = function () { if (header) header.classList.toggle("scrolled", window.scrollY > 8); };
    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();

    // --- mobil menu
    var toggle = document.querySelector("[data-nav-toggle]");
    var nav = document.querySelector("[data-nav]");
    if (toggle && nav) {
        toggle.addEventListener("click", function () {
            var open = nav.classList.toggle("open");
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        });
        nav.addEventListener("click", function (e) {
            if (e.target.tagName === "A") { nav.classList.remove("open"); toggle.setAttribute("aria-expanded", "false"); }
        });
    }

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
