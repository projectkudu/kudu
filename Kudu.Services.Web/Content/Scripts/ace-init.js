// Bindings borrowed from https://github.com/probonogeek/knockout-ace,
// initial version (knockout-ace.js) by Ryan Niemeyer.
// Updated by Scott Messinger, Frederik Raabye, Thomas Hallock, Drew Freyling, and Shane Carr.
// Custom integration into Kudu by Adrian Calinescu (https://github.com/snobu)

// Act I - The Binding Dance
ko.bindingHandlers.ace = {
    init: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
        var value = ko.utils.unwrapObservable(valueAccessor());
        editor.setValue(value);
        editor.gotoLine( 0 );
        editor.getSession().on("change",function(delta){
            if (ko.isWriteableObservable(valueAccessor())) {
                valueAccessor()( editor.getValue() );
            }
        });
        // Destroy the editor instance when element is removed
        ko.utils.domNodeDisposal.addDisposeCallback(element, function() {
            editor.destroy();
        });
    },
    update: function (element, valueAccessor, allBindingsAccessor, viewModel, bindingContext) {
        var value = ko.utils.unwrapObservable(valueAccessor());
        // Handle programmatic updates to the observable,
        // also makes sure it doesn't update it if it's the same.
        // Otherwise, it will reload the instance, causing the cursor to jump.
        var content = editor.getValue();
        if (content !== value) {
            editor.setValue(value);
            editor.gotoLine( 0 );
        }
    }
};

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
    var _xdt = (/.xdt$/i);
    var _aspnet = (/.(cshtml|asp|aspx)$/i);
    var syntax_mode = 'ace/mode/text';
    if (
        filename.match(_config) ||
        filename.match(_csproj) ||
        filename.match(_xdt)
       )
    {
        syntax_mode = 'ace/mode/xml';
    }
    if (filename.match(_aspnet)) {
        syntax_mode = 'ace/mode/csharp';
    }
    return syntax_mode;
}

// Act II - The Editor Awakens
var editor = ace.edit("editor");
editor.setTheme("ace/theme/github");
editor.getSession().setTabSize(4);
editor.getSession().setUseSoftTabs(true);
editor.$blockScrolling = Infinity;
editor.setOptions({
    "showPrintMargin": false,
    "fontSize": 14
});

// Show a red bar if content has changed
var contentHasChanged = false;
editor.on('change', function () {
    // (Attempt to) separate user change from programatical
    // https://github.com/ajaxorg/ace/issues/503
    if (editor.curOp && editor.curOp.command.name) {
        if (contentHasChanged) {
            return;
        }
        $('#statusbar').removeClass('statusbar-saved');
        $('#statusbar').addClass('statusbar-red');
        // Let's be nice to jQuery and only .addClass() on first change
        contentHasChanged = true;
    }
});

// Bind CTRL-S as Save without closing
editor.commands.addCommand({
    name: 'saveItem',
    bindKey: {
        win: 'Ctrl-S',
        mac: 'Command-S',
        sender: 'editor|cli'
    },
    exec: function(env, args, request) {
        viewModel.fileEdit().saveItem();
    }
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
                console.log('Can not get filename. ' + e);
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
            editor.focus();
            // Attach event handler to set new Ace height on browser resize
            $(window).on('resize', function () {
                resizeAce();
            });
        }
    }
});
