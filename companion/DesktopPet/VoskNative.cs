using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DesktopPet;

// UTF-8 is explicit: Windows account and application paths may contain Cyrillic.
internal static class VoskNative
{
    [DllImport("libvosk", EntryPoint = "vosk_model_new", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ModelNew([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
    [DllImport("libvosk", EntryPoint = "vosk_model_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ModelFree(IntPtr model);
    [DllImport("libvosk", EntryPoint = "vosk_model_find_word", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int FindWord(ModelHandle model, [MarshalAs(UnmanagedType.LPUTF8Str)] string word);
    [DllImport("libvosk", EntryPoint = "vosk_recognizer_new_grm", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr RecognizerNew(ModelHandle model, float rate, [MarshalAs(UnmanagedType.LPUTF8Str)] string grammar);
    [DllImport("libvosk", EntryPoint = "vosk_recognizer_free", CallingConvention = CallingConvention.Cdecl)]
    private static extern void RecognizerFree(IntPtr recognizer);
    [DllImport("libvosk", EntryPoint = "vosk_recognizer_accept_waveform", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Accept(RecognizerHandle recognizer, byte[] data, int length);
    [DllImport("libvosk", EntryPoint = "vosk_recognizer_result", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ResultPtr(RecognizerHandle recognizer);
    [DllImport("libvosk", EntryPoint = "vosk_recognizer_partial_result", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr PartialPtr(RecognizerHandle recognizer);
    [DllImport("libvosk", EntryPoint = "vosk_set_log_level", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SetLogLevel(int level);

    internal sealed class ModelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal ModelHandle(IntPtr value) : base(true) => SetHandle(value);
        protected override bool ReleaseHandle() { ModelFree(handle); return true; }
    }
    internal sealed class RecognizerHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal RecognizerHandle(IntPtr value) : base(true) => SetHandle(value);
        protected override bool ReleaseHandle() { RecognizerFree(handle); return true; }
    }
    internal static ModelHandle Load(string path)
    {
        SetLogLevel(-1);
        var handle = new ModelHandle(ModelNew(path));
        if (handle.IsInvalid) { handle.Dispose(); throw new InvalidOperationException("Не удалось загрузить локальную голосовую модель."); }
        return handle;
    }
    internal static RecognizerHandle Create(ModelHandle model, string grammar)
    {
        var handle = new RecognizerHandle(RecognizerNew(model, 16000, grammar));
        if (handle.IsInvalid) { handle.Dispose(); throw new InvalidOperationException("Не удалось запустить голосовое распознавание."); }
        return handle;
    }
    internal static string Result(RecognizerHandle recognizer, bool final) =>
        Marshal.PtrToStringUTF8(final ? ResultPtr(recognizer) : PartialPtr(recognizer)) ?? "{}";
}
