param(
    [Parameter(Mandatory = $false)]
    [string] $Version = 'latest',

    [Parameter(Mandatory = $false)]
    [string] $Repo = 'Master0fFate/parallax-capture',

    [Parameter(Mandatory = $false)]
    [string] $InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\Parallax Capture'),

    [Parameter(Mandatory = $false)]
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-ReleaseAssetUrls {
    if ($Version -eq 'latest') {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest"
        $packageAsset = $release.assets | Where-Object { $_.name -like 'ParallaxCapture-*-win-x64.zip' } | Select-Object -First 1
        $checksumAsset = $release.assets | Where-Object { $_.name -eq 'SHA256SUMS' } | Select-Object -First 1
        if (-not $packageAsset -or -not $checksumAsset) {
            throw 'Latest release is missing the win-x64 zip package or SHA256SUMS manifest.'
        }

        return @{
            ArtifactName = $packageAsset.name
            ArtifactUrl = $packageAsset.browser_download_url
            ManifestUrl = $checksumAsset.browser_download_url
        }
    }

    $baseUrl = "https://github.com/$Repo/releases/download/$Version"
    $artifactName = "ParallaxCapture-$Version-win-x64.zip"
    return @{
        ArtifactName = $artifactName
        ArtifactUrl = "$baseUrl/$artifactName"
        ManifestUrl = "$baseUrl/SHA256SUMS"
    }
}

function Get-ExpectedChecksum {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ManifestPath,

        [Parameter(Mandatory = $true)]
        [string] $ArtifactName
    )

    foreach ($line in Get-Content -LiteralPath $ManifestPath) {
        if ($line -match "^\s*([a-fA-F0-9]{64})\s+\*?(.+?)\s*$") {
            if ([IO.Path]::GetFileName($Matches[2]) -eq $ArtifactName) {
                return $Matches[1].ToLowerInvariant()
            }
        }
    }

    throw "Checksum manifest does not contain $ArtifactName."
}

function Stop-ParallaxProcess {
    Get-Process -Name 'Parallax Capture' -ErrorAction SilentlyContinue | Stop-Process -ErrorAction SilentlyContinue
}

if ($Uninstall) {
    Stop-ParallaxProcess
    if (Test-Path -LiteralPath $InstallRoot) {
        Remove-Item -LiteralPath $InstallRoot -Recurse -Force
    }

    $link = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\Parallax Capture.lnk'
    if (Test-Path -LiteralPath $link) {
        Remove-Item -LiteralPath $link -Force
    }

    Write-Host "Removed Parallax Capture from $InstallRoot."
    exit 0
}

$releaseAsset = Get-ReleaseAssetUrls
$artifactName = $releaseAsset.ArtifactName
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("parallax-install-" + [Guid]::NewGuid().ToString('N'))
$zipPath = Join-Path $tempRoot $artifactName
$manifestPath = Join-Path $tempRoot 'SHA256SUMS'
$extractPath = Join-Path $tempRoot 'extract'

New-Item -ItemType Directory -Force -Path $tempRoot, $extractPath | Out-Null

try {
    Invoke-WebRequest -Uri $releaseAsset.ArtifactUrl -OutFile $zipPath
    Invoke-WebRequest -Uri $releaseAsset.ManifestUrl -OutFile $manifestPath

    $expected = Get-ExpectedChecksum -ManifestPath $manifestPath -ArtifactName $artifactName
    $actual = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Checksum mismatch for $artifactName. Expected $expected but got $actual."
    }

    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force
    Stop-ParallaxProcess

    $parent = Split-Path -Parent $InstallRoot
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    if (Test-Path -LiteralPath $InstallRoot) {
        Remove-Item -LiteralPath $InstallRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
    Copy-Item -Path (Join-Path $extractPath '*') -Destination $InstallRoot -Recurse -Force

    $exe = Join-Path $InstallRoot 'Parallax Capture.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Installed package did not contain Parallax Capture.exe."
    }

    $programs = Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs'
    New-Item -ItemType Directory -Force -Path $programs | Out-Null
    $shortcut = Join-Path $programs 'Parallax Capture.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    $link.TargetPath = $exe
    $link.WorkingDirectory = $InstallRoot
    $link.Save()

    Write-Host "Installed Parallax Capture to $InstallRoot."
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
