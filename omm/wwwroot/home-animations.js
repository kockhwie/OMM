/**
 * home-animations.js
 * Scroll-reveal + animated counters for the OMM landing page.
 * Exposes window.ommAnimations.init(), called from Home.razor via JS interop
 * in OnAfterRenderAsync(firstRender). Safe to call multiple times (idempotent
 * per element — already-observed elements are skipped).
 */

window.ommAnimations = (() => {
    'use strict';

    let revealObserver = null;
    const countedElements = new WeakSet();

    // ── Scroll reveal ────────────────────────────────────────────────────────
    function initReveal() {
        const targets = document.querySelectorAll('[data-reveal]:not([data-reveal-bound])');
        if (targets.length === 0) return;

        if (!revealObserver) {
            revealObserver = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        entry.target.classList.add('in-view');
                        revealObserver.unobserve(entry.target);
                    }
                });
            }, {
                threshold: 0.15,
                rootMargin: '0px 0px -40px 0px'
            });
        }

        targets.forEach((el, i) => {
            el.setAttribute('data-reveal-bound', 'true');

            // Stagger: use an explicit data-reveal-delay if present, otherwise
            // stagger siblings within the same parent automatically.
            if (!el.style.getPropertyValue('--reveal-delay')) {
                const explicit = el.getAttribute('data-reveal-delay');
                const delay = explicit !== null ? parseInt(explicit, 10) : Math.min(i * 80, 400);
                el.style.setProperty('--reveal-delay', `${delay}ms`);
            }

            revealObserver.observe(el);
        });
    }

    // ── Animated counters ────────────────────────────────────────────────────
    // Element needs: data-counter-target="535100" and optionally
    // data-counter-prefix="RM " data-counter-suffix="%" data-counter-decimals="0"
    function animateCounter(el) {
        if (countedElements.has(el)) return;
        countedElements.add(el);

        const target = parseFloat(el.getAttribute('data-counter-target') || '0');
        const prefix = el.getAttribute('data-counter-prefix') || '';
        const suffix = el.getAttribute('data-counter-suffix') || '';
        const decimals = parseInt(el.getAttribute('data-counter-decimals') || '0', 10);
        const duration = parseInt(el.getAttribute('data-counter-duration') || '1400', 10);

        const start = performance.now();
        const startVal = 0;

        function easeOutExpo(t) {
            return t === 1 ? 1 : 1 - Math.pow(2, -10 * t);
        }

        function frame(now) {
            const elapsed = now - start;
            const progress = Math.min(elapsed / duration, 1);
            const eased = easeOutExpo(progress);
            const current = startVal + (target - startVal) * eased;

            el.textContent = prefix + formatNumber(current, decimals) + suffix;

            if (progress < 1) {
                requestAnimationFrame(frame);
            } else {
                el.textContent = prefix + formatNumber(target, decimals) + suffix;
                el.classList.add('omm-counter-done');
            }
        }

        requestAnimationFrame(frame);
    }

    function formatNumber(value, decimals) {
        return value.toLocaleString('en-US', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals
        });
    }

    function initCounters() {
        const targets = document.querySelectorAll('[data-counter-target]:not([data-counter-bound])');
        if (targets.length === 0) return;

        const counterObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    animateCounter(entry.target);
                    counterObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.4 });

        targets.forEach(el => {
            el.setAttribute('data-counter-bound', 'true');
            counterObserver.observe(el);
        });
    }

    // ── Public API ───────────────────────────────────────────────────────────
    function init() {
        initReveal();
        initCounters();
    }

    return { init };
})();