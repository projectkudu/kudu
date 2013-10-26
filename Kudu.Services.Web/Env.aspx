<%@ Page Language="C#" %>
<%@ Import Namespace="System.Configuration" %>
<%@ Import Namespace="System.Collections" %>

<%
    var context = new HttpContextWrapper(HttpContext.Current);
%>

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
        <h2>System info</h2>
        <ul class="fixed-width">
            <li>System up time: <%: TimeSpan.FromMilliseconds(Environment.TickCount) %></li>
            <li>OS version: <%: Environment.OSVersion %></li>
            <li>64 bit system: <%: Environment.Is64BitOperatingSystem %></li>
            <li>64 bit process: <%: Environment.Is64BitProcess %></li>
            <li>Processor count: <%: Environment.ProcessorCount %></li>
            <li>Machine name: <%: Environment.MachineName %></li>
            <li>Instance id: <%: Kudu.Services.InstanceIdUtility.GetInstanceId(context) %></li>
            <li>Short instance id: <%: Kudu.Services.InstanceIdUtility.GetShortInstanceId(context) %></li>
            <li>CLR version: <%: Environment.Version %></li>
            <li>System directory: <%: Environment.SystemDirectory %></li>
            <li>Current working directory: <%: Environment.CurrentDirectory %></li>
            <li>IIS command line: <%: Environment.CommandLine %></li>
        </ul>

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
        <% foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables().OfType<DictionaryEntry>().OrderBy(e => e.Key)) { %>
            <li><%: entry.Key %> = <%: entry.Value %></li>
        <% } %>
        </ul>

        <h2>PATH</h2>
        <ul class="fixed-width">
        <% foreach (string folder in Environment.GetEnvironmentVariable("PATH").Trim(';').Split(';').OrderBy(s => s)) { %>
            <li><%: folder %></li>
        <% } %>
        </ul>

        <h2>HTTP headers</h2>
        <ul class="fixed-width">
        <% foreach (string name in Request.Headers.OfType<string>().OrderBy(s => s)) { %>
            <li><%: name  %>=<%: Request.Headers[name] %></li>
        <% } %>
        </ul>

        <h2>Server variables</h2>
        <ul class="fixed-width">
        <% foreach (string name in Request.ServerVariables.OfType<string>().OrderBy(s => s)) { %>
            <li><%: name  %>=<%: Request.ServerVariables[name] %></li>
        <% } %>
        </ul>
    </div>
</body>
</html>