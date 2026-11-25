using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace DesktopPet;

// Temporary PCM stays in memory; stopping or cancellation releases the microphone.
internal sealed class CloudVoiceRecorder : IDisposable
{
    private readonly WaveInEvent _input = new() { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
    private readonly MemoryStream _pcm = new();
    private readonly object _gate = new();
    private readonly TaskCompletionSource<byte[]> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _stopped;
    public CloudVoiceRecorder()
    {
        _input.DataAvailable += (_, e) => { lock (_gate) { if (Volatile.Read(ref _stopped) == 0 && _pcm.Length < 16_000 * 2 * 15) _pcm.Write(e.Buffer, 0, Math.Min(e.BytesRecorded, (int)(16_000 * 2 * 15 - _pcm.Length))); } };
        _input.RecordingStopped += (_, e) =>
        {
            if (e.Exception != null) { _completion.TrySetException(e.Exception); return; }
            byte[] pcm; lock (_gate) pcm = _pcm.ToArray();
            using var output = new MemoryStream();
            using (var writer = new WaveFileWriter(new NAudio.Utils.IgnoreDisposeStream(output), _input.WaveFormat)) writer.Write(pcm, 0, pcm.Length);
            _completion.TrySetResult(output.ToArray());
        };
    }
    public async Task<byte[]> RecordAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _input.StartRecording();
        using var registration = ct.Register(Stop);
        try
        {
            var timeout = Task.Delay(TimeSpan.FromSeconds(15), ct);
            if (await Task.WhenAny(_completion.Task, timeout) == timeout) Stop();
            var audio = await _completion.Task.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            return audio;
        }
        finally { Stop(); }
    }
    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0)
            try { _input.StopRecording(); } catch (InvalidOperationException) { }
    }
    public void Dispose() { Stop(); _input.Dispose(); /* callbacks may still be draining; GC owns the in-memory buffer */ }
}
