$ErrorActionPreference = "Stop"

# This script merges the History file into the NuSpec (using release agent paths).
# It just exists to enable maintaining the history as a plain text file, not hidden away somewhere.

# Path as it exists on the release agent.
$historyPath = Join-Path $PSScriptRoot "..\History\History"
$releaseNotes = [IO.File]::ReadAllText($historyPath)

foreach ($nuspec in Get-ChildItem -Path $PSScriptRoot -Filter *.nuspec) {
	$content = [IO.File]::ReadAllText($nuspec.FullName)
	$content = $content.Replace("<releaseNotes></releaseNotes>", "<releaseNotes>$releaseNotes</releaseNotes>")
	[IO.File]::WriteAllText($nuspec.FullName, $content)
}