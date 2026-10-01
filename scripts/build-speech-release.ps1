param(
    [string]$GameDir = $env:LETHAL_COMPANY_DIR,
    [switch]$SkipPrepare,
    [string]$DistDir
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'speech-native-utils.ps1')
$manifest = Get-Content -LiteralPath (Join-Path $repo 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$version = [string]$manifest.version_number
if ($version -notmatch '^\d+\.\d+\.\d+$' -or $manifest.name -notmatch '^[A-Za-z0-9_]{1,128}$' -or
    $manifest.description.Length -gt 250 -or !$manifest.dependencies.Count) { throw 'Invalid release metadata.' }
if (!$GameDir -or !(Test-Path -LiteralPath (Join-Path $GameDir 'Lethal Company_Data\Managed\Assembly-CSharp.dll'))) {
    throw 'Set -GameDir or LETHAL_COMPANY_DIR to the installed game.'
}
$project = Join-Path $repo 'src\V81TestChn\V81TestChn.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
if ([string]$projectXml.Project.PropertyGroup.Version -ne $version -or
    (Get-Content -LiteralPath (Join-Path $repo 'src\V81TestChn\Core\Plugin.cs') -Raw) -notmatch
        ('PluginVersion\s*=\s*"' + [regex]::Escape($version) + '"')) { throw 'Plugin and package versions differ.' }
$rootFiles = @('manifest.json','icon.png','README.md','CHANGELOG.md','LICENSE','THIRD_PARTY_LICENSES.md')
foreach ($relative in $rootFiles) {
    if (!(Test-Path -LiteralPath (Join-Path $repo $relative) -PathType Leaf)) { throw "Missing release file: $relative" }
}
$iconBytes = [IO.File]::ReadAllBytes((Join-Path $repo 'icon.png'))
if ($iconBytes.Length -lt 24 -or [BitConverter]::ToString($iconBytes,0,8) -ne '89-50-4E-47-0D-0A-1A-0A' -or
    [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($iconBytes,16)) -ne 256 -or
    [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($iconBytes,20)) -ne 256) { throw 'icon.png must be a 256 x 256 PNG.' }
if (!$SkipPrepare) { & (Join-Path $PSScriptRoot 'prepare-speech-payload.ps1') }
$payload = Join-Path $repo 'assets\speech-payload'
$hashManifest = Join-Path $payload 'files.json'
if (!(Test-Path -LiteralPath $hashManifest)) { throw 'Prepare the complete offline speech payload first.' }
$sourceManifest = Join-Path $repo 'assets\speech-manifest.json'
$speechSource = Get-Content -LiteralPath $sourceManifest -Raw -Encoding UTF8 | ConvertFrom-Json
$hashes = @(Assert-SpeechPayload $payload $speechSource -SourceRoot $repo)
$payloadNames = @($hashes.path) + @('files.json')
if ((Get-FileHash -LiteralPath $sourceManifest).Hash -ne
    (Get-FileHash -LiteralPath (Join-Path $payload 'source-manifest.json')).Hash) { throw 'Speech payload provenance differs from the source manifest.' }
$sources = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
# Fail if an optional CopyToOutputDirectory input was silently omitted by MSBuild.
foreach ($item in $projectXml.Project.ItemGroup.None) {
    if (!$item.CopyToOutputDirectory) { continue }
    $include = [string]$item.Include
    if ($include.Contains('*')) {
        if ($include.StartsWith('..\..\translations-clean\')) {
            foreach ($relative in @('manifest.json','zh-CN.runtime.json','cfg\zh-CN\clean-draft-runtime.cfg')) {
                $sources.Add(('translations-clean\'+$relative).Replace('\','/'), (Join-Path $repo ('translations-clean\'+$relative)))
            }
        } elseif ($include.StartsWith('..\..\assets\speech-payload\')) {
            foreach ($relative in $payloadNames) { $sources.Add('speech/'+$relative, (Join-Path $payload $relative)) }
        } else { throw "Unrecognized runtime content wildcard: $include" }
    } else {
        $sources.Add(([string]$item.Link).Replace('\','/'), [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $project) $include)))
    }
}
foreach ($path in $sources.Values) { if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing required runtime input: $path" } }
$job = Join-Path $repo ('obj\release-build\' + (Get-Date -Format 'yyyyMMdd-HHmmssfff'))
$output = Join-Path $job 'runtime'
& dotnet build $project -c Release "-p:GameDir=$GameDir" -p:EnableTestDeploy=false -p:EnableDistSync=false -o $output --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Plugin Release build failed.' }
$sources.Add('V81TestChn.dll', (Join-Path $output 'V81TestChn.dll'))
# The plugin's own dependency manifest is not shipped; the native worker has none.
$pluginDeps = Join-Path $output 'V81TestChn.deps.json'
$files = @(Get-ChildItem -LiteralPath $output -File -Recurse | Where-Object { $_.Extension -ne '.pdb' -and $_.FullName -ne $pluginDeps })
$actual = @($files | ForEach-Object { $_.FullName.Substring($output.Length+1).Replace('\','/') })
if (@(Compare-Object @($sources.Keys) $actual).Count) { throw 'Build output has unexpected or missing runtime files.' }
$dll = Join-Path $output 'V81TestChn.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString() -ne "$version.0" -or
    [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion -ne "$version.0") { throw 'Built assembly version differs from package metadata.' }
$stage = Join-Path $job 'package'
$packagePlugin = Join-Path $stage 'BepInEx\plugins\V81TestChn'
New-Item -ItemType Directory -Path $packagePlugin -Force | Out-Null
foreach ($file in $files) {
    $relative = $file.FullName.Substring($output.Length+1).Replace('\','/')
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $sources[$relative]).Hash) { throw "Build content mismatch: $relative" }
    $target = Join-Path $packagePlugin $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
}
foreach ($relative in $rootFiles) { Copy-Item -LiteralPath (Join-Path $repo $relative) -Destination $stage }
$dist = if ($DistDir) { [IO.Path]::GetFullPath($DistDir) } else { Join-Path $repo 'dist' }
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$archive = Join-Path $dist "$($manifest.name)-$version.zip"
if (Test-Path -LiteralPath $archive) { throw "Refusing to overwrite an existing release archive: $archive" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName) {
        $relative = $file.FullName.Substring($stage.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $zip.Dispose() }
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    if ($zip.Entries.Count -ne ($files.Count+$rootFiles.Count)) { throw 'Archive entry count mismatch.' }
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -match '(^|/)\.\.(/|$)|^[A-Za-z]:|^/|\\') { throw 'Unsafe archive entry.' }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $digest = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
        finally { $stream.Dispose(); $sha.Dispose() }
        $original = Join-Path $stage $entry.FullName
        if ($digest -ne (Get-FileHash -LiteralPath $original).Hash -or $entry.Length -ne (Get-Item -LiteralPath $original).Length) { throw "Archive readback mismatch: $($entry.FullName)" }
    }
} finally { $zip.Dispose() }
$packageHash = (Get-FileHash -LiteralPath $archive).Hash
$checksum = Join-Path $dist "SHA256SUMS-$version.txt"
[IO.File]::WriteAllText($checksum, "$($packageHash.ToLowerInvariant())  $([IO.Path]::GetFileName($archive))`n", [Text.Encoding]::ASCII)
$evidence = [pscustomobject]@{
    version=$version; package=$archive; checksum=$checksum; packageSha256=$packageHash
    pluginSha256=(Get-FileHash -LiteralPath $dll).Hash; pluginFiles=$files.Count; speechAssets=$hashes.Count
    archiveEntries=$files.Count+$rootFiles.Count; packageBytes=(Get-Item -LiteralPath $archive).Length
    runtime=$output; stage=$stage
    workerDirectory=(Join-Path $packagePlugin 'speech')
    worker='native-cpp'; speechNativeDlls=@($speechSource.nativeLibraries).Count
}
New-Item -ItemType Directory -Path (Join-Path $repo 'obj\speech-verification') -Force | Out-Null
$evidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repo 'obj\speech-verification\package.json') -Encoding utf8
Write-Host "PASS release $version : $($files.Count) runtime files and $($rootFiles.Count) metadata/license files; ZIP hashes verified."
$evidence
