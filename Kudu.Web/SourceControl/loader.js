(function ($, window) {
    var hide = false,
        pendingShow = null,
        id = 0;

    var loader = {
        show: function (value, cssClass) {
            var newId = id++;
            $('#status').attr('data-id', newId);
            $('#status').html(value);
            $('#status').attr('class', 'icon ' + (cssClass || 'icon-loading'));
            $('#status').show();
            return newId;
        },
        showAfter: function (delay, value, cssClass) {
            hide = false;
            pendingShow = window.setTimeout($.proxy(function () {
                if (hide === false) {
                    this.show(value, cssClass);
                }
            }, this), delay);
        },
        hide: function (id) {
            hide = true;

            if (pendingShow) {
                window.clearTimeout(pendingShow);
            }

            window.setTimeout($.proxy(function () {
                if ($('#status').attr('data-id') == id) {
                    $('#status').fadeOut('slow');
                }
            }, this), 100);
        }
    };

    window.loader = loader;

})(jQuery, window);