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
            hasFocus = false,
            nodeCache = {};

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

            $(fileExplorer).trigger('fileExplorer.focusChanged', [value]);

            if (activeNode) {
                activeNode.setFocus(value);
            }
        }

        function getFileNode(file) {
            if (!file) {
                return null;
            }

            return $this.find('[data-path="' + file.getPath() + '"]')
                        .filter('.file');
        }

        function getFolderNode(directory) {
            if (!directory) {
                return null;
            }
            return $this.find('[data-path="' + directory.getPath() + '"]')
                        .filter('.folder');
        }

        function setSelection(path) {
            if (activeNode) {
                activeNode.setFocus(false);
                activeNode.deselect();
            }

            if (path) {
                activeNode = fileExplorer.node(path);
                activeNode.select();
                setFocus(true);
            }
            else {
                activeNode = null;
            }

            if (activeNode) {
                // Get the element bounds
                var min = $this.offset().top;
                var max = min + $this.outerHeight();

                // Get the selection element bounds
                var $e = activeNode.selection();
                var top = $e.offset().top;
                var height = $e.outerHeight();
                var bottom = top + height;

                var threshold = height / 2;
                var topDiff = top - min;
                var bottomDiff = max - bottom;

                // Scroll if the element is out of range
                if (topDiff < 0) {
                    var target = $this.scrollTop() + topDiff - threshold;
                    $this.scrollTop(target);
                }
                else if (bottomDiff < threshold) {
                    var target = $this.scrollTop() + Math.abs(bottomDiff) + height;
                    $this.scrollTop(target);
                }
            }

            $(fileExplorer).trigger('fileExplorer.selectedNodeChanged', [activeNode]);
        }

        // Add the file explorer class so we can apply the appropriate styles
        $this.addClass('fileExplorer');

        // Setup event handlers
        $this.delegate('.selection', 'click', function (ev) {
            var path = $(this).closest('.node').data('path');
            setSelection(path);
        });

        $this.delegate('.icon-folder', 'click', function (ev) {
            var $folder = $(this).closest('.folder');
            var path = $folder.data('path');

            fileExplorer.node(path).toggle();
        });

        $this.delegate('.open', 'dblclick', function (ev) {
            var path = $(this).closest('.file').data('path');
            var file = fs.getFile(path);

            $(fileExplorer).trigger('fileExplorer.fileOpened', [file]);
        });


        $(document).click(function (ev) {
            var target = ev.target;

            var explorer = $(target).closest('.fileExplorer');
            if (explorer.length && explorer[0] === $this[0]) {
                setFocus(true);

                ev.preventDefault();
                return false;
            }
            else {
                setFocus(false);
            }
        });

        function removeNode(node) {
            node.element().remove();

            if (node === activeNode) {
                setSelection(null);
            }

            delete nodeCache[node.path];
        }

        $(fs).bind('fileSystem.removeFile', function (e, file) {
            removeNode(fileExplorer.nodeFor(file));
        });

        $(fs).bind('fileSystem.removeDirectory', function (e, directory) {
            removeNode(fileExplorer.nodeFor(directory));
        });

        $(fs).bind('fileSystem.addFile', function (e, file, index) {
            var node = fileExplorer.nodeFor(file);
            node.parentNode().addFile(file, index);
        });

        $(fs).bind('fileSystem.addDirectory', function (e, directory, index) {
            var node = fileExplorer.nodeFor(directory);
            node.parentNode().addFolder(directory, index);
        });

        function renderExplorer() {
            // Render the top level files
            var rendered = $.render(templates.folder, fs.getRoot());

            // Render the template
            $this.html(rendered);

            // Classify the files
            classifyFiles($this);
        }

        function renderFile(file) {
            return $.render(templates.file, file);
        }

        function renderFolder(directory) {
            return $.render(templates.deferredFolder, directory);
        }

        var throttled = {
            nextSelection: $.utils.throttle(function () {
                fileExplorer.nextSelection();
            }, 25),

            prevSelection: $.utils.throttle(function () {
                fileExplorer.prevSelection();
            }, 25)
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
                    $(fileExplorer).trigger('fileExplorer.fileOpened', [activeNode.item()]);
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
                            fileExplorer.select(path);
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

            if (!file && !directory) {
                throw "Invalid node";
            }

            var that = this;

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

            function getFolderContents() {
                var $folder = getFolderNode(directory);
                if (!$folder) {
                    return false;
                }

                return $folder.find('.folder-contents').first();
            }

            function populateChildren() {
                if (!directory) {
                    return;
                }

                // Get the folder contents
                var folderContents = getFolderContents();

                // On folder click we load the content from the sub folders
                // lazily so that we don't kill the dom for a large number of files
                var deferredFolders = folderContents.children('.deferred-folders');

                if (deferredFolders.length) {
                    // Load the subdirectories
                    var innerFolders = $.render(templates.deferredFolder, directory.getDirectories());

                    deferredFolders.html(innerFolders);

                    deferredFolders.removeClass('deferred-folders');

                    // Re-classify the files
                    classifyFiles(that.element());
                }
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
                if (directory && directory._isRoot()) {
                    return;
                }

                if (that.isCollapsed()) {
                    // Ensure children are populated
                    populateChildren();
                    getFolderContents().show();
                    getFolderToggle().removeClass('folder-collapsed');
                }
            }

            this.collapse = function () {
                if (directory && directory._isRoot()) {
                    return;
                }

                if (!that.isCollapsed()) {
                    getFolderContents().hide();
                    getFolderToggle().addClass('folder-collapsed');
                }
            }

            this.toggle = function () {
                if (that.isCollapsed()) {
                    that.expand();
                }
                else {
                    that.collapse();
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

            function addItem(content, index, containerType, itemType) {
                var $itemsContainer = getFolderContents().children(containerType);
                var $items = $itemsContainer.children(itemType);
                var $item = $items.eq(index);
                if ($item.length) {
                    $item.before($(content));
                }
                else {
                    $itemsContainer.append(content);
                }
            }

            this.addFolder = function (directory, index) {
                if (!directory) {
                    return;
                }

                var folderContent = renderFolder(directory);
                addItem(folderContent, index, '.folders', '.folder');
            }

            this.addFile = function (file, index) {
                if (!directory) {
                    return;
                }

                var fileContent = renderFile(file);
                addItem(fileContent, index, '.files', '.file');
                var $filesContainer = getFolderContents().children('.files');
                classifyFiles($filesContainer);
            }

            this.selection = getSelection;

            this.parentNode = function () {
                return fileExplorer.node(that.parentItem().getPath());
            }

            this.path = that.item().getPath();
        }

        function getSelectionPaths() {
            return $this.find('.selection').not(':hidden').map(function (i, e) {
                return $(e).closest('.node').data('path');
            })
        }

        var fileExplorer = {
            refresh: renderExplorer,
            node: function (path) {
                return nodeCache[path] || (nodeCache[path] = new node(path));
            },
            nodeFor: function (item) {
                return this.node(item.getPath());
            },
            selectedNode: function () {
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

                var paths = getSelectionPaths();
                var index = $.inArray(activeNode.path, paths);

                if (index + 1 < paths.length) {
                    setSelection(paths[index + 1]);
                }
            },
            prevSelection: function () {
                if (!activeNode) {
                    return;
                }

                var paths = getSelectionPaths();
                var index = $.inArray(activeNode.path, paths);

                if ((index - 1) >= 0) {
                    setSelection(paths[index - 1]);
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

        return fileExplorer;
    };

})(jQuery);