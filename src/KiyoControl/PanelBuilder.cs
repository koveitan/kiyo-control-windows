using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Kiyo.Core;
using K = Kiyo.Core.KiyoControls;

namespace KiyoControl;

public enum Tab { Camera, Processing, Image }

/// <summary>
/// Builds the settings panel for one tab, Synapse-style, like the Mac app. Every widget registers an
/// updater that re-reads the controller, so the panel is built once and then only refreshed.
/// </summary>
public sealed class PanelBuilder(AppModel model, Dictionary<string, bool> collapsed)
{
    private readonly List<Action> _updaters = [];
    private bool _updating;
    private CameraController C => model.Controller;

    /// <summary>Re-reads every value from the controller.</summary>
    public void Update()
    {
        _updating = true;
        try { foreach (var u in _updaters) u(); }
        finally { _updating = false; }
    }

    public UIElement Build(Tab tab)
    {
        var panel = new StackPanel();
        var sections = tab switch
        {
            Tab.Camera => CameraTab(),
            Tab.Processing => ProcessingTab(),
            _ => ImageTab(),
        };
        foreach (var s in sections) panel.Children.Add(s);
        Update();
        return panel;
    }

    // Tabs

    private IEnumerable<UIElement> CameraTab()
    {
        yield return Section("Zoom", onReset: () => C.ResetToDefault([K.Zoom, K.Pan, K.Tilt]), content:
        [
            Slider(K.Zoom, format: v => C.SupportsRazer ? $"{Fmt.Zoom(v)} · {Fmt.Fov(v)}" : Fmt.Zoom(v)),
            Slider(K.Pan, format: Fmt.Degrees, minLabel: "LEFT", maxLabel: "RIGHT"),
            Slider(K.Tilt, format: Fmt.Degrees, minLabel: "DOWN", maxLabel: "UP"),
            ZoomPresets(),
        ]);
        if (C.SupportsRazer)
        {
            yield return Section("Field of View", content:
            [
                Options(null, FieldOfView.Presets.Select(p => ((object)p, $"{p} {Fmt.Fov(p.Zoom())}")),
                    () => FieldOfView.PresetFor(C.Value(K.Zoom)), v => C.Set(K.Zoom, ((FieldOfView.Preset)v).Zoom())),
                Caption("Sets the zoom; the camera has no separate field-of-view setting."),
            ]);
        }
        yield return Section("Auto Focus", toggle: K.AutoFocus, content:
        [
            Slider(K.Focus, label: "Manual focus", minLabel: "NEAR", maxLabel: "FAR", disabled: () => C.IsAuto(K.AutoFocus)),
            RazerOptions("Mode", [(false, "Standard"), (true, "Face")], r => r.FaceFocus, (r, v) => r.FaceFocus = (bool)v),
            RazerOptions("Tracking", [(false, "Passive"), (true, "Responsive")], r => r.ResponsiveTracking, (r, v) => r.ResponsiveTracking = (bool)v),
            RazerOptions("Lighting", [(false, "Standard"), (true, "Stylized")], r => r.StylizedLighting, (r, v) => r.StylizedLighting = (bool)v),
        ]);
        yield return Section("Auto Exposure", toggle: K.AutoExposure, onReset: () => C.ResetToDefault([K.AutoExposure]), content:
        [
            // Kiyo V2 Pro: standard exposure time and gain in manual mode.
            Visible(() => !C.SupportsRazer && !C.IsAuto(K.AutoExposure),
                Slider(K.ExposureTime, label: "Shutter speed", format: Fmt.ExposureLog2, minLabel: "FAST", maxLabel: "SLOW"),
                Slider(K.Gain)),
            // Kiyo Pro Ultra: Razer metering and compensation in auto, ISO and shutter in manual.
            Visible(() => C.SupportsRazer && C.IsAuto(K.AutoExposure),
                RazerOptions("Metering", [(RazerCommand.Metering.Average, "Average"), (RazerCommand.Metering.Center, "Center"), (RazerCommand.Metering.Face, "Face")],
                    r => r.Metering, (r, v) => r.Metering = (RazerCommand.Metering)v),
                Compensation()),
            Visible(() => C.SupportsRazer && !C.IsAuto(K.AutoExposure),
                RazerOptions("ISO", RazerCommand.IsoSteps.Select(i => ((object)i, i.ToString())), r => r.Iso, (r, v) => r.Iso = (int)v),
                Shutter()),
        ]);
    }

    private IEnumerable<UIElement> ProcessingTab()
    {
        if (C.SupportsRazer)
        {
            yield return Section("Noise Reduction", content:
            [
                RazerOptions("3D (between frames)", [(false, "Off"), (true, "On")], r => r.NoiseReduction3D, (r, v) => r.NoiseReduction3D = (bool)v),
                RazerOptions("2D (within a frame)", [(false, "Off"), (true, "On")], r => r.NoiseReduction2D, (r, v) => r.NoiseReduction2D = (bool)v),
            ]);
        }
        yield return Section("HDR", content:
        [
            Options(null, [(0, "Off"), (1, "On")], () => C.Value(K.Backlight), v => C.Set(K.Backlight, (int)v), hidden: () => !C.Has(K.Backlight)),
            Caption("Brightens shadows without blowing out highlights, using the camera's backlight compensation."),
        ]);
        if (C.SupportsRazer)
        {
            yield return Section("Lens Distortion Compensation", content:
            [
                RazerOptions(null, [(false, "Off (82°)"), (true, "On (72°)")], r => r.LensCorrection, (r, v) => r.LensCorrection = (bool)v),
                Caption("Straightens lines at the edges. Takes effect after SAVE and replugging the camera."),
            ]);
        }
    }

    private IEnumerable<UIElement> ImageTab()
    {
        yield return Section("Image", onReset: () => C.ResetToDefault([K.Brightness, K.Contrast, K.Saturation, K.Sharpness]), content:
        [
            Slider(K.Brightness),
            Slider(K.Contrast),
            Slider(K.Saturation),
            Slider(K.Sharpness),
        ]);
        yield return Section("Auto White Balance", toggle: K.AutoWhiteBalance, content:
        [
            Slider(K.WhiteBalance, label: "Temperature", format: Fmt.Kelvin, minLabel: "WARM", maxLabel: "COOL",
                disabled: () => C.IsAuto(K.AutoWhiteBalance)),
        ]);
        if (C.SupportsRazer)
        {
            yield return Section("Mirror Video", content:
            [
                RazerOptions(null, [(false, "Off"), (true, "On")], r => r.Mirror, (r, v) => r.Mirror = (bool)v),
            ]);
        }
        yield return Section("Anti-Flicker", content:
        [
            Options(null, K.AntiFlicker.AvailableChoices(C.Range(K.AntiFlicker)).Select(c => ((object)c.Value, c.Label)),
                () => C.Value(K.AntiFlicker), v => C.Set(K.AntiFlicker, (int)v), hidden: () => !C.Has(K.AntiFlicker)),
            Caption("Match your mains frequency: 50 Hz in Europe, 60 Hz in North America."),
        ]);
    }

    // Widgets

    private void Act(Action<CameraController> action)
    {
        if (!_updating) model.Perform(action);
    }

    /// <summary>Collapsible section header with an optional auto switch and reset button.</summary>
    private UIElement Section(string title, UIElement[] content, CameraControl? toggle = null, Action? onReset = null)
    {
        var root = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var chevron = new TextBlock { FontFamily = Res<FontFamily>("IconFont"), FontSize = 11, Text = "", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var titleButton = new Button
        {
            Style = Res<Style>("PlainButton"),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { chevron, new TextBlock { Text = title.ToUpperInvariant(), FontSize = 14, FontWeight = FontWeights.Medium } },
            },
        };
        header.Children.Add(titleButton);

        if (toggle is not null)
        {
            var sw = new ToggleButton { Style = Res<Style>("Switch"), Margin = new Thickness(12, 0, 0, 0), ToolTip = $"{toggle.Name} on/off" };
            sw.Click += (_, _) =>
            {
                var on = sw.IsChecked == true;
                Act(c => c.Set(toggle, toggle.Id == K.AutoExposure.Id ? (on ? K.ExposureAuto : K.ExposureManual) : (on ? 1 : 0)));
            };
            _updaters.Add(() =>
            {
                sw.IsChecked = C.IsAuto(toggle);
                sw.Visibility = C.Has(toggle) ? Visibility.Visible : Visibility.Collapsed;
            });
            header.Children.Add(sw);
        }
        if (onReset is not null)
        {
            var reset = new Button
            {
                Style = Res<Style>("PlainButton"),
                Content = new TextBlock { FontFamily = Res<FontFamily>("IconFont"), Text = "", FontSize = 13 },
                Margin = new Thickness(12, 0, 0, 0),
                ToolTip = "Reset to camera defaults",
            };
            reset.Click += (_, _) => Act(_ => onReset());
            header.Children.Add(reset);
        }
        root.Children.Add(header);

        var body = new StackPanel();
        foreach (var c in content)
        {
            if (c is FrameworkElement fe && fe.Margin == default) fe.Margin = new Thickness(0, 12, 0, 0);
            body.Children.Add(c);
        }
        root.Children.Add(body);
        root.Children.Add(new Border { Height = 1, Background = Res<Brush>("DividerBrush"), Margin = new Thickness(0, 14, 0, 0) });

        void Apply()
        {
            var isCollapsed = collapsed.GetValueOrDefault(title);
            body.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
            chevron.RenderTransform = new RotateTransform(isCollapsed ? -90 : 0, 5.5, 7);
        }
        titleButton.Click += (_, _) =>
        {
            collapsed[title] = !collapsed.GetValueOrDefault(title);
            Apply();
        };
        Apply();
        return root;
    }

    /// <summary>Slider bound to a camera control, with a formatted value and a Default link.</summary>
    private UIElement Slider(CameraControl control, string? label = null, Func<int, string>? format = null,
                             string? minLabel = null, string? maxLabel = null, Func<bool>? disabled = null)
    {
        format ??= v => v.ToString();
        var valueText = new TextBlock { Foreground = Res<Brush>("SecondaryTextBrush"), Margin = new Thickness(8, 0, 0, 0) };
        var defaultButton = new Button { Style = Res<Style>("PlainButton"), Content = new TextBlock { Text = "Default", FontSize = 11, Foreground = Res<Brush>("SecondaryTextBrush") } };
        var top = new DockPanel();
        DockPanel.SetDock(defaultButton, Dock.Right);
        top.Children.Add(defaultButton);
        top.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = label ?? control.Name }, valueText } });

        var slider = new Slider { Style = Res<Style>("KiyoSlider"), Margin = new Thickness(0, 4, 0, 0), IsSnapToTickEnabled = true };
        var row = new StackPanel { Children = { top, slider } };
        if (minLabel is not null || maxLabel is not null) row.Children.Add(Ends(minLabel, maxLabel));

        slider.ValueChanged += (_, e) =>
        {
            var v = (int)Math.Round(e.NewValue);
            valueText.Text = format(v);
            if (!_updating && v != C.Value(control)) Act(c => c.Set(control, v));
        };
        defaultButton.Click += (_, _) =>
        {
            if (C.Range(control) is ControlRange r) Act(c => c.Set(control, r.Default));
        };
        _updaters.Add(() =>
        {
            var range = C.Range(control);
            row.Visibility = range is null ? Visibility.Collapsed : Visibility.Visible;
            if (range is not ControlRange r) return;
            slider.Minimum = r.Min;
            slider.Maximum = Math.Max(r.Max, r.Min + 1);
            slider.TickFrequency = Math.Max(1, r.Step);
            var v = C.Value(control);
            slider.Value = v;
            valueText.Text = format(v);
            defaultButton.Visibility = v != r.Default ? Visibility.Visible : Visibility.Hidden;
            var off = disabled?.Invoke() == true;
            row.IsEnabled = !off;
            row.Opacity = off ? 0.4 : 1;
        });
        return row;
    }

    /// <summary>A slider not tied to a camera control (Razer compensation, shutter).</summary>
    private UIElement CustomSlider(string label, Func<string> valueText, Func<(double Min, double Max)> range, Func<double> get,
                                   Action<int> set, string? minLabel = null, string? maxLabel = null)
    {
        var text = new TextBlock { Foreground = Res<Brush>("SecondaryTextBrush"), Margin = new Thickness(8, 0, 0, 0) };
        var slider = new Slider { Style = Res<Style>("KiyoSlider"), Margin = new Thickness(0, 4, 0, 0), IsSnapToTickEnabled = true, TickFrequency = 1 };
        var row = new StackPanel
        {
            Children = { new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = label }, text } }, slider },
        };
        if (minLabel is not null || maxLabel is not null) row.Children.Add(Ends(minLabel, maxLabel));
        slider.ValueChanged += (_, e) =>
        {
            if (!_updating) Act(_ => set((int)Math.Round(e.NewValue)));
        };
        _updaters.Add(() =>
        {
            var (min, max) = range();
            slider.Minimum = min;
            slider.Maximum = max;
            slider.Value = get();
            text.Text = valueText();
        });
        return row;
    }

    private UIElement Compensation() => CustomSlider(
        "Compensation",
        () => $"{(C.Razer.ExposureCompensation ?? 0) / 10.0:+0.0;-0.0;0.0} EV",
        () => (C.Razer.Metering == RazerCommand.Metering.Face ? -10 : -30, 30),
        () => C.Razer.ExposureCompensation ?? 0,
        v => C.SetRazer(r => r.ExposureCompensation = v));

    private UIElement Shutter()
    {
        var divs = Fmt.ShutterDivisors;
        int Index() => C.Razer.ShutterMicroseconds is int us
            ? Enumerable.Range(0, divs.Length).MinBy(i => Math.Abs(1_000_000 / divs[i] - us))
            : Array.IndexOf(divs, 60);
        return CustomSlider(
            "Shutter speed",
            () => C.Razer.ShutterMicroseconds is null ? "–" : $"1/{divs[Index()]}S",
            () => (0, divs.Length - 1),
            () => Index(),
            i => C.SetRazer(r => r.ShutterMicroseconds = 1_000_000 / divs[Math.Clamp(i, 0, divs.Length - 1)]),
            "FAST", "SLOW");
    }

    /// <summary>Row of bordered option buttons; the selected one is outlined green.</summary>
    private UIElement Options(string? title, IEnumerable<(object Value, string Label)> options, Func<object?> selection,
                              Action<object> onSelect, Func<bool>? hidden = null)
    {
        var list = options.ToList();
        var root = new StackPanel();
        if (title is not null) root.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 0, 0, 6) });
        var grid = new UniformGrid { Columns = Math.Min(list.Count, list.Count > 4 ? 4 : list.Count) };
        var buttons = new List<(ToggleButton Button, object Value)>();
        foreach (var (value, label) in list)
        {
            var b = new ToggleButton { Style = Res<Style>("OptionButton"), Content = label, Margin = new Thickness(0, 0, 8, 8) };
            b.Click += (_, _) => Act(c => onSelect(value));
            buttons.Add((b, value));
            grid.Children.Add(b);
        }
        root.Children.Add(grid);
        _updaters.Add(() =>
        {
            var sel = selection();
            foreach (var (b, v) in buttons) b.IsChecked = sel is not null && sel.Equals(v);
            root.Visibility = hidden?.Invoke() == true ? Visibility.Collapsed : Visibility.Visible;
        });
        return root;
    }

    /// <summary>
    /// Option buttons for a Razer setting. Nothing is selected while the setting is unknown (the camera
    /// can't be asked), so an untouched Synapse setting is left alone.
    /// </summary>
    private UIElement RazerOptions(string? title, IEnumerable<(object Value, string Label)> options,
                                   Func<RazerSettings, object?> get, Action<RazerSettings, object> set) =>
        Options(title, options, () => get(C.Razer), v => C.SetRazer(r => set(r, v)), hidden: () => !C.SupportsRazer);

    private UIElement ZoomPresets()
    {
        var root = new StackPanel();
        root.Children.Add(new TextBlock { Text = "Presets", Margin = new Thickness(0, 0, 0, 6) });
        var grid = new UniformGrid { Columns = ZoomPreset.Slots };
        var buttons = new List<ToggleButton>();
        for (var slot = 0; slot < ZoomPreset.Slots; slot++)
        {
            var s = slot;
            var b = new ToggleButton { Style = Res<Style>("OptionButton"), Content = $"{s + 1}", Margin = new Thickness(0, 0, 8, 0) };
            b.Click += (_, _) => Act(c => c.ApplyZoomPreset(s));
            var store = new MenuItem { Header = $"Store Current View in {s + 1}" };
            store.Click += (_, _) => Act(c => c.StoreZoomPreset(s));
            b.ContextMenu = new ContextMenu { Items = { store } };
            buttons.Add(b);
            grid.Children.Add(b);
        }
        root.Children.Add(grid);
        root.Children.Add(new TextBlock { Text = "Right-click a number to store the current view. Also in the tray icon menu.", Style = Res<Style>("Caption"), Margin = new Thickness(0, 6, 0, 0) });
        _updaters.Add(() =>
        {
            var current = new ZoomPreset(C.Value(K.Zoom), C.Value(K.Pan), C.Value(K.Tilt));
            for (var i = 0; i < buttons.Count; i++)
            {
                var p = C.ZoomPresets[i];
                buttons[i].IsChecked = p is not null && p == current;
                buttons[i].Opacity = p is null ? 0.45 : 1;
                buttons[i].ToolTip = p is null ? "Empty: right-click to store the current view" : Fmt.Zoom(p.Zoom);
            }
        });
        return root;
    }

    private UIElement Visible(Func<bool> visible, params UIElement[] children)
    {
        var panel = new StackPanel();
        foreach (var c in children)
        {
            if (c is FrameworkElement fe && fe.Margin == default) fe.Margin = new Thickness(0, 0, 0, 12);
            panel.Children.Add(c);
        }
        _updaters.Add(() => panel.Visibility = visible() ? Visibility.Visible : Visibility.Collapsed);
        return panel;
    }

    private static UIElement Caption(string text) => new TextBlock { Text = text, Style = Res<Style>("Caption") };

    private static UIElement Ends(string? min, string? max)
    {
        var g = new Grid();
        g.Children.Add(new TextBlock { Text = min ?? "", Style = Res<Style>("Caption"), HorizontalAlignment = HorizontalAlignment.Left });
        g.Children.Add(new TextBlock { Text = max ?? "", Style = Res<Style>("Caption"), HorizontalAlignment = HorizontalAlignment.Right });
        return g;
    }

    private static T Res<T>(string key) => (T)System.Windows.Application.Current.Resources[key];
}
