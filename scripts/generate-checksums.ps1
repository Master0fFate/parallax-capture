param(
    [Parameter(Mandatory = $false)]
    [string] $ArtifactDirectory = (Join-Path $PSScriptRoot '..\artifacts\release'),

    [Parameter(Mandatory = $false)]
    [string] $OutputFile = 'SHA256SUMS'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$artifactRoot = Resolve-Path -LiteralPath $ArtifactDirectory
$outputPath = Join-Path $artifactRoot $OutputFile

$files = Get-ChildItem -LiteralPath $artifactRoot -File |
    Where-Object { $_.Name -ne $OutputFile -and $_.Name -ne "$OutputFile.txt" } |
    Sort-Object Name

if (-not $files) {
    throw "No release artifacts were found in $artifactRoot."
}

$lines = foreach ($file in $files) {
    $hash = Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $($file.Name)"
}

Set-Content -LiteralPath $outputPath -Value $lines -Encoding ascii
Write-Host "Wrote checksum manifest: $outputPath"
