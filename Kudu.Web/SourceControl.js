$(function () {
    var scm = signalR.SourceControl;

    scm.getChanges(function (changeSets) {
        $('#changeset').tmpl(changeSets).appendTo($('#changes'));

        var id = signalR.SourceControl.state.id;


        $('#changes').find('.update').click(function () {
            var item = $.tmplItem(this);
            scm.update(item.data.Id, function () {
                $('#' + id).removeClass('active');
                $('#' + item.data.Id).addClass('active');

                id = item.data.Id;
            });

            return false;
        });
    });

    scm.getStatus(function (status) {

    });

});