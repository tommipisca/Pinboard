# Builds the files of a Pinboard release with Velopack:
#   build\release\<version>\Pinboard-Setup-<version>.exe     installer (per user, no admin rights needed)
#   build\release\<version>\Pinboard-portable-<version>.zip  portable version (it updates itself too)
#   build\release\<version>\*.nupkg, releases.win.json, RELEASES  needed by automatic updates
# The version is <Version> in src\Pinboard\Pinboard.csproj.
#
# Requires vpk:  dotnet tool install -g vpk --version <same version as the Velopack package>
#
# -Feed <folder>: don't download the latest release from GitHub; use this folder as the list of
#   previous versions instead (to test updates locally with PINBOARD_UPDATE_URL, and in CI).
param(
    [string]$Notes,
    [string]$Feed
)
$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$repo = "https://github.com/tommipisca/Pinboard"
$project = Join-Path $root "src\Pinboard\Pinboard.csproj"
[xml]$xml = Get-Content $project
$version = @($xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $version) { throw "Version not found in $project" }

$build = Join-Path $root "build"
$publish = Join-Path $build "publish"
$packages = if ($Feed) { $Feed } else { Join-Path $build "packages" }
$out = Join-Path $build "release\$version"

Write-Host "Pinboard $version" -ForegroundColor Cyan

# 1. The app: a self-contained folder, runs without .NET installed.
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root "src\Pinboard") -c Release -r win-x64 --self-contained true `
    -p:DebugType=None -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# 2. Latest published release: Velopack needs it to build the delta package (a small update).
if (-not $Feed) {
    if (Test-Path $packages) { Remove-Item $packages -Recurse -Force }
    New-Item -ItemType Directory $packages | Out-Null
    vpk download github --repoUrl $repo -o $packages
    if ($LASTEXITCODE -ne 0) { Write-Host "No previous release on GitHub: no delta package." -ForegroundColor Yellow }
}

# 3. Packages, installer and portable version.
$pack = @(
    "pack", "--packId", "Pinboard", "--packVersion", $version, "--packDir", $publish, "--runtime", "win-x64",
    "--mainExe", "Pinboard.exe", "--packTitle", "Pinboard", "--packAuthors", "Tommaso Piscaglia",
    "--icon", (Join-Path $root "src\Pinboard\Assets\Pinboard.ico"), "--outputDir", $packages
)
if ($Notes) { $pack += @("--releaseNotes", $Notes) }
vpk @pack
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

# 4. Files to attach to the release, named like ExamsTrack's.
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory $out | Out-Null
Copy-Item (Join-Path $packages "Pinboard-win-Setup.exe") (Join-Path $out "Pinboard-Setup-$version.exe")
Copy-Item (Join-Path $packages "Pinboard-win-Portable.zip") (Join-Path $out "Pinboard-portable-$version.zip")
Get-ChildItem $packages -Filter "Pinboard-$version-*.nupkg" | Copy-Item -Destination $out
Copy-Item (Join-Path $packages "releases.win.json") $out
if (Test-Path (Join-Path $packages "RELEASES")) { Copy-Item (Join-Path $packages "RELEASES") $out }

Write-Host ""
Write-Host "Release ready in $out" -ForegroundColor Green
Get-ChildItem $out | ForEach-Object { Write-Host ("  {0,-40} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
Write-Host ""
Write-Host "Create release v$version on GitHub and attach all of these files."
