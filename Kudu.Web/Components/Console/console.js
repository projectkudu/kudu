(function ($) {
    $.fn.console = function (options) {
        var config = {
            prompt: '$'
        };

        $.extend(config, options);
        // Get the file system from the options
        var $this = $(this),
            prompt = config.prompt,
            console = null,
            executingCommand = false; // REVIEW: We could allow command queuing.

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


        var commandStack = (function () {
            var at = 0;
            var stack = [];
            return {
                next: function () {
                    if (at < stack.length) {
                        at++;
                    }
                },
                prev: function () {
                    if (at > 0) {
                        at--;
                    }
                },
                add: function (command) {
                    at = stack.push(command);
                },
                getValue: function () {
                    return stack[at];
                }
            };
        })();

        function appendLine(value) {
            $buffer.append('<li>' + value + '</li>');
        }

        function appendCommand(command) {
            commandStack.add(command);
            $buffer.append('<li><span class="prompt">' + prompt + '</span><span class="command">' + command + '</span><span class="icon icon-prompt-loading"></span><li>');
        }

        $this.click(function () {
            console.focus();
        });

        $cmd.bind('keydown', 'esc', $.utils.throttle(function (ev) {
            if (ev.target === $cmd[0]) {
                $(this).val('');
                ev.stopPropagation();
                ev.preventDefault();
                return false;
            }
        }, 50));

        $cmd.bind('keydown', 'tab', function (ev) {
            if (ev.target === $cmd[0]) {
                // Capture tab 
                ev.stopPropagation();
                ev.preventDefault();
                return false;
            }
        });

        $cmd.bind('keydown', 'ctrl+c', $.utils.throttle(function (ev) {
            if (ev.target === $cmd[0] && executingCommand === true) {
                $(console).trigger('console.cancelCommand');
                console.completeCommand();

                ev.stopPropagation();
                ev.preventDefault();
                return false;
            }
        }, 50));

        $cmd.bind('keydown', 'up', $.utils.throttle(function (ev) {
            if (ev.target === $cmd[0]) {
                commandStack.prev();
                var command = commandStack.getValue();
                if (command && $cmd.val() !== command) {
                    $cmd.val(command);
                }
                ev.stopPropagation();
                ev.preventDefault();
                return false;
            }
        }, 50));

        $cmd.bind('keydown', 'down', $.utils.throttle(function (ev) {
            if (ev.target === $cmd[0]) {
                commandStack.next();
                var command = commandStack.getValue();
                if (command && $cmd.val() !== command) {
                    $cmd.val(command);
                }
                ev.stopPropagation();
                ev.preventDefault();
                return false;
            }
        }, 50));

        $cmd.bind('keydown', 'return', $.utils.throttle(function (ev) {
            if (ev.target !== $cmd[0] || executingCommand === true) {
                ev.preventDefault();
                return false;
            }

            var command = $.trim($cmd.val());

            if (command) {
                appendCommand(command);
                executingCommand = true;
                $(console).trigger('console.runCommand', [command]);
                $cmd.val('');
                scrollBuffer();
                console.focus();
            }

            ev.preventDefault();
            return false;
        }, 50));

        function scrollBuffer() {
            $messages.scrollTop($buffer[0].scrollHeight);
        }

        var console = {
            log: function (content) {
                var lines = $.utils.htmlEncode(content).split('\n');
                $.each(lines, function () {
                    var line = this.replace(/\s/g, '&nbsp;');
                    if (!line) {
                        line = '&nbsp;';
                    }
                    appendLine(line);
                });

                scrollBuffer();
            },
            clear: function () {
                $buffer.html('');
            },
            completeCommand: function () {
                executingCommand = false;
                $buffer.find('.icon-prompt-loading').remove();
            },
            focus: function () {
                $cmd.focus();
            }
        };

        return console;
    };

})(jQuery);