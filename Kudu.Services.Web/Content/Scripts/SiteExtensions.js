(function () {
    function adjustTileRaito() {
        var tiles = $("#tile1, #tile2, #tile3");
        tiles.height(tiles.width() * 1.6);
    }

    adjustTileRaito();

    $(window).resize(adjustTileRaito);

    $('a[data-toggle="tab"]').on("shown.bs.tab", function (event) {
        window.location.hash = $(event.target).attr("href").substr(1);
        $("#successNotification").slideUp();
        $("#errorNotification").slideUp();
    });

    $('#tabHeadings a[href="' + window.location.hash + '"]').tab('show');

    $("#successNotification").hide();
    $("#errorNotification").hide();

    $("#successNotificationClose").click(function () {
        $("#successNotification").slideUp();
    });
    $("#errorNotificationClose").click(function () {
        $("#errorNotification").slideUp();
    });

    function displaySuccess(message) {
        $("#successNotificationText").html(message);
        $("#successNotification").slideDown();
    }

    function displayError(message) {
        $("#errorNotificationText").html(message);
        $("#errorNotification").slideDown();
    }

    var activitySpin = "<i class=\"fa fa-spinner fa-spin\"></i>";
    var searchText = "<span>Search</span>";
    var clearText = "<span>Clear</span>";
    var installText = "<span>Install</span>";
    var removeText = "<span>Remove</span>";
    var updateText = "<span>Update</span>";
    var restartText = "<span>Restart Site</span>";

    function buttonResponse(btn, action, text) {
        var width = $(btn).width();
        $(btn).width(width);
        $(btn).html(activitySpin);
        $(btn).prop("disabled", "disabled");
        action(function () {
            $(btn).html(text);
            $(btn).removeProp("disabled");
        });
    }

    $(document).on("click", "#searchButton", function () {
        buttonResponse(this, ko.contextFor(this).$root.populateActiveTab, searchText);
    });

    $(document).on("click", "#clearButton", function () {
        ko.contextFor(this).$root.searchTerms("");
        buttonResponse(this, ko.contextFor(this).$root.populateAllTabs, clearText);
    });

    $(document).on("click", ".installButton", function () {
        var context = ko.contextFor(this);
        var data = ko.dataFor(this);
        buttonResponse(this, function (completionCallback) {
            context.$root.install(data, completionCallback,
                function () {
                    displaySuccess("<strong>" + data.Title
                        + " </strong> is successfully installed. <strong>Restart Site </strong> to make it available.");
                });
        },
        installText);
    });

    $(document).on("click", ".removeButton", function () {
        var context = ko.contextFor(this);
        var data = ko.dataFor(this);
        buttonResponse(this, function (completionCallback) {
            context.$root.remove(data, completionCallback,
                function () {
                    displaySuccess("<strong>" + data.Title
                        + " </strong> is successfully removed.");
                });
        }, removeText);
    });

    $(document).on("click", ".updateButton", function () {
        var context = ko.contextFor(this);
        var data = ko.dataFor(this);
        buttonResponse(this, function (completionCallback) {
            context.$root.install(data, completionCallback,
                function () {
                    displaySuccess("<strong>" + data.Title
                        + " </strong> is successfully updated.");
                });
        }, updateText);
    });

    $(document).on("click", "#restartButton", function () {
        buttonResponse(this, function (completionCallback) {
            $.ajax({
                type: "DELETE",
                url: "/diagnostics/processes/0",
                error: function () {
                    // no op
                },
                complete: completionCallback
            });
        }, restartText);
    });

    function extensionsViewModel() {
        // Data
        var self = this;
        self.gallery = ko.observableArray([]);
        self.installed = ko.observableArray([]);
        self.updates = ko.observableArray([]);
        self.detailedSiteExtension = ko.observable();
        self.searchTerms = ko.observable("");
        self.activeTab = ko.observable("gallery");

        // Operations
        function processExtensions(data) {
            data.forEach(function (ext) {
                if (ext.IconUrl === null) {
                    ext.IconUrl = "../Content/Images/Windows Azure Web Site.png";
                }
                if (ext.DownloadCount < 0) {
                    ext.DownloadCount = null;
                }
                if (ext.Title === null) {
                    ext.Title = ext.Id;
                }
            });
            return data;
        }

        self.populateGallery = function (filter, completionCallback) {
            $.ajax({
                type: "GET",
                url: "/api/extensions/remote?" + $.param({ "filter": filter }),
                dataType: "json",
                success: function (data) {
                    self.gallery(processExtensions(data));
                },
                error: function (jqXhr, textStatus, errorThrown) {
                    displayError(textStatus + ": " + errorThrown);
                },
                complete: completionCallback
            });
        };

        self.populateInstalled = function (filter, completionCallback) {
            $.ajax({
                type: "GET",
                url: "/api/extensions/local?" + $.param({ "filter": filter }),
                dataType: "json",
                success: function (data) {
                    self.installed(processExtensions(data));
                    self.updates(data.filter(function (item) { return !item.IsLatestVersion; }));
                },
                error: function (jqXhr, textStatus, errorThrown) {
                    displayError(textStatus + ": " + errorThrown);
                },
                complete: completionCallback
            });
        };

        self.populateActiveTab = function (completionCallback) {
            if ($("#gallery").hasClass("active")) {
                self.populateGallery(self.searchTerms, completionCallback);
            } else {
                self.populateInstalled(self.searchTerms, completionCallback);
            }
        };

        self.populateAllTabs = function (completionCallback) {
            self.populateGallery(self.searchTerms, completionCallback);
            self.populateInstalled(self.searchTerms, completionCallback);
        };

        self.install = function (extension, completionCallback, successCallback) {
            $.ajax({
                type: "POST",
                url: "/api/extensions",
                contentType: "application/json",
                data: JSON.stringify(extension),
                success: successCallback,
                error: function (jqXhr, textStatus, errorThrown) {
                    displayError(textStatus + ": " + errorThrown);
                },
                complete: function () {
                    self.populateAllTabs(completionCallback);
                }
            });
        };

        self.remove = function (extension, completionCallback, successCallback) {
            $.ajax({
                type: "DELETE",
                url: "/api/extensions/local/" + extension.Id,
                success: successCallback,
                error: function (jqXhr, textStatus, errorThrown) {
                    displayError(textStatus + ": " + errorThrown);
                },
                complete: function () {
                    self.populateActiveTab(completionCallback);
                }
            });
        };

        self.details = function (extension) {
            self.detailedSiteExtension(extension);
        };

        // Initialization
        self.populateAllTabs();
    }

    ko.applyBindings(new extensionsViewModel());
})();