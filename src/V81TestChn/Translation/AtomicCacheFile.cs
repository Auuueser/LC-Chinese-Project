using System;
using System.IO;
using System.Text;

namespace V81TestChn;

internal static class AtomicCacheFile
{
    // The writer owns this path and serializes every call. Failed serialization
    // or IO never truncates the last committed cache file.
    internal static void Write(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".v81-saving";
        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
