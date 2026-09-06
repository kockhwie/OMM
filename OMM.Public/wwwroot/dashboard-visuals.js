/**
 * dashboard-visuals.js
 * Supporting visual scripts for OhMyMine Dashboard:
 * - Stealth / Privacy Mode (persisted in localStorage)
 * - Chart.js Wealth Breakdown Donut
 * - Smooth numeric count-up animations for metric cards
 */

window.ommStealth = {
    init: function () {
        try {
            const isStealth = localStorage.getItem('omm_stealth_mode') === 'true';
            if (isStealth) {
                document.body.classList.add('omm-stealth-mode');
            } else {
                document.body.classList.remove('omm-stealth-mode');
            }
            return isStealth;
        } catch (e) {
            return false;
        }
    },
    toggle: function () {
        try {
            const currentlyStealth = document.body.classList.contains('omm-stealth-mode');
            const nextState = !currentlyStealth;
            if (nextState) {
                document.body.classList.add('omm-stealth-mode');
            } else {
                document.body.classList.remove('omm-stealth-mode');
            }
            localStorage.setItem('omm_stealth_mode', nextState ? 'true' : 'false');
            return nextState;
        } catch (e) {
            return false;
        }
    },
    getState: function () {
        return document.body.classList.contains('omm-stealth-mode');
    }
};

window.ommCharts = {
    wealthChartInstance: null,
    renderWealthDonut: function (canvasId, labels, values, colors) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (this.wealthChartInstance) {
            this.wealthChartInstance.destroy();
            this.wealthChartInstance = null;
        }

        if (typeof Chart === 'undefined') {
            console.warn('Chart.js not yet loaded');
            return;
        }

        this.wealthChartInstance = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors,
                    borderWidth: 3,
                    borderColor: '#ffffff',
                    hoverOffset: 8,
                    borderRadius: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '72%',
                animation: {
                    animateRotate: true,
                    animateScale: true,
                    duration: 1000,
                    easing: 'easeOutQuart'
                },
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: '#0d0e13',
                        titleColor: '#ffffff',
                        bodyColor: '#d1d5db',
                        padding: 12,
                        cornerRadius: 10,
                        titleFont: {
                            family: 'Plus Jakarta Sans',
                            size: 13,
                            weight: '700'
                        },
                        bodyFont: {
                            family: 'Inter',
                            size: 12
                        },
                        callbacks: {
                            label: function (context) {
                                const val = context.parsed || 0;
                                const formatted = 'RM ' + val.toLocaleString('en-US', {
                                    minimumFractionDigits: 0,
                                    maximumFractionDigits: 0
                                });
                                return ' ' + context.label + ': ' + formatted;
                            }
                        }
                    }
                }
            }
        });
    }
};

window.ommCounters = {
    animateDashboard: function (selector) {
        const root = selector ? document.querySelector(selector) : document;
        if (!root) return;

        const targets = root.querySelectorAll('[data-counter-target]:not([data-counter-running])');
        targets.forEach(el => {
            el.setAttribute('data-counter-running', 'true');
            const target = parseFloat(el.getAttribute('data-counter-target') || '0');
            const prefix = el.getAttribute('data-counter-prefix') || '';
            const suffix = el.getAttribute('data-counter-suffix') || '';
            const decimals = parseInt(el.getAttribute('data-counter-decimals') || '0', 10);
            const duration = parseInt(el.getAttribute('data-counter-duration') || '1100', 10);

            const start = performance.now();
            function easeOutExpo(t) {
                return t === 1 ? 1 : 1 - Math.pow(2, -10 * t);
            }

            function frame(now) {
                const elapsed = now - start;
                const progress = Math.min(elapsed / duration, 1);
                const current = target * easeOutExpo(progress);

                el.textContent = prefix + current.toLocaleString('en-US', {
                    minimumFractionDigits: decimals,
                    maximumFractionDigits: decimals
                }) + suffix;

                if (progress < 1) {
                    requestAnimationFrame(frame);
                } else {
                    el.textContent = prefix + target.toLocaleString('en-US', {
                        minimumFractionDigits: decimals,
                        maximumFractionDigits: decimals
                    }) + suffix;
                    el.removeAttribute('data-counter-running');
                }
            }
            requestAnimationFrame(frame);
        });
    }
};
