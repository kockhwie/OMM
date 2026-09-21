window.ommNavMenu = {
    nextHandlerId: 0,
    handlers: new Map(),

    registerOutsideClick: function (element, dotNetReference) {
        const handlerId = ++this.nextHandlerId;
        const handler = function (event) {
            if (!element.contains(event.target)) {
                dotNetReference.invokeMethodAsync('CloseMobileMenuAsync');
            }
        };

        document.addEventListener('pointerdown', handler, true);
        this.handlers.set(handlerId, handler);
        return handlerId;
    },

    unregisterOutsideClick: function (handlerId) {
        const handler = this.handlers.get(handlerId);
        if (handler) {
            document.removeEventListener('pointerdown', handler, true);
            this.handlers.delete(handlerId);
        }
    }
};
