/// <reference path="Scripts/jquery-1.6.1.js" />
/// <reference path="loader.js" />
/// <reference path="../Scripts/sammy-latest.min.js" />
/// <reference path="../Scripts/jquery.cookie.js" />

$(function () {
    var scm = signalR.SourceControl;
    var path = $.cookie("path");
    scm.state.path = path;
    $('#path').val(path || '');

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

    var changesXhr = null;
    var pageSize = 25;

    function getChangeSets(index, onComplete) {
        if (changesXhr) {
            changesXhr.abort();
        }

        changesXhr = scm.getChanges(index, pageSize, function (changes) {
            $.cookie("path", scm.state.path);

            setupActions($('#changeset').tmpl(changes).appendTo($('#changes')));
            $('.timeago').timeago();

            if (changes.length < pageSize) {
                if (onComplete) {
                    onComplete();
                }
            }
            else {
                var next = index + changes.length;
                getChangeSets(next, onComplete);
            }
        })
        .error(function () {
            if (onComplete) {
                onComplete();
            }
        });
    }

    function setupActions(element) {
        var id = scm.state.id;

        element.find('.update').click(function () {
            var item = $.tmplItem(this);
            var newId = item.data.Id;

            $('#' + newId).find('.loading').show();

            scm.update(newId, function () {
                $('#' + newId).find('.loading').hide();
                $('#' + id).find('.status').addClass('hide');
                $('#' + newId).find('.status').removeClass('hide');

                id = newId;
            });

            return false;
        });
    }

    function loadRepository(path) {
        $('#show').hide();
        $('#working').hide();

        $('#changes').html('');
        $('#log').show();

        var token = window.loader.show('Loading commits...');

        scm.getBranches(function (branches) {
            scm.state.branches = branches;

            getChangeSets(0, function () {
                window.loader.hide(token);
            });
        })
        .error(function () {
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
            $('#changeset-detail').tmpl(details).appendTo($('#show'));
            $('.timeago').timeago();

            window.loader.hide(token);
        })
        .error(function () {
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
                $('#diff-view').tmpl(details).appendTo($('#diff'));
            }
            else {
                $('#diff').html('No changes');
            }
        })
        .complete(function () {
            window.loader.hide(token);
        });
    }

    var app = $.sammy(function () {
        this.get('#/', function () {
            var path = scm.state.path;
            if (path) {
                loadRepository(path);
            }
            return false;
        });

        this.post('#/', function () {
            scm.state.path = this.params.path;
            this.redirect('#/');

            return false;
        });

        this.get('#/view/:id', function () {
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
            .complete(function () {
                window.loader.hide(token);
            });

            return false;
        });
    });

    app.run('#/');
});