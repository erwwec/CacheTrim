using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using MessageBox = System.Windows.MessageBox;

namespace CacheTrim;

public partial class MainWindow
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    private const int OpenHotkeyId = 21101, PetHotkeyId = 21102, AnimeHotkeyId = 21103;
    private HwndSource _hotkeySource;
    private IntPtr _hotkeyHandle;
    private bool _openHotkeyRegistered, _petHotkeyRegistered, _animeHotkeyRegistered;

    private void InitializeHotkeys()
    {
        if (_hotkeySource != null) return;
        _hotkeyHandle = new WindowInteropHelper(this).Handle;
        _hotkeySource = HwndSource.FromHwnd(_hotkeyHandle);
        _hotkeySource?.AddHook(HotkeyHook);
        if (!TryRegisterHotkeys(_settings.HotkeyOpen, _settings.HotkeyPet, _settings.HotkeyAnime))
            SetStatus("Не удалось зарегистрировать горячие клавиши: проверь сочетания в настройках.");
    }
    private void DisposeHotkeys()
    {
        ClearHotkeys();
        _hotkeySource?.RemoveHook(HotkeyHook);
        _hotkeySource = null;
    }
    private void ClearHotkeys()
    {
        if (_openHotkeyRegistered) UnregisterHotKey(_hotkeyHandle, OpenHotkeyId);
        if (_petHotkeyRegistered) UnregisterHotKey(_hotkeyHandle, PetHotkeyId);
        if (_animeHotkeyRegistered) UnregisterHotKey(_hotkeyHandle, AnimeHotkeyId);
        _openHotkeyRegistered = _petHotkeyRegistered = _animeHotkeyRegistered = false;
    }
    // Options 1..6 are Ctrl+Alt+F7..F12. Never steal a key if another app registered it.
    private bool TryRegisterHotkeys(int open, int pet, int anime)
    {
        ClearHotkeys();
        if (!HotkeyPolicy.Valid(open, pet, anime)) return false;
        const uint modifiers = 0x0002 | 0x0001 | 0x4000; // Ctrl, Alt, no autorepeat
        if (open != 0 && !(_openHotkeyRegistered = RegisterHotKey(_hotkeyHandle, OpenHotkeyId, modifiers, (uint)(0x75 + open)))) return false;
        if (pet != 0 && !(_petHotkeyRegistered = RegisterHotKey(_hotkeyHandle, PetHotkeyId, modifiers, (uint)(0x75 + pet)))) { ClearHotkeys(); return false; }
        if (anime != 0 && !(_animeHotkeyRegistered = RegisterHotKey(_hotkeyHandle, AnimeHotkeyId, modifiers, (uint)(0x75 + anime)))) { ClearHotkeys(); return false; }
        return true;
    }
    private IntPtr HotkeyHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x0312) return IntPtr.Zero;
        int id = wParam.ToInt32();
        if (id == OpenHotkeyId) {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Show(); Activate(); handled = true;
        } else if (id == PetHotkeyId || id == AnimeHotkeyId) {
            handled = true;
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Show(); Activate();
            if (!_busy) Dispatcher.BeginInvoke(new Action(async () => {
                if (id == PetHotkeyId) await LaunchDesktopPetAsync();
                else await LaunchAnimeAvatarAsync();
            }));
            else SetStatus("Дождись завершения текущей операции.");
        }
        return IntPtr.Zero;
    }
    private StackPanel CreateHotkeySettings()
    {
        var box = new StackPanel();
        var open = new System.Windows.Controls.ComboBox { Width = 180, Margin = new Thickness(0, 4, 0, 4) };
        var pet = new System.Windows.Controls.ComboBox { Width = 180, Margin = new Thickness(0, 4, 0, 4) };
        var anime = new System.Windows.Controls.ComboBox { Width = 180, Margin = new Thickness(0, 4, 0, 4) };
        foreach (var selector in new[] { open, pet, anime }) {
            selector.Items.Add("Выключено");
            for (int f = 7; f <= 12; f++) selector.Items.Add("Ctrl + Alt + F" + f);
        }
        open.SelectedIndex = _settings.HotkeyOpen; pet.SelectedIndex = _settings.HotkeyPet; anime.SelectedIndex = _settings.HotkeyAnime;
        box.Children.Add(new TextBlock { Text = "Работают, пока CacheTrim открыт (можно свернуть). При закрытии приложения не работают.", TextWrapping = TextWrapping.Wrap, Foreground = Brush(Theme.Muted), Margin = new Thickness(0, 0, 0, 8) });
        box.Children.Add(Row("Показать CacheTrim", "Выбери сочетание", open));
        box.Children.Add(Row("Запустить Клода", "Выбери другое сочетание", pet));
        box.Children.Add(Row("Запустить аниме-аватар", "Выбери другое сочетание", anime));
        box.Children.Add(Action("Сохранить клавиши", () => {
            int oldOpen = _settings.HotkeyOpen, oldPet = _settings.HotkeyPet, oldAnime = _settings.HotkeyAnime;
            if (!TryRegisterHotkeys(open.SelectedIndex, pet.SelectedIndex, anime.SelectedIndex)) {
                TryRegisterHotkeys(oldOpen, oldPet, oldAnime);
                MessageBox.Show(this, "Сочетание уже занято или назначено дважды. Выбери другое.", "Горячие клавиши", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _settings.HotkeyOpen = open.SelectedIndex; _settings.HotkeyPet = pet.SelectedIndex; _settings.HotkeyAnime = anime.SelectedIndex;
            if (!SaveConfig(_settings, "горячие клавиши сохранены", "горячие клавиши изменены")) {
                _settings.HotkeyOpen = oldOpen; _settings.HotkeyPet = oldPet; _settings.HotkeyAnime = oldAnime; TryRegisterHotkeys(oldOpen, oldPet, oldAnime);
            }
        }, true));
        return box;
    }
}
