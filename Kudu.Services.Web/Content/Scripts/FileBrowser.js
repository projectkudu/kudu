$(function () {
    var Vfs = {
        getContent: function(item) {
            return $.ajax({
                url: item.href,
                dataType: "text"
            });
        },

        setContent: function (item, text) {
            return $.ajax({
                url: item.href,
                data: text,
                method: "PUT",
                processData: false,
                headers: {
                    "If-Match": "*"
                }
            });
        },

        getChildren: function (item) {
            return $.get(item.href);
        },

        createFolder: function (folder) {
            return $.ajax({
                url: folder.href + "/",
                method: "PUT"
            });
        },

        addFiles: function (files) {
            return whenArray(
                $.map(files, function (item) {
                    return Vfs.setContent({ href: viewModel.selected().href + item.name }, item.contents);
                })
            );
        },

        deleteItems: function (item) {
            var url = item.href;
            
            if (item.mime === "inode/directory") {
                url += "?recursive=true";
            }

            return $.ajax({
                url: url,
                method: "DELETE",
                headers: {
                    "If-Match": "*"
                }
            });
        }
    };

    var node = function (data, parent) {
        this.parent = parent;
        this.name = ko.observable(data.name);
        this.size = ko.observable(data.size ? (Math.ceil(data.size / 1024) + ' KB') : '');
        this.mime = data.mime || (data.type === "dir" && "inode/directory");
        this.isDirectory = ko.observable(this.mime === "inode/directory");
        this.href = data.href;
        this._href = ko.observable(this.href);
        this.modifiedTime = ((data.mtime && new Date(data.mtime)) || new Date()).toLocaleString();
        this.url = ko.observable(this.isDirectory() ? data.href.replace(/\/vfs\//, "/zip/") : data.href);
        this.appRelativePath = ko.computed(function () {
            if (this._href() === '/vfs/') {
                return "";
            }
            var path = this._href().replace(/.*\/vfs($|\/(.*)\/)/, "$2").replace(/\//g, "\\");
            path.length--;
            return path;
        }, this);
        this.children = ko.observableArray([]);
        this.editing = ko.observable(data.editing || false);
        this._fetchStatus;

        this.fetchChildren = function (force) {
            var that = this;

            if (!that._fetchStatus || (force && that._fetchStatus === 2)) {
                that._fetchStatus = 1;
                viewModel.processing(true);

                return Vfs.getChildren(that)
                .done(function (data) {
                    viewModel.processing(false);
                    var children = that.children;
                    children.removeAll();
                    $.each(data, function () {
                        children.push(new node(this, that));
                    });
                    that._fetchStatus = 2;
                }).promise();
            } else {
                return $.Deferred().resolve().promise();
            }
        }
        this.deleteItem = function () {
            if (confirm("Are you sure you want to delete '" + this.name() + "'?")) {
                var that = this;
                viewModel.processing(true);
                Vfs.deleteItems(this).done(function () {
                    that.parent.children.remove(that);
                    if (viewModel.selected() === this) {
                        viewModel.selected(this.parent);
                    }
                }).always(function () {
                    viewModel.processing(false);
                });
            }
        }
        this.selectNode = function () {
            var that = this;
            return this.fetchChildren().pipe(function () {
                stashCurrentSelection(viewModel.selected());
                viewModel.selected(that);

                return $.Deferred().resolve();
            });
        };
        this.selectChild = function (descendantPath) {
            var that = this;
            return this.fetchChildren().pipe(function () {
                var childName = descendantPath.split(/\/|\\/)[0].toLowerCase(),
                    matches = $.grep(that.children(), function (elm) {
                        return elm.name().toLowerCase() === childName;
                    }),
                    deferred;
                if (matches && matches.length) {
                    var selectedChild = matches[0];
                    viewModel.selected(selectedChild);
                    if (descendantPath.length > childName.length) {
                        deferred = selectedChild.selectChild(descendantPath.substring(childName.length + 1));
                    }
                    selectedChild.fetchChildren();
                }

                return deferred || $.Deferred().resolve();
            });
        }

        this.selectParent = function () {
            var that = viewModel.selected();
            if (that.parent) {
                stashCurrentSelection(that);
                viewModel.selected(that.parent);
            }
        }

        this.editItem = function () {
            var that = this;
            viewModel.editText("Fetching changes...");
            viewModel.fileEdit(this);
            Vfs.getContent(this)
               .done(function(data) {
                   viewModel.editText(data);
               });
        }

        this.saveItem = function () {
            var text = viewModel.editText();
            viewModel.editText("Saving changes...");
            Vfs.setContent(this, text)
                .done(function () {
                    viewModel.fileEdit(null);
                });
        }
    }

    var root = new node({ name: "/", type: "dir", href: "/vfs/" }),
        ignoreWorkingDirChange = true,
        workingDirChanging = false,
        viewModel = {
            root: root,
            selected: ko.observable(root), processing: ko.observable(false),
            sort: function (array) {
                return array.sort(function (a, b) {
                    var aDir = a.isDirectory(),
                        bDir = b.isDirectory();

                    if (aDir ^ bDir) {
                        // If one of them is a directory, then it always comes first
                        return aDir ? -1 : 1;
                    }
                    return a.name().localeCompare(b.name());
                });
            },
            fileEdit: ko.observable(null),
            editText: ko.observable(""),
            cancelEdit: function () {
                viewModel.fileEdit(null);
            }

        };

    root.fetchChildren();
    ko.applyBindings(viewModel, document.getElementById("#main"));

    window.KuduExec.workingDir.subscribe(function (newValue) {
        if (ignoreWorkingDirChange) {
            ignoreWorkingDirChange = false;
            return;
        }
        var appRoot = window.KuduExec.appRoot.toLowerCase();
        if (newValue.length >= appRoot.length && newValue.toLowerCase().indexOf(appRoot) === 0) {
            workingDirChanging = true;
            var relativeDir = newValue.substring(appRoot.length).replace(/^(\/|\\)?(.*)(\/|\\)?$/g, '$2'),
                deferred;
            if (!relativeDir) {
                deferred = viewModel.root.selectNode();
            } else {
                stashCurrentSelection(viewModel.selected());
                deferred = viewModel.root.selectChild(relativeDir);
            }
            deferred.done(function () {
                workingDirChanging = false;
            });
        }
    });

    viewModel.selected.subscribe(function (newValue) {
        if (!workingDirChanging) {
            var path = window.KuduExec.appRoot + '\\' + newValue.appRelativePath();
            // Mark it so that no-op the subscribe callback.
            ignoreWorkingDirChange = true;
            window.KuduExec.changeDir(path);

            newValue.fetchChildren(/* force */ true);
        }
    });

    window.KuduExec.completePath = function (value, dirOnly) {
        var subDirs = value.toLowerCase().split(/\/|\\/),
            cur = viewModel.selected(),
            curToken = '';

        while (subDirs.length && cur) {
            curToken = subDirs.shift();
            if (curToken === '..' && cur && cur.parent) {
                cur = cur.parent;
                continue;
            }

            if (!cur.children || !cur.children().length) {
                cur = null;
                break;
            }

            cur = $.grep(cur.children(), function (elm) {
                if (dirOnly && !elm.isDirectory()) {
                    return false;
                }

                return subDirs.length ? (elm.name().toLowerCase() === curToken) : elm.name().toLowerCase().indexOf(curToken) === 0;
            });

            if (cur && cur.length === 1 && subDirs.length) {
                // If there's more path to traverse and we have exactly one match, return
                cur = cur[0];                
            }
        }
        if (cur) {
            return $.map(cur, function (elm) { return elm.name().substring(curToken.length); });
        }
    };

    function stashCurrentSelection(selected) {
        if (window.history && window.history.pushState) {
            window.history.pushState(selected.appRelativePath(), selected.name());
        }
    }

    window.onpopstate = function (evt) {
        var selected = viewModel.selected();
        if (selected.parent) {
            viewModel.selected(selected.parent);
        }
    };

    $("#fileList").on("keydown", "input[type=text]", function (evt) {
        var context = ko.contextFor(this),
            data = context.$data;

        if (evt.which === 27) { // Cancel if Esc is pressed.
            data.parent.children.remove(data);
            return false;
        }
    });

    $("#createFolder").click(function () {
        var newFolder = new node({ name: "", type: "dir", href: "", editing: true }, viewModel.selected());
        $(this).prop("disabled", true);
        viewModel.selected().children.unshift(newFolder);
        $("#fileList input[type='text']").focus();

        newFolder.name.subscribe(function (value) {
            newFolder.href = trimTrailingSlash(newFolder.parent.href) + '/' + value + '/';
            newFolder._href(newFolder.href);
            newFolder.editing(false);
            Vfs.createFolder(newFolder).fail(function () {
                viewModel.selected().children.remove(newFolder);
            });
            $("#createFolder").prop("disabled", false);
        });
    });

    // Drag and drop
    $('#fileList')
      .on('dragenter dragover', function (e) {
          e.preventDefault();
          e.stopPropagation();
      })
      .on('drop', function (evt) {
          evt.preventDefault();
          evt.stopPropagation();

          var dir = viewModel.selected();
          viewModel.processing(true);
          _getInputFiles(evt).done(function (files) {
              Vfs.addFiles(files).always(function () {
                  dir.fetchChildren(/* force */ true);
                  viewModel.processing(false);
              });
          });
      });

    var defaults = { fileList: '40%', console: '45%' };
    $('#resizeHandle .down')
        .on('click', function (e) {
            var fileList = $('#fileList'),
                console = $('#KuduExecConsole');
            if (!console.is(':visible')) {
                return;
            } else if (fileList.is(":visible")) {
                console.slideDown(function () {
                    console.hide();
                    fileList.css('height', '85%');
                });
            } else {
                console.css('height', defaults.console);
                fileList.css('height', defaults.fileList);
                fileList.show();
            }
        });

    $('#resizeHandle .up')
        .on('click', function (e) {
            var fileList = $('#fileList'),
                console = $('#KuduExecConsole');
            if (!fileList.is(':visible')) {
                return;
            } else if (console.is(':visible')) {
                fileList.slideUp(function () {
                    fileList.hide();
                    console.css('height', '85%');
                });
            } else {
                fileList.css('height', defaults.fileList);
                console.css('height', defaults.console);
                console.show();
            }
        });


    function _getInputFiles(evt) {
        var dt = evt.originalEvent.dataTransfer,
            items = evt.originalEvent.dataTransfer.items;

        if (items && items.length) {
            return whenArray($.map(items, function (item) {
                var entry = (item.webkitGetAsEntry || item.getAsEntry).apply(item);
                return _processEntry(entry);
            })).pipe(function () {
                return Array.prototype.concat.apply([], arguments);
            })
        } else {
            return $.Deferred().resolveWith(null, [$.map(dt.files, function (e) {
                return { name: e.name, contents: e };
            })]);
        }
    }

    function _processEntry(entry, parentPath) {
        parentPath = parentPath || '';
        var deferred = $.Deferred();
        if (entry.isFile) {
            entry.file(function (file) {
                deferred.resolveWith(null, [{ name: parentPath + '/' + entry.name, contents: file }]);
            });
        } else {
            entry.createReader().readEntries(function (entries) {
                var directoryPath = parentPath + '/' + entry.name;
                whenArray($.map(entries, function (e) {
                    return _processEntry(e, directoryPath);
                })).done(function () {
                    deferred.resolveWith(null, [Array.prototype.concat.apply([], arguments)]);
                });;
            });
        }
        return deferred.promise();
    }

    function whenArray(deferreds) {
        return $.when.apply($, deferreds);
    }

    function trimTrailingSlash(input) {
        return input.replace(/(\/|\\)$/, '');
    }
});