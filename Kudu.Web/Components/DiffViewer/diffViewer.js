(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.utils.diffClass = function (type) {
        if (type == 1) {
            return ' diff-add';
        }
        else if (type == 2) {
            return ' diff-remove';
        }
        return '';
    };

    $.fn.diffViewer = function (options) {
        /// <summary>Creates a new file explorer with the given options</summary>
        /// <returns type="diffViewer" />
        var $this = $(this),
            templates = options.templates,
            that = null;

        $this.addClass('diffViewer');

        $this.delegate('.toggle', 'click', function (ev) {
            var $source = $(this).parent().next('.source');
            if ($source.is(':hidden')) {
                $source.slideDown();
            }
            else {
                $source.slideUp();
            }

            ev.preventDefault();
            return false;
        });

        that = {
            refresh: function (diff) {
                $this.html($.render(templates.diff, diff));
            }
        };

        return that;

    };

})(jQuery);