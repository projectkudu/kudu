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
                $(this).removeClass('icon-expand-down');
                $(this).addClass('icon-collapse-up');

                $source.slideDown();
            }
            else {
                $(this).addClass('icon-expand-down');
                $(this).removeClass('icon-collapse-up');
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