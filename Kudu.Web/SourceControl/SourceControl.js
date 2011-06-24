/// <reference path="Scripts/jquery-1.6.1.js" />
/// <reference path="loader.js" />

$(function () {
    var scm = signalR.SourceControl;

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

    window.getDiffClass = getDiffClass;
    window.getDiffId = getDiffId;

    var changesXhr = null;

    function getChangeSets(index, onComplete) {
        if (changesXhr) {
            changesXhr.abort();
        }

        changesXhr = scm.getChanges(index, function (result) {
            setupActions($('#changeset').tmpl(result.Items).appendTo($('#changes')));
            $('.timeago').timeago();

            if (result.More === true) {
                getChangeSets(result.Current, onComplete);
            }
            else {
                if (onComplete) {
                    onComplete();
                }
            }
        }).error(function () {
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

        element.find('.view').click(function () {
            var item = $.tmplItem(this);
            show(item.data.Id);
        });
    }

    function loadRepository(path, onComplete) {
        $('#back').hide();
        $('#show').hide();
        $('#working').hide();

        $('#changes').html('');
        $('#log').show();
        $('#show-working').show();

        window.loader.show('Updating repository...');

        var callback = function () {
            if (onComplete) {
                onComplete();
            }
            window.loader.hide();
        };

        if (scm.state.repository !== path) {
            scm.connect(path, function () {
                getChangeSets(0, callback);
            });
        }
        else {
            getChangeSets(0, callback);
        }
    }

    function show(id) {
        if (changesXhr) {
            changesXhr.abort();
        }

        $('#log').hide();
        $('#working').hide();
        $('#show-working').hide();

        $('#show').html('');
        $('#show').show();
        $('#back').show();

        window.loader.show('Loading commit ' + id);
        scm.show(id, function (details) {
            $('#changeset-detail').tmpl(details).appendTo($('#show'));
            $('.timeago').timeago();
        }).complete(function () {
            window.loader.hide();
        });
    }

    function viewWorking() {
        if (changesXhr) {
            changesXhr.abort();
        }

        $('#log').hide();
        $('#show').hide();

        $('#diff').html('');
        $('#working').show();
        $('#back').show();
        window.loader.show('Loading working directory');

        scm.getWorking(function (details) {
            if (details) {
                $('#diff-view').tmpl(details).appendTo($('#diff'));
            }
            else {
                $('#diff').html('No changes');
            }
            $('#diff').show();
        }).complete(function () {
            window.loader.hide();
        });
    }

    $('#connect').submit(function () {
        var button = $('#update');

        $(button).attr('disabled', 'disabled');
        loadRepository($('#path').val(), function () {
            $(button).removeAttr('disabled');
        });

        return false;
    });

    $('#commit').submit(function () {
        var button = $('#perform-commit');

        window.loader.show('Commiting changes');

        $(button).attr('disabled', 'disabled');
        scm.commit($('#commit-message').val(), function (changeSet) {
            if (changeSet) {
                $('#new-commit').html('Successfully commited ' + changeSet.ShortId);
                $('#new-commit').show();

                $('#diff').fadeOut('slow', function () {
                    $('#diff').html('');
                });

                $('#commit-message').val('');

                window.setTimeout(function () {
                    $('#new-commit').fadeOut('slow');
                }, 1000);
            }
            else {
                alert('No pending changes');
            }
        }).complete(function () {
            $(button).removeAttr('disabled');
            window.loader.hide();
        });

        return false;
    });

    $('#back').click(function () {
        $('#show').hide();
        $('#working').hide();
        $(this).hide();

        loadRepository(scm.state.repository);
        return false;
    });

    $('#show-working').click(function () {
        $(this).hide();

        viewWorking();
        return false;
    });

});