<%@ Control Language="C#" %>

    <div class="navbar navbar-inverse navbar-fixed-top" role="navigation">
        <div class="container">
            <div class="navbar-header">
                <a class="navbar-brand" href="/">Kudu</a>
            </div>
            <div class="collapse navbar-collapse">
                <ul class="nav navbar-nav">
                    <li><a runat="server" href="~/Env.aspx">Environment</a></li>
                    <li><a runat="server" href="~/DebugConsole">Debug console</a></li>
                    <li><a runat="server" href="~/dump">Diagnostic dump</a></li>
                    <li><a runat="server" href="~/logstream" title="If no log events are being generated the page may not load.">Log stream</a></li>
                </ul>
            </div>
        </div>
    </div>
