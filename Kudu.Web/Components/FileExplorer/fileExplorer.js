(function ($) {
    /// <param name="$" type="jQuery" />
    "use strict"

    $.fn.fileExplorer = function (options) {
        /// <summary>Creates a new file explorer with the given options</summary>
        /// <returns type="fileExplorer" />

        // Get the file system from the options
        var fs = options.fileSystem,
            templates = options.templates,
            $this = $(this),
            $activeSelection = null,
            hasFocus = false;

        // Classifiy files
        function classifyFiles($container) {
            // Setup images for file types
            $.each($container.find('.file'), function () {
                var path = $(this).data('path');
                var file = fs.getFile(path);
                var iconClass = $.getIconClass(file);

                $(this).find('.open').addClass('icon-' + iconClass);
            });
        }

        function setFocus(value) {
            hasFocus = value;

            $(that).trigger('fileExplorer.focusChanged', [value]);

            if ($activeSelection) {
                if (!value) {
                    $activeSelection.addClass('no-focus');
                }
                else {
                    $activeSelection.removeClass('no-focus');
                }
            }
        }

        function getFileNode(file) {
            return $('#' + file.getElementId());
        }

        function getFolderNode(directory) {
            return $('#' + directory.getElementId());
        }

        function setSelection($elem) {
            if ($activeSelection) {
                $activeSelection.removeClass('selected');
                $activeSelection.removeClass('no-focus');
            }

            $elem.addClass('selected');

            $activeSelection = $elem;
        }

        // Add the file explorer class so we can apply the appropriate styles
        $this.addClass('fileExplorer');

        // Setup event handlers
        $this.delegate('.selection', 'click', function (ev) {
            setSelection($(this));

            setFocus(true);

            ev.preventDefault();
            return false;
        });

        $this.delegate('.icon-folder', 'click', function (ev) {
            var $folder = $(this).closest('.folder');
            var path = $folder.data('path');
            var directory = fs.getDirectory(path);

            // Get the folder contents
            var folderContents = $folder.find('.folder-contents').first();
            folderContents.toggle();

            // On folder click we load the content from the sub folders
            // lazily so that we don't kill the dom for a large number of files
            var deferredFolders = folderContents.children('.deferred-folders');

            if (deferredFolders.length) {
                // Load the subdirectories
                var innerFolders = $.render(templates.deferredFolder, directory.getDirectories());

                deferredFolders.html(innerFolders);

                deferredFolders.removeClass('deferred-folders');

                // Re-classify the files
                classifyFiles($this);
            }

            $(this).toggleClass('folder-collapsed');

            ev.preventDefault();
            return false;
        });

        $this.delegate('.open', 'dblclick', function (ev) {
            var path = $(this).closest('.file').data('path');
            var file = fs.getFile(path);

            $(that).trigger('fileExplorer.fileClicked', [file]);

            setFocus(true);

            ev.preventDefault();
            return false;
        });


        $('body').click(function (evt) {
            var target = evt.target;

            var explorer = $(target).closest('.fileExplorer');
            if (explorer.length) {
                setFocus(true);
            }
            else {
                setFocus(false);
            }
        });

        $(fs).bind('fileSystem.removeFile', function (e, file) {
            getFileNode(file).remove();
        });

        $(fs).bind('fileSystem.removeDirectory', function (e, directory) {
            getFolderNode(directory).remove();
        });

        $(fs).bind('fileSystem.addFile', function (e, file, index) {
            var directory = file.getDirectory();
            var $directory = $('#' + directory.getElementId());

            var fileContent = $.render(templates.file, file);

            var $files = $directory.find('.folder-contents')
                                   .first()
                                   .children('.files');

            var fileAt = $files.children()[index];
            if (fileAt) {
                $(fileAt).before($(fileContent));
            }
            else {
                $files.append(fileContent);
            }

            classifyFiles($directory);
        });

        $(fs).bind('fileSystem.addDirectory', function (e, directory, index) {
            var parentDirectory = directory.getParent();
            var $parentDirectoryDirectory = $('#' + parentDirectory.getElementId());

            var directoryContent = $.render(templates.deferredFolder, directory);
            var $directories = $parentDirectoryDirectory.find('.folder-contents')
                                                        .first()
                                                        .children('.folders');

            var directoryAt = $directories.children()[index];
            if (directoryAt) {
                $(directoryAt).before($(directoryContent));
            }
            else {
                $directories.append(directoryContent);
            }
        });

        function renderExplorer() {
            // Render the top level files
            var rendered = $.render(templates.folder, fs.getRoot());

            // Render the template
            $this.html(rendered);

            // Classify the files
            classifyFiles($this);
        }

        var that = {
            refresh: renderExplorer,
            getFileNode: getFileNode,
            getFolderNode: getFolderNode,
            getSelectedItem: function () {
                if (!$activeSelection) {
                    return null;
                }

                if ($activeSelection.hasClass('file')) {
                    var filePath = $activeSelection.data('path');
                    return {
                        file: fs.getFile(filePath)
                    };
                }

                var folderPath = $activeSelection.closest('.folder').data('path');
                return {
                    directory: fs.getDirectory(folderPath)
                };
            },
            clearSelection: function () {
                if ($activeSelection) {
                    $activeSelection.removeClass('selected');
                    $activeSelection = null;
                }
            },
            nextSelection: function () {
                if ($activeSelection.hasClass('file')) {
                    var next = $activeSelection.next();
                    if (next.length) {
                        setSelection(next);
                    }
                }
            },
            prevSelection: function () {
                if ($activeSelection.hasClass('file')) {
                    var prev = $activeSelection.prev();
                    if (prev.length) {
                        setSelection(prev);
                    }
                }
            },
            hasFocus: function () {
                return hasFocus;
            },
            setFocus : setFocus
        };

        return that;
    };

})(jQuery);