/// <reference path="Scripts/jquery-1.6.1.js" />
/// <reference path="loader.js" />

$(function () {
    var scm = signalR.SourceControl;

    function loadRepository(path, onComplete) {
        $('#hide').hide();
        $('#log').show();
        window.loader.show('Updating repository...');

        scm.connect(path, function () {
            scm.getChanges(function (changeSets) {
                $('#changes').html('');

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
        $('#log').hide();
        $('#diff').show();

        scm.show(id, function () {
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