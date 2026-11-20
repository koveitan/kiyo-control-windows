using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KiyoControl;

/// <summary>A small dark dialog that asks for one line of text.</summary>
public static class InputDialog
{
    public static string? Ask(Window owner, string title, string message, string okLabel)
    {
        var res = System.Windows.Application.Current.Resources;
        var box = new TextBox { Margin = new Thickness(0, 12, 0, 0), MinWidth = 300 };
        var ok = new Button { Content = okLabel, Style = (Style)res["GreenButton"], IsDefault = true };
        var cancel = new Button { Content = "Cancel", Style = (Style)res["GreyButton"], IsCancel = true, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        var dialog = new Window
        {
            Title = title,
            Owner = owner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = (Brush)res["SurfaceBrush"],
            Foreground = (Brush)res["TextBrush"],
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = message },
                    box,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 16, 0, 0),
                        Children = { cancel, ok },
                    },
                },
            },
        };
        ok.Click += (_, _) => dialog.DialogResult = true;
        dialog.Loaded += (_, _) => Keyboard.Focus(box);
        return dialog.ShowDialog() == true ? box.Text : null;
    }
}
