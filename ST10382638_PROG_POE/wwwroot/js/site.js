// wwwroot/js/site.js
document.addEventListener('DOMContentLoaded', function () {
    const fileInput = document.getElementById('Files');
    const listEl = document.getElementById('FileList');
    const form = document.querySelector('form[enctype="multipart/form-data"]');

    if (!fileInput || !listEl || !form) return;

    // --- Canonical store of selected files ---
    // Use an array (order stable) and a Set for dedupe
    const selected = [];
    const sigSet = new Set(); // signatures to prevent duplicates: name|size|mtime

    const signature = f => `${f.name}|${f.size}|${f.lastModified}`;

    function escapeHtml(s) {
        return s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    // Rebuild the input's FileList from our canonical array
    function syncInputFromSelected() {
        const dt = new DataTransfer();
        for (const f of selected) dt.items.add(f);
        fileInput.files = dt.files;
    }

    // Re-render the visible list
    function renderList() {
        listEl.innerHTML = '';
        selected.forEach((f, i) => {
            const li = document.createElement('li');
            li.className = 'd-flex align-items-center justify-content-between border rounded px-2 py-1 mb-1';
            const sizeKb = (f.size / 1024).toFixed(1);
            li.innerHTML = `
                <span class="me-2 text-truncate" style="max-width:70%;">
                    ${escapeHtml(f.name)} <small class="text-muted">(${sizeKb} KB)</small>
                </span>
                <button type="button" class="btn btn-sm btn-link text-danger" data-idx="${i}">
                    remove
                </button>
            `;
            listEl.appendChild(li);
        });
    }

    // Central sync: update input and list together
    function syncAll() {
        syncInputFromSelected();
        renderList();
    }

    // Add files (from picker or drop), with dedupe
    function addFiles(fileList) {
        if (!fileList || !fileList.length) return;
        for (const f of fileList) {
            const sig = signature(f);
            if (!sigSet.has(sig)) {
                selected.push(f);
                sigSet.add(sig);
            }
        }
        syncAll();
    }

    // Handle picker change
    fileInput.addEventListener('change', () => {
        addFiles(fileInput.files);
        // Clear real input so user can pick the same file again later
        fileInput.value = '';
    });

    // Remove handler (prevent bubbling)
    listEl.addEventListener('click', (e) => {
        const btn = e.target.closest('button[data-idx]');
        if (!btn) return;
        e.preventDefault();
        e.stopPropagation();
        const idx = Number(btn.getAttribute('data-idx'));
        if (!Number.isNaN(idx) && idx >= 0 && idx < selected.length) {
            const removed = selected.splice(idx, 1)[0];
            if (removed) sigSet.delete(signature(removed));
            syncAll();
        }
    });

    // FINAL GUARANTEE: just before submit, repopulate the input from our store
    form.addEventListener('submit', () => {
        syncInputFromSelected();
        // optional: quick log for local debugging
        // console.log('Submitting files:', fileInput.files.length);
    });
});
