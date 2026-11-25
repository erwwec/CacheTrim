using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace DesktopPet;

internal static class OfflineSpeech
{
    internal static Task<string> TranscribeAsync(byte[] wav, CancellationToken ct) => Task.Run(() => Transcribe(wav, Path.Combine(AppContext.BaseDirectory, "VoiceModel", "vosk-model-small-ru-0.22"), ct), ct);
    internal static string Transcribe(byte[] wav, string path, CancellationToken ct)
    {
        if (!File.Exists(Path.Combine(path, "am", "final.mdl"))) throw new InvalidOperationException("Локальная модель не найдена. Распакуй архив полностью: папка VoiceModel должна быть рядом с DesktopPet.exe.");
        using var reader = new WaveFileReader(new MemoryStream(wav, false));
        if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm || reader.WaveFormat.SampleRate != 16000 || reader.WaveFormat.BitsPerSample != 16 || reader.WaveFormat.Channels != 1 || reader.Length < 3200 || reader.Length > 480000)
            throw new InvalidOperationException("Некорректная или слишком короткая запись.");
        ct.ThrowIfCancellationRequested();
        using var model = VoskNative.Load(path);
        using var recognizer = VoskNative.CreateFull(model);
        var words = new List<string>();
        void Add(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
            { var text = value.GetString()?.Trim(); if (!string.IsNullOrEmpty(text)) words.Add(text); }
        }
        byte[] buffer = new byte[3200];
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            int result = VoskNative.Accept(recognizer, buffer, count);
            if (result < 0) throw new InvalidOperationException("Ошибка локального распознавания.");
            if (result == 1) Add(VoskNative.Result(recognizer, true));
        }
        ct.ThrowIfCancellationRequested();
        Add(VoskNative.FinalResult(recognizer));
        var resultText = string.Join(" ", words).Trim();
        if (resultText.Length == 0 || resultText.Length > 1000) throw new InvalidOperationException("Не удалось распознать короткую команду. Попробуй ещё раз.");
        return resultText;
    }
}
