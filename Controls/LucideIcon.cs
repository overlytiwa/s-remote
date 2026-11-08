using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SamsungMiniRemote.Controls;

/// <summary>
/// Lightweight vector icons using the 24×24 Lucide stroke geometry.
/// No emoji, Unicode icon glyphs, or platform-specific icon fonts.
/// </summary>
public sealed class LucideIcon : System.Windows.Controls.Control
{
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(RemoteIcon), typeof(LucideIcon),
            new FrameworkPropertyMetadata(RemoteIcon.Power, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(LucideIcon),
            new FrameworkPropertyMetadata(1.85, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillGeometryProperty =
        DependencyProperty.Register(nameof(FillGeometry), typeof(bool), typeof(LucideIcon),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public RemoteIcon Kind
    {
        get => (RemoteIcon)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool FillGeometry
    {
        get => (bool)GetValue(FillGeometryProperty);
        set => SetValue(FillGeometryProperty, value);
    }

    private static readonly Dictionary<RemoteIcon, GeometryEntry[]> Geometries = BuildGeometries();

    public LucideIcon()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (!Geometries.TryGetValue(Kind, out var parts)) return;
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var size = Math.Min(ActualWidth, ActualHeight);
        double scale = size / 24.0;
        dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));

        var pen = new System.Windows.Media.Pen(Foreground, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        foreach (var entry in parts)
        {
            var fill = entry.Filled || FillGeometry ? Foreground : null;
            dc.DrawGeometry(fill, pen, entry.Geometry);
        }

        dc.Pop();
        dc.Pop();
    }

    private static Dictionary<RemoteIcon, GeometryEntry[]> BuildGeometries()
    {
        var source = new Dictionary<RemoteIcon, (string path, bool filled)[]>
        {
            [RemoteIcon.Mute] = new[]
            {
                ("M11,5 L6,9 H2 V15 H6 L11,19 Z", false),
                ("M22,9 L16,15", false),
                ("M16,9 L22,15", false)
            },
            [RemoteIcon.VolumeOff] = new[]
            {
                ("M11,5 L6,9 H2 V15 H6 L11,19 Z", false),
                ("M22,9 L16,15", false),
                ("M16,9 L22,15", false)
            },
            [RemoteIcon.VolumeOn] = new[]
            {
                ("M11,5 L6,9 H2 V15 H6 L11,19 Z", false),
                ("M15.54,8.46 A5,5 0 0 1 15.54,15.54", false),
                ("M19.07,4.93 A10,10 0 0 1 19.07,19.07", false)
            },
            [RemoteIcon.VolumeDown] = new[]
            {
                ("M11,5 L6,9 H2 V15 H6 L11,19 Z", false),
                ("M16,12 H22", false)
            },
            [RemoteIcon.VolumeUp] = new[]
            {
                ("M11,5 L6,9 H2 V15 H6 L11,19 Z", false),
                ("M16,12 H22", false),
                ("M19,9 V15", false)
            },
            [RemoteIcon.Power] = new[]
            {
                ("M18.36,6.64 A9,9 0 1 1 5.64,6.64", false),
                ("M12,2 V12", false)
            },
            [RemoteIcon.ChevronRight] = new[] { ("M9,18 L15,12 L9,6", false) },
            [RemoteIcon.ChevronLeft] = new[] { ("M15,18 L9,12 L15,6", false) },
            [RemoteIcon.ChevronDown] = new[] { ("M6,9 L12,15 L18,9", false) },
            [RemoteIcon.PlayPause] = new[]
            {
                ("M3,5 L3,19 L13,12 Z", false),
                ("M17,5 V19", false),
                ("M21,5 V19", false)
            },
            [RemoteIcon.Play] = new[]
            {
                ("M6,4 L6,20 L20,12 Z", false)
            },
            [RemoteIcon.Pause] = new[]
            {
                ("M6,4 V20", false),
                ("M18,4 V20", false)
            },
            [RemoteIcon.Home] = new[]
            {
                ("M3,12 L12,3 L21,12", false),
                ("M5,10 V20 H19 V10", false),
                ("M9,20 V14 H15 V20", false)
            },
            [RemoteIcon.Monitor] = new[]
            {
                ("M2,4 H22 V18 H2 Z", false),
                ("M8,22 H16", false),
                ("M12,18 V22", false)
            },
            [RemoteIcon.ArrowUp] = new[] { ("M12,19 V5", false), ("M5,12 L12,5 L19,12", false) },
            [RemoteIcon.ArrowDown] = new[] { ("M12,5 V19", false), ("M19,12 L12,19 L5,12", false) },
            [RemoteIcon.ArrowLeft] = new[] { ("M19,12 H5", false), ("M12,19 L5,12 L12,5", false) },
            [RemoteIcon.ArrowRight] = new[] { ("M5,12 H19", false), ("M12,5 L19,12 L12,19", false) },
            [RemoteIcon.Minus] = new[] { ("M5,12 H19", false) },
            [RemoteIcon.Close] = new[] { ("M18,6 L6,18", false), ("M6,6 L18,18", false) },
            [RemoteIcon.Undo] = new[]
            {
                ("M9,14 L4,9 L9,4", false),
                ("M4,9 H14.5 A5.5,5.5 0 0 1 20,14.5 A5.5,5.5 0 0 1 14.5,20 H11", false)
            },
            [RemoteIcon.Wifi] = new[]
            {
                ("M12,20 L12.01,20", false),
                ("M8.5,16.5 A5,5 0 0 1 15.5,16.5", false),
                ("M5,13 A9,9 0 0 1 19,13", false),
                ("M1.5,9.5 A13,13 0 0 1 22.5,9.5", false)
            },
            [RemoteIcon.WifiOff] = new[]
            {
                ("M12,20 L12.01,20", false),
                ("M1,1 L23,23", false)
            },
            [RemoteIcon.Loader] = new[]
            {
                ("M12,2 V6", false),
                ("M12,18 V22", false),
                ("M4.93,4.93 L7.76,7.76", false),
                ("M16.24,16.24 L19.07,19.07", false),
                ("M2,12 H6", false),
                ("M18,12 H22", false),
                ("M4.93,19.07 L7.76,16.24", false),
                ("M16.24,7.76 L19.07,4.93", false)
            }
        };

        var dictionary = new Dictionary<RemoteIcon, GeometryEntry[]>();
        foreach (var (icon, data) in source)
        {
            var parts = new GeometryEntry[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                var geo = Geometry.Parse(data[i].path);
                geo.Freeze();
                parts[i] = new GeometryEntry(geo, data[i].filled);
            }
            dictionary[icon] = parts;
        }
        return dictionary;
    }

    private readonly record struct GeometryEntry(Geometry Geometry, bool Filled);
}

public enum RemoteIcon
{
    Mute,
    VolumeOff,
    VolumeOn,
    VolumeDown,
    VolumeUp,
    Power,
    ChevronRight,
    ChevronLeft,
    ChevronDown,
    PlayPause,
    Play,
    Pause,
    Home,
    Monitor,
    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,
    Minus,
    Close,
    Undo,
    Wifi,
    WifiOff,
    Loader
}
