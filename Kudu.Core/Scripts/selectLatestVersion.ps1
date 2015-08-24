<#
this script takes in two param, target foler path and output file path
what it does is, it assume immediate sub folder of targer folder are all name with version
and this script is trying to get the folder name has the largest version
#>

PARAM(
	[parameter(Position=0, Mandatory=$true)]
	[string]$folderPath, 
	[parameter(Position=1, Mandatory=$true)]
	[string]$outputPath
)

$latestVersion =
	Get-ChildItem "$folderPath" |
	Where-Object {$_.Name -as [Version]} |
	Sort-Object -Property @{e={[Version] $_.name};Ascending=$false} |
	Select-Object -First 1 |
	Select Name -ExpandProperty Name

[System.IO.File]::WriteAllText("$outputPath", $latestVersion)

EXIT 0