(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"
    $.fn.commitViewer = function (options) {
        /// <summary>Creates a commit viewer</summary>
        /// <returns type="diffViewer" />
        var config = {
            readonly: true
        };

        $.extend(config, options);

        var $this = $(this),
            $commitMessage = $('<textarea/>').addClass('message'),
            $diffViewer = $('<div/>'),
            diffViewer = null,
            templates = config.templates,
            that = null;

        $this.addClass('commitViewer');

        $commitMessage.appendTo($this);
        $diffViewer.appendTo($this);

        diffViewer = $diffViewer.diffViewer({
            templates: templates,
            readonly: false
        });

        that = {
            refresh: function (diff) {
                diffViewer.refresh(diff);
            }
        };

        return that;

    };

})(jQuery);