(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.utils = {
        throttle: function (fn, delay) {
            var canInvoke = true;
            var invokeDelay = function () {
                canInvoke = true;
            };

            return function () {
                if (canInvoke) {
                    fn.apply(this, arguments);
                    canInvoke = false;
                    setTimeout(invokeDelay, delay);
                }
            };
        }
    };

})(jQuery);