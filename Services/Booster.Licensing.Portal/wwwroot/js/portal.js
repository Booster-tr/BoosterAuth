// Kopyala butonlari: data-copy="<eleman id>".
document.addEventListener("click", async (e) => {
  const btn = e.target.closest("[data-copy]");
  if (!btn) return;
  const el = document.getElementById(btn.dataset.copy);
  if (!el) return;
  await navigator.clipboard.writeText(el.textContent.trim());
  const old = btn.textContent;
  btn.textContent = "Kopyalandı";
  setTimeout(() => (btn.textContent = old), 1500);
});

// Lisans turu degisince varsayilan limit ve sureler (deneme: 7 gun + 3, 100/1/3; ucretli: 365 + 14).
const type = document.getElementById("type");
if (type) {
  const set = (id, v) => { const el = document.getElementById(id); if (el) el.value = v; };
  type.addEventListener("change", () => {
    if (type.value === "trial") {
      set("Plan", "trial"); set("MaxUsers", 100); set("MaxPools", 1); set("MaxClients", 3);
      set("DurationDays", 7); set("GraceDays", 3);
    } else {
      set("Plan", "standard"); set("DurationDays", 365); set("GraceDays", 14);
    }
  });
}
