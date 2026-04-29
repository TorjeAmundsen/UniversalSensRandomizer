$ErrorActionPreference = "Stop"

$project = "UniversalSensRandomizer.csproj"
$appName = "UniversalSensRandomizer"
$rid = "win-x64"

$debug = $args -contains "--debug"

$csprojVersion = ([xml](Get-Content "$PSScriptRoot/$project")).Project.PropertyGroup.Version
$appVersion = "v$csprojVersion"

function Build {
    $output = "$PSScriptRoot/build/$rid"

    if (Test-Path $output) { Remove-Item $output -Recurse -Force }

    Write-Host "Building $appName ($rid)..."

    $dotnetArgs = @(
        "publish", $project,
        "-c", "Release",
        "-r", $rid,
        "--self-contained", "true",
        "/p:PublishAot=true",
        "-o", $output
    )

    if ($debug) {
        $dotnetArgs += "/p:NativeDebugSymbols=true"
        $dotnetArgs += "/p:StripSymbols=false"
    }

    dotnet @dotnetArgs | Out-Host

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $rid with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    if (-not $debug) {
        Get-ChildItem "$output" -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item
    }

    Write-Host "  Output: $output"
    return $output
}

function ZipBuild($outputPath) {
    $zipName = "$PSScriptRoot/build/$appName-$appVersion-$rid.zip"

    if (Test-Path $zipName) { Remove-Item $zipName }

    Compress-Archive -Path "$outputPath/*" -DestinationPath $zipName
    Write-Host "  Zipped: $zipName"
}

$out = Build
if ($args -contains "--zip") {
    ZipBuild $out
}

Write-Host "Build complete."
