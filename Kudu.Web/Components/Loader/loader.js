(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    var token = 0;

    $.fn.loader = function () {
        var $this = $(this),
            that = null;

        that = {
            show: function (value) {
                token++;
                $this.data('token', token);
                $this.html(value);
                $this.attr('class', 'icon icon-loading');
                $this.show();
                return token;
            },
            hide: function () {
                window.setTimeout(function () {
                    if ($this.data('token') == token) {
                        $this.fadeOut('slow');
                    }
                }, 100);
            }
        };

        return that;
    }

})(jQuery);