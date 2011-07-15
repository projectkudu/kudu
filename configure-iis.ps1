$webRoot = Join-Path (Split-Path $MyInvocation.MyCommand.Path) Kudu.Services.Web\App_Data\_root\wwwroot
$iisDllPath = "$env:windir\System32\inetsrv\Microsoft.Web.Administration.dll"

if(!(Test-Path $iisDllPath)) {
    "Unable to locate Microsoft.Web.Administration. Do you have IIS configured?"
    return
}

# Add a reference to the iis dll
Add-Type -Path $iisDllPath

# Create the server manager
$iis = New-Object Microsoft.Web.Administration.ServerManager
$defaultWebSite = $iis.Sites["Default Web Site"]

if ($defaultWebSite) {
    $kuduApp = $defaultWebSite.Applications["/kudu"]
    if ($kuduApp) {
        "Removing existing kudu application"
        $defaultWebSite.Applications.Remove($kuduApp)
    }

    "Creating kudu application pointing to $webRoot"
    $kuduApp = $defaultWebSite.Applications.Add("/kudu", $webRoot)
    $kuduApp.ApplicationPoolName = "ASP.NET v4.0";
    $iis.CommitChanges()
    "Kudu demo application is now running on Default Web Site"
}
else {
    "Unable to find site 'Default Web Site'."
}