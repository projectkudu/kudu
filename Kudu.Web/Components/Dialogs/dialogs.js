(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.dialogs = {
        show: function (value) {
            return confirm(value);
        }
    };

})(jQuery);