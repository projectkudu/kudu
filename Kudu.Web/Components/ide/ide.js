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
                documents.openFile(path)
                     .done(function (content) {
                         editor.setContent(path, content);
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

            openDocument(file);
        });

        $(fileExplorer).bind('fileExplorer.beforeFileDeleted', function (e) {

        });

        $(fileExplorer).bind('fileExplorer.afterFileDeleted', function (e, file) {
            tabManager.remove(file.getPath());
        });

        $(tabManager).bind('tabManager.beforeActiveTabChanged', function (e, activeTab) {
            if (activeTab) {
                activeTab.file.setBuffer(editor.getContent());
            }
        });

        $(tabManager).bind('tabManager.tabClicked', function (e, tab) {
            openDocument(tab.file);
        });

        $(tabManager).bind('tabManager.beforeTabClosed', function (e) {
            if (e.tab.file.isDirty()) {
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

        $.connection.hub.start(function () {
            documents.getStatus()
                 .done(function (project) {
                     fs.create(project.Files);

                     fileExplorer.refresh();
                 });
        });

    };

})(jQuery);