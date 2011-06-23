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
        $('#changes').html('');

        $('#show').hide();
        $('#log').show();
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
                onComplete();
                window.loader.hide();
            });
        });
    }

    function show(id) {
        $('#show').html('');
        $('#log').hide();
        $('#show').show();
        window.loader.show('Loading commit ' + id);
        scm.show(id, function (details) {
            $('#changeset-detail').tmpl(details).appendTo($('#show'));
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

});