(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.ide = function (options) {
        var fs = new FileSystem(),
            $fileExplorer = options.fileExplorer,
            $tabManager = options.tabManager,
            $editor = options.editor,
            tabManager = null,
            editor = null,
            fileExplorer = null;

        $.fs = fs;

        var documents = $.connection.documents;
        documents.appName = options.appName;

        var templates = {
            folder: $('#fileExplorer_folderTemplate'),
            file: $('#fileExplorer_fileTemplate'),
            deferredFolder: $('#fileExplorer_deferredFolderTemplate'),
            tab: $('#tabManager_tabTemplate')
        };

        function openDocument(file) {
            var path = file.getRelativePath();

            if (file.getBuffer() !== null) {
                editor.setContent(path, file.getBuffer());
            }
            else {
                // Get the file content from the server
                documents.openFile(path)
                     .done(function (content) {
                         editor.setContent(path, content);

                         file.setBuffer(content);
                     });
            }
        }

        // Create components
        fileExplorer = $fileExplorer.fileExplorer({
            templates: templates,
            fileSystem: fs
        });

        tabManager = $tabManager.tabManager({
            templates: templates,
            fileSystem: fs
        });

        editor = $editor.editor({
            fileSystem: fs
        });

        $(fileExplorer).bind('fileExplorer.fileClicked', function (e, file) {
            var path = file.getPath();
            tabManager.setActive(path);
        });

        $(tabManager).bind('tabManager.beforeActiveTabChanged', function (e, tab) {
            if (tab) {
                tab.file.setBuffer(editor.getContent());
            }
        });

        $(tabManager).bind('tabManager.afterActiveTabChanged', function (e, tab) {
            openDocument(tab.file);
        });

        $(tabManager).bind('tabManager.beforeTabClosed', function (e) {
            if (e.tab.file.isDirty()) {
                // TODO: Prompt for confirmation
                e.preventDefault();
            }
        });

        $(tabManager).bind('tabManager.afterTabClosed', function (e, tab) {
            if (tab.active) {
                // If the closed tab was active, get the new active tab
                // and set the editor's content to the new active tab
                var newActiveTab = tabManager.getActive();
                if (newActiveTab) {
                    // Get document content
                    openDocument(newActiveTab.file);
                }
                else {
                    // Clear the editor
                    editor.clear();
                }
            }

            // Set the buffer the null since the file has been closed
            tab.file.setBuffer(null);
        });

        $(editor).bind('editor.contentChanged', function (e) {
            var tab = tabManager.getActive();

            if (tab) {
                if (!tab.file.isDirty()) {
                    tab.file.setDirty(true);
                }
            }
        });

        $(document).bind('keydown', 'ctrl+s', function (ev) {

            ev.preventDefault();
            return false;
        });

        $(document).bind('keydown', 'del', function (ev) {
            if (fileExplorer.hasFocus()) {
                var item = fileExplorer.getSelectedItem();
                // TODO: Prompt here
                if (item) {
                    if (item.file) {
                        fs.removeFile(item.file.getPath());
                    }
                    else {
                        fs.removeDirectory(item.directory.getPath());
                    }

                    ev.preventDefault();
                    return false;
                }
            }
        });

        var throttled = {
            nextSelection: $.utils.throttle(function () {
                fileExplorer.nextSelection();
            }, 50),

            prevSelection: $.utils.throttle(function () {
                fileExplorer.prevSelection();
            }, 50)
        };

        $(document).bind('keydown', 'down', function (ev) {
            if (fileExplorer.hasFocus()) {
                throttled.nextSelection();
                ev.preventDefault();
                return true;
            }
        });

        $(document).bind('keydown', 'up', function (ev) {
            if (fileExplorer.hasFocus()) {
                throttled.prevSelection();
                ev.preventDefault();
                return true;
            }
        });

        $(document).bind('keydown', 'return', function (ev) {
            if (fileExplorer.hasFocus()) {
                var item = fileExplorer.getSelectedItem();
                if (item && item.file) {
                    $(fileExplorer).trigger('fileExplorer.fileClicked', [item.file]);
                    ev.preventDefault();
                    return true;
                }
            }
        });

        $.connection.hub.start(function () {
            documents.getStatus()
                 .done(function (project) {
                     fs.create(project.Files);

                     fileExplorer.refresh();
                 });
        });
    };

})(jQuery);