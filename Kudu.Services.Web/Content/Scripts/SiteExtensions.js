(function () {
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

    $('#navTabs a[href="#installed"]').click(function (e) {
        e.preventDefault();
        switchTab(e);
        var context = ko.contextFor(this);
        context.$root.display(context.$root.installed());
        $(this).tab('show');
    });

    $('#navTabs a[href="#gallery"]').click(function (e) {
        e.preventDefault();
        switchTab(e);
        var context = ko.contextFor(this);
        context.$root.display(context.$root.gallery());
        $(this).tab('show');
    });

    function switchTab(event) {
        $("#successNotification").slideUp();
        $("#errorNotification").slideUp();
        window.location.hash = $(event.target).attr("href").substr(1);
    };

    if (window.location.hash !== "#installed" && window.location.hash !== "#gallery") {
        window.location.hash = "#installed";
    }

    $('#tabHeadings a[href="' + window.location.hash + '"]').tab('show');

    function processExtensions(ext) {
        if (!ext.icon_url) {
            ext.icon_url = "../Content/Images/Windows Azure Web Site.png";
        }
        if (ext.download_count < 0) {
            ext.download_count = null;
        }
        if (!ext.title) {
            ext.title = ext.id;
        }
        if (ext.extension_url) {
            ext.primaryAction = ko.observable('Launch');
        } else {
            ext.primaryAction = ko.observable('Install');
        }
        return ext;
    }

    var activitySpin = "<i class=\"fa fa-spinner fa-spin\"></i>";
    var searchText = "<span>Search</span>";
    var clearText = "<span>Clear</span>";
    var removeText = '<i class="fa fa-times"></i>';
    var updateText = '<i class="fa fa-arrow-up"></i>';
    var restartText = "<span>Restart Site</span>";

    function buttonResponse(btn, action, text) {
        var width = $(btn).width();
        $(btn).width(width);
        $(btn).html(activitySpin);
        $(btn).attr("disabled", true);
        action(function () {
            $(btn).html(text);
            $(btn).removeAttr("disabled");
            $(btn).css("width", "");
        });
    }

    $(document).on("click", "#searchButton", function () {
        buttonResponse(this, ko.contextFor(this).$root.populateActiveTab, searchText);
    });

    $(document).on("click", "#clearButton", function () {
        ko.contextFor(this).$root.searchTerms("");
        buttonResponse(this, ko.contextFor(this).$root.populateAllTabs, clearText);
    });

    $(document).on("click", ".installDialog", function () {
        var btn = this;
        var context = ko.contextFor(btn);
        var data = ko.dataFor(btn);
        context.$root.clickedButton(btn);
        context.$root.detailedSiteExtension(data);
    });

    $(document).on("click", ".installButton", function () {
        var context = ko.contextFor(this);
        var data = context.$root.detailedSiteExtension();
        data.primaryAction('Wait');
        $.ajax({
            type: "PUT",
            url: "/api/siteextensions/" + data.id,
            success: function (result) {
                result = processExtensions(result);
                context.$root.addInstalled(result);
                $("#restartButton").attr("data-content", "<strong>" + result.title
                    + "</strong> is successfully installed. <strong>Restart Site </strong> to make it available.");
                $("#restartButton").popover('show');
                setTimeout(function () {
                    $("#restartButton").popover('hide');
                }, 5000);
            },
            error: function (jqXhr, textStatus, errorThrown) {
                displayError("Failed to install <strong>" + data.title + "</strong>: " + textStatus + " - " + errorThrown);
            },
            complete: function () {
                // no op
            }
        });
    });

    $(document).on("click", ".removeButton", function () {
        var context = ko.contextFor(this);
        var data = ko.dataFor(this);
        buttonResponse(this, function (completionCallback) {
            context.$root.remove(data, completionCallback,
                function () {
                });
        }, removeText);
    });

    $(document).on("click", ".updateButton", function () {
        var btn = this;
        var context = ko.contextFor(btn);
        var data = ko.dataFor(btn);
        $(btn).html(activitySpin);
        $(btn).prop("disabled", "disabled");
        $.ajax({
            type: "PUT",
            url: "/api/siteextensions/" + data.id,
            success: function (result) {
            },
            error: function (jqXhr, textStatus, errorThrown) {
                displayError("Failed to update <strong>" + result.title + "</strong>: " + textStatus + " - " + errorThrown);
            },
            complete: function () {
                context.$root.populateAllTabs();
            }
        });
    });

    $(document).on("click", "#restartButton", function () {
        buttonResponse(this, function (completionCallback) {
            $.ajax({
                type: "DELETE",
                url: "/diagnostics/processes/0",
                error: function () {
                    // no op
                },
                complete: function() {
                    setTimeout(completionCallback, 5000);
                }
            });
        }, restartText);
    });

    var extensionsViewModel = function () {
        // Data
        var self = this;
        self.loadingGallery = ko.observable(true);
        self.loadingInstalled = ko.observable(true);
        self.detailedSiteExtension = ko.observable();
        self.clickedButton = ko.observable();
        self.searchTerms = ko.observable("");
        self.installed = ko.observableArray();
        self.gallery = ko.observableArray();
        self.display = ko.observableArray();

        // Operations

        self.populateGallery = function (filter, completionCallback) {
            self.loadingGallery(true);
            $.ajax({
                type: "GET",
                url: "/api/extensionfeed?" + $.param({ "filter": filter }),
                dataType: "json",
                success: function (data) {
                    data.forEach(processExtensions);
                    self.gallery(data);
                    if ($("#gallery").hasClass("active")) {
                        self.display(data);
                    }
                },
                error: function (jqXhr, textStatus, errorThrown) {
                    displayError(textStatus + ": " + errorThrown);
                },
                complete: function () {
                    self.loadingGallery(false);
                    if (typeof (completionCallback) === "function") {
                        completionCallback();
                    }
                }
            });
        };

        self.populateInstalled = function (filter, completionCallback) {
            self.loadingInstalled(true);
            $.ajax({
                type: "GET",
                url: "/api/siteextensions?" + $.param({ "filter": filter }),
                dataType: "json",
                success: function (data) {
                    data.forEach(processExtensions);
                    self.installed(data);
                    if ($("#installed").hasClass("active")) {
                        self.display(data);
                    }
                },
                error: function (jqXhr, textStatus, errorThrown) {
                    displayError(textStatus + ": " + errorThrown);
                },
                complete: function () {
                    self.loadingInstalled(false);
                    if (typeof (completionCallback) === "function") {
                        completionCallback();
                    }
                }
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

        self.remove = function (extension, completionCallback, successCallback) {
            $.ajax({
                type: "DELETE",
                url: "/api/siteextensions/" + extension.id,
                success: successCallback,
                error: function (jqXhr, textStatus, errorThrown) {
                    displayError(textStatus + ": " + errorThrown);
                },
                complete: function () {
                    self.populateAllTabs(completionCallback);
                }
            });
        };

        self.details = function (extension) {
            self.detailedSiteExtension(extension);
        };

        self.addInstalled = function (newExt) {
            var index = -1;
            self.installed().forEach(function(installedExt, i) {
                if (newExt.id === installedExt.id) {
                    index = i;
                    return true;
                }
                return false;
            });
            if (index === -1) {
                self.installed().push(newExt);
            } else {
                self.installed()[index] = newExt;
            }

            index = -1;
            self.gallery().forEach(function (galleryExt, i) {
                if (newExt.id === galleryExt.id) {
                    index = i;
                    return true;
                }
                return false;
            });
            if (index !== -1) {
                self.gallery()[index] = newExt;
            }

            if (window.location.hash === "#gallery") {
                self.display(self.gallery());
            } else {
                self.display(self.installed());
            }
        };

        // Initialization
        self.populateAllTabs();
    };

    ko.applyBindings(new extensionsViewModel());
})();