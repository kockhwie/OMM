/**
 * dividend-charts.js
 * Chart.js rendering helpers for the OMM Dividend Calculator.
 * Exposes window.dividendCharts.* functions called via Blazor JS interop.
 *
 * Palette matches OMM design system:
 *   Clay gold:  #d4a838
 *   Mine green: #10b981
 *   Ink 900:    #0d0e13
 *   Blue:       #3b82f6
 *   Muted:      #6b7280
 */

window.dividendCharts = (() => {
    'use strict';

    const chartInstances = {};

    // ── Palette ──────────────────────────────────────────────────────────────
    const palette = {
        gold:       '#d4a838',
        goldFaint:  'rgba(212,168,56,0.15)',
        green:      '#10b981',
        greenFaint: 'rgba(16,185,129,0.15)',
        blue:       '#3b82f6',
        blueFaint:  'rgba(59,130,246,0.15)',
        ink:        '#0d0e13',
        inkMid:     '#4b5263',
        inkLight:   '#e5e7eb',
        red:        '#ef4444',
        redFaint:   'rgba(239,68,68,0.15)',
    };

    // ── Global defaults ──────────────────────────────────────────────────────
    function applyDefaults() {
        if (!window.Chart) return;
        Chart.defaults.font.family = "'Inter', system-ui, -apple-system, sans-serif";
        Chart.defaults.font.size = 12;
        Chart.defaults.color = palette.inkMid;
        Chart.defaults.plugins.legend.labels.boxWidth = 12;
        Chart.defaults.plugins.legend.labels.padding = 16;
        Chart.defaults.plugins.tooltip.backgroundColor = palette.ink;
        Chart.defaults.plugins.tooltip.titleColor = '#ffffff';
        Chart.defaults.plugins.tooltip.bodyColor = 'rgba(255,255,255,0.75)';
        Chart.defaults.plugins.tooltip.borderColor = 'rgba(255,255,255,0.1)';
        Chart.defaults.plugins.tooltip.borderWidth = 1;
        Chart.defaults.plugins.tooltip.padding = 12;
        Chart.defaults.plugins.tooltip.cornerRadius = 8;
    }

    // ── Destroy existing chart instance ──────────────────────────────────────
    function destroyExisting(id) {
        if (chartInstances[id]) {
            chartInstances[id].destroy();
            delete chartInstances[id];
        }
    }

    // ── Shared axis config ───────────────────────────────────────────────────
    function xAxis(labels) {
        return {
            grid: { color: 'rgba(0,0,0,0.04)', drawBorder: false },
            ticks: { maxRotation: 0, color: palette.inkMid }
        };
    }

    function yAxis(prefix) {
        const p = prefix || 'RM ';
        return {
            grid: { color: 'rgba(0,0,0,0.04)', drawBorder: false },
            ticks: {
                color: palette.inkMid,
                callback: (v) => p + (p.endsWith(' ') ? '' : ' ') + formatCompact(v)
            }
        };
    }

    function formatCompact(v) {
        const abs = Math.abs(v);
        if (abs >= 1_000_000) return (v / 1_000_000).toFixed(1) + 'M';
        if (abs >= 1_000) return (v / 1_000).toFixed(0) + 'k';
        return v.toFixed(0);
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. Portfolio Growth — line chart
    // ────────────────────────────────────────────────────────────────────────
    function renderPortfolioGrowth(canvasId, labels, portfolioValues, contributionValues, currencyPrefix) {
        if (!window.Chart) return;
        applyDefaults();
        destroyExisting(canvasId);
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        const cur = currencyPrefix || 'RM ';

        chartInstances[canvasId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels,
                datasets: [
                    {
                        label: 'Portfolio Value',
                        data: portfolioValues,
                        borderColor: palette.gold,
                        backgroundColor: palette.goldFaint,
                        fill: true,
                        tension: 0.4,
                        pointRadius: labels.length > 15 ? 0 : 4,
                        pointHoverRadius: 6,
                        borderWidth: 2.5
                    },
                    {
                        label: 'Total Invested',
                        data: contributionValues,
                        borderColor: palette.inkMid,
                        backgroundColor: 'transparent',
                        fill: false,
                        tension: 0.1,
                        pointRadius: 0,
                        borderWidth: 1.5,
                        borderDash: [4, 4]
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'top' },
                    tooltip: {
                        callbacks: {
                            label: (ctx) => ` ${ctx.dataset.label}: ${cur}${ctx.parsed.y.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
                        }
                    }
                },
                scales: {
                    x: xAxis(labels),
                    y: yAxis(cur)
                }
            }
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. Dividend Income Growth — bar chart
    // ────────────────────────────────────────────────────────────────────────
    function renderDividendIncome(canvasId, labels, grossValues, netValues, currencyPrefix) {
        if (!window.Chart) return;
        applyDefaults();
        destroyExisting(canvasId);
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        const cur = currencyPrefix || 'RM ';
        const hasNet = netValues && netValues.some(v => v !== grossValues[netValues.indexOf(v)]);

        const datasets = [
            {
                label: 'Annual Dividend' + (hasNet ? ' (Gross)' : ''),
                data: grossValues,
                backgroundColor: palette.green,
                borderRadius: 4,
                borderSkipped: false
            }
        ];

        if (hasNet) {
            datasets.push({
                label: 'Annual Dividend (Net)',
                data: netValues,
                backgroundColor: palette.greenFaint,
                borderColor: palette.green,
                borderWidth: 1,
                borderRadius: 4,
                borderSkipped: false
            });
        }

        chartInstances[canvasId] = new Chart(ctx, {
            type: 'bar',
            data: { labels, datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'top' },
                    tooltip: {
                        callbacks: {
                            label: (ctx) => ` ${ctx.dataset.label}: ${cur}${ctx.parsed.y.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
                        }
                    }
                },
                scales: {
                    x: xAxis(labels),
                    y: yAxis(cur)
                }
            }
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. Shares Growth — area chart
    // ────────────────────────────────────────────────────────────────────────
    function renderSharesGrowth(canvasId, labels, totalShares, dripShares) {
        if (!window.Chart) return;
        applyDefaults();
        destroyExisting(canvasId);
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        const hasDrip = dripShares && dripShares.some(v => v > 0);

        const datasets = [
            {
                label: 'Total Shares',
                data: totalShares,
                borderColor: palette.blue,
                backgroundColor: palette.blueFaint,
                fill: true,
                tension: 0.4,
                pointRadius: labels.length > 15 ? 0 : 3,
                borderWidth: 2.5
            }
        ];

        if (hasDrip) {
            datasets.push({
                label: 'Shares via DRIP',
                data: dripShares,
                borderColor: palette.green,
                backgroundColor: 'transparent',
                fill: false,
                tension: 0.4,
                pointRadius: 0,
                borderWidth: 1.5,
                borderDash: [4, 4]
            });
        }

        chartInstances[canvasId] = new Chart(ctx, {
            type: 'line',
            data: { labels, datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'top' },
                    tooltip: {
                        callbacks: {
                            label: (ctx) => ` ${ctx.dataset.label}: ${ctx.parsed.y.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
                        }
                    }
                },
                scales: {
                    x: xAxis(labels),
                    y: {
                        grid: { color: 'rgba(0,0,0,0.04)', drawBorder: false },
                        ticks: { color: palette.inkMid }
                    }
                }
            }
        });
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. Cash vs Reinvested Dividends — grouped bar
    // ────────────────────────────────────────────────────────────────────────
    function renderCashVsReinvested(canvasId, labels, withDripValues, withoutDripValues, currencyPrefix) {
        if (!window.Chart) return;
        applyDefaults();
        destroyExisting(canvasId);
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        const cur = currencyPrefix || 'RM ';

        chartInstances[canvasId] = new Chart(ctx, {
            type: 'bar',
            data: {
                labels,
                datasets: [
                    {
                        label: 'With DRIP',
                        data: withDripValues,
                        backgroundColor: palette.gold,
                        borderRadius: 4,
                        borderSkipped: false
                    },
                    {
                        label: 'Cash Dividends',
                        data: withoutDripValues,
                        backgroundColor: palette.inkLight,
                        borderRadius: 4,
                        borderSkipped: false
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'top' },
                    tooltip: {
                        callbacks: {
                            label: (ctx) => ` ${ctx.dataset.label}: ${cur}${ctx.parsed.y.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`
                        }
                    }
                },
                scales: {
                    x: xAxis(labels),
                    y: yAxis(cur)
                }
            }
        });
    }

    // ── Destroy all charts ──────────────────────────────────────────────────
    function destroyAll() {
        Object.keys(chartInstances).forEach(id => {
            if (chartInstances[id]) {
                chartInstances[id].destroy();
                delete chartInstances[id];
            }
        });
    }

    return {
        renderPortfolioGrowth,
        renderDividendIncome,
        renderSharesGrowth,
        renderCashVsReinvested,
        destroyAll
    };
})();
