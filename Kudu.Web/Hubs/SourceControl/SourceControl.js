/// <reference path="Scripts/jquery-1.6.1.js" />
/// <reference path="loader.js" />
/// <reference path="../Scripts/sammy-latest.min.js" />
/// <reference path="../Scripts/jquery.cookie.js" />

$(function () {
    var scm = signalR.sourceControl;

    var infiniteScrollCheck = false;
    var changesXhr = null;
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

    function getDeploymentStatus(deploymentInfo) {
        if (deploymentInfo.Status == 0) {
            return "Pending";
        }
        if (deploymentInfo.Status == 1) {
            return "Failed";
        }
        return "Success";
    }

    function getLogClass(type) {
        switch (type) {
            case 0:
                return "icon-message";
            case 1:
                return "icon-warning";
            case 2:
                return "icon-error";
        }
    }

    window.getLogClass = getLogClass;
    window.getDiffClass = getDiffClass;
    window.getDiffId = getDiffId;
    window.getFileClass = getFileClass;
    window.getDeploymentStatus = getDeploymentStatus;

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

        if (scm.state.full === true) {
            callback();
            return;
        }

        changesXhr = scm.getChanges(index, pageSize, function (changes) {
            $.each(changes, function () {
                // Add the cross cutting data
                this.branches = scm.state.branches[this.Id];
                this.deploymentInfo = scm.state.deployments[this.Id];
            });

            $('#changes').append($('#changeset').render(changes));
            scm.state.index = index + changes.length;

            $('.timeago').timeago();

            if (changes.length < pageSize) {
                scm.state.full = true;
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

    function initialize() {
        $('#diff').delegate('.revert', 'click', function () {
            var path = $(this).closest('.file').attr('data-path');
            if (confirm('Are you sure you want to revert "' + path + '" ?')) {
                scm.revert(path)
                   .done(viewWorking)
                   .fail(onError);
            }
        });

        $('#changes').delegate('.update', 'click', function () {
            var id = scm.state.id;

            var newId = $(this).attr('data-id');
            var branch = $(this).attr('data-branch');

            $('#' + newId).find('.loading').show();
            $('#' + id).find('.status').addClass('hide');

            scm.deploy(branch || newId)
               .done(function () {
                   scm.state.id = newId;
                   scm.state.branch = branch;

                   $('#' + newId).find('.loading').hide();
                   $('#' + newId).find('.status').removeClass('hide');

                   id = newId;
               })
               .fail(function (e) {
                   $('#' + id).find('.loading').hide();
                   $('#' + id).find('.status').removeClass('hide');
                   $('#' + newId).find('.loading').hide();
                   onError(e);
               });

            return false;
        });
    }

    function loadRepository() {
        $('#show').hide();
        $('#working').hide();
        $('#deploy-log').hide();

        $('#changes').html('');
        $('#log').show();

        var token = window.loader.show('Loading commits...');
        scm.state.index = 0;
        scm.state.full = false;

        scm.getRepositoryInfo()
           .done(function (info) {
               scm.state.branches = info.Branches;
               scm.state.deployments = info.Deployments;

               getChangeSets(0, function () {
                   window.loader.hide(token);

                   if (infiniteScrollCheck === false) {
                       getMoreChanges();
                       infiniteScrollCheck = true;
                   }
               });
           })
           .fail(function (e) {
               onError(e);
               window.loader.hide(token);
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

        if (scm.state.full === true) {
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

                getChangeSets(scm.state.index, function () {
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

    signalR.hub.start(function () {
        app.run('#/');
    });
});