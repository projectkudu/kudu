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

            $(that).trigger('fileExplorer.fileOpened', [file]);

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

        var throttled = {
            nextSelection: $.utils.throttle(function () {
                that.nextSelection();
            }, 50),

            prevSelection: $.utils.throttle(function () {
                that.prevSelection();
            }, 50)
        };

        $(document).bind('keydown', 'down', function (ev) {
            if (hasFocus) {
                throttled.nextSelection();
                ev.preventDefault();
                return true;
            }
        });

        $(document).bind('keydown', 'up', function (ev) {
            if (hasFocus) {
                throttled.prevSelection();
                ev.preventDefault();
                return true;
            }
        });

        $(document).bind('keydown', 'return', $.utils.throttle(function (ev) {
            if (hasFocus) {
                var item = that.getSelectedItem();
                if (item && item.file) {
                    $(that).trigger('fileExplorer.fileOpened', [item.file]);
                }
                ev.preventDefault();
                return true;
            }
        }, 50));

        $(document).bind('keydown', 'right', $.utils.throttle(function (ev) {
            if (hasFocus) {
                that.expandActiveNode();
                ev.preventDefault();
                return true;
            }
        }, 50));

        $(document).bind('keydown', 'left', $.utils.throttle(function (ev) {
            if (hasFocus) {
                if (that.collapseActiveNode() === false) {
                    // If we couldn't collapse the node then select the parent
                    var item = that.getSelectedItem();
                    if (item) {
                        var path = null;
                        if (item.directory) {
                            path = item.directory.getParent().getPath();
                        }
                        else if (item.file) {
                            path = item.file.getDirectory().getPath();
                        }

                        if (path) {
                            that.select(path);
                        }
                    }
                }
                ev.preventDefault();
                return true;
            }
        }, 50));

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
                var selections = $this.find('.selection').not(':hidden');
                var index = $.inArray($activeSelection[0], selections);

                if (index + 1 < selections.length) {
                    setSelection(selections.eq(index + 1));
                }
            },
            prevSelection: function () {
                var selections = $this.find('.selection').not(':hidden');
                var index = $.inArray($activeSelection[0], selections);

                if ((index - 1) >= 0) {
                    setSelection(selections.eq(index - 1));
                }
            },
            expandActiveNode: function () {
                if (!$activeSelection.hasClass('file')) {
                    var $folderToggle = $activeSelection.find('.icon-folder').first();
                    if ($folderToggle.hasClass('folder-collapsed')) {
                        $folderToggle.trigger('click');
                        return true;
                    }
                }
                return false;
            },
            collapseActiveNode: function () {
                if (!$activeSelection.hasClass('file')) {
                    var $folderToggle = $activeSelection.find('.icon-folder').first();
                    if (!$folderToggle.hasClass('folder-collapsed')) {
                        $folderToggle.trigger('click');
                        return true;
                    }
                }
                return false;
            },
            hasFocus: function () {
                return hasFocus;
            },
            setFocus: setFocus,
            select: function (path) {
                var file = fs.getFile(path);
                if (file) {
                    setSelection(getFileNode(file));
                }
                else {
                    var directory = fs.getDirectory(path);
                    if (directory) {
                        var folder = getFolderNode(directory);
                        setSelection(folder.find('.selection').first());
                    }
                }
            }
        };

        return that;
    };

})(jQuery);