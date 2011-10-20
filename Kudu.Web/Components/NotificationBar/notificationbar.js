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
                window.setTimeout(function () {
                    if ($this.data('token') == token) {
                        $this.slideUp('slow');
                    }
                }, 100);
            }
        };

        return that;
    }

})(jQuery);