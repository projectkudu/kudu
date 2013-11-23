var switched;

function SwitchConsole() {
    if (!switched) {
        $('#KuduExecConsole').replaceWith("<div id=\"KuduExecConsoleV2\" class=\"left-aligned\"></div>");
        $('#SwitchConsoleLink').text("Refresh to go back to the old console");
        LoadConsoleV2();
        switched = true;
    } else {
        location.reload(false);
    }
}

var curWorkingDir = ko.observable("");
window.KuduExec = { workingDir: curWorkingDir };

function LoadConsoleV2() {

    var fileExplorerChanged = false;
    function _changeDir(value) {
        value = value || window.KuduExec.appRoot;
        curWorkingDir(value);
        _sendCommand("cd /d \"" + value + "\"");
        fileExplorerChanged = true;
    };

    window.KuduExec.changeDir = _changeDir;
    // call make console after this first command so the current working directory is set.
    var lastLine = "";
    var lastUserInput = null;
    var kuduExecConsole = $('<div class="console">');
    var curReportFun;
    var height = parseInt(window.localStorage.debugconsole_height);
    height = !!height ? height : 500;
    var heightOffset = height / 10;
    var controller = kuduExecConsole.console({
        continuedPrompt: true,
        promptLabel: function() {
            return getJSONValue(lastLine);
        },
        commandValidate: function() {
            return true;
        },
        commandHandle: function (line, reportFn) {
            curReportFun = reportFn;
            if (line.trim().toUpperCase() === "CLS") {
                controller.reset();
                $(".jquery-console-inner").append($(".jquery-console-prompt-box").css("display", "inline-block"));
                controller.message("", "jquery-console-message-value");
            } else {
                lastUserInput = line + "\n";
                if (lastLine.Output) {
                    lastLine.Output += lastUserInput;
                }
                else if (lastLine.Error) {
                    lastLine.Error += lastUserInput;
                } else {
                    lastLine.Output = lastUserInput;
                }
                _sendCommand(line);
                controller.resetHistory();
                DisplayAndUpdate(lastLine);
                lastLine.Output = "";
                DisplayAndUpdate(lastLine);
                fileExplorerChanged = false;
                if (line.trim().toUpperCase() == "EXIT") {
                    controller.enableInput();
                }
            }
        },
        cancelHandle: function () {
            //sending CTRL+C character (^C) to the server to cancel the current command
            _sendCommand("\x03");
        },
        completeHandle: function (line) {
            var cdRegex = /^cd\s+(.+)$/,
                        pathRegex = /.+\s+(.+)/,
                        matches;
            var result = [];
            if (matches = line.match(cdRegex)) {
                result = window.KuduExec.completePath(matches[1], /* dirOnly */ true);
            } else if (matches = line.match(pathRegex)) {
                result = window.KuduExec.completePath(matches[1]);
            }
            if (result.length > 0) {
                $(".jquery-console-prompt-box").last().css("display", "block");
            }
            return result;
        },
        cols: 3,
        autofocus: true,
        animateScroll: true,
        promptHistory: true,
        welcomeMessage: "Kudu Remote Execution Console\r\nType 'exit' then hit 'enter' to get a new cmd.exe process.\r\nType 'cls' to clear the console\r\n\r\n"
    });
    $('#KuduExecConsoleV2').append(kuduExecConsole);

    var connection = $.connection('/commandstream');

    connection.start({
        waitForPageLoad: true,
        transport: "auto"
    });

    connection.received(function (data) {
        DisplayAndUpdate(data);
        controller.enableInput();
    });
    
    function _sendCommand(input) {
        _sendMessage(input);
    }

    function _sendMessage(input) {
        connection.send(input);
    }
    
    function endsWith(str, suffix) {
        return str.indexOf(suffix, str.length - suffix.length) !== -1;
    }
    
    function getJSONValue(input) {
        return input? (input.Output || input.Error || "").toString() : "";
    }

    function DisplayAndUpdate(data) {
        var prompt = getJSONValue(data);
        var lastLinestr = getJSONValue(lastLine);
        //this means the last command should be cleared and the next one will be written over it.
        if (endsWith(prompt, "\r") && !endsWith(lastLinestr, "\n")) {
            lastLinestr = "";
            lastLine = null;
        }

        var consoleMessages = $(".jquery-console-message");
        if (consoleMessages.length > height && consoleMessages.length % heightOffset == (heightOffset - 1)) {
            consoleMessages.slice(0, consoleMessages.length - height).remove();
        }

        //if the data has the same class as the last ".jquery-console-message"
        //then just append it to the last one, if not, create a new div.
        var lastConsoleMessage = consoleMessages.last();
        lastConsoleMessage.text(lastConsoleMessage.text() + lastLinestr);
        lastLine = null;

        //if the prompt is just \r this means that we don't really need to display anything, just marking the line as 
        if (prompt == "\r") {
            return;
        }


        $(".jquery-console-inner").append($(".jquery-console-prompt-box").last().css("display", "inline"));
        if (data.Error) {
            $(".jquery-console-prompt-label").last().text(prompt).css("color", "red");
        } else {
            $(".jquery-console-prompt-label").last().text(prompt).css("color", "white");
        }

        controller.promptText("");

        if (endsWith(prompt, "\r")) {
            return;
        }


        //Now create the div for the new line that will be printed the next time with the correct class
        if (data.Error) {
            if (!lastConsoleMessage.hasClass("jquery-console-message-error")) {
                controller.message("", "jquery-console-message-error");
            }
        } else if (!lastConsoleMessage.hasClass("jquery-console-message-value") || endsWith(lastLinestr, "\n")) {
            controller.message("", "jquery-console-message-value");
        }

        //save last line for next time.
        lastLine = data;

        if (!endsWith(prompt, "\n") && !fileExplorerChanged) {
            var windowsPath = prompt.replace("\n", "").replace(">", "");
            if (windowsPath.match(/^[a-zA-Z]:(\\\w+)*([\\])?$/)) {
                if (!window.KuduExec.appRoot) {
                    window.KuduExec.appRoot = windowsPath;
                } else {
                    curWorkingDir(windowsPath);
                }
            }
        }
    }

    window.setInterval(function () {
        controller.enableInput();
    }, 2000);
}
