// Custom status bar for Ace (aka Project Wunderbar)
var statusbar = {
    showFilename:
        function () {
            var filename;
            try {
                filename = viewModel.fileEdit.peek().name();
            }
            catch (e) {
                filename = 'Can not get filename. See console for details.';
                if (typeof console == 'object') {
                    console.error('Can not get filename: %s', e);
                }
            }
            finally {
                $('#statusbar').text(filename);
            }
        },
    reset:
        function () {
            $('#statusbar').text('');
            $('#statusbar').removeClass('statusbar-red');
            $('#statusbar').removeClass('statusbar-saved');
            $('#statusbar').css('background', 'none');
            // Clear editor window
            editor.setValue('');
            // Flag from ace-init.js
            contentHasChanged = false;
            // Clear search box
            if (editor.searchBox) {
                editor.searchBox.activeInput.value = '';
                editor.searchBox.hide();
            }
        },
    savingChanges:
        function () {
            $('#statusbar').text('Saving changes...');
            $('#statusbar').prepend('<i class="glyphicon glyphicon-cloud-upload" style="margin-right: 6px"></i>');
        },
    fetchingContents:
        function () {
            $('#statusbar').text('Fetching contents...');
            $('#statusbar').prepend('<i class="glyphicon glyphicon-cloud-download" style="margin-right: 6px"></i>');
        },
    acknowledgeSave:
        function () {
            this.errorState.remove();
            $('#statusbar').addClass('statusbar-saved');
            contentHasChanged = false;
            this.showFilename();
        },
    errorState:
        {
            set: function () {
                // We could not save the file
                // Mild panic attack, turn statusbar red
                statusbar.showFilename();
                $('#statusbar').css('background', '#ffdddd');
            },
            remove: function () {
                $('#statusbar').css('background', 'none');
                $('#statusbar').removeClass('statusbar-red');
            }
        }
};

function showAceHelpModal() {
    $('#ace-help-modal').modal();
    $('#ace-help-modal .modal-body').load('/DebugConsole/AceHelp.html',
        function (response, status, xhr) {
            if (status == 'error') {
                $(this).html('<div class="alert alert-warning" role="alert">' +
                             'Yikes! Can not load help page:<br>' +
                             'Error Code ' + xhr.status + ' ' + xhr.statusText + '</div>');
                if (typeof console == 'object') {
                    console.error('Can not load help page: ' + 'xhr.status = ' +
                                  xhr.status + ' ' + xhr.statusText);
                }
            }
        });
}


var copyObjectsManager = {
    init: function () {
        this._copyProgressObjects = {};
        this.infoMessage = '';
    },
    getInfoMessage: function () {
        return this._infoMessage;
    },
    setInfoMessage: function (message) {
        this._infoMessage = message;
    },
    addCopyStats: function (uri, loadedData, totalData) {

        uri = uri.substring(uri.indexOf('/vfs') + 5, uri.length); // slice uri to be prettier[ex: http://localhost:37911/api/vfs/ttesstt//Kudu.FunctionalTests/Vfs/VfsControllerTest.cs => ttesstt//Kudu.FunctionalTests/Vfs/VfsControllerTest.cs]
        if (this._copyProgressObjects[uri]) {
            if (loadedData === totalData) {
                this._copyProgressObjects[uri].endDate = $.now();
            } else {
                this._copyProgressObjects[uri].copyPackEnded = false;
            }
        } else {
            this._copyProgressObjects[uri] = {};
            this._copyProgressObjects[uri].startDate = $.now();
            this._copyProgressObjects[uri].copyPackEnded = false; //this is used for when copying multiple files in the same time so that i may still have a coherent percentage
        }

        if (totalData === 0) { // empty files appear to have size 0
            totalData = loadedData = 1;
        }

        this._copyProgressObjects[uri].loadedData = loadedData;
        this._copyProgressObjects[uri].totalData = totalData;
    },
    getCopyStats: function () {
        return this._copyProgressObjects;
    },
    getCurrentPercentCompletion: function () {
        var currentTransfered = 0;
        var finalTransfered = 0;
        var foundItem = false;

        for (var key in this._copyProgressObjects) {
            var co = this._copyProgressObjects[key];
            if (co.copyPackEnded === false) {
                foundItem = true;
                currentTransfered += co.loadedData;
                finalTransfered += co.totalData;
            }
        }

        var perc = 0;
        if (foundItem) {
            perc = parseInt((currentTransfered / finalTransfered) * 100);
        } else { // to avoid 0/0
            perc = 100;
        }

        if (perc === 100 && foundItem) { // if all transactions have finished & have some unmarked transaction pack, cancel it out
            for (var key in this._copyProgressObjects) {
                this._copyProgressObjects[key].copyPackEnded = true;
            }
        }

        return perc;
    },
    removeAtIndex: function (index) {
        delete this._copyProgressObjects[index];
    },
    clearData: function () {
        var date = new Date();
        this._infoMessage = 'You have cleared the cache at ' + date.toLocaleString();
        this._copyProgressObjects = {};
    }
}

copyObjectsManager.init();

$.connection.hub.url = appRoot + "api/filesystemhub";
var fileSystemHub = $.connection.fileSystemHub;
fileSystemHub.client.fileExplorerChanged = function () {
    window.viewModel.selected().fetchChildren(true);
};
$.connection.hub.start().done(function () {
    var Vfs = {
        getContent: function (item) {
            return $.ajax({
                url: item.href,
                dataType: "text"
            });
        },

        setContent: function (item, text) {
            var _url = item.href.replace(/#/g, encodeURIComponent("#"));
            return $.ajax({
                url: _url,
                data: text,
                method: "PUT",
                xhr: function () {  // Custom XMLHttpRequest
                    var myXhr = $.ajaxSettings.xhr();
                    if (myXhr.upload) { // Check if upload property exists
                        myXhr.upload.addEventListener('progress', function (e) {
                            copyProgressHandlingFunction(e, _url);
                        }, false); // For handling the progress of the upload
                    }
                    return myXhr;
                },
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
                // Add trailing slash for new folder when calling VFS
                // https://github.com/projectkudu/kudu/wiki/REST-API
                url: folder.href.replace(/#/g, encodeURIComponent("#")) + "/",
                method: "PUT",
                error: function (xhr, status, error) {
                    if (xhr.statusText === 'error') {
                        showErrorAsToast('Error when calling virtual file system REST backend. Check F12 Console for more.');
                    }
                    else {
                        showErrorAsToast(xhr);
                    }
                }
            });
        },

        createFile: function (file) {
            return $.ajax({
                // No trailing slash for new file when calling VFS
                // https://github.com/projectkudu/kudu/wiki/REST-API
                url: file.href.replace(/#/g, encodeURIComponent("#")),
                method: "PUT",
                error: function (xhr, status, error) { showErrorAsToast(xhr); }
            });
        },

        addFiles: function (files, unzip) {
            return whenArray(
                $.map(files, function (item) {
                    var baseHref = unzip ? viewModel.selected().href.replace(/\/vfs\//, "/zip/") : viewModel.selected().href;
                    var finalHref = (baseHref + (unzip ? "" : item.name));
                    copyObjectsManager.addCopyStats(finalHref, 0, item.contents.size); //files copy progress data for monitory
                    return Vfs.setContent({ href: finalHref }, item.contents);
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

    var MAX_VIEW_ITEMS = 300;

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
        this.path = ko.observable(data.path);
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

                    // maxViewItems overridable by localStorage setting.
                    var maxViewItems = getLocalStorageSetting("maxViewItems", MAX_VIEW_ITEMS);
                    var folders = [];
                    var files = $.map(data, function (elem) {
                        if (elem.mime === "inode/shortcut") {
                            viewModel.specialDirs.push(new node(elem));
                        } else if (--maxViewItems > 0) {
                            if (elem.mime === "inode/directory") {
                                // track folders explicitly to avoid additional sort
                                folders.push(new node(elem, that));
                            } else {
                                return new node(elem, that);
                            }
                        }
                    });

                    // view display folders then files
                    children.push.apply(children, folders);
                    children.push.apply(children, files);

                    that._fetchStatus = 2;
                }).fail(showError);
            } else {
                return $.Deferred().resolve();
            }
        }
        this.deleteItem = function () {
            if (confirm("Are you sure you want to delete '" + this.name() + "'?")) {
                var that = this;
                viewModel.processing(true);
                Vfs.deleteItems(this).done(function () {
                    that.parent.children.remove(that);
                    if (viewModel.selected() === this) {
                        updateSelectedAndNotifyCommandLine(this.parent);
                    }
                    viewModel.processing(false);
                }).fail(function (error) {
                    showErrorAsToast(error);
                });
            }
        }

        this.selectNode = function () {
            stashCurrentSelection(viewModel.selected());
            updateSelectedAndNotifyCommandLine(this);
        };

        this.selectChild = function (descendantPath) {
            var childName = descendantPath.split(/\/|\\/)[0].toLowerCase(),
                    matches = $.grep(this.children(), function (elm) {
                        return elm.name().toLowerCase() === childName;
                    });

            if (matches && matches.length) {
                var selectedChild = matches[0];
                updateSelectedOnly(selectedChild).done(function () {
                    if (descendantPath.length > childName.length) {
                        selectedChild.selectChild(descendantPath.substring(childName.length + 1));
                    }
                });
            }
        }

        this.selectParent = function () {
            var that = viewModel.selected();
            if (that.parent) {
                stashCurrentSelection(that);
                updateSelectedAndNotifyCommandLine(that.parent);
            }
        }

        this.editItem = function () {
            var that = this;
            // Blank out the editor before fetching new content
            viewModel.editText('');
            statusbar.fetchingContents();
            viewModel.fileEdit(this);
            if (this.mime === "text/xml") {
                Vfs.getContent(this)
                   .done(function (data) {
                       viewModel.editText(vkbeautify.xml(data));
                       statusbar.showFilename();
                       // Editor h-scroll workaround
                       editor.session.setScrollLeft(-1);
                   }).fail(showError);
            }
            else {
                Vfs.getContent(this)
                   .done(function (data) {
                       viewModel.editText(data);
                       statusbar.showFilename();
                       // Editor h-scroll workaround
                       editor.session.setScrollLeft(-1);
                   }).fail(showError);
            }
        }

        this.saveItem = function () {
            var text = viewModel.editText();
            statusbar.savingChanges();
            Vfs.setContent(this, text)
                .done(function () {
                    statusbar.acknowledgeSave();
                }).fail(function (error) {
                    removeAllToasts();
                    showErrorAsToast(error);
                    statusbar.errorState.set();
                });
        }

        this.saveItemAndClose = function () {
            var text = viewModel.editText();
            statusbar.savingChanges();
            Vfs.setContent(this, text)
                .done(function () {
                    viewModel.fileEdit(null);
                    statusbar.reset();
                }).fail(function (error) {
                    removeAllToasts();
                    showErrorAsToast(error);
                    statusbar.errorState.set();
                });
        }
    }

    var root = new node({ name: "/", type: "dir", href: appRoot + "api/vfs/" }),
        ignoreWorkingDirChange = false, // global variables
        viewModel = {
            root: root,
            copyProgStats: ko.observable(),
            specialDirs: ko.observableArray([]),
            selected: ko.observable(root),
            koprocessing: ko.observable(false),
            fileEdit: ko.observable(null),
            editText: ko.observable(""),
            isTransferInProgress: ko.observable(false),
            cancelEdit: function () {
                viewModel.fileEdit(null);
                statusbar.reset();
                removeAllToasts();
            },
            selectSpecialDir: function (name) {
                var item = viewModel.specialDirsIndex()[name];
                if (item) {
                    item.selectNode();
                }
            },
            showCopyProgressModal: function () {
                $('#files-transfered-modal').modal();
                copyProgressHandlingFunction(null, null, true);
            },
            clearCopyProgressCache: function () {
                copyObjectsManager.clearData();
                viewModel.copyProgStats("");
            },
            getCopyPercentage: function (item) {
                return (item.loadedData * 100 / item.totalData).toFixed(1);
            },
            getCopyPercentageDisplay: function (item) {
                return formatHandler.fileSize(item.loadedData, true) + " / " + formatHandler.fileSize(item.totalData, true);
            },
            errorText: ko.observable(),
            inprocessing: 0,
            processing: function (value) {
                value ? viewModel.inprocessing++ : viewModel.inprocessing--;
                if (viewModel.inprocessing > 0) {
                    viewModel.koprocessing(true);
                } else {
                    viewModel.koprocessing(false);
                    viewModel.isTransferInProgress(false);
                }
            }
        };

    viewModel.specialDirsIndex = ko.dependentObservable(function () {
        var result = {};
        ko.utils.arrayForEach(viewModel.specialDirs(), function (value) {
            result[value.name()] = value;
        });
        return result;
    }, viewModel),

    viewModel.showSiteRoot = ko.computed(function () {
        if ($.isEmptyObject(viewModel.specialDirsIndex())) {
            return true;
        }
        return viewModel.specialDirsIndex()['LocalSiteRoot'] !== undefined;
    }, viewModel);

    root.fetchChildren();
    ko.applyBindings(viewModel, document.getElementById("#main"));
    setupFileSystemWatcher();

    window.KuduExec.workingDir.subscribe(function (newValue) {
        if (ignoreWorkingDirChange) {
            ignoreWorkingDirChange = false;
            return;
        }

        function getRelativePath(parent, childDir) {
            var parentPath = (parent.path() || window.KuduExec.appRoot).toLowerCase();
            if (childDir.length >= parentPath.length && childDir.toLowerCase().indexOf(parentPath) === 0) {
                return { parent: parent, relativePath: childDir.substring(parentPath.length).replace(/^(\/|\\)?(.*)(\/|\\)?$/g, "$2") };
            }
        }

        var relativeDir = getRelativePath(viewModel.root, newValue) ||
            getRelativePath(viewModel.specialDirsIndex()["LocalSiteRoot"], newValue) ||
            getRelativePath(viewModel.specialDirsIndex()["SystemDrive"], newValue)

        stashCurrentSelection(viewModel.selected());
        if (!relativeDir || !relativeDir.relativePath) {
            updateSelectedOnly((relativeDir && relativeDir.parent) || viewModel.root)
        } else {
            relativeDir.parent.selectChild(relativeDir.relativePath);
        }
    });

    updateSelectedAndNotifyCommandLine = function (newValue) {
        updateSelectedOnly(newValue);

        // notify command line 
        ignoreWorkingDirChange = true;
        window.KuduExec.changeDir(newValue.path());
    }

    // updateSelectedOnly return a promise since it also update its children
    updateSelectedOnly = function (newValue) {
        viewModel.selected(newValue); // update selected
        updateFileSystemWatcher(newValue.path()); // update the filesystem watcher, always accompany selected(newValue)
        // in old code, children are ONLY FORCE update if navigate using File Explorer, in NEW CODE, we standardize them
        return newValue.fetchChildren(/* force */ true); // update children of selected
    }

    window.KuduExec.completePath = function (value, dirOnly) {
        var subDirs = value.toLowerCase().split(/\/|\\/),
            cur = viewModel.selected(),
            curToken = "";

        while (subDirs.length && cur) {
            curToken = subDirs.shift();
            if (curToken === ".." && cur && cur.parent) {
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
            return $.map(cur, function (elm) { return elm.name(); });
        }
    };

    //monitor file upload progress
    function copyProgressHandlingFunction(e, uniqueUrl, forceUpdateModal) {
        if (e && uniqueUrl && e.lengthComputable) {
            copyObjectsManager.addCopyStats(uniqueUrl, e.loaded, e.total); //add/update stats
        }
        var perc = copyObjectsManager.getCurrentPercentCompletion(); // perc-per-total transaction
        var copyObjs = copyObjectsManager.getCopyStats();

        $('#copy-percentage').text(perc + "%");

        if (perc != 100 && perc != 0) {
            viewModel.isTransferInProgress(true);
        }

        //handler for clearing out cache once it gets too large
        var currentObjCount = Object.keys(copyObjs).length;
        if (currentObjCount > 2000) {
            for (var i = 0; i < 1000; i++) { //delete oldest 1000 copy prog objects
                copyObjectsManager.removeAtIndex(0);
            }
            var date = new Date();
            copyObjectsManager.setInfoMessage('Cache was partialy auto-cleared at ' + date.toLocaleString() + ' for performance improvements');
        }

        if ($('#files-transfered-modal').is(':visible') || forceUpdateModal) { // update if modal visible
            viewModel.copyProgStats(copyObjs); // update viewmodel

            var modalHeaderText = '';
            if (perc < 100) {
                modalHeaderText = 'Transferred Files (<b>' + perc + '%</b>).';
            } else {
                modalHeaderText = '<b style =\' color:green\'> Transferred Files (' + perc + '%).</b>';
            }
            modalHeaderText += ' ' + ((_temp = copyObjectsManager.getInfoMessage()) ? _temp : "");
            $('#files-transfered-modal .modal-header').html(modalHeaderText);
        }

    }

    function setupFileSystemWatcher() {
        updateFileSystemWatcher(null);
    }

    function updateFileSystemWatcher(newValue) {
        window.viewModel = viewModel;
        fileSystemHub.server.register(newValue);
    }

    window.KuduExec.updateFileSystemWatcher = updateFileSystemWatcher;

    function stashCurrentSelection(selected) {
        if (window.history && window.history.pushState) {
            // shunTODO, onpopstate does not care about this value
            window.history.pushState(selected.path(), selected.name());
        }
    }

    function getLocalStorageSetting(name, defaultValue) {
        try {
            var value = window.localStorage[name];
            if (value === undefined) {
                return defaultValue;
            }

            if (typeof (defaultValue) === "number") {
                return parseInt(value);
            } else if (typeof (defaultValue) === "boolean") {
                return !!value;
            } else {
                return value;
            }
        } catch (e) {
            return defaultValue;
        }
    }

    window.onpopstate = function (evt) {
        if (viewModel.fileEdit()) {
            // If we're editing, exit the editing.
            viewModel.fileEdit(null);
        } else {
            var selected = viewModel.selected();
            if (selected.parent) {
                updateSelectedAndNotifyCommandLine(selected.parent);
            }
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

    $("#createFolder").click(function (evt) {
        evt.preventDefault();

        var newFolder = new node({ name: "", type: "dir", href: "", editing: true }, viewModel.selected());
        $(this).prop("disabled", true);
        viewModel.selected().children.unshift(newFolder);
        $("#fileList input[type='text']").focus();

        newFolder.name.subscribe(function (value) {
            newFolder.href = trimTrailingSlash(newFolder.parent.href) + "/" + value + "/";
            newFolder._href(newFolder.href);
            newFolder.editing(false);
            Vfs.createFolder(newFolder).fail(function () {
                viewModel.selected().children.remove(newFolder);
            });
            $("#createFolder").prop("disabled", false);
        });
    });

    $("#createFile").click(function (evt) {
        evt.preventDefault();

        var newFile = new node({ name: "", type: "", href: "", editing: true }, viewModel.selected());
        $(this).prop("disabled", true);
        viewModel.selected().children.unshift(newFile);
        $("#fileList input[type='text']").focus();

        newFile.name.subscribe(function (value) {
            newFile.href = trimTrailingSlash(newFile.parent.href) + "/" + value;
            newFile._href(newFile.href);
            newFile.editing(false);
            Vfs.createFile(newFile).fail(function () {
                viewModel.selected().children.remove(newFile);
            });
            $("#createFile").prop("disabled", false);
        });
    });

    // Drag and drop
    $("#fileList")
        .on("dragenter dragover", function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (_isZipFile(e)) {
                $(".show-on-hover").addClass('upload-unzip-show');
            }
        })
        .on("drop", function (evt) {
            evt.preventDefault();
            evt.stopPropagation();

            $(".show-on-hover").removeClass('upload-unzip-show');
            $(".show-on-hover").removeClass('upload-unzip-hover');
            $("#copy-percentage").text("");
            var dir = viewModel.selected();
            viewModel.processing(true);
            _getInputFiles(evt).done(function (files) {
                Vfs.addFiles(files).always(function () {
                    dir.fetchChildren( /* force */ true);
                    viewModel.processing(false);
                    $("#copy-percentage").text("");
                });
            });
        }).on("dragleave", function (e) {
            $(".show-on-hover").removeClass('upload-unzip-show');
        });

    $("#upload-unzip")
        .on("dragenter dragover", function (e) {
            $(".show-on-hover").addClass('upload-unzip-hover');
        })
        .on("drop", function (evt) {
            evt.preventDefault();
            evt.stopPropagation();

            $(".show-on-hover").removeClass('upload-unzip-show');
            $(".show-on-hover").removeClass('upload-unzip-hover');
            var dir = viewModel.selected();
            viewModel.processing(true);
            _getInputFiles(evt).done(function (files) {
                Vfs.addFiles(files, _isZipFile(evt)).always(function () {
                    dir.fetchChildren( /* force */ true);
                    viewModel.processing(false);
                });
            });
        }).on("dragleave", function (e) {
            $(".show-on-hover").removeClass('upload-unzip-hover');
        });

    var defaults = { fileList: '40%', console: '45%' };
    $('#resizeHandle .down')
        .on('click', function (e) {
            var fileList = $('#fileList'),
                console = window.$KuduExecConsole;
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
                console = window.$KuduExecConsole;
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
            items = evt.originalEvent.dataTransfer.items,
            isSupportedGetAsEntry = typeof DataTransferItem !== "undefined" && !!(DataTransferItem.prototype.webkitGetAsEntry || DataTransferItem.prototype.getAsEntry);

        if (items && items.length && isSupportedGetAsEntry) {
            return whenArray($.map(items, function (item) {
                if (item.kind === 'file') {
                    var entry = (item.webkitGetAsEntry || item.getAsEntry).apply(item);
                    return _processEntry(entry);
                }
            })).pipe(function () {
                return Array.prototype.concat.apply([], arguments);
            })
        } else {
            return $.Deferred().resolveWith(null, [$.map(dt.files, function (e) {
                return { name: e.name, contents: e };
            })]);
        }
    }

    function _isZipFile(evt) {
        if (evt.originalEvent.dataTransfer === null) {
            //dataTransfer is null in Edge / IE. Assume a zip file. Unzip will no-op 
            return true;
        }
        var items = evt.originalEvent.dataTransfer.items || evt.originalEvent.dataTransfer.files;
        if (items) {
            var filesArray = $.map(items, function (item) {
                if (item.type === 'application/x-zip-compressed' || item.type === 'application/zip' || item.type === '')
                    return item;
            });
            if (filesArray && filesArray.length === items.length) {
                return true;
            } else {
                return false;
            }
        } else {
            //if both items and files are undefined, that means the browser (IE, FF)
            //doesn't support showing files on dragging, only on dropping, then assume a zip file.
            //Extracting will no-op if it's not a zip file anyway.
            return true;
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

    function showError(error) {
        if (error.status === 403) {
            $('#403-error-modal').modal();
        }
        viewModel.processing(false);
        // Should we also display a '403 Forbidden' toast? It's probably too much.
        // showErrorAsToast(error);
    }

    function showErrorAsToast(error) {
        viewModel.processing(false);
        // Check if 'error' has a status property.
        // If true, treat as xhr response, otherwise string.
        if (error.status) {
            try {
                var message = JSON.parse(error.responseText).Message;
            }
            catch (e) {
                // error.responseText may be poisoned with HTML
                // (i.e. session expires and the 403 Forbidden response from App Service contains tons of markup)
                // Let's just ignore it if that's the case. We would need Cortana or something to parse that and
                // extract a meaningful message.
                if (!(/\<html\>/i.test(error.responseText))) {
                    var message = error.responseText;
                }
            }
            var status = error.status;
            var statusText = error.statusText;
            var textToRender = status + ' ' + statusText + (typeof message !== 'undefined' ? ': ' + message : '');
            toast(textToRender);
        }
            // 'error' is a string
        else toast(error);
    }

});

// Toast notifications for backend errors
function toast(errorMsg) {
    var scaffold = '\
        <div class="row row-eq-height error notification">\
            <div id="toast-close" class="col-md-1">\
                <i class="glyphicon glyphicon-remove" aria-hidden="true"></i>\
            </div>\
            <div id="toast-msg" class="col-md-10">\
                <p><strong>ERROR</strong></p>' +
                    errorMsg +
            '</div>\
        </div>';
    var item = $(scaffold);
    $('#toast').append($(item));
    $(item).animate({ 'right': '12px' }, 'fast');
    $('#toast').on('click', '#toast-close', function () {
        var notification = $(this).parent();
        notification.animate({ 'right': '-400px' }, function () {
            notification.remove();
        });
    });
}

function removeAllToasts() {
    $('.notification').remove();
}