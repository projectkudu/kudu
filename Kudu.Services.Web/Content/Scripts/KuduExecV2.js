function SwitchConsole() {
    var id = window.$KuduExecConsole.attr("id");
    if (id === "KuduExecConsoleV2") {
        window.$KuduExecConsole.data('connection').stop();
        window.$KuduExecConsole.replaceWith("<div id=\"KuduExecConsole\" class=\"left-aligned\"></div>");
        $('#SwitchConsoleLink').text("Use new interactive console");
        LoadConsole();
    } else {
        window.$KuduExecConsole.replaceWith("<div id=\"KuduExecConsoleV2\" class=\"left-aligned\"></div>");
        $('#SwitchConsoleLink').text("Use old console");
        LoadConsoleV2();
    }
}

var curWorkingDir = ko.observable("");
window.KuduExec = { workingDir: curWorkingDir };

function LoadConsoleV2() {

    var fileExplorerChanged = false;
    //diretory change callback from FileBrowser.js
    function _changeDir(value) {
        //for the very first time, value is empty but we know that the file explorer root is appRoot
        value = value || window.KuduExec.appRoot;
        curWorkingDir(value);
        if (getShell().toUpperCase() === "POWERSHELL") {
            //PowerShell doesn't return a new line after CD, so let's add a new line in the UI 
            DisplayAndUpdate({ Error: "", Output: "\n" });
            _sendCommand("cd \"" + value + "\"");
        } else {
            //CMD can't CD into different drives without /d and it's harmless for normal directories
            _sendCommand("cd /d \"" + value + "\"");
        }
        //the change notification goes both ways (console <--> file explorer)
        //the console uses this flag to break the loop
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
                lastLine.Error = "";
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
        welcomeMessage: "Kudu Remote Execution Console\r\nType 'exit' then hit 'enter' to get a new " + getShell() + " process.\r\nType 'cls' to clear the console\r\n\r\n"
    });
    window.$KuduExecConsole = $('#KuduExecConsoleV2');
    window.$KuduExecConsole.append(kuduExecConsole);
    if (getShell().toUpperCase() === "POWERSHELL") {
        $("div.jquery-console-inner").css("background-color", "#012456");
    }

    var connection = $.connection('/commandstream', "shell=" + getShell(), true);
    window.$KuduExecConsole.data('connection', connection);

    connection.start({
        waitForPageLoad: true,
        transport: "auto"
    });


    connection.received(function (data) {
        DisplayAndUpdate(data);
        controller.enableInput();
    });

    function _sendCommand(input) {
        connection.send(input);
    }

    function endsWith(str, suffix) {
        return str.indexOf(suffix, str.length - suffix.length) !== -1;
    }

    function startsWith(str, prefix) {
        return str.indexOf(prefix) == 0;
    }

    function getJSONValue(input) {
        return input? (input.Output || input.Error || "").toString() : "";
    }

    function getShell() {
        var regex = new RegExp("[\\?&]shell=([^&#]*)"),
            results = regex.exec(location.search);
        return results == null ? "CMD" : decodeURIComponent(results[1].replace(/\+/g, " "));
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
        prompt = prompt.trim();
        if (!endsWith(prompt, "\n") && endsWith(prompt, ">") && !fileExplorerChanged) {
            var windowsPath = prompt.replace("\n", "").replace(">", "");
            if (startsWith(windowsPath, "PS ")) {
                windowsPath = windowsPath.substr(3);
            }
            if (windowsPath.match(/^[a-zA-Z]:(\\\w+)*(.*)$/)) {
                if (!window.KuduExec.appRoot) {
                    window.KuduExec.appRoot = windowsPath;
                }
                curWorkingDir(windowsPath);
                if (window.KuduExec.updateFileSystemWatcher)
                    window.KuduExec.updateFileSystemWatcher(windowsPath);
            }
        }
    }

    window.setInterval(function () {
        controller.enableInput();
    }, 2000);
}

$(function () {
    LoadConsoleV2();
})
