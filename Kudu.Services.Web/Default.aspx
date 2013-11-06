<%@ page language="C#" %>

<%@ import namespace="System.IO" %>
<%@ import namespace="System.Web.Hosting" %>
<%@ import namespace="Kudu.Services.Web" %>
<%@ import namespace="Kudu.Services" %>

<!DOCTYPE html>
<html>
<head>
    <title>Kudu Services</title>
    <link rel="stylesheet" type="text/css" href="//ajax.aspnetcdn.com/ajax/bootstrap/3.0.2/css/bootstrap.min.css" />
    <style type="text/css">
        body {
            padding-top: 50px;
        }
        
        .row > div {
            padding-bottom: 10px;
        }
    </style>
</head>
<body>
    <div class="navbar navbar-inverse navbar-fixed-top" role="navigation">
        <div class="container">
            <div class="navbar-header">
                <a class="navbar-brand" href="/">Kudu</a>
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
        <%
        string commitFile = MapPath("~/commit.txt");
        string sha = File.Exists(commitFile) ? File.ReadAllText(commitFile).Trim() : null;
        var version = typeof(Kudu.Services.Web.Tracing.TraceModule).Assembly.GetName().Version;
        %>

        <h3>Environment</h3>
        <div class="row">
            <div class="col-md-2">
                <strong>Build</strong>
            </div>
            <div>
                <%=version%>
                <% if (!String.IsNullOrEmpty(sha)) { %>
                (<a id="sha" href="https://github.com/projectkudu/kudu/commit/<%: sha %>"><%: sha.Substring(0, 10) %></a>)
                <% } %>
            </div>
        </div>

        <div class="row">
            <div class="col-md-2">
                <strong>Site up time</strong>
            </div>
            <div>
                <%: Kudu.Services.Web.Tracing.TraceModule.UpTime%>
            </div>
        </div>
        <div class="row">
            <div class="col-md-2">
                <strong>Site folder</strong>
            </div>
            <div>
                <%: Kudu.Services.Web.PathResolver.ResolveRootPath()%>
            </div>
        </div>
        <div class="row">
            <div class="col-md-2">
                <strong>Temp folder</strong>
            </div>
            <div>
                <%: Path.GetTempPath()%>
            </div>
        </div>

        <h3>REST API <small>(works best when using a JSON viewer extension)</small></h3>
        <ul>
            <li>
                <a href="settings">App Settings</a>
            </li>
            <li>
                <a href="deployments">Deployments</a>
            </li>  
            <li>
                <a href="vfs">Files</a>
            </li>
            <li>
                <a href="diagnostics/processes">Processes and mini-dumps</a>
            </li>
            <li>
                <a href="diagnostics/runtime">Runtime versions</a>
            </li>
            <li>
                <a href="scm/info">Source control info</a>
            </li>
            <li>
                <a href="hooks">Web hooks</a>
            </li>
            <li>
                <a href="jobs">Web jobs</a>
            </li>
        </ul>
    </div>
</body>
</html>
