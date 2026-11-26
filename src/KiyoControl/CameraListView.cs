using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Kiyo.Core;

namespace KiyoControl;

/// <summary>
/// Every USB camera plugged in, with what the app can do with it: connected, switch to it, not responding,
/// a Razer model not supported yet, or not a Kiyo. Shown in the settings panel while no camera is connected.
/// </summary>
public static class CameraListView
{
    /// <summary>The "No Kiyo connected" card shown in place of the settings while disconnected.</summary>
    public static UIElement NoCameraCard(AppModel model)
    {
        var card = new Border
        {
            BorderBrush = Res<Brush>("BorderBrushKiyo"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 20, 0, 20),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "NO KIYO CONNECTED", FontSize = 14, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 14) },
                    List(model),
                },
            },
        };
        return card;
    }

    public static UIElement List(AppModel model)
    {
        var root = new StackPanel();
        if (model.Cameras.Count == 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = "No USB cameras found. Plug the Kiyo straight into the PC.",
                Foreground = Res<Brush>("SecondaryTextBrush"),
                TextWrapping = TextWrapping.Wrap,
            });
        }
        for (var i = 0; i < model.Cameras.Count; i++) root.Children.Add(Row(model, model.Cameras[i], i + 1));

        root.Children.Add(new Border { Height = 1, Background = Res<Brush>("DividerBrush"), Margin = new Thickness(0, 12, 0, 10) });
        var check = new Button
        {
            Style = Res<Style>("GreyButton"),
            Content = "Check Again",
            ToolTip = "Look for cameras and try to connect again",
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10, 0, 0, 0),
        };
        check.Click += (_, _) => model.Poll();
        var footer = new DockPanel();
        DockPanel.SetDock(check, Dock.Right);
        footer.Children.Add(check);
        footer.Children.Add(new TextBlock
        {
            Text = "Supported: " + string.Join(", ", KiyoDevice.All.Select(d => d.Name)),
            Style = Res<Style>("Caption"),
        });
        root.Children.Add(footer);
        return root;
    }

    private static UIElement Row(AppModel model, UsbCamera camera, int number)
    {
        var c = model.Controller;
        var connected = c.Connected && CameraDiscovery.SameDevice(camera.Id, c.CameraId);
        // The camera the app tries to connect to; while disconnected, that one is not responding.
        var chosen = model.ChosenCamera is { } ch && CameraDiscovery.SameDevice(ch.Id, camera.Id);
        var canSwitch = camera.Device is not null && !connected && (c.Connected || !chosen);

        var (glyph, color) = camera.Status switch
        {
            UsbCamera.Kind.Supported when connected => ("", Res<Brush>("AccentBrush")),
            UsbCamera.Kind.UnsupportedRazer => ("", new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x18))),
            _ => ("", Res<Brush>(camera.Status == UsbCamera.Kind.Other ? "SecondaryTextBrush" : "TextBrush")),
        };
        var detail = camera.Status switch
        {
            UsbCamera.Kind.Supported when c.Connected => camera.UsbId,
            UsbCamera.Kind.Supported => chosen ? "Not responding. Unplug it and plug it back in." : camera.UsbId,
            UsbCamera.Kind.UnsupportedRazer => $"{camera.UsbId} · not supported yet",
            _ => "Not a Kiyo",
        };

        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        text.Children.Add(new TextBlock { Text = camera.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock { Text = detail, Style = Res<Style>("Caption"), Margin = new Thickness(0, 2, 0, 0) });
        if (camera.Status == UsbCamera.Kind.UnsupportedRazer)
        {
            var link = new Hyperlink(new Run("Help add it…"))
            {
                NavigateUri = new Uri(CameraDiscovery.SupportRequestUrl(camera, number)),
                Foreground = Res<Brush>("AccentBrush"),
                ToolTip = "Opens a GitHub issue with this camera's USB id, asking for the details needed to add it.",
            };
            link.RequestNavigate += (_, e) => Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            text.Children.Add(new TextBlock(link) { FontSize = 11, Margin = new Thickness(0, 2, 0, 0) });
        }

        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Opacity = camera.Status == UsbCamera.Kind.Other ? 0.5 : 1 };
        if (connected)
        {
            var label = new TextBlock { Text = "Connected", Foreground = Res<Brush>("AccentBrush") };
            DockPanel.SetDock(label, Dock.Right);
            row.Children.Add(label);
        }
        else if (canSwitch)
        {
            var use = new Button { Style = Res<Style>("GreyButton"), Content = "Use", VerticalAlignment = VerticalAlignment.Top };
            use.Click += (_, _) => model.Select(camera);
            DockPanel.SetDock(use, Dock.Right);
            row.Children.Add(use);
        }
        var icon = new TextBlock { FontFamily = Res<FontFamily>("IconFont"), Text = glyph, Foreground = color, Width = 18, Margin = new Thickness(0, 2, 0, 0) };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(text);
        return row;
    }

    private static T Res<T>(string key) => (T)System.Windows.Application.Current.Resources[key];
}
