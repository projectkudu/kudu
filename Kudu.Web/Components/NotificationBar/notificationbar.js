(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    var token = 0;

    $.fn.notificationBar = function () {
        var $this = $(this),
            that = null;


        $this.addClass('notificationBar');

        $this.click(function (ev) {
            if (ev.target == $this[0]) {
                $this.slideUp('slow');
            }
        });

        that = {
            show: function (value) {
                token++;
                $this.data('token', token);
                $this.html(value);
                $this.slideDown();
                return token;
            },
            hide: function () {
                var d = $.Deferred();
                window.setTimeout(function () {
                    if ($this.data('token') == token) {
                        $this.slideUp('slow', function () { d.resolveWith(that); });
                    }
                }, 100);

                return d;
            }
        };

        return that;
    }

})(jQuery);