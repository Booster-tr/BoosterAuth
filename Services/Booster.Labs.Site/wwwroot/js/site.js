/* Boosters Labs site — ilerlemeli zenginlestirme. JS kapaliysa site tam calisir. */
(function () {
    "use strict";
    document.documentElement.classList.remove("no-js");

    // Mobil menu
    var header = document.querySelector("[data-header]");
    var burger = document.querySelector("[data-burger]");
    var menu = document.querySelector("[data-mobile]");
    if (header && burger && menu) {
        burger.addEventListener("click", function () {
            var open = header.classList.toggle("open");
            burger.setAttribute("aria-expanded", open ? "true" : "false");
            menu.hidden = !open;
        });
        menu.addEventListener("click", function (e) {
            if (e.target.closest("a")) {
                header.classList.remove("open");
                burger.setAttribute("aria-expanded", "false");
                menu.hidden = true;
            }
        });
    }

    // Reveal (IntersectionObserver). Desteklenmiyorsa hepsini gorunur yap.
    var items = document.querySelectorAll(".reveal");
    if (!("IntersectionObserver" in window) || !items.length) {
        items.forEach(function (el) { el.classList.add("in"); });
        return;
    }
    var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (en) {
            if (en.isIntersecting) { en.target.classList.add("in"); io.unobserve(en.target); }
        });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.08 });
    items.forEach(function (el) { io.observe(el); });

    // Emniyet: 2.5 sn sonra hala gorunmeyen varsa ac (gec yuklenen icerik).
    setTimeout(function () {
        document.querySelectorAll(".reveal:not(.in)").forEach(function (el) { el.classList.add("in"); });
    }, 2500);
})();
