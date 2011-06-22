(function ($, window) {
    var hide = false,
        pendingShow = null;

    var loader = {
        show: function (value, cssClass) {
            if ($('#status').is(':hidden')) {
                $('#status').html(value);
                $('#status').attr('class', 'icon ' + (cssClass || 'icon-loading'));
                $('#status').show();
            }
        },
        showAfter: function (delay, value, cssClass) {
            hide = false;
            pendingShow = window.setTimeout($.proxy(function () {
                if (hide === false) {
                    this.show(value, cssClass);
                }
            }, this), delay);
        },
        hide: function () {
            hide = true;

            if (pendingShow) {
                window.clearTimeout(pendingShow);
            }

            window.setTimeout($.proxy(function () {
                $('#status').fadeOut('slow');
            }, this), 100);
        }
    };

    window.loader = loader;

})(jQuery, window);