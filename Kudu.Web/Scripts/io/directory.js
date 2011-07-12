/// <reference path="../jquery-1.5.2.js" />
// Directory
Directory = function (path, fileSystem) {
    this._initialize(path, fileSystem);
};

Directory.prototype = {
    _initialize: function (path, fileSystem) {
        // directory virtual paths end with /
        this.path = path;
        this.pathParts = this.path.split('/');
        // remove empty space
        this.pathParts.pop();
        this.name = null;
        this.files = [];
        this.directories = [];
        this.parent = null;
        this.fileSystem = fileSystem;
        this._contents = {};
    },
    getPath: function () {
        return this.path;
    },
    getRelativePath: function () {
        return this.path.substr(1);
    },
    getName: function () {
        if (!this.name) {
            if (this._isRoot()) {
                this.name = this.fileSystem.getRootName();
            }
            else {
                this.name = this.pathParts[this.pathParts.length - 1];
            }
        }
        return this.name;
    },
    isReadOnly: function () {
        return this.fileSystem.isReadOnly();
    },
    isEmpty: function () {
        return this.getFiles().length == 0 && this.getDirectories().length == 0;
    },
    _ensureFiles: function () {
        var globalFiles = this.fileSystem.getFiles();
        for (var i = 0; i < globalFiles.length; ++i) {
            var file = globalFiles[i];
            if (file.getDirectory().equals(this)) {
                if (!this._contents[file.getPath()]) {
                    this.files.push(file);
                }
                this._contents[file.getPath()] = true;
            }
        }
        this.files.sort(this._comparer);
    },
    _ensureDirectories: function () {
        var directoryMap = {};
        // Process files
        var globalFiles = this.fileSystem.getFiles();
        for (var i = 0; i < globalFiles.length; ++i) {
            var file = globalFiles[i];
            this._processDirectory(file.getDirectory(), directoryMap);
        }

        // Process directories
        var globalDirs = this.fileSystem.getDirectories();
        for (var i = 0; i < globalDirs.length; ++i) {
            this._processDirectory(globalDirs[i], directoryMap);
        }

        for (var path in directoryMap) {
            this.directories.push(directoryMap[path]);
        }
        this.directories.sort(this._comparer);
    },
    _processDirectory: function (directory, directoryMap) {
        if (directory.equals(this) || directoryMap[directory.getPath()]) {
            return;
        }
        // REVIEW: Walking up the tree may be slow
        if (directory.isDescendentOf(this)) {
            while (!directory.getParent().equals(this)) {
                directory = directory.getParent();
            }

            if (!this._contents[directory.getPath()]) {
                directoryMap[directory.getPath()] = directory;
            }

            // we don't care about the object we're just using it as a dumb hash table
            this._contents[directory.getPath()] = true;
        }
    },
    _isRoot: function () {
        return this.equals(this.fileSystem.getRoot());
    },
    getFiles: function () {
        this._ensureFiles();
        return this.files;
    },
    getDirectories: function () {
        this._ensureDirectories();
        return this.directories;
    },
    removeDirectory: function (directory) {
        this._removeElement(this.directories, directory);
        delete this._contents[directory.getPath()];
    },
    removeFile: function (file) {
        this._removeElement(this.files, file);
        delete this._contents[file.getPath()];
    },
    addFile: function (file) {
        var path = file.getPath();
        if (!this._contents[path]) {
            // Show the files as sorted
            this.files.push(file);
            this.files.sort(this._comparer);
            this._contents[path] = true;
        }
    },
    _comparer: function (aItem, bItem) {
        var aPath = aItem.getPath();
        var bPath = bItem.getPath();
        if (aPath == bPath) {
            return 0;
        }
        else if (aPath > bPath) {
            return 1;
        }
        return -1;
    },
    addDirectory: function (directory) {
        var path = directory.getPath();
        if (!this._contents[path]) {
            this.directories.push(directory);
            this.directories.sort(this._comparer);
            this._contents[path] = true;
        }

    },
    getParent: function () {
        if (this._isRoot()) {
            this.parent = this.fileSystem.getRoot();
        }
        if (!this.parent) {
            // REVIEW: just use substring?
            var parentDir = this.pathParts.slice(0, this.pathParts.length - 1);
            // Parent folder virtual path
            var path = parentDir.join('/') + '/';
            this.parent = this._ensureDirectory(path);
        }
        return this.parent;
    },
    _ensureDirectory: function (path) {
        var dir = this.fileSystem.getDirectory(path);
        if (!dir) {
            this.fileSystem.addDirectory(path);
            dir = this.fileSystem.getDirectory(path);
        }
        return dir;
    },
    isDescendentOf: function (directory) {
        var path = this.getPath();
        var otherPath = directory.getPath ? directory.getPath() : directory;

        if (path.length < otherPath.length) {
            return false;
        }
        return path.substr(0, otherPath.length) == otherPath;
    },
    equals: function (directory) {
        return this.getPath() == directory.getPath();
    },
    _removeElement: function (array, element) {
        var index = $.inArray(element, array);
        if (index >= 0) {
            array.splice(index, 1);
        }
    },
    _onClear: function (sender, args) {
        this.files = [];
        this.directories = [];
        this._contents = {};

    }
};