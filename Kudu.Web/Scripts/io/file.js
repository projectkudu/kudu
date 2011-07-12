// Files are mutable, we manipulate files through the file system
File = function (path, fileSystem) {
    this._initialize(path, fileSystem);
};

File.prototype = {
    _initialize: function (path, fileSystem) {
        this.path = path;
        this.pathParts = this.path.split('/');
        this.name = null;
        this.directory = null;
        this.fileSystem = fileSystem;
        var buffer = "";
        var dirty = false;

        this.setBuffer = function (value) {
            buffer = value;
        };

        this.getBuffer = function () {
            return buffer;
        }

        this.setDirty = function (value) {
            dirty = value;
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
        return this.getRelativePath().replace(/\./g, '-').replace(/\//g, '-');
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