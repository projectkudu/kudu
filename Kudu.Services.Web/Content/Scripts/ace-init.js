// Resize editor window based on browser window.innerHeight
function resizeAce() {
    // http://stackoverflow.com/questions/11584061/
    var new_height = (window.innerHeight - 170) + 'px';
    $('#editor').css({'height': new_height});
    editor.resize();
}


// Additional syntax highlight logic
function getCustomMode(filename) {
    var _config = (/^(web|app).config$/i);
    var _csproj = (/.(cs|vb)proj$/i);
    var syntax_mode = 'ace/mode/text';
    if (
        filename.match(_config) ||
        filename.match(_csproj)
       )
    {
        syntax_mode = 'ace/mode/xml';
    }
    return syntax_mode;
}


// Init Ace
var editor = ace.edit("editor");
editor.setTheme("ace/theme/github");
editor.getSession().setTabSize(4);
editor.getSession().setUseSoftTabs(true);
editor.$blockScrolling = Infinity;
editor.setOptions({
    "showPrintMargin": false,
    "fontSize": 14
});


// Hook the little pencil glyph and apply Ace syntax mode based on file extension
$('#fileList').on('click', '.glyphicon-pencil', function () {
    if ($('.edit-view').is(':visible')) {
        var filename;
        try {
            filename = (window.viewModel.fileEdit.peek()).name();
        }
        catch (e) {
            if (typeof console == 'object') {
                console.log('Can\'t get filename. ' + e);
            }
        }
        finally {
            if (typeof filename !== 'undefined') {
                var modelist = ace.require('ace/ext/modelist');
                var mode = modelist.getModeForPath(filename).mode;
                if (mode === 'ace/mode/text') {
                    mode = getCustomMode(filename);
                }
                // Apply computed syntax mode or default to 'ace/mode/text'
                editor.session.setMode(mode);
            }
            // Set Ace height
            resizeAce();
            // Attach event handler to set new Ace height on browser resize
            $(window).on('resize', function () {
                resizeAce();
            });
        }
    }
});
