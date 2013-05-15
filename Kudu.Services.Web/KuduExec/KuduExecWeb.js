
var curWorkingDir = null;
$(document).ready(function () {

    var resizeHeight = function () {
        var conHeight = $(window).height() * 0.8;
        $('#KuduExecConsole').css({ height: conHeight });
    }
    $(window).resize(resizeHeight);
    // Update height for the first time.
    resizeHeight();

    var makeConsole = function () {
        var getPrompText = function () {
            return curWorkingDir + ">";
        }
        var kuduExecConsole = $('<div class="console">');
        var curReportFun = null;
        var controller = kuduExecConsole.console({
            promptLabel: getPrompText,
            commandValidate: function (line) {
                return true;
            },
            commandHandle: function (line, reportFn) {
                if (line.trim() === "exit") {
                    controller.reset();
                }
                curReportFun = reportFn;
                SubmitCommand(line, reportFn);
            },
            cancelHandle: function () {
                if (curReportFun) {
                    curReportFun([{ msg: "Command canceled by user.", className: "jquery-console-message-error" }]);
                }
            },
            autofocus: true,
            animateScroll: true,
            promptHistory: true,
            welcomeMessage: "Kudu Remote Execution Console\nType 'exit' to reset this console."
        });
        $('#KuduExecConsole').append(kuduExecConsole);
    }

    // call make console after this first command
    // so the current working directory is set.
    SubmitCommand("echo.", makeConsole);
});

function MessagesFromResponse(resp) {
    // parse the output to separate the command output from
    // our tacked on expression to get the current working directory
    // after the command.
    var output;
    var resultOutput;
    var cdIndex;
    if (resp.Output) {
        resultOutput = resp.Output.trim();
        cdIndex = resultOutput.lastIndexOf("\r\n");
        if (cdIndex < 0) {
            // The original command is has no output it is just our output.
            curWorkingDir = resultOutput.trim();
        } else {
            // happen
            curWorkingDir = resultOutput.substr(cdIndex).trim();
            output = resultOutput.substr(0, cdIndex);
        }
    }
    if (output) {
        return FormatCommandOutput(output, "jquery-console-message-value");
    }
    if (resp.Error) {
        return FormatCommandOutput(resp.Error + "\n", "jquery-console-message-error");
    }
}

function FormatCommandOutput(text, className) {
    // need to do some massaging of newlines to make it look right
    var fmtText = text.replace(/\r\n/g, '\n');
    return [{ msg: fmtText, className: className }];
}

// returns a request promise
function RemoteExecuteCommandRequest(command) {
    var uri = "/command";
    var dir = curWorkingDir ? curWorkingDir : "";
    var commandExec = { command : command, dir : dir };
    var request = { 
           method: "POST", 
      contentType: "application/json", 
             data: JSON.stringify(commandExec) 
    };
    return $.ajax(uri,request);
}

function SubmitCommand(command, reportFn) {
    if (command == "") {
        reportFn([{ msg: "", className: "jquery-console-message-value" }]);
    } else {
        // always append these commands so the working directory after the command is returned.
        var remoteCommand = command + " & echo. & cd";
        var request = RemoteExecuteCommandRequest(remoteCommand);
        request.done(function (resp) {
            var msgs = MessagesFromResponse(resp);
            reportFn(msgs);
        });
    }
}
