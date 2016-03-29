
$(function() {
    var clipboard = new Clipboard('.btn-clipboard');

    clipboard.on('success', function (e) {
        e.clearSelection();

        $(e.trigger).tooltip({ title: 'Copied!' }).tooltip('show');
    });

    clipboard.on('error', function (e) {
        console.error('Action:', e.action);
        console.error('Trigger:', e.trigger);

        $(e.trigger).tooltip({ title: fallbackMessage(e.action) }).tooltip('show');
    });

    $('#clone-url').click(function () {
        this.select();
    });
});

function fallbackMessage(action) {
    var actionMsg = '';
    var actionKey = (action === 'cut' ? 'X' : 'C');

    if (/Mac/i.test(navigator.userAgent)) {
        actionMsg = 'Press ⌘-' + actionKey + ' to ' + action;
    }
    else {
        actionMsg = 'Press Ctrl-' + actionKey + ' to ' + action;
    }

    return actionMsg;
}
