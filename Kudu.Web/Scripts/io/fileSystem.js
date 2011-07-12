// Global File system is a hashtable from virtual path to file
// REVIEW: Should we should generalize that directories are actually files?
/// <reference path="../jquery-1.5.2.js" />

FileSystem = function () {
    this.fileCache = {};
    this.directoryCache = {};
    // Add root directory
    this.directoryCache['/'] = new Directory('/', this);
    this.rootName = null;
    this.readonly = null;
};

FileSystem.prototype = {
    setReadOnly: function (readonly) {
        this.readonly = readonly;
    },
    isReadOnly: function () {
        return this.readonly;
    },
    getRoot: function () {
        return this.getDirectory('/');
    },
    getRootName: function () {
        return this.rootName;
    },
    setRootName: function (name) {
        this.rootName = name;
    },
    create: function (files) {
        this.clear();

        var that = this;

        $.each(files, function (index, item) {
            that.addItem(item.Path);
        });
    },
    _prependSlash: function (path) {
        // prepend / if its not already there
        if (path.length > 0 && path.charAt(0) != '/') {
            return "/" + path;
        }
        return path;
    },
    _isDirectory: function (path) {
        return path.charAt(path.length - 1) == '/';
    },
    _ensureParents: function (directory) {
        // Force creation of parent directories
        var current = directory;
        var root = this.getRoot();
        while (!current.equals(root)) {
            current = current.getParent();
        }
    },
    addItem: function (path) {
        // prepend / if its not already there
        path = this._prependSlash(path);

        if (path == '/') {
            return;
        }

        // If it looks like a directory i.e. ends with / then make it so
        if (this._isDirectory(path)) {
            this.addDirectory(path);
        }
        else {
            // Otherwise it's a file
            this.addFile(path);
        }
    },
    addFile: function (path) {
        path = this._prependSlash(path);

        if (!this.getFile(path)) {
            var file = new File(path, this);
            this.fileCache[path] = file;
            this._ensureParents(file.getDirectory());

            file.getDirectory().addFile(file);
        }
    },
    getFile: function (path) {
        path = this._prependSlash(path);
        return this.fileCache[path];
    },
    removeFile: function (path) {
        path = this._prependSlash(path);
        var file = this.getFile(path);
        if (file) {
            file.getDirectory().removeFile(file);
            delete this.fileCache[path];
        }
    },
    renameFile: function (oldPath, newPath) {
        this.removeFile(oldPath);
        this.addFile(newPath);
    },
    getFiles: function () {
        var files = [];
        for (var path in this.fileCache) {
            files.push(this.fileCache[path]);
        }
        return files;
    },
    getDirectories: function () {
        var directories = [];
        for (var path in this.directoryCache) {
            directories.push(this.directoryCache[path]);
        }
        return directories;
    },
    directoryExists: function (path) {
        path = this._prependSlash(path);
        return this.getgetDirectory(path) != null;
    },
    fileExists: function (path) {
        path = this._prependSlash(path);
        return this.getFile(path) != null;
    },
    getDirectory: function (path) {
        return this.directoryCache[path];
    },
    addDirectory: function (path) {
        path = this._prependSlash(path);
        if (!this.getDirectory(path)) {
            var directory = new Directory(path, this);
            this.directoryCache[path] = directory;
            this._ensureParents(directory);

            directory.getParent().addDirectory(directory);
        }
    },
    removeDirectory: function (path) {
        path = this._prependSlash(path);
        var directory = this.getDirectory(path);
        if (directory) {
            directory.getParent().removeDirectory(directory);
            delete this.fileCache[path];
        }
    },
    clear: function () {
        this.fileCache = {};
        this.directoryCache = {};
        this.directoryCache['/'] = new Directory('/', this);
        // REVIEW: We need bulk commands for remove and add so this is more efficient
    }
};