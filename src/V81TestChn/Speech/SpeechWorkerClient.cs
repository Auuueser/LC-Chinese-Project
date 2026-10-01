using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace V81TestChn;

internal sealed class SpeechWorkerClient : IDisposable
{
    private readonly object _gate = new();
    private readonly string _directory;
    private Process? _process;
    private BinaryReader? _reader;
    private BinaryWriter? _writer;
    private volatile bool _disposed;
    internal readonly Task Ready;

    internal SpeechWorkerClient(string directory)
    {
        _directory = directory;
        Ready = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var load = Task.Run(() =>
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SpeechWorkerClient));
            var start = new ProcessStartInfo(Path.Combine(_directory, "V81SpeechWorker.exe"))
            {
                WorkingDirectory = _directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var process = Process.Start(start) ?? throw new IOException("Speech worker did not start.");
            lock (_gate) _process = process;
            if (_disposed) { StopProcess(); throw new ObjectDisposedException(nameof(SpeechWorkerClient)); }
            // Drain diagnostics so a full stderr pipe cannot stall the recognizer.
            process.ErrorDataReceived += IgnoreDiagnostic;
            process.BeginErrorReadLine();
            _reader = new BinaryReader(process.StandardOutput.BaseStream);
            _writer = new BinaryWriter(process.StandardInput.BaseStream);
            var kind = SpeechWire.Header(_reader, out _);
            if (kind == SpeechMessage.Error) throw new IOException("Speech worker initialization failed: " + SpeechWire.Text(_reader));
            if (kind != SpeechMessage.Ready) throw new InvalidDataException("Speech worker is not ready.");
        });
        if (await Task.WhenAny(load, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false) != load)
        { Dispose(); Observe(load); throw new TimeoutException("Speech model load timed out."); }
        try { await load.ConfigureAwait(false); }
        catch { Dispose(); throw; }
    }

    internal async Task<string> RecognizeAsync(long id, float[] samples, int count)
    {
        await Ready.ConfigureAwait(false);
        if (_disposed) throw new ObjectDisposedException(nameof(SpeechWorkerClient));
        var decode = Task.Run(() =>
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SpeechWorkerClient));
            var writer = _writer!;
            SpeechWire.Header(writer, SpeechMessage.Recognize, id);
            SpeechWire.Samples(writer, samples, count);
            writer.Flush();
            var reader = _reader!;
            var kind = SpeechWire.Header(reader, out var resultId);
            if (kind == SpeechMessage.Error) throw new IOException("Speech recognition failed: " + SpeechWire.Text(reader));
            if (kind != SpeechMessage.Result || resultId != id) throw new InvalidDataException("Stale speech response.");
            return SpeechWire.Text(reader);
        });
        if (await Task.WhenAny(decode, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false) != decode)
        { Dispose(); Observe(decode); throw new TimeoutException("Speech recognition timed out."); }
        try { return await decode.ConfigureAwait(false); }
        catch { Dispose(); throw; }
    }

    private static void IgnoreDiagnostic(object sender, DataReceivedEventArgs args) { }
    private static void Observe(Task? task)
    {
        if (task == null) return;
        task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    internal static void Forget(Task task) => Observe(task);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Observe(Ready);
        // Neither process exit nor pipe I/O is waited on by the Unity thread.
        Task.Run(StopProcess);
    }

    private void StopProcess()
    {
        Process? process;
        lock (_gate) { process = _process; _process = null; }
        if (process == null) return;
        try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { process.Dispose(); }
    }
}
