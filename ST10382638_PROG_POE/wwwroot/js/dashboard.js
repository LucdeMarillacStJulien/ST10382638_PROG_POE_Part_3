(function () {
    const canvas = document.getElementById('hoursChart');
    if (!canvas) return;

    const labelsAttr = canvas.getAttribute('data-labels') || '';
    const hoursAttr = canvas.getAttribute('data-hours') || '';

    const labels = labelsAttr ? labelsAttr.split('|') : [];
    const hours = hoursAttr ? hoursAttr.split('|').map(x => Number(x)) : [];

    const cs = getComputedStyle(document.documentElement);
    const accent = cs.getPropertyValue('--accent').trim() || '#00ffe0';
    const accent2 = cs.getPropertyValue('--accent-2').trim() || '#6a00ff';
    const text = cs.getPropertyValue('--text').trim() || '#e6f1ff';
    const grid = 'rgba(255,255,255,0.08)';

    const ctx = canvas.getContext('2d');
    const gradient = ctx.createLinearGradient(0, 0, 0, 200);
    gradient.addColorStop(0, accent);
    gradient.addColorStop(1, accent2);

    new Chart(ctx, {
        type: 'bar',
        data: { labels, datasets: [{ label: 'Hours', data: hours, backgroundColor: gradient }] },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { color: grid }, ticks: { color: text } },
                y: { beginAtZero: true, grid: { color: grid }, ticks: { color: text } }
            }
        }
    });
})();
