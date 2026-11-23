using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UiButton = Wpf.Ui.Controls.Button;

namespace CacheTrim;

internal sealed class WhatsNewWindow : Window
{
    private static readonly Brush BackgroundBrush = new SolidColorBrush(Color.FromRgb(17, 21, 27));
    private static readonly Brush CardBrush = new SolidColorBrush(Color.FromRgb(27, 33, 41));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(238, 243, 249));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(159, 174, 191));
    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(139, 196, 255));

    public WhatsNewWindow(string previousVersion, string currentVersion, string notes)
    {
        Title = "Что нового в CacheTrim";
        Width = 560;
        Height = 500;
        MinWidth = 520;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = BackgroundBrush;
        Foreground = TextBrush;
        ShowInTaskbar = false;

        Grid layout = new();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StackPanel heading = new() { Margin = new Thickness(26, 24, 26, 18) };
        heading.Children.Add(new TextBlock { Text = "CacheTrim обновился", FontSize = 25, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
        heading.Children.Add(new TextBlock { Text = previousVersion + "  →  " + currentVersion, FontSize = 13, Foreground = AccentBrush, Margin = new Thickness(0, 6, 0, 0) });
        layout.Children.Add(heading);

        Border notesCard = new() { Background = CardBrush, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(24, 0, 24, 16) };
        ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        StackPanel noteList = new();
        string[] lines = (notes ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal) && !line.Equals("В этой версии:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.TrimStart('-', '*', '•', ' '))
            .Where(line => line.Length > 0)
            .ToArray();
        if (lines.Length == 0)
            lines = new[] { "Исправления и улучшения CacheTrim." };
        foreach (string line in lines)
        {
            StackPanel row = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 12) };
            row.Children.Add(new TextBlock { Text = "•", FontSize = 17, Foreground = AccentBrush, Margin = new Thickness(0, -2, 10, 0) });
            row.Children.Add(new TextBlock { Text = line, FontSize = 14, Foreground = TextBrush, TextWrapping = TextWrapping.Wrap, MaxWidth = 445 });
            noteList.Children.Add(row);
        }
        scroll.Content = noteList;
        notesCard.Child = scroll;
        Grid.SetRow(notesCard, 1);
        layout.Children.Add(notesCard);

        DockPanel footer = new() { Margin = new Thickness(24, 0, 24, 20), LastChildFill = false };
        TextBlock hint = new() { Text = "Обновление установлено успешно", Foreground = MutedBrush, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        footer.Children.Add(hint);
        UiButton close = new() { Content = "Понятно", Width = 112, Height = 38, Appearance = Wpf.Ui.Controls.ControlAppearance.Primary, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right);
        footer.Children.Add(close);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        Content = layout;
    }
}
