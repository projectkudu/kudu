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
    //diretory change callback from FileBrowser.js
    function _changeDir(value) {
        //for the very first time, value is empty but we know that the file explorer root is appRoot
        value = value || window.KuduExec.appRoot;
        // curWorkingDir(value); since _sendCommand will update the current working directory
        if (getShell().toUpperCase() === "POWERSHELL") {
            //PowerShell doesn't return a new line after CD, so let's add a new line in the UI 
            DisplayAndUpdate({ Error: "", Output: "\n" });
            _sendCommand("cd \"" + value + "\"\n");
        } else {
            //CMD can't CD into different drives without /d and it's harmless for normal directories
            _sendCommand("cd /d \"" + value + "\"\n");
        }
        //the change notification goes both ways (console <--> file explorer)
    };

    window.KuduExec.changeDir = _changeDir;
    var originalMatchString = undefined;
    var currentMatchIndex = -1;
    var lastLine;
    var lastUserInput = null;
    var kuduExecConsole = $('<div class="console">');
    var curReportFun;
    var height = parseInt(window.localStorage.debugconsole_height);
    height = !!height ? height : 500;
    var heightOffset = height / 10;
    var controller = kuduExecConsole.console({
        continuedPrompt: true,
        promptLabel: function () {
            return getJSONValue(lastLine);
        },
        commandValidate: function () {
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
                _sendCommand(lastUserInput);
                controller.resetHistory();
                DisplayAndUpdate(lastLine);
                lastLine = {
                    Output: "",
                    Error: ""
                };
                DisplayAndUpdate(lastLine);
                if (line.trim().toUpperCase() == "EXIT") {
                    controller.enableInput();
                }
            }
        },
        cancelHandle: function () {
            //sending CTRL+C character (^C) to the server to cancel the current command
            _sendCommand("\x03");
        },
        completeHandle: function (line, reverse) {
            if (originalMatchString === undefined) {
                originalMatchString = line;
                currentMatchIndex = -1;
            }
            var cdRegex = /^(cd\s+)(.+)$/,
                        matches;
            var result = [];
            var dirOnly = false;
            if (matches = originalMatchString.match(cdRegex)) {
                dirOnly = true;
                result = window.KuduExec.completePath(matches[2], /* dirOnly */ dirOnly);
            } else if (matches = originalMatchString.split(" ")) {
                result = window.KuduExec.completePath(matches.pop());
            }
            if (result.length > 0) {
                result = $.map(result, function (elm) {
                    if (getShell().toUpperCase() === "POWERSHELL") {
                        elm = ".\\" + elm;
                    }
                    if (elm.indexOf(" ") !== -1) {
                        elm = '"' + elm + '"';
                    }
                    if (dirOnly) {
                        elm = matches[1] + elm;
                    } else {
                        var prefix = "";
                        for (var i = 0; i < matches.length; i++) {
                            prefix += matches[i] + " ";
                        }
                        elm = prefix + elm;
                    }
                    return elm;
                });
                var fullLength = result.length;
                currentMatchIndex = (currentMatchIndex + (reverse ? -1 : 1)) % fullLength;
                currentMatchIndex = currentMatchIndex < 0 ? fullLength - 1 : currentMatchIndex;
                result = result.slice(currentMatchIndex, currentMatchIndex + 1);
            }
            return result;
        },
        userInputHandle: function (keycode) {
            //reset the string we match on if the user type anything other than tab == 9
            if (keycode !== 9 && keycode != 16) {
                originalMatchString = undefined;
            }
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

    var connection = $.connection(appRoot + 'api/commandstream', "shell=" + getShell(), true);
    window.$KuduExecConsole.data('connection', connection);

    connection.error(function (error) {
        if (error && error.context.status === 403) {
            $('#403-error-modal').modal();
        }
    });

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
        return input ? (input.Output || input.Error || "").toString() : "";
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
        // case 1. lastLine = "progress 10%", prompt = "\r" ==> lastLine is not written into HTML (curl)
        // case 2. lastLine = "progress 10%\r", prompt = "progress 20%\r" ==> lastLine is not written into HTML (youtube-dl)
        //         lastLine = "version 123\r", prompt = "\r\n" ==> lastLine IS WRITTEN into HTML (dotnet tsc)
        if ((endsWith(prompt, "\r") && !endsWith(lastLinestr, "\n")) ||
            (endsWith(lastLinestr, "\r") && prompt !== "\r\n" && prompt !== "\n")) {
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

        // display output, but not updating the HTML
        $(".jquery-console-inner").append($(".jquery-console-prompt-box").last().css("display", "inline"));
        if (data.Error) {
            $(".jquery-console-prompt-label").last().text(prompt).css("color", "red");
        } else {
            $(".jquery-console-prompt-label").last().text(prompt).css("color", "white");
        }

        controller.promptText("");

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
        if (!endsWith(prompt, "\n") && endsWith(prompt, ">")) {
            var windowsPath = prompt.replace("\n", "").replace(">", "");
            if (startsWith(windowsPath, "PS ")) {
                windowsPath = windowsPath.substr(3);
            }
            if (windowsPath.match(/^[a-zA-Z]:(\\\w+)*(.*)$/)) {
                if (!window.KuduExec.appRoot) {
                    window.KuduExec.appRoot = windowsPath;
                }
                curWorkingDir(windowsPath);
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
