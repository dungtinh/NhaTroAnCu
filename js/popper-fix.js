// Fix for Bootstrap/Popper.js version conflicts
(function () {
    'use strict';

    // Check if Bootstrap and jQuery are loaded
    if (typeof jQuery === 'undefined') {
        console.error('jQuery is required for Bootstrap components');
        return;
    }

    // Wait for DOM ready
    jQuery(function ($) {
        // Fix Bootstrap tooltip/popover initialization
        var initializeBootstrapComponents = function () {
            // Check Bootstrap version
            var bootstrapVersion = $.fn.tooltip ? $.fn.tooltip.Constructor.VERSION : null;

            if (!bootstrapVersion) {
                console.log('Bootstrap tooltip not found');
                return;
            }

            console.log('Bootstrap version detected:', bootstrapVersion);

            // Bootstrap 5.x
            if (bootstrapVersion && bootstrapVersion.startsWith('5')) {
                // Bootstrap 5 uses data-bs-* attributes
                var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
                var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
                    try {
                        return new bootstrap.Tooltip(tooltipTriggerEl);
                    } catch (e) {
                        console.log('Tooltip initialization failed:', e);
                        return null;
                    }
                });

                // Also initialize elements with title attribute
                $('[title]:not([data-bs-toggle="tooltip"])').each(function () {
                    try {
                        new bootstrap.Tooltip(this);
                    } catch (e) {
                        // Fallback to jQuery UI tooltip if available
                        if ($.fn.tooltip && !$.fn.tooltip.Constructor) {
                            $(this).tooltip();
                        }
                    }
                });
            }
            // Bootstrap 4.x
            else if (bootstrapVersion && bootstrapVersion.startsWith('4')) {
                // Bootstrap 4 uses data-toggle
                try {
                    $('[data-toggle="tooltip"], [title]').tooltip({
                        container: 'body',
                        trigger: 'hover focus',
                        placement: 'auto'
                    });
                } catch (e) {
                    console.log('Bootstrap 4 tooltip initialization failed:', e);
                }
            }
            // Bootstrap 3.x
            else if (bootstrapVersion && bootstrapVersion.startsWith('3')) {
                try {
                    $('[data-toggle="tooltip"], [title]').tooltip();
                } catch (e) {
                    console.log('Bootstrap 3 tooltip initialization failed:', e);
                }
            }
        };

        // Dispose existing tooltips to prevent conflicts
        var disposeExistingTooltips = function () {
            // Bootstrap 5
            if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
                var tooltips = document.querySelectorAll('[data-bs-toggle="tooltip"]');
                tooltips.forEach(function (el) {
                    var instance = bootstrap.Tooltip.getInstance(el);
                    if (instance) {
                        instance.dispose();
                    }
                });
            }

            // Bootstrap 4 and 3
            if ($.fn.tooltip && $.fn.tooltip.Constructor) {
                $('[data-toggle="tooltip"], [title]').each(function () {
                    var $this = $(this);
                    if ($this.data('bs.tooltip') || $this.data('tooltip')) {
                        try {
                            $this.tooltip('dispose');
                        } catch (e) {
                            try {
                                $this.tooltip('destroy');
                            } catch (e2) {
                                // Silent fail
                            }
                        }
                    }
                });
            }
        };

        // Fix Popper namespace issue
        if (typeof Popper !== 'undefined' && !window.Popper__namespace) {
            window.Popper__namespace = Popper;
        }

        // Initialize with delay to ensure all scripts are loaded
        setTimeout(function () {
            try {
                disposeExistingTooltips();
                initializeBootstrapComponents();
            } catch (e) {
                console.log('Bootstrap component initialization error:', e);

                // Fallback: Simple CSS-only tooltip
                $('[title]').each(function () {
                    var $this = $(this);
                    var title = $this.attr('title');
                    if (title) {
                        $this.attr('data-tooltip', title);
                        $this.removeAttr('title');
                        $this.addClass('has-tooltip');
                    }
                });
            }
        }, 100);

        // Reinitialize on dynamic content
        $(document).on('shown.bs.modal shown.bs.dropdown shown.bs.tab', function () {
            setTimeout(initializeBootstrapComponents, 100);
        });
    });

    // Add CSS fallback for tooltips
    var style = document.createElement('style');
    style.textContent = `
        .has-tooltip {
            position: relative;
        }
        .has-tooltip:hover::after {
            content: attr(data-tooltip);
            position: absolute;
            bottom: 100%;
            left: 50%;
            transform: translateX(-50%);
            background: #333;
            color: white;
            padding: 5px 10px;
            border-radius: 4px;
            white-space: nowrap;
            font-size: 14px;
            z-index: 1000;
            pointer-events: none;
            margin-bottom: 5px;
        }
        .has-tooltip:hover::before {
            content: '';
            position: absolute;
            bottom: 100%;
            left: 50%;
            transform: translateX(-50%);
            border: 5px solid transparent;
            border-top-color: #333;
            z-index: 1000;
            pointer-events: none;
        }
    `;
    document.head.appendChild(style);
})();