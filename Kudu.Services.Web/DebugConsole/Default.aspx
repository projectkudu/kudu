﻿<%@ Page Language="C#" %>
<%@ Register Src="~/Menu.ascx" TagPrefix="uc1" TagName="Menu" %>


<!DOCTYPE html>

<html>
<head>
    <title>Diagnostic Console</title>
    <meta charset="utf-8" />
    <link href="/content/styles/filebrowser.css" rel="stylesheet" />
    <link href="//netdna.bootstrapcdn.com/bootstrap/3.0.2/css/bootstrap.min.css" rel="stylesheet" />
    <link href="//netdna.bootstrapcdn.com/font-awesome/4.0.2/css/font-awesome.css" rel="stylesheet" />
</head>
<body>
    <uc1:menu runat="server" id="Menu" />
    <div id="main" class="container">
        <div class="view main-view" data-bind="visible: !fileEdit()">
            <div id="fileList">
                <div id="operations" class="h3">
                    <span data-bind="visible: selected().parent">
                        <a href="#" data-bind="click: selected().selectParent, attr: { title: selected().parent && ('Up to ' + selected().parent.name()) }">...</a> /
                    </span>
                    <span data-bind="text: selected().name()"></span>
                    <a href="#">
                        <i class="glyphicon glyphicon-plus " title="Add Folder" id="createFolder"></i></a>
                    |
                    <span data-bind="text: selected().children().length"></span> items
                    <a class="btn" href="#" data-bind="click: function() { root.selectNode() }">
                        <i class="h4 glyphicon glyphicon-home" title="Home"></i>
                    </a>
                    <a class="btn" href="#" data-bind="click: function() { selectSpecialDir('LocalSiteRoot') }, visible: specialDirsIndex()['LocalSiteRoot']">
                        <i class="h4 glyphicon glyphicon-globe" title="Site Root"></i>
                    </a>
                    <a class="btn" href="#" data-bind="click: function() { selectSpecialDir('SystemDrive') }">
                        <i class="h4 glyphicon glyphicon-hdd" title="System Drive"></i>
                    </a>
                    <div class="spinner">
                        <i data-bind="visible: processing()" class="fa fa-spinner fa-spin" title="Please wait"></i>
                        <i data-bind="visible: errorText, attr: {title: errorText}" class="alert-warning glyphicon glyphicon-exclamation-sign"></i>
                    </div>
                </div>
                <div class="table-container">
                    <table class="table table-striped table-bordered table-hover table-condensed table-responsive">
                        <thead>
                            <tr>
                                <th scope="col"></th>
                                <th scope="col">Name</th>
                                <th scope="col">Modifed</th>
                                <th scope="col">Size</th>
                            </tr>
                        </thead>
                        <tbody data-bind="foreach: selected().children()">
                            <tr>
                                <td class="actions">
                                    <div data-bind="visible: !editing()">
                                        <a data-bind="attr: { href: url() }" target="_blank">
                                            <i class="glyphicon glyphicon-download-alt" title="Download"></i></a>
                                        <span data-bind="if: !isDirectory()">
                                            <a data-bind="click: editItem" href="#">
                                                <i class="glyphicon glyphicon-pencil" title="Edit"></i></a>
                                        </span>
                                        <a href="#" data-bind="click: deleteItem">
                                            <i class="glyphicon glyphicon-minus-sign" title="Delete"></i></a>
                                    </div>
                                </td>
                                <td class="name">
                                    <span data-bind="if: isDirectory()">
                                        <i class="glyphicon glyphicon-folder-open" title="Folder"></i>&nbsp;
                                        <a href="#" data-bind="click: selectNode, text: name, visible: !editing()" target="_blank"></a>
                                        <input type="text" data-bind="value: name, valueUpdate: 'afterKeyDown', visible: editing()" />
                                    </span>
                                    <span data-bind="if: !isDirectory()">
                                        <i class="glyphicon glyphicon-file" title="File"></i>
                                        <span data-bind="text: name"></span>
                                    </span>

                                </td>
                                <td data-bind="text: modifiedTime" class="date"></td>
                                <td data-bind="text: size" class="size"></td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>
            <div id="resizeHandle">
                <h4>
                    <span class="down" title="Slide down"><i class="glyphicon glyphicon-chevron-down"></i></span>
                    <span class="up" title="Slide up"><i class="glyphicon glyphicon-chevron-up"></i></span>
                </h4>
            </div>
            <a id="SwitchConsoleLink" href="javascript:SwitchConsole();" class="right">Use old console</a>
            <div id="KuduExecConsoleV2" class="left-aligned"></div>
        </div>
        <div class="view edit-view" data-bind="visible: fileEdit()">
            <div class="form-group form-inline">
                <form role="form">
                    <p>
                        &nbsp;
                        <button class="btn btn-primary btn-default" data-bind="click: function () { return fileEdit().saveItem(); }">Save</button>
                        &nbsp;
                        <button class="btn" data-bind="click: cancelEdit">Cancel</button>
                    </p>
                    <textarea class="span12 form-control" rows="20" id="txtarea" data-bind="value: editText, valueUpdate: 'afterkeydown'"></textarea>
                </form>
            </div>
        </div>
    </div>
    <script src="//ajax.aspnetcdn.com/ajax/jquery/jquery-1.9.1.min.js"></script>
    <script src="//ajax.aspnetcdn.com/ajax/knockout/knockout-2.2.1.js"></script>
    <script src="//netdna.bootstrapcdn.com/bootstrap/3.0.2/js/bootstrap.min.js"></script>
    <script src="/content/scripts/jquery.signalr-1.1.3.min.js"></script>
    <script src="/content/scripts/kuduexec.js"></script>
    <script src="/content/scripts/kuduexecV2.js"></script>
    <script src="/content/scripts/filebrowser.js"></script>
    <script src="/content/scripts/jquery-console/jquery.console.js"></script>
</body>
</html>
