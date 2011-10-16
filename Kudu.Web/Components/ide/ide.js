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
                editor.focus();
                fileExplorer.setFocus(false);
            }
            else {
                // Get the file content from the server
                documents.openFile(path)
                     .done(function (content) {
                         editor.setContent(path, content);
                         editor.focus();
                         fileExplorer.setFocus(false);

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

        $(fileExplorer).bind('fileExplorer.fileOpened', function (e, file) {
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
                $.dialogs.prompt("Do you want to save the changes to '" + e.tab.file.getPath() + "'", ['yes', 'no', 'cancel'])
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
                var selectedNode = fileExplorer.selectedNode();
                // TODO: Prompt here
                if (selectedNode) {
                    if (selectedNode.isFile()) {
                        fs.removeFile(selectedNode.path);
                    }
                    else {
                        fs.removeDirectory(selectedNode.path);
                    }

                    ev.preventDefault();
                    return false;
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


        $.fs = fs;
        $.fe = fileExplorer;
    };

})(jQuery);