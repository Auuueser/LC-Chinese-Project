param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'speech-native-utils.ps1')
$manifest = Get-Content -LiteralPath (Join-Path $repo 'assets\speech-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$header = Resolve-SpeechChildPath $repo $manifest.cApiHeader.path
if (!(Test-Path -LiteralPath $header -PathType Leaf) -or (Get-FileHash -LiteralPath $header).Hash -ne $manifest.cApiHeader.sha256) {
    throw 'sherpa-onnx C API header does not match its pinned official source.'
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path -LiteralPath $vswhere -PathType Leaf)) { throw 'Install Visual Studio 2022 C++ tools and CMake before building the native worker.' }
$instance = (& $vswhere -latest -version '[17.0,18.0)' -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Out-String).Trim()
if (!$instance) { throw 'Visual Studio 2022 x64 C++ tools were not found.' }
$cmakeCommand = Get-Command cmake.exe -ErrorAction SilentlyContinue
$cmake = if ($cmakeCommand) { $cmakeCommand.Source } else { Join-Path $instance 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' }
if (!(Test-Path -LiteralPath $cmake -PathType Leaf)) { throw 'Install the C++ CMake tools component or put cmake.exe on PATH.' }
$job = Join-Path $repo ('obj\speech-native-build\' + [Guid]::NewGuid().ToString('N'))
$publish = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repo ('obj\speech-worker-publish\' + [Guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $publish) { throw "Native worker output must be a new directory: $publish" }
& $cmake -S (Join-Path $repo 'src\V81SpeechWorker') -B $job -G 'Visual Studio 17 2022' -A x64 "-DCMAKE_GENERATOR_INSTANCE=$instance" | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Native speech worker CMake configuration failed.' }
& $cmake --build $job --config Release | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Native speech worker Release build failed.' }
New-Item -ItemType Directory -Path $publish | Out-Null
Copy-Item -LiteralPath (Join-Path $job 'Release\V81SpeechWorker.exe') -Destination $publish
$native = @(Assert-SpeechNativeBinaries $publish $manifest -WorkerOnly)
$record = [pscustomobject]@{
    worker = 'native-cpp'
    publishDirectory = $publish; buildDirectory = $job; generator = 'Visual Studio 17 2022'; configuration = 'Release'; architecture = 'x64'
    cApiHeaderSha256 = (Get-FileHash -LiteralPath $header).Hash; workerSha256 = (Get-FileHash -LiteralPath (Join-Path $publish 'V81SpeechWorker.exe')).Hash
    sourceFiles = @(@('src/V81SpeechWorker/CMakeLists.txt', 'src/V81SpeechWorker/main.cpp', [string]$manifest.cApiHeader.path) | ForEach-Object {
        [pscustomobject]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Resolve-SpeechChildPath $repo $_)).Hash }
    })
    binaries = $native
}
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $job 'build-evidence.json') -Encoding UTF8
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $publish 'worker-build.json') -Encoding UTF8
$record
