
FileSystem = function () {
    this.fileCache = {};
    this.directoryCache = {};
    // Add root directory
    this.directoryCache['/'] = new Directory('/', this);
    this.rootName = null;
    this.readonly = null;
    this._refreshing = false;
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
        this._refreshing = true;
        this.clear();

        var that = this;

        $.each(files, function (index, item) {
            that.addItem(item.Path);
        });

        this._refreshing = false;
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

            var directory = file.getDirectory();
            directory.addFile(file);
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

            var parentDirectory = directory.getParent();
            parentDirectory.addDirectory(directory);
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


// Files are mutable, we manipulate files through the file system
File = function (path, fileSystem) {
    this._initialize(path, fileSystem);
};

File.prototype = {
    _initialize: function (path, fileSystem) {
        var that = this;
        this.path = path;
        this.pathParts = this.path.split('/');
        this.name = null;
        this.directory = null;
        this.fileSystem = fileSystem;
        var buffer = null;
        var dirty = false;

        this.setBuffer = function (value) {
            buffer = value;
        };

        this.getBuffer = function () {
            return buffer;
        }

        this.setDirty = function (value) {
            dirty = value;

            $(that).trigger('file.dirty', [value]);
        }

        this.isDirty = function () {
            return dirty;
        }
    },
    isReadOnly: function () {
        return this.getDirectory().isReadOnly();
    },
    getRelativePath: function () {
        return this.path.substr(1);
    },
    getElementId: function () {
        return 'file-' + this.getRelativePath().replace(/\./g, '-').replace(/\//g, '-');
    },
    getPath: function () {
        return this.path;
    },
    getExtension: function () {
        var name = this.getName();
        return name.substr(name.lastIndexOf('.'));
    },
    getName: function () {
        if (!this.name) {
            this.name = this.pathParts[this.pathParts.length - 1];
        }
        return this.name;
    },
    getDirectory: function () {
        if (!this.directory) {
            if (this.pathParts.length == 2) {
                // Root
                this.directory = this.fileSystem.getRoot();
            }
            else {
                var dir = this.pathParts.slice(0, this.pathParts.length - 1);
                // Get the directory virtual path
                var path = this._appendTrailingSlash(dir.join('/'));
                // Check if the directory exists
                this.directory = this.fileSystem.getDirectory(path);
                // If it doesn't create it
                if (!this.directory) {
                    // Add this directory
                    this.fileSystem.addDirectory(path);
                    // Get a reference to the added directory
                    this.directory = this.fileSystem.getDirectory(path);
                }
            }
        }
        return this.directory;
    },
    equals: function (file) {
        return this.getPath() == file.getPath();
    },
    _appendTrailingSlash: function (path) {
        if (path.length > 0 && path.charAt(path.length - 1) == '/') {
            return path;
        }
        return path + '/';
    }
};


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
    getElementId: function () {
        return 'directory-' + this.getRelativePath().replace(/\./g, '-').replace(/\//g, '-');
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

        if (this.fileSystem._refreshing == false) {
            this.files.sort(this._comparer);
        }
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

        if (this.fileSystem._refreshing == false) {
            this.directories.sort(this._comparer);
        }
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

        if (this.fileSystem._refreshing === false) {
            $(this.fileSystem).trigger('fileSystem.removeDirectory', [directory]);
        }
    },
    removeFile: function (file) {
        this._removeElement(this.files, file);
        delete this._contents[file.getPath()];

        if (this.fileSystem._refreshing === false) {
            $(this.fileSystem).trigger('fileSystem.removeFile', [file]);
        }
    },
    addFile: function (file) {
        var path = file.getPath();
        if (!this._contents[path]) {
            // Show the files as sorted
            this.files.push(file);
            this._contents[path] = true;

            if (this.fileSystem._refreshing == false) {
                this.files.sort(this._comparer);
                var index = $.inArray(file, file.directory.files);
                $(this.fileSystem).trigger('fileSystem.addFile', [file, index]);
            }
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
            this._contents[path] = true;

            if (this.fileSystem._refreshing == false) {
                this.directories.sort(this._comparer);

                var index = $.inArray(directory, directory.parent.directories);
                $(this.fileSystem).trigger('fileSystem.addDirectory', [directory, index]);
            }            
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