<%@ Page Language="C#" %>
<%@ Import Namespace="System.Configuration" %>
<%@ Import Namespace="System.Collections" %>

<html xmlns="http://www.w3.org/1999/xhtml">
<head id="Head1" runat="server">
    <title></title>
    <style type="text/css">
        body
        {
            color: #646465;
            font-family: 'Segoe UI', "Helvetica Neue", Helvetica, Arial, sans-serif;
            font-size: 12px;
            margin-left: 2%;
        }
        
        a {
            color: blue;
            text-decoration: none;
        }
        
        a:visited {
            color: blue;
        }
        
        a:hover {
            text-decoration: underline;
        }
        
        h1
        {
            padding-top: 20px;
            font-size: 20px;
        }
        
        table {
            border-collapse: collapse;
            font-family: Helvetica, sans-serif;
            font-size: 13px;
        }
        
        table td {
            padding: 4px;
        }
        
        #footer {
            text-align: center;
        }
        
        .header {
            font-size: 15px;
            font-weight: bold;
            border-bottom:1px solid #ccc; 
            width:30%; 
            margin-top:40px;
            margin-bottom:5px;
        }
        
        ul li {
           margin-top: 5px;
        }
        
        .fixed-width {
            font-size: 13px;
            font-family: Consolas;
        } 
    </style>
</head>
<body>
    <div>
        <h2>AppSettings</h2>
        <ul class="fixed-width">
        <% foreach (string name in ConfigurationManager.AppSettings) { %>
            <li>
            <%: name %> = <%: ConfigurationManager.AppSettings[name] %>
            </li>
        <% } %>
        </ul>

        <h2>Connection Strings</h2>
        <ul>
        <% foreach (ConnectionStringSettings settings in ConfigurationManager.ConnectionStrings) { %>
            <li>
            <span class="fixed-width"><%: settings.Name %></span>
            <ul class="fixed-width">
                <li>ConnectionString = <%: settings.ConnectionString %></li>
                <li>ProviderName = <%: settings.ProviderName %></li>
            </ul>
            </li>
       <% } %>
        </ul>

        <h2>Environment variables</h2>
        <ul class="fixed-width">
        <% foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables()) { %>
            <li><%: entry.Key %> = <%: entry.Value %></li>
        <% } %>
        </ul>

        <h2>HTTP headers</h2>
        <ul class="fixed-width">
        <% foreach (string name in Request.Headers) { %>
            <li><%: name  %>=<%: Request.Headers[name] %></li>
        <% } %>
        </ul>
    </div>
</body>
</html>