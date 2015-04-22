#Requires -Version 3

if (Test-Path env:WEBSITE_SITE_NAME)
{
    # This script is run in Azure Web Sites
    # Disable progress indicator
    $ProgressPreference = "SilentlyContinue"
}

$ScriptPath = $MyInvocation.MyCommand.Definition

$Script:UseWriteHost = $true
function _WriteDebug($msg) {
    if($Script:UseWriteHost) {
        try {
            Write-Debug $msg
        } catch {
            $Script:UseWriteHost = $false
            _WriteDebug $msg
        }
    }
}

function _WriteOut {
    param(
        [Parameter(Mandatory=$false, Position=0, ValueFromPipeline=$true)][string]$msg,
        [Parameter(Mandatory=$false)][ConsoleColor]$ForegroundColor,
        [Parameter(Mandatory=$false)][ConsoleColor]$BackgroundColor,
        [Parameter(Mandatory=$false)][switch]$NoNewLine)

    if(!$Script:UseWriteHost) {
        if(!$msg) {
            $msg = ""
        }
        if($NoNewLine) {
            [Console]::Write($msg)
        } else {
            [Console]::WriteLine($msg)
        }
    }
    else {
        try {
            if(!$ForegroundColor) {
                $ForegroundColor = $host.UI.RawUI.ForegroundColor
            }
            if(!$BackgroundColor) {
                $BackgroundColor = $host.UI.RawUI.BackgroundColor
            }

            Write-Host $msg -ForegroundColor:$ForegroundColor -BackgroundColor:$BackgroundColor -NoNewLine:$NoNewLine
        } catch {
            $Script:UseWriteHost = $false
            _WriteOut $msg
        }
    }

    if($__TeeTo) {
        $cur = Get-Variable -Name $__TeeTo -ValueOnly -Scope Global -ErrorAction SilentlyContinue
        $val = $cur + "$msg"
        if(!$NoNewLine) {
            $val += [Environment]::NewLine
        }
        Set-Variable -Name $__TeeTo -Value $val -Scope Global -Force
    }
}

### Constants
$ProductVersion="1.0.0"
$BuildVersion="beta4-10366"
$Authors="Microsoft Open Technologies, Inc."

# If the Version hasn't been replaced...
# We can't compare directly with the build version token
# because it'll just get replaced here as well :)
if($BuildVersion.StartsWith("{{")) {
    # We're being run from source code rather than the "compiled" artifact
    $BuildVersion = "HEAD"
}
$FullVersion="$ProductVersion-$BuildVersion"

Set-Variable -Option Constant "CommandName" ([IO.Path]::GetFileNameWithoutExtension($ScriptPath))
Set-Variable -Option Constant "CommandFriendlyName" ".NET Version Manager"
Set-Variable -Option Constant "DefaultUserDirectoryName" ".dnx"
Set-Variable -Option Constant "OldUserDirectoryNames" @(".kre", ".k")
Set-Variable -Option Constant "RuntimePackageName" "dnx"
Set-Variable -Option Constant "DefaultFeed" "https://www.nuget.org/api/v2"
Set-Variable -Option Constant "CrossGenCommand" "k-crossgen"
Set-Variable -Option Constant "CommandPrefix" "dnvm-"
Set-Variable -Option Constant "DefaultArchitecture" "x86"
Set-Variable -Option Constant "DefaultRuntime" "clr"
Set-Variable -Option Constant "AliasExtension" ".txt"

# These are intentionally using "%" syntax. The environment variables are expanded whenever the value is used.
Set-Variable -Option Constant "OldUserHomes" @("%USERPROFILE%\.kre","%USERPROFILE%\.k")
Set-Variable -Option Constant "DefaultUserHome" "%USERPROFILE%\$DefaultUserDirectoryName"
Set-Variable -Option Constant "HomeEnvVar" "DNX_HOME"

Set-Variable -Option Constant "RuntimeShortFriendlyName" "DNX"

Set-Variable -Option Constant "AsciiArt" @"
   ___  _  ___   ____  ___
  / _ \/ |/ / | / /  |/  /
 / // /    /| |/ / /|_/ / 
/____/_/|_/ |___/_/  /_/  
"@

$ExitCodes = @{
    "Success"                   = 0
    "AliasDoesNotExist"         = 1001
    "UnknownCommand"            = 1002
    "InvalidArguments"          = 1003
    "OtherError"                = 1004
    "NoSuchPackage"             = 1005
    "NoRuntimesOnFeed"          = 1006
}

$ColorScheme = $DnvmColors
if(!$ColorScheme) {
    $ColorScheme = @{
        "Banner"=[ConsoleColor]::Cyan
        "RuntimeName"=[ConsoleColor]::Yellow
        "Help_Header"=[ConsoleColor]::Yellow
        "Help_Switch"=[ConsoleColor]::Green
        "Help_Argument"=[ConsoleColor]::Cyan
        "Help_Optional"=[ConsoleColor]::Gray
        "Help_Command"=[ConsoleColor]::DarkYellow
        "Help_Executable"=[ConsoleColor]::DarkYellow
    }
}

Set-Variable -Option Constant "OptionPadding" 20

# Test Control Variables
if($__TeeTo) {
    _WriteDebug "Saving output to '$__TeeTo' variable"
    Set-Variable -Name $__TeeTo -Value "" -Scope Global -Force
}

# Commands that have been deprecated but do still work.
$DeprecatedCommands = @("unalias")

# Load Environment variables
$RuntimeHomes = $env:DNX_HOME
$UserHome = $env:DNX_USER_HOME
$ActiveFeed = $env:DNX_FEED

# Default Exit Code
$Script:ExitCode = $ExitCodes.Success

############################################################
### Below this point, the terms "DNVM", "DNX", etc.      ###
### should never be used. Instead, use the Constants     ###
### defined above                                        ###
############################################################
# An exception to the above: The commands are defined by functions
# named "dnvm-[command name]" so that extension functions can be added

$StartPath = $env:PATH

if($CmdPathFile) {
    if(Test-Path $CmdPathFile) {
        _WriteDebug "Cleaning old CMD PATH file: $CmdPathFile"
        Remove-Item $CmdPathFile -Force
    }
    _WriteDebug "Using CMD PATH file: $CmdPathFile"
}

if(!$ActiveFeed) {
    $ActiveFeed = $DefaultFeed
}

# Determine where runtimes can exist (RuntimeHomes)
if(!$RuntimeHomes) {
    # Set up a default value for the runtime home
    $UnencodedHomes = "%USERPROFILE%\$DefaultUserDirectoryName"
} else {
    $UnencodedHomes = $RuntimeHomes
}

$UnencodedHomes = $UnencodedHomes.Split(";")
$RuntimeHomes = $UnencodedHomes | ForEach-Object { [Environment]::ExpandEnvironmentVariables($_) }
$RuntimeDirs = $RuntimeHomes | ForEach-Object { Join-Path $_ "runtimes" }

# Determine the default installation directory (UserHome)
if(!$UserHome) {
    _WriteDebug "Detecting User Home..."
    $pf = $env:ProgramFiles
    if(Test-Path "env:\ProgramFiles(x86)") {
        $pf32 = cat "env:\ProgramFiles(x86)"
    }

    # Canonicalize so we can do StartsWith tests
    if(!$pf.EndsWith("\")) { $pf += "\" }
    if($pf32 -and !$pf32.EndsWith("\")) { $pf32 += "\" }

    $UserHome = $RuntimeHomes | Where-Object {
        # Take the first path that isn't under program files
        !($_.StartsWith($pf) -or $_.StartsWith($pf32))
    } | Select-Object -First 1

    _WriteDebug "Found: $UserHome"
    
    if(!$UserHome) {
        $UserHome = "$env:USERPROFILE\$DefaultUserDirectoryName"
    }
}

_WriteDebug ""
_WriteDebug "=== Running $CommandName ==="
_WriteDebug "Runtime Homes: $RuntimeHomes"
_WriteDebug "User Home: $UserHome"
$AliasesDir = Join-Path $UserHome "alias"
$RuntimesDir = Join-Path $UserHome "runtimes"
$Aliases = $null

### Helper Functions
function GetArch($Architecture, $FallBackArch = $DefaultArchitecture) {
    if(![String]::IsNullOrWhiteSpace($Architecture)) {
        $Architecture
    } elseif($CompatArch) {
        $CompatArch
    } else {
        $FallBackArch
    }
}

function GetRuntime($Runtime) {
    if(![String]::IsNullOrWhiteSpace($Runtime)) {
        $Runtime
    } else {
        $DefaultRuntime
    }
}

function Write-Usage {
    _WriteOut -ForegroundColor $ColorScheme.Banner $AsciiArt
    _WriteOut "$CommandFriendlyName v$FullVersion"
    if(!$Authors.StartsWith("{{")) {
        _WriteOut "By $Authors"
    }
    _WriteOut
    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Header "usage:"
    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Executable " $CommandName"
    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Command " <command>"
    _WriteOut -ForegroundColor $ColorScheme.Help_Argument " [<arguments...>]"
}

function Get-RuntimeAlias {
    if($Aliases -eq $null) {
        _WriteDebug "Scanning for aliases in $AliasesDir"
        if(Test-Path $AliasesDir) {
            $Aliases = @(Get-ChildItem ($UserHome + "\alias\") | Select-Object @{label='Alias';expression={$_.BaseName}}, @{label='Name';expression={Get-Content $_.FullName }})
        } else {
            $Aliases = @()
        }
    }
    $Aliases
}

function IsOnPath {
    param($dir)

    $env:Path.Split(';') -icontains $dir
}

function Get-RuntimeId(
    [Parameter()][string]$Architecture,
    [Parameter()][string]$Runtime) {

    $Architecture = GetArch $Architecture
    $Runtime = GetRuntime $Runtime

    "$RuntimePackageName-$Runtime-win-$Architecture".ToLowerInvariant()
}

function Get-RuntimeName(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter()][string]$Architecture,
    [Parameter()][string]$Runtime) {

    $aliasPath = Join-Path $AliasesDir "$Version$AliasExtension"

    if(Test-Path $aliasPath) {
        $BaseName = Get-Content $aliasPath

        $Architecture = GetArch $Architecture (Get-PackageArch $BaseName)
        $Runtime = GetRuntime $Runtime (Get-PackageArch $BaseName)
        $Version = Get-PackageVersion $BaseName
    }
    
    "$(Get-RuntimeId $Architecture $Runtime).$Version"
}

filter List-Parts {
    param($aliases)

    $binDir = Join-Path $_.FullName "bin"
    if (!(Test-Path $binDir)) {
        return
    }
    $active = IsOnPath $binDir
    
    $fullAlias=""
    $delim=""

    foreach($alias in $aliases) {
        if($_.Name.Split('\', 2) -contains $alias.Name) {
            $fullAlias += $delim + $alias.Alias
            $delim = ", "
        }
    }

    $parts1 = $_.Name.Split('.', 2)
    $parts2 = $parts1[0].Split('-', 4)
    return New-Object PSObject -Property @{
        Active = $active
        Version = $parts1[1]
        Runtime = $parts2[1]
        OperatingSystem = $parts2[2]
        Architecture = $parts2[3]
        Location = $_.Parent.FullName
        Alias = $fullAlias
    }
}

function Read-Alias($Name) {
    _WriteDebug "Listing aliases matching '$Name'"

    $aliases = Get-RuntimeAlias

    $result = @($aliases | Where-Object { !$Name -or ($_.Alias.Contains($Name)) })
    if($Name -and ($result.Length -eq 1)) {
        _WriteOut "Alias '$Name' is set to '$($result[0].Name)'"
    } elseif($Name -and ($result.Length -eq 0)) {
        _WriteOut "Alias does not exist: '$Name'"
        $Script:ExitCode = $ExitCodes.AliasDoesNotExist
    } else {
        $result
    }
}

function Write-Alias {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Version,
        [Parameter(Mandatory=$false)][string]$Architecture,
        [Parameter(Mandatory=$false)][string]$Runtime)

    # If the first character is non-numeric, it's a full runtime name
    if(![Char]::IsDigit($Version[0])) {
        $runtimeFullName = $Version
    } else {
        $runtimeFullName = Get-RuntimeName $Version $Architecture $Runtime
    }
    $aliasFilePath = Join-Path $AliasesDir "$Name.txt"
    $action = if (Test-Path $aliasFilePath) { "Updating" } else { "Setting" }
    
    if(!(Test-Path $AliasesDir)) {
        _WriteDebug "Creating alias directory: $AliasesDir"
        New-Item -Type Directory $AliasesDir | Out-Null
    }
    _WriteOut "$action alias '$Name' to '$runtimeFullName'"
    $runtimeFullName | Out-File $aliasFilePath ascii
}

function Delete-Alias {
    param(
        [Parameter(Mandatory=$true)][string]$Name)

    $aliasPath = Join-Path $AliasesDir "$Name.txt"
    if (Test-Path -literalPath "$aliasPath") {
        _WriteOut "Removing alias $Name"

        # Delete with "-Force" because we already confirmed above
        Remove-Item -literalPath $aliasPath -Force
    } else {
        _WriteOut "Cannot remove alias '$Name'. It does not exist."
        $Script:ExitCode = $ExitCodes.AliasDoesNotExist # Return non-zero exit code for scripting
    }
}

function Apply-Proxy {
param(
  [System.Net.WebClient] $wc,
  [string]$Proxy
)
  if (!$Proxy) {
    $Proxy = $env:http_proxy
  }
  if ($Proxy) {
    $wp = New-Object System.Net.WebProxy($Proxy)
    $pb = New-Object UriBuilder($Proxy)
    if (!$pb.UserName) {
        $wp.Credentials = [System.Net.CredentialCache]::DefaultCredentials
    } else {
        $wp.Credentials = New-Object System.Net.NetworkCredential($pb.UserName, $pb.Password)
    }
    $wc.Proxy = $wp
  }
}

function Find-Latest {
    param(
        [string]$runtime = "",
        [string]$architecture = "",
        [string]$Feed,
        [string]$Proxy
    )
    if(!$Feed) { $Feed = $ActiveFeed }

    _WriteOut "Determining latest version"

    $RuntimeId = Get-RuntimeId -Architecture:"$architecture" -Runtime:"$runtime"
    $url = "$Feed/GetUpdates()?packageIds=%27$RuntimeId%27&versions=%270.0%27&includePrerelease=true&includeAllVersions=false"

    # NOTE: DO NOT use Invoke-WebRequest. It requires PowerShell 4.0!

    $wc = New-Object System.Net.WebClient
    Apply-Proxy $wc -Proxy:$Proxy
    _WriteDebug "Downloading $url ..."
    try {
        [xml]$xml = $wc.DownloadString($url)
    } catch {
        $Script:ExitCode = $ExitCodes.NoRuntimesOnFeed
        throw "Unable to find any runtime packages on the feed!"
    }

    $version = Select-Xml "//d:Version" -Namespace @{d='http://schemas.microsoft.com/ado/2007/08/dataservices'} $xml

    if (![String]::IsNullOrWhiteSpace($version)) {
        _WriteDebug "Found latest version: $version"
        $version
    }
}

function Get-PackageVersion() {
    param(
        [string] $runtimeFullName
    )
    return $runtimeFullName -replace '[^.]*.(.*)', '$1'
}

function Get-PackageRuntime() {
    param(
        [string] $runtimeFullName
    )
    return $runtimeFullName -replace "$RuntimePackageName-([^-]*).*", '$1'
}

function Get-PackageArch() {
    param(
        [string] $runtimeFullName
    )
    return $runtimeFullName -replace "$RuntimePackageName-[^-]*-[^-]*-([^.]*).*", '$1'
}

function Download-Package(
    [string]$Version,
    [string]$Architecture,
    [string]$Runtime,
    [string]$DestinationFile,
    [string]$Feed,
    [string]$Proxy) {

    if(!$Feed) { $Feed = $ActiveFeed }
    
    $url = "$Feed/package/" + (Get-RuntimeId $Architecture $Runtime) + "/" + $Version
    
    _WriteOut "Downloading $runtimeFullName from $feed"
    $wc = New-Object System.Net.WebClient
    try {
      Apply-Proxy $wc -Proxy:$Proxy     
      _WriteDebug "Downloading $url ..."

      Register-ObjectEvent $wc DownloadProgressChanged -SourceIdentifier WebClient.ProgressChanged -action {
        $Global:downloadData = $eventArgs
      } | Out-Null

      Register-ObjectEvent $wc DownloadFileCompleted -SourceIdentifier WebClient.ProgressComplete -action {
        $Global:downloadData = $eventArgs
        $Global:downloadCompleted = $true
      } | Out-Null

      $wc.DownloadFileAsync($url, $DestinationFile)

      while(-not $Global:downloadCompleted){
        $percent = $Global:downloadData.ProgressPercentage
        $totalBytes = $Global:downloadData.TotalBytesToReceive
        $receivedBytes = $Global:downloadData.BytesReceived
        If ($percent -ne $null) {
            Write-Progress -Activity ("Downloading $RuntimeShortFriendlyName from $url") `
                -Status ("Downloaded $($Global:downloadData.BytesReceived) of $($Global:downloadData.TotalBytesToReceive) bytes") `
                -PercentComplete $percent -Id 2 -ParentId 1
        }
      }

      if($Global:downloadData.Error){
        throw "Unable to download package: {0}" -f $Global:downloadData.Error.Message
      }

      Write-Progress -Activity ("Downloading $RuntimeShortFriendlyName from $url") -Id 2 -ParentId 1 -Completed
    }
    finally {
        Remove-Variable downloadData -Scope "Global"
        Remove-Variable downloadCompleted -Scope "Global"
        Unregister-Event -SourceIdentifier WebClient.ProgressChanged
        Unregister-Event -SourceIdentifier WebClient.ProgressComplete
        $wc.Dispose()
    }
}

function Unpack-Package([string]$DownloadFile, [string]$UnpackFolder) {
    _WriteDebug "Unpacking $DownloadFile to $UnpackFolder"

    $compressionLib = [System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem')

    if($compressionLib -eq $null) {
      try {
          # Shell will not recognize nupkg as a zip and throw, so rename it to zip
          $runtimeZip = [System.IO.Path]::ChangeExtension($DownloadFile, "zip")
          Rename-Item $DownloadFile $runtimeZip
          # Use the shell to uncompress the nupkg
          $shell_app=new-object -com shell.application
          $zip_file = $shell_app.namespace($runtimeZip)
          $destination = $shell_app.namespace($UnpackFolder)
          $destination.Copyhere($zip_file.items(), 0x14) #0x4 = don't show UI, 0x10 = overwrite files
      }
      finally {
        # Clean up the package file itself.
        Remove-Item $runtimeZip -Force
      }
    } else {
        [System.IO.Compression.ZipFile]::ExtractToDirectory($DownloadFile, $UnpackFolder)
        
        # Clean up the package file itself.
        Remove-Item $DownloadFile -Force
    }

    If (Test-Path -LiteralPath ($UnpackFolder + "\[Content_Types].xml")) {
        Remove-Item -LiteralPath ($UnpackFolder + "\[Content_Types].xml")
    }
    If (Test-Path ($UnpackFolder + "\_rels\")) {
        Remove-Item -LiteralPath ($UnpackFolder + "\_rels\") -Force -Recurse
    }
    If (Test-Path ($UnpackFolder + "\package\")) {
        Remove-Item -LiteralPath ($UnpackFolder + "\package\") -Force -Recurse
    }
}

function Get-RuntimePath($runtimeFullName) {
    _WriteDebug "Resolving $runtimeFullName"
    foreach($RuntimeHome in $RuntimeHomes) {
        $runtimeBin = "$RuntimeHome\runtimes\$runtimeFullName\bin"
        _WriteDebug " Candidate $runtimeBin"
        if (Test-Path "$runtimeBin") {
            _WriteDebug " Found in $runtimeBin"
            return $runtimeBin
        }
    }
    return $null
}

function Change-Path() {
    param(
        [string] $existingPaths,
        [string] $prependPath,
        [string[]] $removePaths
    )
    _WriteDebug "Updating value to prepend '$prependPath' and remove '$removePaths'"
    
    $newPath = $prependPath
    foreach($portion in $existingPaths.Split(';')) {
        if(![string]::IsNullOrWhiteSpace($portion)) {
            $skip = $portion -eq ""
            foreach($removePath in $removePaths) {
                if(![string]::IsNullOrWhiteSpace($removePath)) {
                    $removePrefix = if($removePath.EndsWith("\")) { $removePath } else { "$removePath\" }

                    if ($removePath -and (($portion -eq $removePath) -or ($portion.StartsWith($removePrefix)))) {
                        _WriteDebug " Removing '$portion' because it matches '$removePath'"
                        $skip = $true
                    }
                }
            }
            if (!$skip) {
                if(![String]::IsNullOrWhiteSpace($newPath)) {
                    $newPath += ";"
                }
                $newPath += $portion
            }
        }
    }
    return $newPath
}

function Set-Path() {
    param(
        [string] $newPath
    )

    $env:PATH = $newPath

    if($CmdPathFile) {
        $Parent = Split-Path -Parent $CmdPathFile
        if(!(Test-Path $Parent)) {
            New-Item -Type Directory $Parent -Force | Out-Null
        }
        _WriteDebug " Writing PATH file for CMD script"
        @"
SET "PATH=$newPath"
"@ | Out-File $CmdPathFile ascii
    }
}

function Ngen-Library(
    [Parameter(Mandatory=$true)]
    [string]$runtimeBin,

    [ValidateSet("x86","x64")]
    [Parameter(Mandatory=$true)]
    [string]$architecture) {

    if ($architecture -eq 'x64') {
        $regView = [Microsoft.Win32.RegistryView]::Registry64
    }
    elseif ($architecture -eq 'x86') {
        $regView = [Microsoft.Win32.RegistryView]::Registry32
    }
    else {
        _WriteOut "Installation does not understand architecture $architecture, skipping ngen..."
        return
    }

    $regHive = [Microsoft.Win32.RegistryHive]::LocalMachine
    $regKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($regHive, $regView)
    $frameworkPath = $regKey.OpenSubKey("SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full").GetValue("InstallPath")
    $ngenExe = Join-Path $frameworkPath 'ngen.exe'

    $ngenCmds = ""
    foreach ($bin in Get-ChildItem $runtimeBin -Filter "Microsoft.CodeAnalysis.CSharp.dll") {
        $ngenCmds += "$ngenExe install $($bin.FullName);"
    }

    $ngenProc = Start-Process "$psHome\powershell.exe" -Verb runAs -ArgumentList "-ExecutionPolicy unrestricted & $ngenCmds" -Wait -PassThru -WindowStyle Hidden
}

function Is-Elevated() {
    $user = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    return $user.IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}

### Commands

<#
.SYNOPSIS
    Displays a list of commands, and help for specific commands
.PARAMETER Command
    A specific command to get help for
#>
function dnvm-help {
    [CmdletBinding(DefaultParameterSetName="GeneralHelp")]
    param(
        [Parameter(Mandatory=$true,Position=0,ParameterSetName="SpecificCommand")][string]$Command,
        [switch]$PassThru)

    if($Command) {
        $cmd = Get-Command "dnvm-$Command" -ErrorAction SilentlyContinue
        if(!$cmd) {
            _WriteOut "No such command: $Command"
            dnvm-help
            $Script:ExitCodes = $ExitCodes.UnknownCommand
            return
        }
        $help = Get-Help "dnvm-$Command"
        if($PassThru) {
            $help
        } else {
            _WriteOut -ForegroundColor $ColorScheme.Help_Header "$CommandName-$Command"
            _WriteOut "  $($help.Synopsis.Trim())"
            _WriteOut
            _WriteOut -ForegroundColor $ColorScheme.Help_Header "usage:"
            $help.Syntax.syntaxItem | ForEach-Object {
                _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Executable "  $CommandName "
                _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Command "$Command"
                if($_.parameter) {
                    $_.parameter | ForEach-Object {
                        $cmdParam = $cmd.Parameters[$_.name]
                        $name = $_.name
                        if($cmdParam.Aliases.Length -gt 0) {
                            $name = $cmdParam.Aliases | Sort-Object | Select-Object -First 1
                        }

                        _WriteOut -NoNewLine " "
                        
                        if($_.required -ne "true") {
                            _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Optional "["
                        }

                        if($_.position -eq "Named") {
                            _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Switch "-$name"
                        }
                        if($_.parameterValue) {
                            if($_.position -eq "Named") {
                                _WriteOut -NoNewLine " "       
                            }
                            _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Argument "<$($_.name)>"
                        }

                        if($_.required -ne "true") {
                            _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Optional "]"
                        }
                    }
                }
                _WriteOut
            }

            if($help.parameters -and $help.parameters.parameter) {
                _WriteOut
                _WriteOut -ForegroundColor $ColorScheme.Help_Header "options:"
                $help.parameters.parameter | ForEach-Object {
                    $cmdParam = $cmd.Parameters[$_.name]
                    $name = $_.name
                    if($cmdParam.Aliases.Length -gt 0) {
                        $name = $cmdParam.Aliases | Sort-Object | Select-Object -First 1
                    }
                    
                    _WriteOut -NoNewLine "  "
                    
                    if($_.position -eq "Named") {
                        _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Switch "-$name".PadRight($OptionPadding)
                    } else {
                        _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Argument "<$($_.name)>".PadRight($OptionPadding)
                    }
                    _WriteOut " $($_.description.Text)"
                }
            }

            if($help.description) {
                _WriteOut
                _WriteOut -ForegroundColor $ColorScheme.Help_Header "remarks:"
                $help.description.Text.Split(@("`r","`n"), "RemoveEmptyEntries") | 
                    ForEach-Object { _WriteOut "  $_" }
            }

            if($DeprecatedCommands -contains $Command) {
                _WriteOut "This command has been deprecated and should not longer be used"
            }
        }
    } else {
        Write-Usage
        _WriteOut
        _WriteOut -ForegroundColor $ColorScheme.Help_Header "commands: "
        Get-Command "$CommandPrefix*" | 
            ForEach-Object {
                $h = Get-Help $_.Name
                $name = $_.Name.Substring($CommandPrefix.Length)
                if($DeprecatedCommands -notcontains $name) {
                    _WriteOut -NoNewLine "    "
                    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Command $name.PadRight(10)
                    _WriteOut " $($h.Synopsis.Trim())"
                }
            }
    }
}

<#
.SYNOPSIS
    Lists available runtimes
.PARAMETER PassThru
    Set this switch to return unformatted powershell objects for use in scripting
#>
function dnvm-list {
    param(
        [Parameter(Mandatory=$false)][switch]$PassThru)
    $aliases = Get-RuntimeAlias

    $items = @()
    $RuntimeHomes | ForEach-Object {
        _WriteDebug "Scanning $_ for runtimes..."
        if (Test-Path "$_\runtimes") {
            $items += Get-ChildItem "$_\runtimes\$RuntimePackageName-*" | List-Parts $aliases
        }
    }

    if($PassThru) {
        $items
    } else {
        $items | 
            Sort-Object Version, Runtime, Architecture, Alias | 
            Format-Table -AutoSize -Property @{name="Active";expression={if($_.Active) { "*" } else { "" }};alignment="center"}, "Version", "Runtime", "Architecture", "Location", "Alias"
    }
}

<#
.SYNOPSIS
    Lists and manages aliases
.PARAMETER Name
    The name of the alias to read/create/delete
.PARAMETER Version
    The version to assign to the new alias
.PARAMETER Architecture
    The architecture of the runtime to assign to this alias
.PARAMETER Runtime
    The flavor of the runtime to assign to this alias
.PARAMETER Delete
    Set this switch to delete the alias with the specified name
.DESCRIPTION
    If no arguments are provided, this command lists all aliases. If <Name> is provided,
    the value of that alias, if present, is displayed. If <Name> and <Version> are
    provided, the alias <Name> is set to the runtime defined by <Version>, <Architecture>
    (defaults to 'x86') and <Runtime> (defaults to 'clr').

    Finally, if the '-d' switch is provided, the alias <Name> is deleted, if it exists.
#>
function dnvm-alias {
    param(
        [Alias("d")]
        [Parameter(ParameterSetName="Delete",Mandatory=$true)]
        [switch]$Delete,

        [Parameter(ParameterSetName="Read",Mandatory=$false,Position=0)]
        [Parameter(ParameterSetName="Write",Mandatory=$true,Position=0)]
        [Parameter(ParameterSetName="Delete",Mandatory=$true,Position=0)]
        [string]$Name,
        
        [Parameter(ParameterSetName="Write",Mandatory=$true,Position=1)]
        [string]$Version,

        [Alias("arch")]
        [ValidateSet("", "x86","x64")]
        [Parameter(ParameterSetName="Write", Mandatory=$false)]
        [string]$Architecture = "",

        [Alias("r")]
        [ValidateSet("", "clr","coreclr")]
        [Parameter(ParameterSetName="Write")]
        [string]$Runtime = "")

    switch($PSCmdlet.ParameterSetName) {
        "Read" { Read-Alias $Name }
        "Write" { Write-Alias $Name $Version -Architecture $Architecture -Runtime $Runtime }
        "Delete" { Delete-Alias $Name }
    }
}

<#
.SYNOPSIS
    [DEPRECATED] Removes an alias
.PARAMETER Name
    The name of the alias to remove
#>
function dnvm-unalias {
    param(
        [Parameter(Mandatory=$true,Position=0)][string]$Name)
    _WriteOut "This command has been deprecated. Use '$CommandName alias -d' instead"
    dnvm-alias -Delete -Name $Name
}

<#
.SYNOPSIS
    Installs the latest version of the runtime and reassigns the specified alias to point at it
.PARAMETER Alias
    The alias to upgrade (default: 'default')
.PARAMETER Architecture
    The processor architecture of the runtime to install (default: x86)
.PARAMETER Runtime
    The runtime flavor to install (default: clr)
.PARAMETER Force
    Overwrite an existing runtime if it already exists
.PARAMETER Proxy
    Use the given address as a proxy when accessing remote server
.PARAMETER NoNative
    Skip generation of native images
.PARAMETER Ngen
    For CLR flavor only. Generate native images for runtime libraries on Desktop CLR to improve startup time. This option requires elevated privilege and will be automatically turned on if the script is running in administrative mode. To opt-out in administrative mode, use -NoNative switch.
#>
function dnvm-upgrade {
    param(
        [Alias("a")]
        [Parameter(Mandatory=$false, Position=0)]
        [string]$Alias = "default",

        [Alias("arch")]
        [ValidateSet("", "x86","x64")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [Alias("r")]
        [ValidateSet("", "clr","coreclr")]
        [Parameter(Mandatory=$false)]
        [string]$Runtime = "",

        [Alias("f")]
        [Parameter(Mandatory=$false)]
        [switch]$Force,

        [Parameter(Mandatory=$false)]
        [string]$Proxy,

        [Parameter(Mandatory=$false)]
        [switch]$NoNative,

        [Parameter(Mandatory=$false)]
        [switch]$Ngen)

    dnvm-install "latest" -Alias:$Alias -Architecture:$Architecture -Runtime:$Runtime -Force:$Force -Proxy:$Proxy -NoNative:$NoNative -Ngen:$Ngen -Persistent:$true
}

<#
.SYNOPSIS
    Installs a version of the runtime
.PARAMETER VersionNuPkgOrAlias
    The version to install from the current channel, the path to a '.nupkg' file to install, 'latest' to
    install the latest available version from the current channel, or an alias value to install an alternate
    runtime or architecture flavor of the specified alias.
.PARAMETER Architecture
    The processor architecture of the runtime to install (default: x86)
.PARAMETER Runtime
    The runtime flavor to install (default: clr)
.PARAMETER Alias
    Set alias <Alias> to the installed runtime
.PARAMETER Force
    Overwrite an existing runtime if it already exists
.PARAMETER Proxy
    Use the given address as a proxy when accessing remote server
.PARAMETER NoNative
    Skip generation of native images
.PARAMETER Ngen
    For CLR flavor only. Generate native images for runtime libraries on Desktop CLR to improve startup time. This option requires elevated privilege and will be automatically turned on if the script is running in administrative mode. To opt-out in administrative mode, use -NoNative switch.
.PARAMETER Persistent
    Make the installed runtime useable across all processes run by the current user
.DESCRIPTION
    A proxy can also be specified by using the 'http_proxy' environment variable

#>
function dnvm-install {
    param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$VersionNuPkgOrAlias,

        [Alias("arch")]
        [ValidateSet("", "x86","x64")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [Alias("r")]
        [ValidateSet("", "clr","coreclr")]
        [Parameter(Mandatory=$false)]
        [string]$Runtime = "",

        [Alias("a")]
        [Parameter(Mandatory=$false)]
        [string]$Alias,

        [Alias("f")]
        [Parameter(Mandatory=$false)]
        [switch]$Force,

        [Parameter(Mandatory=$false)]
        [string]$Proxy,

        [Parameter(Mandatory=$false)]
        [switch]$NoNative,

        [Parameter(Mandatory=$false)]
        [switch]$Ngen,

        [Parameter(Mandatory=$false)]
        [switch]$Persistent)

    if(!$VersionNuPkgOrAlias) {
        _WriteOut "A version, nupkg path, or the string 'latest' must be provided."
        dnvm-help install
        $Script:ExitCode = $ExitCodes.InvalidArguments
        return
    }

    if ($VersionNuPkgOrAlias -eq "latest") {
        Write-Progress -Activity "Installing runtime" "Determining latest runtime" -Id 1
        $VersionNuPkgOrAlias = Find-Latest $Runtime $Architecture
    }

    $IsNuPkg = $VersionNuPkgOrAlias.EndsWith(".nupkg")

    if ($IsNuPkg) {
        if(!(Test-Path $VersionNuPkgOrAlias)) {
            throw "Unable to locate package file: '$VersionNuPkgOrAlias'"
        }
        Write-Progress -Activity "Installing runtime" "Parsing package file name" -Id 1
        $runtimeFullName = [System.IO.Path]::GetFileNameWithoutExtension($VersionNuPkgOrAlias)
        $Architecture = Get-PackageArch $runtimeFullName
        $Runtime = Get-PackageRuntime $runtimeFullName
    } else {
        $runtimeFullName = Get-RuntimeName $VersionNuPkgOrAlias -Architecture:$Architecture -Runtime:$Runtime
    }

    $PackageVersion = Get-PackageVersion $runtimeFullName
    
    _WriteDebug "Preparing to install runtime '$runtimeFullName'"
    _WriteDebug "Architecture: $Architecture"
    _WriteDebug "Runtime: $Runtime"
    _WriteDebug "Version: $PackageVersion"

    $RuntimeFolder = Join-Path $RuntimesDir $runtimeFullName
    _WriteDebug "Destination: $RuntimeFolder"

    if((Test-Path $RuntimeFolder) -and $Force) {
        _WriteOut "Cleaning existing installation..."
        Remove-Item $RuntimeFolder -Recurse -Force
    }

    if(Test-Path $RuntimeFolder) {
        _WriteOut "'$runtimeFullName' is already installed."
    }
    else {
        $Architecture = GetArch $Architecture
        $Runtime = GetRuntime $Runtime
        $UnpackFolder = Join-Path $RuntimesDir "temp"
        $DownloadFile = Join-Path $UnpackFolder "$runtimeFullName.nupkg"

        if(Test-Path $UnpackFolder) {
            _WriteDebug "Cleaning temporary directory $UnpackFolder"
            Remove-Item $UnpackFolder -Recurse -Force
        }
        New-Item -Type Directory $UnpackFolder | Out-Null

        if($IsNuPkg) {
            Write-Progress -Activity "Installing runtime" "Copying package" -Id 1
            _WriteDebug "Copying local nupkg $VersionNuPkgOrAlias to $DownloadFile"
            Copy-Item $VersionNuPkgOrAlias $DownloadFile
        } else {
            # Download the package
            Write-Progress -Activity "Installing runtime" "Downloading runtime" -Id 1
            _WriteDebug "Downloading version $VersionNuPkgOrAlias to $DownloadFile"
            Download-Package $PackageVersion $Architecture $Runtime $DownloadFile -Proxy:$Proxy
        }

        Write-Progress -Activity "Installing runtime" "Unpacking runtime" -Id 1
        Unpack-Package $DownloadFile $UnpackFolder

        New-Item -Type Directory $RuntimeFolder -Force | Out-Null
        _WriteOut "Installing to $RuntimeFolder"
        _WriteDebug "Moving package contents to $RuntimeFolder"
        Move-Item "$UnpackFolder\*" $RuntimeFolder
        _WriteDebug "Cleaning temporary directory $UnpackFolder"
        Remove-Item $UnpackFolder -Force | Out-Null

        dnvm-use $PackageVersion -Architecture:$Architecture -Runtime:$Runtime -Persistent:$Persistent

        if ($Runtime -eq "clr") {
            if (-not $NoNative) {
                if ((Is-Elevated) -or $Ngen) {
                    $runtimeBin = Get-RuntimePath $runtimeFullName
                    Write-Progress -Activity "Installing runtime" "Generating runtime native images" -Id 1
                    Ngen-Library $runtimeBin $Architecture
                }
                else {
                    _WriteOut "Native image generation (ngen) is skipped. Include -Ngen switch to turn on native image generation to improve application startup time."
                }
            }
        }
        elseif ($Runtime -eq "coreclr") {
            if ($NoNative) {
                _WriteOut "Skipping native image compilation."
            }
            else {
                _WriteOut "Compiling native images for $runtimeFullName to improve startup performance..."
                Write-Progress -Activity "Installing runtime" "Generating runtime native images" -Id 1
                if ($DebugPreference -eq 'SilentlyContinue') {
                    Start-Process $CrossGenCommand -Wait -WindowStyle Hidden
                }
                else {
                    Start-Process $CrossGenCommand -Wait -NoNewWindow
                }
                _WriteOut "Finished native image compilation."
            }
        }
        else {
            _WriteOut "Unexpected platform: $Runtime. No optimization would be performed on the package installed."
        }
    }

    if($Alias) {
        _WriteDebug "Aliasing installed runtime to '$Alias'"
        dnvm-alias $Alias $PackageVersion -Architecture:$Architecture -Runtime:$Runtime
    }

    Write-Progress -Activity "Install complete" -Id 1 -Complete
}


<#
.SYNOPSIS
    Adds a runtime to the PATH environment variable for your current shell
.PARAMETER VersionOrAlias
    The version or alias of the runtime to place on the PATH
.PARAMETER Architecture
    The processor architecture of the runtime to place on the PATH (default: x86, or whatever the alias specifies in the case of use-ing an alias)
.PARAMETER Runtime
    The runtime flavor of the runtime to place on the PATH (default: clr, or whatever the alias specifies in the case of use-ing an alias)
.PARAMETER Persistent
    Make the change persistent across all processes run by the current user
#>
function dnvm-use {
    param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$VersionOrAlias,

        [Alias("arch")]
        [ValidateSet("", "x86","x64")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [Alias("r")]
        [ValidateSet("", "clr","coreclr")]
        [Parameter(Mandatory=$false)]
        [string]$Runtime = "",

        [Alias("p")]
        [Parameter(Mandatory=$false)]
        [switch]$Persistent)

    if([String]::IsNullOrWhiteSpace($VersionOrAlias)) {
        _WriteOut "Missing version or alias to add to path"
        dnvm-help use
        $Script:ExitCode = $ExitCodes.InvalidArguments
        return
    }

    if ($versionOrAlias -eq "none") {
        _WriteOut "Removing all runtimes from process PATH"
        Set-Path (Change-Path $env:Path "" ($RuntimeDirs))

        if ($Persistent) {
            _WriteOut "Removing all runtimes from user PATH"
            $userPath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
            $userPath = Change-Path $userPath "" ($RuntimeDirs)
            [Environment]::SetEnvironmentVariable("Path", $userPath, [System.EnvironmentVariableTarget]::User)
        }
        return;
    }

    $runtimeFullName = Get-RuntimeName $VersionOrAlias $Architecture $Runtime
    $runtimeBin = Get-RuntimePath $runtimeFullName
    if ($runtimeBin -eq $null) {
        throw "Cannot find $runtimeFullName, do you need to run '$CommandName install $versionOrAlias'?"
    }

    _WriteOut "Adding $runtimeBin to process PATH"
    Set-Path (Change-Path $env:Path $runtimeBin ($RuntimeDirs))

    if ($Persistent) {
        _WriteOut "Adding $runtimeBin to user PATH"
        $userPath = [Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::User)
        $userPath = Change-Path $userPath $runtimeBin ($RuntimeDirs)
        [Environment]::SetEnvironmentVariable("Path", $userPath, [System.EnvironmentVariableTarget]::User)
    }
}

<#
.SYNOPSIS
    Gets the full name of a runtime
.PARAMETER VersionOrAlias
    The version or alias of the runtime to place on the PATH
.PARAMETER Architecture
    The processor architecture of the runtime to place on the PATH (default: x86, or whatever the alias specifies in the case of use-ing an alias)
.PARAMETER Runtime
    The runtime flavor of the runtime to place on the PATH (default: clr, or whatever the alias specifies in the case of use-ing an alias)
#>
function dnvm-name {
    param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$VersionOrAlias,

        [Alias("arch")]
        [ValidateSet("x86","x64")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [Alias("r")]
        [ValidateSet("clr","coreclr")]
        [Parameter(Mandatory=$false)]
        [string]$Runtime = "")

    Get-RuntimeName $VersionOrAlias $Architecture $Runtime
}


# Checks if a specified file exists in the destination folder and if not, copies the file
# to the destination folder. 
function Safe-Filecopy {
    param(
        [Parameter(Mandatory=$true, Position=0)] $Filename, 
        [Parameter(Mandatory=$true, Position=1)] $SourceFolder,
        [Parameter(Mandatory=$true, Position=2)] $DestinationFolder)

    # Make sure the destination folder is created if it doesn't already exist.
    if(!(Test-Path $DestinationFolder)) {
        _WriteOut "Creating destination folder '$DestinationFolder' ... "
        
        New-Item -Type Directory $Destination | Out-Null
    }

    $sourceFilePath = Join-Path $SourceFolder $Filename
    $destFilePath = Join-Path $DestinationFolder $Filename

    if(Test-Path $sourceFilePath) {
        _WriteOut "Installing '$Filename' to '$DestinationFolder' ... "

        if (Test-Path $destFilePath) {
            _WriteOut "  Skipping: file already exists" -ForegroundColor Yellow
        }
        else {
            Copy-Item $sourceFilePath $destFilePath -Force
        }
    }
    else {
        _WriteOut "WARNING: Unable to install: Could not find '$Filename' in '$SourceFolder'. " 
    }
}

<#
.SYNOPSIS
    Installs the version manager into your User profile directory
.PARAMETER SkipUserEnvironmentInstall
    Set this switch to skip configuring the user-level DNX_HOME and PATH environment variables
#>
function dnvm-setup {
    param(
        [switch]$SkipUserEnvironmentInstall)

    $DestinationHome = "$env:USERPROFILE\$DefaultUserDirectoryName"

    # Install scripts
    $Destination = "$DestinationHome\bin"
    _WriteOut "Installing $CommandFriendlyName to $Destination"

    $ScriptFolder = Split-Path -Parent $ScriptPath

    # Copy script files (if necessary):
    Safe-Filecopy "$CommandName.ps1" $ScriptFolder $Destination
    Safe-Filecopy "$CommandName.cmd" $ScriptFolder $Destination

    # Configure Environment Variables
    # Also, clean old user home values if present
    # We'll be removing any existing homes, both
    $PathsToRemove = @(
        "%USERPROFILE%\$DefaultUserDirectoryName",
        [Environment]::ExpandEnvironmentVariables($OldUserHome),
        $DestinationHome,
        $OldUserHome)

    # First: PATH
    _WriteOut "Adding $Destination to Process PATH"
    Set-Path (Change-Path $env:PATH $Destination $PathsToRemove)

    if(!$SkipUserEnvironmentInstall) {
        _WriteOut "Adding $Destination to User PATH"
        $userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
        $userPath = Change-Path $userPath $Destination $PathsToRemove
        [Environment]::SetEnvironmentVariable("PATH", $userPath, "User")
    }

    # Now the HomeEnvVar
    _WriteOut "Adding $DestinationHome to Process $HomeEnvVar"
    $processHome = ""
    if(Test-Path "env:\$HomeEnvVar") {
        $processHome = cat "env:\$HomeEnvVar"
    }
    $processHome = Change-Path $processHome "%USERPROFILE%\$DefaultUserDirectoryName" $PathsToRemove
    Set-Content "env:\$HomeEnvVar" $processHome

    if(!$SkipUserEnvironmentInstall) {
        _WriteOut "Adding $DestinationHome to User $HomeEnvVar"
        $userHomeVal = [Environment]::GetEnvironmentVariable($HomeEnvVar, "User")
        $userHomeVal = Change-Path $userHomeVal "%USERPROFILE%\$DefaultUserDirectoryName" $PathsToRemove
        [Environment]::SetEnvironmentVariable($HomeEnvVar, $userHomeVal, "User")
    }
}

### The main "entry point"

# Check for old DNX_HOME values
if($UnencodedHomes -contains $OldUserHome) {
    _WriteOut -ForegroundColor Yellow "WARNING: Found '$OldUserHome' in your $HomeEnvVar value. This folder has been deprecated."
    if($UnencodedHomes -notcontains $DefaultUserHome) {
        _WriteOut -ForegroundColor Yellow "WARNING: Didn't find '$DefaultUserHome' in your $HomeEnvVar value. You should run '$CommandName setup' to upgrade."
    }
}

# Check for old KRE_HOME variable
if(Test-Path env:\KRE_HOME) {
    _WriteOut -ForegroundColor Yellow "WARNING: Found a KRE_HOME environment variable. This variable has been deprecated and should be removed, or it may interfere with DNVM and the .NET Execution environment"
}

# Read arguments

$cmd = $args[0]

if($args.Length -gt 1) {
    $cmdargs = @($args[1..($args.Length-1)])
} else {
    $cmdargs = @()
}

# Can't add this as script-level arguments because they mask '-a' arguments in subcommands!
# So we manually parse them :)
if($cmdargs -icontains "-amd64") {
    $CompatArch = "x64"
    _WriteOut "The -amd64 switch has been deprecated. Use the '-arch x64' parameter instead"
} elseif($cmdargs -icontains "-x86") {
    $CompatArch = "x86"
    _WriteOut "The -x86 switch has been deprecated. Use the '-arch x86' parameter instead"
} elseif($cmdargs -icontains "-x64") {
    $CompatArch = "x64"
    _WriteOut "The -x64 switch has been deprecated. Use the '-arch x64' parameter instead"
}
$cmdargs = @($cmdargs | Where-Object { @("-amd64", "-x86", "-x64") -notcontains $_ })

if(!$cmd) {
    _WriteOut "You must specify a command!"
    $cmd = "help"
    $Script:ExitCode = $ExitCodes.InvalidArguments
}

# Check for the command and run it
try {
    if(Get-Command -Name "$CommandPrefix$cmd" -ErrorAction SilentlyContinue) {
        _WriteDebug "& dnvm-$cmd $cmdargs"
        & "dnvm-$cmd" @cmdargs
    }
    else {
        _WriteOut "Unknown command: '$cmd'"
        dnvm-help
        $Script:ExitCode = $ExitCodes.UnknownCommand
    }
} catch {
    Write-Error $_
    if(!$Script:ExitCode) { $Script:ExitCode = $ExitCodes.OtherError }
}

_WriteDebug "=== End $CommandName (Exit Code $Script:ExitCode) ==="
_WriteDebug ""
exit $Script:ExitCode

# SIG # Begin signature block
# MIIkCwYJKoZIhvcNAQcCoIIj/DCCI/gCAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCBQsFhkTzOSfM2+
# OwyQ+jl51kW9i3tAfGcKiUFUoNnZvqCCDZIwggYQMIID+KADAgECAhMzAAAAOI0j
# bRYnoybgAAAAAAA4MA0GCSqGSIb3DQEBCwUAMH4xCzAJBgNVBAYTAlVTMRMwEQYD
# VQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNy
# b3NvZnQgQ29ycG9yYXRpb24xKDAmBgNVBAMTH01pY3Jvc29mdCBDb2RlIFNpZ25p
# bmcgUENBIDIwMTEwHhcNMTQxMDAxMTgxMTE2WhcNMTYwMTAxMTgxMTE2WjCBgzEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjENMAsGA1UECxMETU9Q
# UjEeMBwGA1UEAxMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMIIBIjANBgkqhkiG9w0B
# AQEFAAOCAQ8AMIIBCgKCAQEAwt7Wz+K3fxFl/7NjqfNyufEk61+kHLJEWetvnPtw
# 22VpmquQMV7/3itkEfXtbOkAIYLDkMyCGaPjmWNlir3T1fsgo+AZf7iNPGr+yBKN
# 5dM5701OPoaWTBGxEYSbJ5iIOy3UfRjzBeCtSwQ+Q3UZ5kbEjJ3bidgkh770Rye/
# bY3ceLnDZaFvN+q8caadrI6PjYiRfqg3JdmBJKmI9GNG6rsgyQEv2I4M2dnt4Db7
# ZGhN/EIvkSCpCJooSkeo8P7Zsnr92Og4AbyBRas66Boq3TmDPwfb2OGP/DksNp4B
# n+9od8h4bz74IP+WGhC+8arQYZ6omoS/Pq6vygpZ5Y2LBQIDAQABo4IBfzCCAXsw
# HwYDVR0lBBgwFgYIKwYBBQUHAwMGCisGAQQBgjdMCAEwHQYDVR0OBBYEFMbxyhgS
# CySlRfWC5HUl0C8w12JzMFEGA1UdEQRKMEikRjBEMQ0wCwYDVQQLEwRNT1BSMTMw
# MQYDVQQFEyozMTY0MitjMjJjOTkzNi1iM2M3LTQyNzEtYTRiZC1mZTAzZmE3MmMz
# ZjAwHwYDVR0jBBgwFoAUSG5k5VAF04KqFzc3IrVtqMp1ApUwVAYDVR0fBE0wSzBJ
# oEegRYZDaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraW9wcy9jcmwvTWljQ29k
# U2lnUENBMjAxMV8yMDExLTA3LTA4LmNybDBhBggrBgEFBQcBAQRVMFMwUQYIKwYB
# BQUHMAKGRWh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lvcHMvY2VydHMvTWlj
# Q29kU2lnUENBMjAxMV8yMDExLTA3LTA4LmNydDAMBgNVHRMBAf8EAjAAMA0GCSqG
# SIb3DQEBCwUAA4ICAQCecm6ourY1Go2EsDqVN+I0zXvsz1Pk7qvGGDEWM3tPIv6T
# dVZHTXRrmYdcLnSIcKVGb7ScG5hZEk00vtDcdbNdDDPW2AX2NRt+iUjB5YmlLTo3
# J0ce7mjTaFpGoqyF+//Q6OjVYFXnRGtNz73epdy71XqL0+NIx0Z7dZhz+cPI7IgQ
# C/cqLRN4Eo/+a6iYXhxJzjqmNJZi2+7m4wzZG2PH+hhh7LkACKvkzHwSpbamvWVg
# Dh0zWTjfFuEyXH7QexIHgbR+uKld20T/ZkyeQCapTP5OiT+W0WzF2K7LJmbhv2Xj
# 97tj+qhtKSodJ8pOJ8q28Uzq5qdtCrCRLsOEfXKAsfg+DmDZzLsbgJBPixGIXncI
# u+OKq39vCT4rrGfBR+2yqF16PLAF9WCK1UbwVlzypyuwLhEWr+KR0t8orebVlT/4
# uPVr/wLnudvNvP2zQMBxrkadjG7k9gVd7O4AJ4PIRnvmwjrh7xy796E3RuWGq5eu
# dXp27p5LOwbKH6hcrI0VOSHmveHCd5mh9yTx2TgeTAv57v+RbbSKSheIKGPYUGNc
# 56r7VYvEQYM3A0ABcGOfuLD5aEdfonKLCVMOP7uNQqATOUvCQYMvMPhbJvgfuS1O
# eQy77Hpdnzdq2Uitdp0v6b5sNlga1ZL87N/zsV4yFKkTE/Upk/XJOBbXNedrODCC
# B3owggVioAMCAQICCmEOkNIAAAAAAAMwDQYJKoZIhvcNAQELBQAwgYgxCzAJBgNV
# BAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
# HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xMjAwBgNVBAMTKU1pY3Jvc29m
# dCBSb290IENlcnRpZmljYXRlIEF1dGhvcml0eSAyMDExMB4XDTExMDcwODIwNTkw
# OVoXDTI2MDcwODIxMDkwOVowfjELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hp
# bmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jw
# b3JhdGlvbjEoMCYGA1UEAxMfTWljcm9zb2Z0IENvZGUgU2lnbmluZyBQQ0EgMjAx
# MTCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAKvw+nIQHC6t2G6qghBN
# NLrytlghn0IbKmvpWlCquAY4GgRJun/DDB7dN2vGEtgL8DjCmQawyDnVARQxQtOJ
# DXlkh36UYCRsr55JnOloXtLfm1OyCizDr9mpK656Ca/XllnKYBoF6WZ26DJSJhIv
# 56sIUM+zRLdd2MQuA3WraPPLbfM6XKEW9Ea64DhkrG5kNXimoGMPLdNAk/jj3gcN
# 1Vx5pUkp5w2+oBN3vpQ97/vjK1oQH01WKKJ6cuASOrdJXtjt7UORg9l7snuGG9k+
# sYxd6IlPhBryoS9Z5JA7La4zWMW3Pv4y07MDPbGyr5I4ftKdgCz1TlaRITUlwzlu
# ZH9TupwPrRkjhMv0ugOGjfdf8NBSv4yUh7zAIXQlXxgotswnKDglmDlKNs98sZKu
# HCOnqWbsYR9q4ShJnV+I4iVd0yFLPlLEtVc/JAPw0XpbL9Uj43BdD1FGd7P4AOG8
# rAKCX9vAFbO9G9RVS+c5oQ/pI0m8GLhEfEXkwcNyeuBy5yTfv0aZxe/CHFfbg43s
# TUkwp6uO3+xbn6/83bBm4sGXgXvt1u1L50kppxMopqd9Z4DmimJ4X7IvhNdXnFy/
# dygo8e1twyiPLI9AN0/B4YVEicQJTMXUpUMvdJX3bvh4IFgsE11glZo+TzOE2rCI
# F96eTvSWsLxGoGyY0uDWiIwLAgMBAAGjggHtMIIB6TAQBgkrBgEEAYI3FQEEAwIB
# ADAdBgNVHQ4EFgQUSG5k5VAF04KqFzc3IrVtqMp1ApUwGQYJKwYBBAGCNxQCBAwe
# CgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8GA1UdEwEB/wQFMAMBAf8wHwYDVR0j
# BBgwFoAUci06AjGQQ7kUBU7h6qfHMdEjiTQwWgYDVR0fBFMwUTBPoE2gS4ZJaHR0
# cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMvTWljUm9vQ2Vy
# QXV0MjAxMV8yMDExXzAzXzIyLmNybDBeBggrBgEFBQcBAQRSMFAwTgYIKwYBBQUH
# MAKGQmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljUm9vQ2Vy
# QXV0MjAxMV8yMDExXzAzXzIyLmNydDCBnwYDVR0gBIGXMIGUMIGRBgkrBgEEAYI3
# LgMwgYMwPwYIKwYBBQUHAgEWM2h0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2lv
# cHMvZG9jcy9wcmltYXJ5Y3BzLmh0bTBABggrBgEFBQcCAjA0HjIgHQBMAGUAZwBh
# AGwAXwBwAG8AbABpAGMAeQBfAHMAdABhAHQAZQBtAGUAbgB0AC4gHTANBgkqhkiG
# 9w0BAQsFAAOCAgEAZ/KGpZjgVHkaLtPYdGcimwuWEeFjkplCln3SeQyQwWVfLiw+
# +MNy0W2D/r4/6ArKO79HqaPzadtjvyI1pZddZYSQfYtGUFXYDJJ80hpLHPM8QotS
# 0LD9a+M+By4pm+Y9G6XUtR13lDni6WTJRD14eiPzE32mkHSDjfTLJgJGKsKKELuk
# qQUMm+1o+mgulaAqPyprWEljHwlpblqYluSD9MCP80Yr3vw70L01724lruWvJ+3Q
# 3fMOr5kol5hNDj0L8giJ1h/DMhji8MUtzluetEk5CsYKwsatruWy2dsViFFFWDgy
# cScaf7H0J/jeLDogaZiyWYlobm+nt3TDQAUGpgEqKD6CPxNNZgvAs0314Y9/HG8V
# fUWnduVAKmWjw11SYobDHWM2l4bf2vP48hahmifhzaWX0O5dY0HjWwechz4GdwbR
# BrF1HxS+YWG18NzGGwS+30HHDiju3mUv7Jf2oVyW2ADWoUa9WfOXpQlLSBCZgB/Q
# ACnFsZulP0V3HjXG0qKin3p6IvpIlR+r+0cjgPWe+L9rt0uX4ut1eBrs6jeZeRhL
# /9azI2h15q/6/IvrC4DqaTuv/DDtBEyO3991bWORPdGdVk5Pv4BXIqF4ETIheu9B
# CrE/+6jMpF3BoYibV3FWTkhFwELJm3ZbCoBIa/15n8G9bW1qyVJzEw16UM0xghXP
# MIIVywIBATCBlTB+MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQ
# MA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u
# MSgwJgYDVQQDEx9NaWNyb3NvZnQgQ29kZSBTaWduaW5nIFBDQSAyMDExAhMzAAAA
# OI0jbRYnoybgAAAAAAA4MA0GCWCGSAFlAwQCAQUAoIG6MBkGCSqGSIb3DQEJAzEM
# BgorBgEEAYI3AgEEMBwGCisGAQQBgjcCAQsxDjAMBgorBgEEAYI3AgEVMC8GCSqG
# SIb3DQEJBDEiBCAj0MJSOotVO6CflbdVx7d/z2KbTtUmc2tEO2xfMLWVojBOBgor
# BgEEAYI3AgEMMUAwPqAkgCIATQBpAGMAcgBvAHMAbwBmAHQAIABBAFMAUAAuAE4A
# RQBUoRaAFGh0dHA6Ly93d3cuYXNwLm5ldC8gMA0GCSqGSIb3DQEBAQUABIIBAIn7
# dzL8pibaK9qBu0YGvZ6Z2vHJ6ywwVwMFtFlP68gPEPuWIN/KFLPF1mPdBLK67GB8
# EcWe3H8YKWmtr7NFirA1sJyd9DsrGl4VqkmE2WOQmlsBGTMV65z+A2lat3YuCo6G
# qEjBF6E4Sfpqh3dj0ZmEN5QKbkPnZhAvUE2XXbUAC0hB4necaiXgXort8FjMogeL
# LmvYgRB0xxce0pZMjaVSP9dSLboad74vbhX5jGI+KNQLHmMls7MA/QRPmomORbi+
# HJPQT+Hm7davdraWPOwSkFZcqFwnkAeTdqrrTzT2THQR0pfCNFTrqyHO/Xzsv7FL
# fGuxHK+2FyrLv/OWD76hghNNMIITSQYKKwYBBAGCNwMDATGCEzkwghM1BgkqhkiG
# 9w0BBwKgghMmMIITIgIBAzEPMA0GCWCGSAFlAwQCAQUAMIIBPQYLKoZIhvcNAQkQ
# AQSgggEsBIIBKDCCASQCAQEGCisGAQQBhFkKAwEwMTANBglghkgBZQMEAgEFAAQg
# U2bWSgqu4Eeegd5tvlnrCcjE3DaSiTnjd6cC5VCZGq8CBlUkKRAWbRgTMjAxNTA0
# MTcxMzEzMjAuMzk4WjAHAgEBgAIB9KCBuaSBtjCBszELMAkGA1UEBhMCVVMxEzAR
# BgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1p
# Y3Jvc29mdCBDb3Jwb3JhdGlvbjENMAsGA1UECxMETU9QUjEnMCUGA1UECxMebkNp
# cGhlciBEU0UgRVNOOkMwRjQtMzA4Ni1ERUY4MSUwIwYDVQQDExxNaWNyb3NvZnQg
# VGltZS1TdGFtcCBTZXJ2aWNloIIO0DCCBnEwggRZoAMCAQICCmEJgSoAAAAAAAIw
# DQYJKoZIhvcNAQELBQAwgYgxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5n
# dG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9y
# YXRpb24xMjAwBgNVBAMTKU1pY3Jvc29mdCBSb290IENlcnRpZmljYXRlIEF1dGhv
# cml0eSAyMDEwMB4XDTEwMDcwMTIxMzY1NVoXDTI1MDcwMTIxNDY1NVowfDELMAkG
# A1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQx
# HjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWljcm9z
# b2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAw
# ggEKAoIBAQCpHQ28dxGKOiDs/BOX9fp/aZRrdFQQ1aUKAIKF++18aEssX8XD5WHC
# drc+Zitb8BVTJwQxH0EbGpUdzgkTjnxhMFmxMEQP8WCIhFRDDNdNuDgIs0Ldk6zW
# czBXJoKjRQ3Q6vVHgc2/JGAyWGBG8lhHhjKEHnRhZ5FfgVSxz5NMksHEpl3RYRNu
# KMYa+YaAu99h/EbBJx0kZxJyGiGKr0tkiVBisV39dx898Fd1rL2KQk1AUdEPnAY+
# Z3/1ZsADlkR+79BL/W7lmsqxqPJ6Kgox8NpOBpG2iAg16HgcsOmZzTznL0S6p/Tc
# ZL2kAcEgCZN4zfy8wMlEXV4WnAEFTyJNAgMBAAGjggHmMIIB4jAQBgkrBgEEAYI3
# FQEEAwIBADAdBgNVHQ4EFgQU1WM6XIoxkPNDe3xGG8UzaFqFbVUwGQYJKwYBBAGC
# NxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8GA1UdEwEB/wQFMAMBAf8w
# HwYDVR0jBBgwFoAU1fZWy4/oolxiaNE9lJBb186aGMQwVgYDVR0fBE8wTTBLoEmg
# R4ZFaHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwvcHJvZHVjdHMvTWlj
# Um9vQ2VyQXV0XzIwMTAtMDYtMjMuY3JsMFoGCCsGAQUFBwEBBE4wTDBKBggrBgEF
# BQcwAoY+aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0cy9NaWNSb29D
# ZXJBdXRfMjAxMC0wNi0yMy5jcnQwgaAGA1UdIAEB/wSBlTCBkjCBjwYJKwYBBAGC
# Ny4DMIGBMD0GCCsGAQUFBwIBFjFodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vUEtJ
# L2RvY3MvQ1BTL2RlZmF1bHQuaHRtMEAGCCsGAQUFBwICMDQeMiAdAEwAZQBnAGEA
# bABfAFAAbwBsAGkAYwB5AF8AUwB0AGEAdABlAG0AZQBuAHQALiAdMA0GCSqGSIb3
# DQEBCwUAA4ICAQAH5ohRDeLG4Jg/gXEDPZ2joSFvs+umzPUxvs8F4qn++ldtGTCz
# wsVmyWrf9efweL3HqJ4l4/m87WtUVwgrUYJEEvu5U4zM9GASinbMQEBBm9xcF/9c
# +V4XNZgkVkt070IQyK+/f8Z/8jd9Wj8c8pl5SpFSAK84Dxf1L3mBZdmptWvkx872
# ynoAb0swRCQiPM/tA6WWj1kpvLb9BOFwnzJKJ/1Vry/+tuWOM7tiX5rbV0Dp8c6Z
# ZpCM/2pif93FSguRJuI57BlKcWOdeyFtw5yjojz6f32WapB4pm3S4Zz5Hfw42JT0
# xqUKloakvZ4argRCg7i1gJsiOCC1JeVk7Pf0v35jWSUPei45V3aicaoGig+JFrph
# pxHLmtgOR5qAxdDNp9DvfYPw4TtxCd9ddJgiCGHasFAeb73x4QDf5zEHpJM692VH
# eOj4qEir995yfmFrb3epgcunCaw5u+zGy9iCtHLNHfS4hQEegPsbiSpUObJb2sgN
# VZl6h3M7COaYLeqN4DMuEin1wC9UJyH3yKxO2ii4sanblrKnQqLJzxlBTeCG+Sqa
# oxFmMNO7dDJL32N79ZmKLxvHIa9Zta7cRDyXUHHXodLFVeNp3lfB0d4wwP3M5k37
# Db9dT+mdHhk4L7zPWAUu7w2gUDXa7wknHNWzfjUeCLraNtvTX4/edIhJEjCCBNow
# ggPCoAMCAQICEzMAAABSmjsjqtx/tG8AAAAAAFIwDQYJKoZIhvcNAQELBQAwfDEL
# MAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
# bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UEAxMdTWlj
# cm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAwHhcNMTUwMzIwMTczMjI2WhcNMTYw
# NjIwMTczMjI2WjCBszELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0b24x
# EDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
# bjENMAsGA1UECxMETU9QUjEnMCUGA1UECxMebkNpcGhlciBEU0UgRVNOOkMwRjQt
# MzA4Ni1ERUY4MSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2aWNl
# MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4u+xeznVhw+KPMBaR3Ge
# b5WOMURiB7+jUC1I3LSwBhxSCZmsFs3+Q13lKtvTN4LRTKFMYhlipVAj9jULGGkl
# z8sZ+sTFslxWWX/M9j9P4YIV8eBa7cmZgnvNxnqvuIYUijlRjetkVTH8VbrlDMn/
# +/4mdK1KMucdHlaKOJDvlOsgV/J36ABzG4dOvyuwL62ws0aSSmkAzSDtzxdo6O4e
# V6s/fIgW1RaXjqHyfF6XScJEJ3dXYsW1kjb9IjXCZsSB21hrnWqqBMM0B/2CmwZJ
# xWJrrSKgetpr9wg5zxcew4Pr07r564i1/3d6Mia/2Co7SDWrsnIGxMOdgXCuL50D
# XwIDAQABo4IBGzCCARcwHQYDVR0OBBYEFDEVGYZQaLJOTk2ardABxuJTxIP9MB8G
# A1UdIwQYMBaAFNVjOlyKMZDzQ3t8RhvFM2hahW1VMFYGA1UdHwRPME0wS6BJoEeG
# RWh0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3RzL01pY1Rp
# bVN0YVBDQV8yMDEwLTA3LTAxLmNybDBaBggrBgEFBQcBAQROMEwwSgYIKwYBBQUH
# MAKGPmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljVGltU3Rh
# UENBXzIwMTAtMDctMDEuY3J0MAwGA1UdEwEB/wQCMAAwEwYDVR0lBAwwCgYIKwYB
# BQUHAwgwDQYJKoZIhvcNAQELBQADggEBAAzzb6963MxBhEcsp/ZoRPxgurQPWCsz
# rVHtRhRCcjAzIRdptUlL711P7+edzlVvv/KlzOPT9MacqngPtyFsYIAH1he5rDYe
# 2HZNErkg5SZ+Oh2ydVLezIWMK0t52nmfV9wN8QWDd2wgLoFKMVKhpkl6qQxTPPOv
# kohBCGk18y291wGGrodXaQdUkJGVPVMX/DYJ+RrMHNLVIEnENjTNj3geU+0f9HJC
# lYXkHfJ0jtfuBrWDPuAfRZr+A2bHjwpFyHO48vyyxtof1/tOYxjUGU+dM3gYAmrK
# e4aetgWNQy2p/ZGQ5U/ztCUM8GuQr+A4sSYPGZfs6qW9f1ExAqYsBTahggN5MIIC
# YQIBATCB46GBuaSBtjCBszELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hpbmd0
# b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3Jh
# dGlvbjENMAsGA1UECxMETU9QUjEnMCUGA1UECxMebkNpcGhlciBEU0UgRVNOOkMw
# RjQtMzA4Ni1ERUY4MSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBTZXJ2
# aWNloiUKAQEwCQYFKw4DAhoFAAMVAHo06chk4p/sHrk9I+rEd4NsMM+8oIHCMIG/
# pIG8MIG5MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UE
# BxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMQ0wCwYD
# VQQLEwRNT1BSMScwJQYDVQQLEx5uQ2lwaGVyIE5UUyBFU046NTdGNi1DMUUwLTU1
# NEMxKzApBgNVBAMTIk1pY3Jvc29mdCBUaW1lIFNvdXJjZSBNYXN0ZXIgQ2xvY2sw
# DQYJKoZIhvcNAQEFBQACBQDY2256MCIYDzIwMTUwNDE3MTEzNTIyWhgPMjAxNTA0
# MTgxMTM1MjJaMHcwPQYKKwYBBAGEWQoEATEvMC0wCgIFANjbbnoCAQAwCgIBAAIC
# FvUCAf8wBwIBAAICGYswCgIFANjcv/oCAQAwNgYKKwYBBAGEWQoEAjEoMCYwDAYK
# KwYBBAGEWQoDAaAKMAgCAQACAxbjYKEKMAgCAQACAwehIDANBgkqhkiG9w0BAQUF
# AAOCAQEANaevKMc7pteZkl+StaMq6DgTeP15x42WD7JqEg3a3KRuUZPX1ggSvPnk
# eHpfTicpZGPs9oW4ueIIRsByRNlVKen1HHNmJqBkk3JuKPoQxPEF9P8BLOqxzP9a
# FqGRa7UahKDa09L3AmBhrhiNm55ZUJhGTC/8Ykye/iFv44b8BnT1K4v/J9nfel/x
# oJzqZ8ooPrs5uxGMOELoT0h/EubgM5b2frOV5wgb22u2ZFsgBJJI/pweW4dQ155S
# 52b7MZsAwLHIK59k5YOJwTBVSWOSdwBE81F9Vhxo3jmjSUa7kJJ+rcVWu+Gy0lJN
# v9kmLN8YvflELYd+vIVYrykPm8pfZTGCAvUwggLxAgEBMIGTMHwxCzAJBgNVBAYT
# AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYD
# VQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBU
# aW1lLVN0YW1wIFBDQSAyMDEwAhMzAAAAUpo7I6rcf7RvAAAAAABSMA0GCWCGSAFl
# AwQCAQUAoIIBMjAaBgkqhkiG9w0BCQMxDQYLKoZIhvcNAQkQAQQwLwYJKoZIhvcN
# AQkEMSIEIFcl7mO97MPdBlq4Ude67IF2Xumnz7IgUE/pUAJn7EJpMIHiBgsqhkiG
# 9w0BCRACDDGB0jCBzzCBzDCBsQQUejTpyGTin+weuT0j6sR3g2wwz7wwgZgwgYCk
# fjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
# UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYDVQQD
# Ex1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAxMAITMwAAAFKaOyOq3H+0bwAA
# AAAAUjAWBBRTO1oLftl7iI7VyIHt2tzH7CGRyTANBgkqhkiG9w0BAQsFAASCAQDc
# yyufInUE0/1YURQtH5tZuTLbWoO9jV91rAQIe6o+dJjBmHX60uh3XmdqgJNOvQmu
# 5nifOOF3mTj6mAgX8hWstn2p3VFO560YXOhKSttaswIB14TEqoMv3jk4GgsJ+URg
# pUpHbh8YD6hRJ/ceQhKgnAxddQg9U52mzYFXIoGRrLEhi1VS0UDoJuXYqpQy1/Wm
# stPMBsbe+h0bCl7X+paiHB9ShR8jVJ87Bh7Qnga3tVZ7USEYudCxKQZvTZM7HvD/
# q54z3t8u442EYhaiSY+TLIMoqzWiWl34Wm4kZLsQuzGj66Hafo1HUOrhTO6akKnk
# ar6M7bQHaqH+dLlH2W08
# SIG # End signature block
