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
$kuduWebSite = $iis.Sites["kudu"]

if ($kuduWebSite) {
    "Removing existing kudu site"
    $kuduWebSite.Delete()
}

"Creating kudu site pointing to $webRoot"
$kuduWebSite = $iis.Sites.Add("kudu", $webRoot, 8080)
$kuduWebSite.ApplicationDefaults.ApplicationPoolName = "ASP.NET v4.0";
$iis.CommitChanges()
"Kudu demo site is now running on http://localhost:8080/"
