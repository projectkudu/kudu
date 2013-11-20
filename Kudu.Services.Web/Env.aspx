<%@ Page Language="C#" %>
<%@ Import Namespace="System.Configuration" %>
<%@ Import Namespace="System.Collections" %>

<%
    var context = new HttpContextWrapper(HttpContext.Current);
%>
<!DOCTYPE html>
<html>
<head>
    <title>Kudu Services</title>
    <link rel="stylesheet" type="text/css" href="//ajax.aspnetcdn.com/ajax/bootstrap/3.0.2/css/bootstrap.min.css" />
    <style type="text/css">
        body {
            padding-top: 50px;
        }
    </style>
</head>
<body>
    <div class="navbar navbar-inverse navbar-fixed-top" role="navigation">
        <div class="container">
            <div class="navbar-header">
                <a class="navbar-brand" href="<%=ResolveUrl("~/")%>">Kudu</a>
            </div>
            <div class="collapse navbar-collapse">
                <ul class="nav navbar-nav">
                    <li><a href="Env.aspx">Runtime Environment</a></li>
                    <li><a href="DebugConsole">Diagnostic console</a></li>
                    <li><a href="logstream" title="If no log events are being generated the page may not load.">Diagnostic log stream</a></li>
                </ul>
            </div>
        </div>
    </div>

    <div class="container">
        <h3>Index</h3>
        <ul>
            <li><a href="#sysInfo">System Info</a></li>
            <li><a href="#appSettings">App Settings</a></li>
            <li><a href="#connectionString">Connection Strings</a></li>
            <li><a href="#envVariables">Environment variables</a></li>
            <li><a href="#path">PATH</a></li>
            <li><a href="#httpHeaders">HTTP Headers</a></li>
            <li><a href="#serverVar">Server variables</a></li>
        </ul>
    </div>

    <div class="container">
        <h3 id="sysInfo">System info</h3>
        <ul>
            <li>System up time: <%: TimeSpan.FromMilliseconds(Environment.TickCount) %></li>
            <li>OS version: <%: Environment.OSVersion %></li>
            <li>64 bit system: <%: Environment.Is64BitOperatingSystem %></li>
            <li>64 bit process: <%: Environment.Is64BitProcess %></li>
            <li>Processor count: <%: Environment.ProcessorCount %></li>
            <li>Machine name: <%: Environment.MachineName %></li>
            <li>Instance id: <%: Kudu.Core.Infrastructure.InstanceIdUtility.GetInstanceId() %></li>
            <li>Short instance id: <%: Kudu.Core.Infrastructure.InstanceIdUtility.GetShortInstanceId() %></li>
            <li>CLR version: <%: Environment.Version %></li>
            <li>System directory: <%: Environment.SystemDirectory %></li>
            <li>Current working directory: <%: Environment.CurrentDirectory %></li>
            <li>IIS command line: <%: Environment.CommandLine %></li>
        </ul>

        <h3 id="appSettings">AppSettings</h3>
        <ul class="fixed-width">
        <% foreach (string name in ConfigurationManager.AppSettings) { %>
            <li>
            <%: name %> = <%: ConfigurationManager.AppSettings[name] %>
            </li>
        <% } %>
        </ul>

        <h3 id="connectionStrings">Connection Strings</h3>
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

        <h3 id="envVariables">Environment variables</h3>
        <ul class="fixed-width">
        <% foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables().OfType<DictionaryEntry>().OrderBy(e => e.Key)) { %>
            <li><%: entry.Key %> = <%: entry.Value %></li>
        <% } %>
        </ul>

        <h3 id="path">PATH</h3>
        <ul class="fixed-width">
        <% foreach (string folder in Environment.GetEnvironmentVariable("PATH").Trim(';').Split(';').OrderBy(s => s)) { %>
            <li><%: folder %></li>
        <% } %>
        </ul>

        <h3 id="httpHeaders">HTTP headers</h3>
        <ul class="fixed-width">
        <% foreach (string name in Request.Headers.OfType<string>().OrderBy(s => s)) { %>
            <li><%: name  %>=<%: Request.Headers[name] %></li>
        <% } %>
        </ul>

        <h3 id="serverVar">Server variables</h3>
        <ul class="fixed-width">
        <% foreach (string name in Request.ServerVariables.OfType<string>().OrderBy(s => s)) { %>
            <li><%: name  %>=<%: Request.ServerVariables[name] %></li>
        <% } %>
        </ul>
    </div>
</body>
</html>