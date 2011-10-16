(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.fn.editor = function (options) {
        /// <summary>Creates a new file explorer with the given options</summary>
        /// <returns type="fileExplorer" />

        // Get the file system from the options
        var fs = options.fileSystem,
            templates = options.templates,
            $this = $(this),
            updatingEditor = false,
            that = null;

        var modes = {
            '.css': 'css',
            '.js': 'javascript',
            '.json': 'javascript',
            '.markdown': 'markdown',
            '.md': 'markdown',
            '.html': 'htmlmixed',
            '.htm': 'htmlmixed',
            '.ejs': 'htmlmixed',
            '.aspx': 'htmlmixed',
            '.ascx': 'htmlmixed',
            '.master': 'htmlmixed',
            '.cshtml': 'cshtml',
            '.php': 'php',
            '.xml': 'xml',
            '.config': 'xml',
            '.nuspec': 'xml',
            '.cs': 'text/x-java',
            '.java': 'text/x-java'
        };

        var editor = CodeMirror.fromTextArea($this[0], {
            lineNumbers: true,
            matchBrackets: true,
            indentUnit: 4,
            indentWithTabs: false,
            enterMode: "keep",
            tabMode: "shift",
            readOnly: true,
            onChange: function (e) {
                if (updatingEditor === false) {
                    $(that).trigger('editor.contentChanged');
                }
            }
        });

        function performEditorUpdate(fn) {
            updatingEditor = true;
            fn.apply(this, arguments);
            updatingEditor = false;
        }

        that = {
            setContent: function (path, content) {
                var file = fs.getFile(path);
                var mode = modes[file.getExtension()] || 'text/plain';

                performEditorUpdate(function () {
                    editor.setOption('mode', mode);
                    editor.setOption('readOnly', false);
                    editor.setValue(content);
                });

                file.setBuffer(content);
            },

            clear: function () {
                performEditorUpdate(function () {
                    editor.setValue('');
                    editor.setOption('readOnly', true);
                });
            },

            getContent: function () {
                return editor.getValue();
            },

            focus: function () {
                editor.focus();
            }
        };

        return that;
    }

})(jQuery);