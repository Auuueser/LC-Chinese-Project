param([switch]$SkipWorkerBuild, [string]$WorkerPublishDir)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'speech-native-utils.ps1')
$manifestPath = Join-Path $repo 'assets\speech-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$cache = Join-Path $repo 'obj\speech-downloads'
$payload = Join-Path $repo 'assets\speech-payload'
$latestPublish = Join-Path $repo 'obj\speech-worker-publish-latest.txt'
if ($SkipWorkerBuild) {
    if (!$WorkerPublishDir) {
        if (!(Test-Path -LiteralPath $latestPublish)) { throw 'SkipWorkerBuild requires a native worker build or -WorkerPublishDir.' }
        $WorkerPublishDir = (Get-Content -LiteralPath $latestPublish -Raw).Trim()
    }
    $publish = [IO.Path]::GetFullPath($WorkerPublishDir).TrimEnd('\')
} else {
    if ($WorkerPublishDir) { throw 'WorkerPublishDir is only supported with SkipWorkerBuild; new builds always use a fresh directory.' }
    $build = & (Join-Path $PSScriptRoot 'build-speech-worker.ps1')
    $publish = $build.publishDirectory
}
# Build provenance prevents a standalone .NET apphost from passing native PE checks.
$publishFiles = @(Get-SpeechFiles $publish | ForEach-Object { $_.FullName.Substring($publish.Length + 1).Replace('\','/') })
if (@(Compare-Object @('V81SpeechWorker.exe', 'worker-build.json') $publishFiles).Count) { throw 'Native worker build directory must contain only its EXE and build evidence.' }
$buildEvidence = Get-Content -LiteralPath (Join-Path $publish 'worker-build.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($buildEvidence.worker -ne 'native-cpp' -or $buildEvidence.configuration -ne 'Release' -or $buildEvidence.architecture -ne 'x64' -or
    (Get-FileHash -LiteralPath (Join-Path $publish 'V81SpeechWorker.exe')).Hash -ne $buildEvidence.workerSha256 -or
    $buildEvidence.cApiHeaderSha256 -ne $manifest.cApiHeader.sha256) { throw 'Native worker build evidence is invalid.' }
$sourceNames = @('src/V81SpeechWorker/CMakeLists.txt', 'src/V81SpeechWorker/main.cpp', [string]$manifest.cApiHeader.path)
if (@(Compare-Object $sourceNames @($buildEvidence.sourceFiles.path)).Count) { throw 'Native worker source inventory is invalid.' }
foreach ($source in $buildEvidence.sourceFiles) {
    if ((Get-FileHash -LiteralPath (Resolve-SpeechChildPath $repo $source.path)).Hash -ne $source.sha256) { throw "Native worker build is stale: $($source.path)" }
}
Assert-SpeechNativeBinaries $publish $manifest -WorkerOnly | Out-Null
New-Item -ItemType Directory -Path $cache -Force | Out-Null

function Get-PinnedSpeechDownload([string]$Path, [string]$Url, [string]$Sha256) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        & curl.exe -sS -L --fail --retry 3 --output $Path $Url
        if ($LASTEXITCODE -ne 0) { throw "Speech asset download failed: $Url" }
    }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) { throw "Downloaded speech asset hash mismatch: $Path" }
}
$archive = Join-Path $cache 'sensevoice.tar.bz2'
Get-PinnedSpeechDownload $archive $manifest.modelArchiveUrl $manifest.modelArchiveSha256
$model = Join-Path $cache $manifest.model
$complete = Join-Path $model '.extraction-complete'
if (!(Test-Path -LiteralPath $complete) -or (Get-Content -LiteralPath $complete -Raw).Trim() -ne $manifest.modelArchiveSha256) {
    $entries = @(& tar -tf $archive)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to list model archive.' }
    foreach ($entry in $entries) {
        if (!$entry.StartsWith($manifest.model + '/', [StringComparison]::Ordinal) -or $entry -match '(^|/)\.\.(/|$)|^[A-Za-z]:|\\') { throw 'Unexpected model archive path.' }
    }
    & tar -xjf $archive -C $cache
    if ($LASTEXITCODE -ne 0) { throw 'Unable to unpack model.' }
    Set-Content -LiteralPath $complete -Value $manifest.modelArchiveSha256 -Encoding ascii
}
$modelFile = Join-Path $model 'model.int8.onnx'
$tokensFile = Join-Path $model 'tokens.txt'
if ((Get-FileHash -LiteralPath $modelFile).Hash -ne $manifest.modelSha256 -or
    (Get-FileHash -LiteralPath $tokensFile).Hash -ne $manifest.tokensSha256) { throw 'Extracted model or tokens hash mismatch.' }
$noticeCache = Join-Path $cache 'notices'
New-Item -ItemType Directory -Path $noticeCache -Force | Out-Null
foreach ($notice in $manifest.notices) { Get-PinnedSpeechDownload (Join-Path $noticeCache $notice.file) $notice.url $notice.sha256 }
$runtimeArchive = Join-Path $cache "$($manifest.runtimePackage.name).$($manifest.runtimePackage.version).nupkg"
Get-PinnedSpeechDownload $runtimeArchive $manifest.runtimePackage.url $manifest.runtimePackage.sha256

# Assemble into a new directory before touching the existing payload.
$stage = Join-Path $repo ('obj\speech-payload-stage\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $stage 'models'), (Join-Path $stage 'licenses') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publish 'V81SpeechWorker.exe') -Destination $stage
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = [IO.Compression.ZipFile]::OpenRead($runtimeArchive)
try {
    foreach ($library in $manifest.nativeLibraries) {
        $entry = $package.GetEntry($library.archivePath)
        if (!$entry -or $entry.Length -ne $library.size) { throw "Native library missing from its official package: $($library.file)" }
        $destination = Resolve-SpeechChildPath $stage $library.file
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination)
        if ((Get-FileHash -LiteralPath $destination).Hash -ne $library.sha256) { throw "Official native library hash mismatch: $($library.file)" }
    }
} finally { $package.Dispose() }
Copy-Item -LiteralPath $modelFile,$tokensFile -Destination (Join-Path $stage 'models')
foreach ($notice in $manifest.notices) { Copy-Item -LiteralPath (Join-Path $noticeCache $notice.file) -Destination (Join-Path $stage 'licenses') }
Copy-Item -LiteralPath (Join-Path $model 'LICENSE') -Destination (Join-Path $stage 'licenses\SenseVoice-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $model 'README.md') -Destination (Join-Path $stage 'licenses\SenseVoice-README.md')
Copy-Item -LiteralPath (Join-Path $repo 'licenses\Apache-2.0.txt') -Destination (Join-Path $stage 'licenses\sherpa-onnx-Apache-2.0.txt')
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $stage 'source-manifest.json')
[pscustomobject][ordered]@{
    schema=1; worker='native-cpp'; configuration='Release'; architecture='x64'
    workerSha256=$buildEvidence.workerSha256; cApiHeaderSha256=$buildEvidence.cApiHeaderSha256
    sourceFiles=@($buildEvidence.sourceFiles | ForEach-Object { [pscustomobject][ordered]@{ path=[string]$_.path; sha256=[string]$_.sha256 } })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'worker-source.json') -Encoding UTF8
$hashes = @(Get-SpeechFiles $stage | Sort-Object FullName | ForEach-Object {
    [pscustomobject][ordered]@{ path = $_.FullName.Substring($stage.Length + 1).Replace('\','/'); size = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
})
$hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'files.json') -Encoding UTF8
Assert-SpeechPayload $stage $manifest -SourceRoot $repo | Out-Null

# Preserve edits and foreign files; remove only inventoried, unchanged old assets.
$oldInventory = Join-Path $payload 'files.json'
$oldEntries = if (Test-Path -LiteralPath $oldInventory -PathType Leaf) { @(Get-Content -LiteralPath $oldInventory -Raw -Encoding UTF8 | ConvertFrom-Json) } else { @() }
$oldNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $oldEntries) {
    $oldFile = Resolve-SpeechChildPath $payload $entry.path
    if (!$oldNames.Add([string]$entry.path) -or $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $entry.size -lt 0) { throw 'Invalid previous speech inventory.' }
    if (Test-Path -LiteralPath $oldFile -PathType Leaf) {
        if ((Get-Item -LiteralPath $oldFile).Length -ne $entry.size -or (Get-FileHash -LiteralPath $oldFile).Hash -ne $entry.sha256) {
            throw "Preserving modified payload asset; restore or move it before preparation: $oldFile"
        }
    }
}
if (Test-Path -LiteralPath $payload) {
    foreach ($file in @(Get-SpeechFiles $payload)) {
        $relative = $file.FullName.Substring($payload.Length + 1).Replace('\','/')
        if ($relative -ne 'files.json' -and !$oldNames.Contains($relative)) { throw "Preserving unexpected payload file; move it before preparation: $relative" }
    }
}
$backup = Join-Path $repo ('obj\speech-payload-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmssfff'))
New-Item -ItemType Directory -Path $payload,$backup -Force | Out-Null
foreach ($file in @(Get-SpeechFiles $payload)) {
    $relative = $file.FullName.Substring($payload.Length + 1).Replace('\','/')
    # The fixed, unchanged model remains available in both the cache and stage.
    if ($relative.StartsWith('models/', [StringComparison]::OrdinalIgnoreCase)) { continue }
    $destination = Resolve-SpeechChildPath $backup $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
}
$newNames = @(Get-SpeechExpectedPayloadNames $manifest)
foreach ($entry in $oldEntries) {
    if ($entry.path -in $newNames) { continue }
    $oldFile = Resolve-SpeechChildPath $payload $entry.path
    if (Test-Path -LiteralPath $oldFile -PathType Leaf) { Remove-Item -LiteralPath $oldFile -Force }
}
foreach ($file in @(Get-SpeechFiles $stage)) {
    $relative = $file.FullName.Substring($stage.Length + 1).Replace('\','/')
    $destination = Resolve-SpeechChildPath $payload $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}
Assert-SpeechPayload $payload $manifest -SourceRoot $repo | Out-Null
if (!$SkipWorkerBuild) { Set-Content -LiteralPath $latestPublish -Value $publish -Encoding UTF8 }
$native = @(Assert-SpeechNativeBinaries $payload $manifest)
New-Item -ItemType Directory -Path (Join-Path $repo 'obj\speech-verification') -Force | Out-Null
[pscustomobject]@{ payload=$payload; stage=$stage; publish=$publish; backup=$backup; assets=$hashes.Count; nativeLibraries=@($manifest.nativeLibraries).Count; binaries=$native } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $repo 'obj\speech-verification\native-payload.json') -Encoding UTF8
Write-Host "Speech payload ready: $($hashes.Count) verified assets, $(@($manifest.nativeLibraries).Count) native DLLs, $([math]::Round(($hashes | Measure-Object size -Sum).Sum / 1MB,1)) MiB; previous non-model assets backed up to $backup."
