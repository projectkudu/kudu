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
            activeNode = null,
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

            if (activeNode) {
                activeNode.setFocus(value);
            }
        }

        function getFileNode(file) {
            if (!file) {
                return null;
            }
            return $('#' + file.getElementId());
        }

        function getFolderNode(directory) {
            if (!directory) {
                return null;
            }
            return $('#' + directory.getElementId());
        }

        function setSelection(path) {
            if (activeNode) {
                activeNode.setFocus(false);
                activeNode.deselect();
            }

            activeNode = new node(path);
            activeNode.select();
            activeNode.setFocus(true);
        }

        // Add the file explorer class so we can apply the appropriate styles
        $this.addClass('fileExplorer');

        // Setup event handlers
        $this.delegate('.selection', 'click', function (ev) {
            var path = $(this).closest('.node').data('path');
            setSelection(path);

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
            if (explorer.length && explorer[0] === $this[0]) {
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
                if (activeNode && activeNode.isFile()) {
                    $(that).trigger('fileExplorer.fileOpened', [activeNode.item()]);
                }
                ev.preventDefault();
                return true;
            }
        }, 50));

        $(document).bind('keydown', 'right', $.utils.throttle(function (ev) {
            if (hasFocus) {
                if (activeNode) {
                    activeNode.expand();
                }
                ev.preventDefault();
                return true;
            }
        }, 50));

        $(document).bind('keydown', 'left', $.utils.throttle(function (ev) {
            if (hasFocus) {
                if (activeNode) {

                    var expandParent = true;
                    if (activeNode.isFolder()) {
                        if (!activeNode.isCollapsed()) {
                            activeNode.collapse();
                            expandParent = false;
                        }
                    }

                    if (expandParent) {
                        var path = activeNode.parentNode().path;
                        if (path) {
                            that.select(path);
                        }
                    }
                }
                ev.preventDefault();
                return true;
            }
        }, 50));


        function node(path) {
            var file = fs.getFile(path);
            var directory = fs.getDirectory(path);

            var that = this;

            this.path = path;

            function getSelection() {
                return that.element().find('.selection').first();
            }

            function getFolderToggle() {
                var $folder = getFolderNode(directory);
                if (!$folder) {
                    return false;
                }

                return $folder.find('.icon-folder').first();
            }

            this.parentItem = function () {
                if (directory) {
                    return directory.getParent();
                }
                return file.getDirectory();
            }

            this.isFile = function () {
                return file;
            }

            this.isFolder = function () {
                return directory;
            }

            this.expand = function () {
                if (that.isCollapsed()) {
                    getFolderToggle().trigger('click');
                }
            }

            this.collapse = function () {
                if (!that.isCollapsed()) {
                    getFolderToggle().trigger('click');
                }
            }

            this.element = function () {
                return getFileNode(file) || getFolderNode(directory);
            }

            this.item = function () {
                return file || directory;
            }

            this.select = function () {
                that.selection().addClass('selected');
            }

            this.deselect = function () {
                that.selection().removeClass('selected');
            }

            this.isCollapsed = function () {
                var $folderToggle = getFolderToggle();
                if (!$folderToggle) {
                    return false;
                }
                return $folderToggle.hasClass('folder-collapsed');
            }

            this.setFocus = function (value) {
                if (value === true) {
                    that.selection().removeClass('no-focus');
                }
                else {
                    that.selection().addClass('no-focus');
                }
            }

            this.selection = getSelection;

            this.parentNode = function () {
                return new node(that.parentItem().getPath());
            }
        }

        var that = {
            refresh: renderExplorer,
            getFileNode: getFileNode,
            getFolderNode: getFolderNode,
            getSelectedItem: function () {
                return activeNode;
            },
            clearSelection: function () {
                if (activeNode) {
                    activeNode.deselect();
                    activeNode = null;
                }
            },
            nextSelection: function () {
                if (!activeNode) {
                    return;
                }

                var selections = $this.find('.selection').not(':hidden');
                var index = $.inArray(activeNode.selection()[0], selections);

                if (index + 1 < selections.length) {
                    var elem = selections.eq(index + 1);
                    var path = elem.closest('.node').data('path');
                    setSelection(path);
                }
            },
            prevSelection: function () {
                if (!activeNode) {
                    return;
                }

                var selections = $this.find('.selection').not(':hidden');
                var index = $.inArray(activeNode.selection()[0], selections);

                if ((index - 1) >= 0) {
                    var elem = selections.eq(index - 1);
                    var path = elem.closest('.node').data('path');
                    setSelection(path);
                }
            },
            hasFocus: function () {
                return hasFocus;
            },
            setFocus: setFocus,
            select: function (path) {
                setSelection(path);
                return activeNode;
            }
        };

        return that;
    };

})(jQuery);