param(
    [Parameter(Mandatory = $false)]
    [string] $Configuration = 'Release',

    [Parameter(Mandatory = $false)]
    [string] $RuntimeIdentifier = 'win-x64',

    [Parameter(Mandatory = $false)]
    [string] $Version = '1.1.0',

    [Parameter(Mandatory = $false)]
    [string] $ArtifactsDirectory = (Join-Path $PSScriptRoot '..\artifacts\release')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function ConvertTo-PackageVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string] $InputVersion
    )

    $match = [regex]::Match($InputVersion, '^(?<prefix>v?)(?<version>[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?)$')
    if (-not $match.Success) {
        throw "Version must be SemVer without build metadata, for example v1.2.3 or v1.2.3-rc.1: $InputVersion"
    }

    [pscustomobject] @{
        PackageVersion = $match.Groups['version'].Value
        ArtifactVersion = "$($match.Groups['prefix'].Value)$($match.Groups['version'].Value)"
    }
}

if ($RuntimeIdentifier -ne 'win-x64') {
    throw "Windows packaging only supports win-x64. Requested: $RuntimeIdentifier"
}

$versionInfo = ConvertTo-PackageVersion -InputVersion $Version
$packageVersion = $versionInfo.PackageVersion
$artifactVersion = $versionInfo.ArtifactVersion
$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')
$artifactRoot = New-Item -ItemType Directory -Force -Path $ArtifactsDirectory
$publishRoot = Join-Path $repoRoot "artifacts\publish\$RuntimeIdentifier"
$packageName = "ParallaxCapture-$artifactVersion-$RuntimeIdentifier"
$zipPath = Join-Path $artifactRoot "$packageName.zip"

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

dotnet publish (Join-Path $repoRoot 'src\Parallax.App.Avalonia\Parallax.App.Avalonia.csproj') `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained false `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=false `
    -p:Version=$packageVersion `
    -o $publishRoot

$appHost = Join-Path $publishRoot 'Parallax.App.Avalonia.exe'
if (Test-Path -LiteralPath $appHost) {
    Copy-Item -LiteralPath $appHost -Destination (Join-Path $publishRoot 'Parallax Capture.exe') -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Created Windows per-user install archive: $zipPath"

$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if ($iscc) {
    $legacyPublish = Join-Path $repoRoot 'parallax\publish'
    dotnet publish (Join-Path $repoRoot 'parallax\parallax.csproj') `
        -c $Configuration `
        -p:Platform=x64 `
        -p:Version=$packageVersion `
        -o $legacyPublish

    & $iscc.Source (Join-Path $repoRoot 'parallax\installer.iss')
    Write-Host 'Created optional Inno Setup installer with PrivilegesRequired=lowest.'
}
else {
    Write-Host 'Inno Setup is not installed. Skipping optional EXE installer and keeping the zip package for the checksum-verified one-line install path.'
}

& (Join-Path $PSScriptRoot 'generate-checksums.ps1') -ArtifactDirectory $artifactRoot.FullName
