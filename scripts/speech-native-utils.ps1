# Shared build-time checks. This file never starts a speech worker.
if (!('V81SpeechBuild.PeReader' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace V81SpeechBuild {
    public sealed class PeInfo {
        public ushort Machine;
        public bool Managed;
        public bool IsDll;
        public string[] Imports;
        public string[] DelayImports;
    }
    public static class PeReader {
        private sealed class Image {
            private readonly byte[] data;
            private readonly int sections;
            private readonly int sectionCount;
            private readonly uint headerSize;
            private readonly ulong imageBase;
            internal Image(byte[] bytes, int table, int count, uint headers, ulong basis) {
                data = bytes; sections = table; sectionCount = count; headerSize = headers; imageBase = basis;
            }
            internal int Offset(uint rva) {
                if (rva < headerSize) { Check(data, rva, 1); return checked((int)rva); }
                for (int i = 0; i < sectionCount; i++) {
                    int section = sections + i * 40;
                    uint start = U32(data, section + 12), rawSize = U32(data, section + 16);
                    if (rva >= start && (ulong)rva - start < rawSize) {
                        ulong offset = (ulong)U32(data, section + 20) + rva - start;
                        Check(data, offset, 1); return checked((int)offset);
                    }
                }
                throw new InvalidDataException("Invalid PE RVA.");
            }
            internal string Name(uint rva) {
                int offset = Offset(rva), count = 0;
                while (count < 512) {
                    Check(data, (ulong)offset + (uint)count, 1);
                    byte value = data[offset + count];
                    if (value == 0) {
                        if (count == 0) throw new InvalidDataException("Empty PE import name.");
                        return Encoding.ASCII.GetString(data, offset, count);
                    }
                    if (value > 127) throw new InvalidDataException("Non-ASCII PE import name.");
                    count++;
                }
                throw new InvalidDataException("PE import name exceeds limit.");
            }
            internal string[] ReadImports(uint rva, uint size, bool delay) {
                if (rva == 0 && size == 0) return new string[0];
                int width = delay ? 32 : 20;
                if (rva == 0 || size < width || size > 1048576) throw new InvalidDataException("Invalid PE import table.");
                var result = new List<string>();
                for (uint consumed = 0; (ulong)consumed + (uint)width <= size; consumed += (uint)width) {
                    int offset = Offset(checked(rva + consumed));
                    Check(data, (ulong)offset, (uint)width);
                    bool empty = true;
                    for (int j = 0; j < width; j++) empty &= data[offset + j] == 0;
                    if (empty) return result.ToArray();
                    uint name = U32(data, offset + (delay ? 4 : 12));
                    if (delay && (U32(data, offset) & 1) == 0) {
                        if (name < imageBase || (ulong)name - imageBase > UInt32.MaxValue)
                            throw new InvalidDataException("Invalid PE delay import VA.");
                        name = (uint)((ulong)name - imageBase);
                    }
                    result.Add(Name(name));
                    if (result.Count > 1024) throw new InvalidDataException("Too many PE imports.");
                }
                throw new InvalidDataException("Unterminated PE import table.");
            }
        }
        private static void Check(byte[] bytes, ulong offset, ulong count) {
            if (offset > (ulong)bytes.Length || count > (ulong)bytes.Length - offset)
                throw new InvalidDataException("Truncated PE image.");
        }
        private static ushort U16(byte[] bytes, int offset) { Check(bytes, (ulong)offset, 2); return BitConverter.ToUInt16(bytes, offset); }
        private static uint U32(byte[] bytes, int offset) { Check(bytes, (ulong)offset, 4); return BitConverter.ToUInt32(bytes, offset); }
        public static PeInfo Read(string path) {
            byte[] bytes = File.ReadAllBytes(path);
            if (U16(bytes, 0) != 0x5a4d) throw new InvalidDataException("Expected a PE image.");
            int pe = checked((int)U32(bytes, 0x3c));
            if (U32(bytes, pe) != 0x00004550) throw new InvalidDataException("Invalid PE signature.");
            ushort count = U16(bytes, pe + 6), optionalSize = U16(bytes, pe + 20);
            if (count == 0 || count > 96 || optionalSize < 240) throw new InvalidDataException("Invalid PE headers.");
            int optional = checked(pe + 24), table = checked(optional + optionalSize);
            Check(bytes, (ulong)optional, optionalSize); Check(bytes, (ulong)table, (uint)count * 40);
            if (U16(bytes, optional) != 0x20b || U32(bytes, optional + 108) < 15)
                throw new InvalidDataException("Expected a complete PE32+ image.");
            var image = new Image(bytes, table, count, U32(bytes, optional + 60), BitConverter.ToUInt64(bytes, optional + 24));
            int directories = optional + 112;
            return new PeInfo {
                Machine = U16(bytes, pe + 4),
                Managed = U32(bytes, directories + 14 * 8) != 0 || U32(bytes, directories + 14 * 8 + 4) != 0,
                IsDll = (U16(bytes, pe + 22) & 0x2000) != 0,
                Imports = image.ReadImports(U32(bytes, directories + 8), U32(bytes, directories + 12), false),
                DelayImports = image.ReadImports(U32(bytes, directories + 13 * 8), U32(bytes, directories + 13 * 8 + 4), true)
            };
        }
    }
}
'@
}

function Resolve-SpeechChildPath([string]$Directory, [string]$Relative) {
    if (!$Relative -or $Relative -match '(^|/)\.\.?(/|$)|[:\\]|^/|//$|//') { throw "Unsafe speech path: $Relative" }
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\')
    $path = [IO.Path]::GetFullPath((Join-Path $root $Relative))
    if (!$path.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Speech path escaped its root: $Relative" }
    $current = $path
    while ($current -and $current.Length -ge $root.Length) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Speech path contains a reparse point: $current" }
        }
        $current = Split-Path -Parent $current
    }
    return $path
}

function Get-SpeechFiles([string]$Directory) {
    $directories = [Collections.Generic.Stack[string]]::new()
    $directories.Push([IO.Path]::GetFullPath($Directory))
    while ($directories.Count) {
        $current = $directories.Pop()
        foreach ($item in Get-ChildItem -LiteralPath $current -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Speech directory contains a reparse point: $($item.FullName)" }
            if ($item.PSIsContainer) { $directories.Push($item.FullName) } else { $item }
        }
    }
}

function Assert-SpeechNativeBinaries([string]$Directory, $Manifest, [switch]$WorkerOnly) {
    $names = @('V81SpeechWorker.exe')
    if (!$WorkerOnly) { $names += @($Manifest.nativeLibraries | ForEach-Object { [string]$_.file }) }
    $systemNames = @('KERNEL32.dll', 'ADVAPI32.dll', 'api-ms-win-core-path-l1-1-0.dll', 'dbghelp.dll', 'SETUPAPI.dll', 'dxgi.dll')
    $metadata = foreach ($name in $names) {
        $path = Resolve-SpeechChildPath $Directory $name
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing native speech binary: $name" }
        $pe = [V81SpeechBuild.PeReader]::Read($path)
        if ($pe.Machine -ne 0x8664 -or $pe.Managed -or $pe.IsDll -ne ($name -ne 'V81SpeechWorker.exe')) { throw "Speech binary is not the expected native x64 image: $name" }
        foreach ($dependency in @($pe.Imports) + @($pe.DelayImports)) {
            if ($dependency -notin $systemNames -and $dependency -notin $names) { throw "Unexpected dependency $dependency in $name; bundle resolution cannot rely on installed runtimes." }
        }
        if ($name -eq 'V81SpeechWorker.exe') {
            if ((Get-Item -LiteralPath $path).Length -ge 1MB) { throw 'Native worker unexpectedly large; inference libraries must remain separate.' }
        } else {
            $source = @($Manifest.nativeLibraries | Where-Object file -eq $name)
            if ($source.Count -ne 1 -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $source[0].sha256 -or
                (Get-Item -LiteralPath $path).Length -ne $source[0].size) { throw "Native speech library differs from its pinned official source: $name" }
        }
        [pscustomobject]@{ file = $name; machine = 'x64'; managed = $false; imports = @($pe.Imports); delayImports = @($pe.DelayImports) }
    }
    return $metadata
}

function Get-SpeechExpectedPayloadNames($Manifest) {
    return @('V81SpeechWorker.exe', 'models/model.int8.onnx', 'models/tokens.txt',
        'licenses/SenseVoice-LICENSE.txt', 'licenses/SenseVoice-README.md', 'licenses/sherpa-onnx-Apache-2.0.txt', 'source-manifest.json', 'worker-source.json') +
        @($Manifest.nativeLibraries | ForEach-Object { [string]$_.file }) + @($Manifest.notices | ForEach-Object { 'licenses/' + $_.file })
}

function Assert-SpeechPayload([string]$Directory, $Manifest, [string]$SourceRoot) {
    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\')
    $inventory = Resolve-SpeechChildPath $root 'files.json'
    if (!(Test-Path -LiteralPath $inventory -PathType Leaf)) { throw 'Prepare the complete native offline speech payload first.' }
    $entries = @(Get-Content -LiteralPath $inventory -Raw -Encoding UTF8 | ConvertFrom-Json)
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries) {
        $relative = [string]$entry.path
        $path = Resolve-SpeechChildPath $root $relative
        if (!$seen.Add($relative) -or $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $entry.size -lt 0) { throw "Invalid or duplicate speech inventory entry: $relative" }
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -ne $entry.size -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Speech asset mismatch: $relative" }
    }
    $expected = @(Get-SpeechExpectedPayloadNames $Manifest)
    if (@(Compare-Object $expected @($entries.path)).Count) { throw 'Speech inventory contains unexpected or missing native payload files.' }
    $actual = @(Get-SpeechFiles $root | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\','/') })
    if (@(Compare-Object ($expected + @('files.json')) $actual).Count) { throw 'Speech directory contains unexpected or missing files.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root 'models/model.int8.onnx')).Hash -ne $Manifest.modelSha256 -or
        (Get-FileHash -LiteralPath (Join-Path $root 'models/tokens.txt')).Hash -ne $Manifest.tokensSha256) { throw 'Speech model or tokens do not match the fixed source.' }
    foreach ($notice in $Manifest.notices) {
        if ((Get-FileHash -LiteralPath (Join-Path $root ('licenses/' + $notice.file))).Hash -ne $notice.sha256) { throw "Speech notice differs from its fixed source: $($notice.file)" }
    }
    $source = Get-Content -LiteralPath (Join-Path $root 'worker-source.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($source.schema -ne 1 -or $source.worker -ne 'native-cpp' -or $source.configuration -ne 'Release' -or $source.architecture -ne 'x64' -or
        $source.workerSha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $source.cApiHeaderSha256 -ne $Manifest.cApiHeader.sha256 -or
        $source.workerSha256 -ne (Get-FileHash -LiteralPath (Join-Path $root 'V81SpeechWorker.exe')).Hash) { throw 'Speech worker source record is not bound to its native binary.' }
    $sourceNames = @('src/V81SpeechWorker/CMakeLists.txt', 'src/V81SpeechWorker/main.cpp', [string]$Manifest.cApiHeader.path)
    if (@(Compare-Object $sourceNames @($source.sourceFiles.path)).Count) { throw 'Speech worker source record has unexpected files.' }
    foreach ($entry in $source.sourceFiles) {
        if ($entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Invalid speech worker source hash.' }
        if ($SourceRoot -and (Get-FileHash -LiteralPath (Resolve-SpeechChildPath $SourceRoot $entry.path)).Hash -ne $entry.sha256) {
            throw "Speech worker payload is stale: $($entry.path). Run prepare-speech-payload.ps1 without SkipWorkerBuild."
        }
    }
    Assert-SpeechNativeBinaries $root $Manifest | Out-Null
    return $entries
}
