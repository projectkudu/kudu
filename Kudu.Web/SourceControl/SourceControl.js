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

    function getBranches(id) {
        if (scm.state.branches && scm.state.branches[id]) {
            return scm.state.branches[id];
        }
        return [];
    }

    window.getDiffClass = getDiffClass;
    window.getDiffId = getDiffId;
    window.getFileClass = getFileClass;
    window.getBranches = getBranches;

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
            setupActions($('#changes').append($('#changeset').render(changes)));
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

    function setupActions(element) {
        var id = scm.state.id;

        element.find('.update').click(function () {
            var newId = $(this).attr('data-id');
            var branch = $(this).attr('data-branch');

            $('#' + newId).find('.loading').show();
            $('#' + id).find('.status').addClass('hide');

            scm.update(branch || newId)
               .done(function () {
                   scm.state.id = newId;
                   scm.state.branch = branch;

                   $('#' + newId).find('.loading').hide();
                   $('#' + newId).find('.status').removeClass('hide');

                   id = newId;
               })
               .fail(onError);

            return false;
        });
    }

    function loadRepository() {
        $('#show').hide();
        $('#working').hide();

        $('#changes').html('');
        $('#log').show();

        var token = window.loader.show('Loading commits...');
        scm.state.index = 0;
        scm.state.full = false;

        scm.getBranches(function (branches) {
            scm.state.branches = branches;

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

    function viewWorking() {
        if (changesXhr) {
            changesXhr.abort();
        }

        $('#log').hide();
        $('#show').hide();

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

    signalR.hub.start(function () {
        app.run('#/');
    });
});