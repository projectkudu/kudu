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
            $commitButtons = $('<div/>').addClass('buttons'),
            $commit = $('<input/>').attr('type', 'button')
                                   .val('Commit'),
            $refreshLink = $('<a/>').attr('href', '#')
                                    .addClass('refresh')
                                    .addClass('icon')
                                    .addClass('icon-refresh')
                                    .html('Refresh'),
            diffViewer = null,
            templates = config.templates,
            that = null;

        $this.addClass('commitViewer');

        $refreshLink.appendTo($this);
        $commitMessage.appendTo($this);
        $commit.appendTo($commitButtons);
        $commitButtons.appendTo($this);
        $diffViewer.appendTo($this);

        diffViewer = $diffViewer.diffViewer({
            templates: templates,
            readonly: false
        });

        // Forward the event
        $(diffViewer).bind('diffViewer.openFile', function (e, path) {
            $(that).trigger('commitViewer.openFile', [path]);
        });

        $refreshLink.click(function (ev) {
            $(that).trigger('commitViewer.refresh');

            ev.preventDefault();
            return false;
        });

        $commit.click(function () {
            var message = $.trim($commitMessage.val());
            if (message) {
                $(that).trigger('commitViewer.commit', [message]);
            }
        });

        that = {
            refresh: function (diff) {
                diffViewer.refresh(diff);
            },
            clear: function () {
                diffViewer.refresh({ Files: {} });
                $commitMessage.val('');
            }
        };

        return that;

    };

})(jQuery);