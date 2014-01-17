<%@ control language="C#" %>

<nav class="navbar navbar-default navbar-fixed-top navbar-inverse" role="navigation">
    <div class="container">
        <div class="navbar-header">
            <button type="button" class="navbar-toggle" data-toggle="collapse" data-target=".navbar-collapse">
                <span class="sr-only">Toggle navigation</span>
                <span class="icon-bar"></span>
                <span class="icon-bar"></span>
                <span class="icon-bar"></span>
              </button>
            <a class="navbar-brand" href="/">Kudu</a>
        </div>
        <div class="collapse navbar-collapse">
            <ul class="nav navbar-nav">
                <li><a runat="server" href="~/Env.aspx">Environment</a></li>
                <li><a runat="server" href="~/DebugConsole">Debug console</a></li>
                <li><a runat="server" href="~/dump">Diagnostic dump</a></li>
                <li><a runat="server" href="~/logstream" title="If no log events are being generated the page may not load.">Log stream</a></li>
                <li><a runat="server" href="~/Hooks.aspx">Web hooks</a></li>
            </ul>
        </div>
    </div>
</nav>
