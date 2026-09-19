window.bootstrapHelpers = {
    // Initialize all [data-bs-toggle="popover"] elements on the page.
    // Safe to call multiple times — skips elements already initialized.
    initPopovers: function () {
        document.querySelectorAll('[data-bs-toggle="popover"]').forEach(function (el) {
            if (!bootstrap.Popover.getInstance(el)) {
                new bootstrap.Popover(el);
            }
        });
    },

    // Dispose all active popovers (call before component teardown).
    disposePopovers: function () {
        document.querySelectorAll('[data-bs-toggle="popover"]').forEach(function (el) {
            var instance = bootstrap.Popover.getInstance(el);
            if (instance) instance.dispose();
        });
        // Remove any orphaned popover elements left in the body
        document.querySelectorAll('.popover').forEach(function (el) { el.remove(); });
    }
};
