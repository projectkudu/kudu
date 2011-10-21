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
                var d = $.Deferred();
                window.setTimeout(function () {
                    if ($this.data('token') == token) {
                        $this.fadeOut('slow', function () { d.resolveWith(that); });
                    }
                }, 100);

                return d;
            }
        };

        return that;
    }

})(jQuery);