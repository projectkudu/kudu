(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    var token = 0;

    $.fn.notificationBar = function () {
        var $this = $(this),
            that = null;


        $this.addClass('notificationBar');

        $this.click(function () {
            $this.slideUp('slow');
        });

        that = {
            show: function (value) {
                token++;
                $this.data('token', token);
                $this.html(value);
                $this.show();
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