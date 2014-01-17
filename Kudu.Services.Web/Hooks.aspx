<%@ Page Language="C#" %>
<%@ Register Src="~/Menu.ascx" TagPrefix="uc1" TagName="Menu" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
    
<head>
    <title>Web hooks</title>
    <link href="//netdna.bootstrapcdn.com/bootstrap/3.0.2/css/bootstrap.min.css" rel="stylesheet" />

    <style type="text/css">
        body {
            padding-top: 50px;
        }
    </style>
    
    <script src="//ajax.aspnetcdn.com/ajax/jquery/jquery-1.9.1.min.js"></script>
    <script src="//ajax.aspnetcdn.com/ajax/knockout/knockout-2.2.1.js"></script>

</head>

<body>
    <uc1:menu runat="server" id="Menu" />
    
    <div class="container">
        <h3>About</h3>
        Urls can be registered as web hooks. Information is posted to them when events occur.<br/>
        Only "PostDeployment" event is supported for now.
    </div>
    
    <div class="container">
        <h3>Add Subscriber Url</h3>
        <form class="form-inline" role="form">
            <div class="form-group" id="urlInput">
                <input type="url" class="form-control" id="urlTextBox" placeholder="Subscriber Url"
                     data-bind="value: newHookUrl">
            </div>
            <button type="button" class="btn btn-default" data-bind="click: addHook">Add Url</button>
        </form>
    </div>

    <div class="container">
        <h3>Subscribed Web Hooks (<a href="hooks">View json</a>)</h3>
        <table class="table table-hover" id="hooksTable">
            <thead>
                <tr>
                    <th>Url</th>
                    <th>Event</th>
                    <th>Last Callback Time</th>
                    <th>Status</th>
                    <th>Reason</th>
                </tr>
            </thead>
            <tbody id="hooksTableBody" data-bind="foreach: hooks">
                <tr>
                    <td data-bind="text: url"></td>
                    <td data-bind="text: event"></td>
                    <td data-bind="text: last_datetime"></td>
                    <td data-bind="text: last_status"></td>
                    <td data-bind="text: last_reason"></td>
                    <td><a href="" data-bind="click: $parent.removeHook">Remove</a><td>
                </tr>
           </tbody>
        </table>
    </div>
    
    <script type="text/javascript">
        function HookTableViewModel() {
            // Data
            var self = this;
            self.hooks = ko.observableArray([]);
            self.newHookUrl = ko.observable();

            // Operations
            self.populateHooks = function() {
                $.ajax({
                    type: "GET",
                    url: "hooks",
                    dataType: "json",
                    success: function(data) {
                        // The /hooks api returns an array of json objects.
                        // Each object has fields including id, url, event 
                        self.hooks(data);
                    },
                    error: function(jqXhr, textStatus, errorThrown) {
                        alert(textStatus + ": " + errorThrown);
                    }
                });
            };

            self.addHook = function() {
                $.ajax({
                    type: "POST",
                    url: "hooks",
                    contentType: "application/json",
                    data: JSON.stringify({ url: self.newHookUrl(), event: "PostDeployment" }),
                    success: function() {
                        self.newHookUrl("");
                    },
                    error: function (jqXhr, textStatus, errorThrown) {
                        alert(textStatus + ": " + errorThrown);
                    },
                    complete: function() {
                        self.populateHooks();
                    }
                });
            };

            self.removeHook = function(hook) {
                $.ajax({
                    type: "DELETE",
                    url: "hooks/" + hook.id,
                    error: function (jqXhr, textStatus, errorThrown) {
                        alert(textStatus + ": " + errorThrown);
                    },
                    complete: self.populateHooks
                });
            };

            // Initialization
            self.populateHooks();
        }

        ko.applyBindings(new HookTableViewModel());

    </script>

</body>
</html>
