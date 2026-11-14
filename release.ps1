# Crea i file di una release di Pinboard con Velopack:
#   build\release\<versione>\Pinboard-Setup-<versione>.exe     installer (per l'utente, senza permessi di amministratore)
#   build\release\<versione>\Pinboard-portable-<versione>.zip  versione portable (si aggiorna anche lei)
#   build\release\<versione>\*.nupkg, releases.win.json, RELEASES  servono agli aggiornamenti automatici
# La versione e' quella di <Version> in src\Pinboard\Pinboard.csproj.
#
# Serve vpk:  dotnet tool install -g vpk --version <stessa versione del pacchetto Velopack>
#
# -Feed <cartella>: non scarica l'ultima release da GitHub ma usa questa cartella come elenco delle
#   versioni precedenti (per provare gli aggiornamenti in locale con PINBOARD_UPDATE_URL).
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
if (-not $version) { throw "Versione non trovata in $project" }

$build = Join-Path $root "build"
$publish = Join-Path $build "publish"
$packages = if ($Feed) { $Feed } else { Join-Path $build "packages" }
$out = Join-Path $build "release\$version"

Write-Host "Pinboard $version" -ForegroundColor Cyan

# 1. Applicazione: cartella self-contained, funziona senza .NET installato.
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish (Join-Path $root "src\Pinboard") -c Release -r win-x64 --self-contained true `
    -p:DebugType=None -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish non riuscito" }

# 2. Ultima release pubblicata: serve a Velopack per creare il pacchetto delta (aggiornamento piccolo).
if (-not $Feed) {
    if (Test-Path $packages) { Remove-Item $packages -Recurse -Force }
    New-Item -ItemType Directory $packages | Out-Null
    vpk download github --repoUrl $repo -o $packages
    if ($LASTEXITCODE -ne 0) { Write-Host "Nessuna release precedente su GitHub: niente pacchetto delta." -ForegroundColor Yellow }
}

# 3. Pacchetti, installer e portable.
$pack = @(
    "pack", "--packId", "Pinboard", "--packVersion", $version, "--packDir", $publish, "--runtime", "win-x64",
    "--mainExe", "Pinboard.exe", "--packTitle", "Pinboard", "--packAuthors", "Tommaso Piscaglia",
    "--icon", (Join-Path $root "src\Pinboard\Assets\Pinboard.ico"), "--outputDir", $packages
)
if ($Notes) { $pack += @("--releaseNotes", $Notes) }
vpk @pack
if ($LASTEXITCODE -ne 0) { throw "vpk pack non riuscito" }

# 4. File da allegare alla release, con i nomi come in ExamsTrack.
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory $out | Out-Null
Copy-Item (Join-Path $packages "Pinboard-win-Setup.exe") (Join-Path $out "Pinboard-Setup-$version.exe")
Copy-Item (Join-Path $packages "Pinboard-win-Portable.zip") (Join-Path $out "Pinboard-portable-$version.zip")
Get-ChildItem $packages -Filter "Pinboard-$version-*.nupkg" | Copy-Item -Destination $out
Copy-Item (Join-Path $packages "releases.win.json") $out
if (Test-Path (Join-Path $packages "RELEASES")) { Copy-Item (Join-Path $packages "RELEASES") $out }

Write-Host ""
Write-Host "Release pronta in $out" -ForegroundColor Green
Get-ChildItem $out | ForEach-Object { Write-Host ("  {0,-40} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
Write-Host ""
Write-Host "Crea su GitHub la release v$version e allega tutti questi file."
