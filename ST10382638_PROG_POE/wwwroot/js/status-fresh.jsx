// Auto-refresh the status panel every 5s (vanilla JS, no jQuery required)
(function () {
    const panel = document.getElementById("status-panel");
    if (!panel) return;

    async function refresh() {
        try {
            const res = await fetch(window.location.href, { cache: "no-store" });
            const html = await res.text();
            const parser = new DOMParser();
            const doc = parser.parseFromString(html, "text/html");
            const fresh = doc.getElementById("status-panel");
            if (fresh) panel.innerHTML = fresh.innerHTML;
        } catch { /* swallow errors to stay quiet in UI */ }
    }

    setInterval(refresh, 5000);
})();
