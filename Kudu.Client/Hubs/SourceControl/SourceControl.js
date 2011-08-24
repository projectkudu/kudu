/// <reference path="Scripts/jquery-1.6.1.js" />
/// <reference path="loader.js" />
/// <reference path="../Scripts/sammy-latest.min.js" />
/// <reference path="../Scripts/jquery.cookie.js" />

$(function () {
    var scm = $.connection.sourceControl;

    var infiniteScrollCheck = false;
    var changesXhr = null;
    var loadingRepository = false;
    var pageSize = 15;

    function getDiffClass(type) {
        if (type == 1) {
            return ' diff-add';
        }
        else if (type == 2) {
            return ' diff-remove';
        }
        return '';
    }

    function getDiffId(path) {
        return path.replace(/\/|\./g, "-")
    }

    function getFileClass(file) {
        if (file.Status == 1) {
            return 'icon-file-added';
        }
        else if (file.Status == 2) {
            return 'icon-file-deleted';
        }
        else if (file.Status == 3) {
            return 'icon-file-modified';
        }
        else if (file.Binary) {
            return 'icon-binary-file';
        }
        return 'icon-file';
    }

    function getLogClass(type) {
        switch (type) {
            case 0:
                return "icon-log-message";
            case 1:
                return "icon-log-warning";
            case 2:
                return "icon-log-error";
        }
    }

    window.getLogClass = getLogClass;
    window.getDiffClass = getDiffClass;
    window.getDiffId = getDiffId;
    window.getFileClass = getFileClass;

    function onError(e) {
        $('#error').html(e);
        $('#error').show();
    }

    function getChangeSets(index, onComplete) {
        var callback = function () {
            if (onComplete) {
                onComplete();
            }
        };

        if (changesXhr) {
            callback();
            return;
        }

        if (scm.full === true) {
            callback();
            return;
        }

        changesXhr = scm.getChanges(index, pageSize, function (changes) {
            $.each(changes, function () {
                // Add the cross cutting data
                this.branches = scm.branches[this.Id];
                this.deploymentInfo = scm.deployments[this.Id];
                this.showDeploy = !this.Active &&
                                   this.deploymentInfo &&
                                   this.deploymentInfo.Status == 'Success';

                this.showLoading = this.deploymentInfo &&
                                   this.deploymentInfo.Status !== 'Success' &&
                                   this.deploymentInfo.Status !== 'Failed';

                this.failed = this.deploymentInfo &&
                              this.deploymentInfo.Status === 'Failed';
            });

            $('#changes').append($('#changeset').render(changes));
            scm.index = index + changes.length;

            processChanges();

            $('.timeago').timeago();

            if (changes.length < pageSize) {
                scm.full = true;
            }

            callback();
            changesXhr = null;
        })
        .fail(onError)
        .always(function () {
            callback();
            changesXhr = null;
        });
    }

    function updateStatus(result) {
        if (scm.id === result.Id) {
            return;
        }

        var oldItem = $('#' + scm.id);
        var newItem = $('#' + result.Id);

        newItem.find('.loading').show();
        oldItem.find('.status').hide();

        // Update the deployment status
        var status = newItem.find('.deploy-status');
        status.html(result.Status);
        status.show();

        if (result.Status == 'Success') {
            newItem.find('.loading').hide();
            newItem.find('.deploy').hide();
            newItem.find('.status').show();

            oldItem.find('.deploy').show();

            scm.id = result.Id;
        }
        else if (result.Status == 'Failed') {
            oldItem.find('.loading').hide();
            oldItem.find('.deploy').show();
            oldItem.find('.status').show();

            newItem.find('.loading').hide();
        }
    }

    function processChanges() {
        var deploymentsOnly = $('#filter-changes').is(':checked');
        if (deploymentsOnly) {
            $('#changes').find('.not-deployed').hide();
        }
        else {
            $('#changes').find('.not-deployed').show();
        }
    }

    function initialize() {
        $('#filter-changes').click(function () {
            processChanges();
        });

        $('#diff').delegate('.revert', 'click', function () {
            var path = $(this).closest('.file').attr('data-path');
            if (confirm('Are you sure you want to revert "' + path + '" ?')) {
                scm.revert(path)
                   .done(viewWorking)
                   .fail(onError);
            }
        });

        $('#changes').delegate('.update', 'click', function () {
            var newId = $(this).attr('data-id');
            var branch = $(this).attr('data-branch');

            scm.deploy(branch || newId);

            return false;
        });
    }

    function loadRepository(onComplete) {
        loadingRepository = true;

        $('#show').hide();
        $('#working').hide();
        $('#deploy-log').hide();

        $('#changes').html('');
        $('#log').show();

        var token = window.loader.show('Loading commits...');
        scm.index = 0;
        scm.full = false;

        scm.getRepositoryInfo()
           .done(function (info) {
               scm.branches = info.Branches;
               scm.deployments = info.Deployments;

               getChangeSets(0, function () {
                   window.loader.hide(token);

                   loadingRepository = false;

                   if (onComplete) {
                       onComplete();
                   }

                   if (infiniteScrollCheck === false) {
                       getMoreChanges();
                       infiniteScrollCheck = true;
                   }
               });
           })
           .fail(function (e) {
               onError(e);
               window.loader.hide(token);
               loadingRepository = false;
           });
    }

    function show(id) {
        if (changesXhr) {
            changesXhr.abort();
        }

        $('#log').hide();
        $('#working').hide();
        $('#deploy-log').hide();

        $('#show').html('');
        $('#show').show();

        var token = window.loader.show('Loading commit ' + id);

        scm.show(id, function (details) {
            $('#show').append($('#changeset-detail').render(details));
            $('.timeago').timeago();

            window.loader.hide(token);
        })
        .fail(onError)
        .always(function () {
            window.loader.hide(token);
        });
    }

    function viewDeployLog(id) {
        if (changesXhr) {
            changesXhr.abort();
        }
        $('#log').hide();
        $('#show').hide();
        $('#diff').hide();
        $('#working').hide();

        var token = window.loader.show('Loading deployment log...');

        scm.getDeployLog(id, function (logs) {
            $('#deploy-log').html($('#logTemplate').render(logs));
            $('#deploy-log').show();
        })
        .fail(onError)
        .always(function () {
            window.loader.hide(token);
        });
    }

    function viewWorking() {
        if (changesXhr) {
            changesXhr.abort();
        }

        $('#log').hide();
        $('#show').hide();
        $('#deploy-log').hide();

        $('#diff').html('');
        $('#diff').show();
        $('#working').show();

        var token = window.loader.show('Loading working directory...');

        scm.getWorking(function (details) {
            if (details) {
                $('#diff').html($('#diff-view').render(details));
            }
            else {
                $('#diff').html('No changes');
            }
        })
        .fail(onError)
        .always(function () {
            window.loader.hide(token);
        });
    }

    function getMoreChanges() {
        var callback = function () {
            setTimeout(getMoreChanges, 500);
        };

        if (scm.full === true) {
            callback();
            return;
        }

        var threshold = 25;
        var min = $(document).scrollTop();
        var max = min + $(window).height();

        var e = $('#changes').find('tr:last');
        var pos = e.position();

        if (pos) {
            var top = pos.top - threshold;

            // Load more changes if we're in range
            if (top >= min && top <= max) {
                var token = window.infiniteLoader.show('Loading more commits...');

                getChangeSets(scm.index, function () {
                    window.infiniteLoader.hide(token);

                    callback();
                });
            }
            else {
                callback();
            }
        }
        else {
            callback();
        }
    }

    var app = $.sammy(function () {
        this.get('#/', function () {
            loadRepository();
            return false;
        });

        this.post('#/', function () {
            this.redirect('#/');

            return false;
        });

        this.get('#/commit/:id', function () {
            show(this.params.id);
        });

        this.get('#/view-log/:id', function () {
            viewDeployLog(this.params.id);
        });

        this.get('#/working', function () {
            viewWorking();
        });

        this.post('#/commit', function () {
            var context = this;

            var token = window.loader.show('Commiting changes...');

            scm.commit(this.params.message, function (changeSet) {
                if (changeSet) {
                    $('#new-commit').html('Successfully commited ' + changeSet.ShortId);
                    $('#new-commit').slideDown();
                    $('#commit-message').val('');

                    window.setTimeout(function () {
                        $('#new-commit').slideUp('slow', function () {
                            window.setTimeout(function () {
                                context.redirect('#/');
                            }, 300);
                        });
                    }, 1000);
                }
                else {
                    alert('No pending changes');
                }
            })
            .fail(onError)
            .always(function () {
                window.loader.hide(token);
            });

            return false;
        });
    });

    initialize();

    scm.updateDeployStatus = function (result) {
        // Don't force the view to change if it's not visible
        if (!$('#log').is(':visible')) {
            return;
        }

        if (loadingRepository === false) {
            if (!document.getElementById(result.Id)) {
                loadRepository();
            }
            else {
                updateStatus(result);
            }
        }
    };

    $.connection.hub.start(function () {
        app.run('#/');
    });
});