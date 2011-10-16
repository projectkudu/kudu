(function ($) {
    $.fn.console = function (options) {
        // Get the file system from the options
        var $this = $(this),
            prompt = options.prompt || '$',
            console = null,
            pendingCommand = false; // REVIEW: We could allow command queuing.

        $this.addClass('console');

        var $messages = $('<div/>').addClass('messages');
        var $buffer = $('<ul/>').addClass('buffer');

        var $container = $('<div/>').addClass('container');
        var $prompt = $('<span/>').addClass('prompt').text(prompt);
        var $cmd = $('<input/>').addClass('new-command')
                                .attr('type', 'input')
                                .attr('autocomplete', 'off')
                                .css('font-family', $this.css('font-family'))
                                .css('font-size', $this.css('font-size'));

        $buffer.appendTo($messages);
        $messages.appendTo($this);

        $prompt.appendTo($container);
        $cmd.appendTo($container);
        $container.appendTo($this);

        function appendLine(value) {
            $buffer.append('<li>' + value + '</li>');
        }

        function appendCommand(command) {
            $buffer.append('<li><span class="prompt">' + prompt + '</span><span class="command">' + command + '</span><span class="icon icon-prompt-loading"></span><li>');
        }

        $cmd.bind('keydown', 'return', $.utils.throttle(function (ev) {
            if (pendingCommand === true) {
                ev.preventDefault();
                return false;
            }

            var command = $.trim($cmd.val());

            if (command) {
                appendCommand(command);
                pendingCommand = true;
                $(console).trigger('console.runCommand', [command]);
                $cmd.val('');
            }

            ev.preventDefault();
            return false;
        }, 50));

        var console = {
            append: function (content) {
                var lines = $.utils.htmlEncode(content).split('\n');
                $.each(lines, function () {
                    var line = this.replace(/\s/g, '&nbsp;');
                    if (!line) {
                        line = '&nbsp;';
                    }
                    $buffer.append('<li>' + line + '</li>');
                });

                $messages.scrollTop($buffer[0].scrollHeight);
            },
            clear: function () {
                $buffer.html('');
            },
            completeCommand: function () {
                pendingCommand = false;
                $this.find('.icon-prompt-loading').remove();
            }
        };

        return console;
    };

})(jQuery);