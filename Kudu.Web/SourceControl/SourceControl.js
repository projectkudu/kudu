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

    function loadRepository(path, onComplete) {
        $('#back').hide();
        $('#show').hide();
        $('#working').hide();

        $('#changes').html('');
        $('#log').show();
        $('#show-working').show();

        window.loader.show('Updating repository...');

        scm.connect(path, function () {
            scm.getChanges(function (changeSets) {
                $('#changeset').tmpl(changeSets).appendTo($('#changes'));

                var id = scm.state.id;

                $('#changes').find('.update').click(function () {
                    var item = $.tmplItem(this);
                    scm.update(item.data.Id, function () {
                        $('#' + id).removeClass('active');
                        $('#' + item.data.Id).addClass('active');

                        id = item.data.Id;
                    });

                    return false;
                });

                $('#changes').find('.view').click(function () {
                    var item = $.tmplItem(this);
                    show(item.data.Id);
                });

            }).complete(function () {
                if (onComplete) {
                    onComplete();
                }
                window.loader.hide();
            });
        });
    }

    function show(id) {
        $('#log').hide();
        $('#working').hide();
        $('#show-working').hide();

        $('#show').html('');
        $('#show').show();
        $('#back').show();

        window.loader.show('Loading commit ' + id);
        scm.show(id, function (details) {
            $('#changeset-detail').tmpl(details).appendTo($('#show'));
        }).complete(function () {
            window.loader.hide();
        });
    }

    function viewWorking() {
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

        $(button).attr('disabled', 'disabled');
        scm.commit($('#commit-message').val(), function (changeSet) {
            if (changeSet) {
                alert('Successfully commited ' + changeSet.ShortId);
                $('#commit-message').val('');
            }
            else {
                alert('No pending changes');
            }
        }).complete(function () {
            $(button).removeAttr('disabled');
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