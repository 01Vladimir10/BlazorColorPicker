using System.Globalization;
using System.Text.RegularExpressions;
using System.Timers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Timer = System.Timers.Timer;

namespace ColorPicker.Shared;

public sealed partial class ColorPickerComponent : IDisposable
{
    private const int UnitPixel = 10;
    private Rgb _baseSelectedColor = new(255, 0, 0);
    private Rgb _selectedColorRgb = new(10, 10, 10);
    private Hsv _selectedHsvColor = new();

    private string _oldInputColor = string.Empty;
    [Parameter] public double Size { get; set; } = 22;
    [Parameter] public string Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public ColorFormat OutputFormat { get; set; } = ColorFormat.Hex;

    private bool _isMovingInWindow;
    private double SizeInPixels => Size * UnitPixel;

    private int Hue
    {
        get => _selectedHsvColor.Hue;
        set
        {
            if (value == _selectedHsvColor.Hue)
                return;
            _selectedHsvColor.Hue = value;
            OnColorChanged();
        }
    }

    private double Saturation
    {
        get => _selectedHsvColor.Saturation;
        set => _selectedHsvColor.Saturation = value;
    }

    private double ValueColor
    {
        get => _selectedHsvColor.Value;
        set => _selectedHsvColor.Value = value;
    }

    private double Opacity
    {
        get => _selectedHsvColor.Opacity;
        set
        {
            if (Math.Abs(value - _selectedHsvColor.Opacity) < 0.01)
                return;
            _selectedHsvColor.Opacity = value;
            OnColorChanged();
        }
    }

    private void OnMouseMoveInWindow(MouseEventArgs mouseEvent)
    {
        if (!_isMovingInWindow)
            return;
        MovePickerPointer(mouseEvent);
    }

    private void MovePickerPointer(MouseEventArgs mouseEvent)
    {
        Saturation = mouseEvent.OffsetX / SizeInPixels;
        ValueColor = 1 - mouseEvent.OffsetY / SizeInPixels;
        OnColorChanged();
    }

    private void OnColorChanged()
    {
        _selectedColorRgb = _selectedHsvColor.ToRgb();
        _baseSelectedColor = new Hsv(Hue, 1, 1).ToRgb();
        PublishValue();
    }

    private Timer _updateValueTimer = new(50);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _updateValueTimer.Elapsed += UpdateValueTimerOnElapsed;
    }

    private void PublishValue()
    {
        _updateValueTimer.Stop();
        _updateValueTimer.Start();
    }

    private void UpdateValueTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        ValueChanged.InvokeAsync(_selectedHsvColor.ToCss(OutputFormat));
    }

    public void Dispose()
    {
        _updateValueTimer.Elapsed -= UpdateValueTimerOnElapsed;
        _updateValueTimer.Dispose();
    }
}

public enum ColorFormat
{
    Hex,
    Rgb
}

internal record Rgb(int Red, int Green, int Blue, double Opacity = 0);

internal class Hsv
{
    public override string ToString()
    {
        return
            $"{nameof(Opacity)}: {Opacity}, {nameof(Hue)}: {Hue}, {nameof(Saturation)}: {Saturation}, {nameof(Value)}: {Value}";
    }

    public Hsv()
    {
    }

    public Hsv(int hue, double saturation, double value, double opacity = 1.0)
    {
        Hue = hue;
        Saturation = saturation;
        Value = value;
        Opacity = opacity;
    }

    public double Opacity { get; set; } = 1;
    public int Hue { get; set; }
    public double Saturation { get; set; }
    public double Value { get; set; }
}

internal static partial class ColorComponentExtensions
{
    internal static string ToCss(this Rgb rgb) => $"rgba({rgb.Red}, {rgb.Green}, {rgb.Blue}, {rgb.Opacity})";
    internal static string ToCss(this Hsv hsv) => $"hsv({hsv.Hue}, {hsv.Saturation}, {hsv.Value}, {hsv.Opacity})";

    internal static string ToCss(this Hsv hsv, ColorFormat format) =>
        format == ColorFormat.Hex ? hsv.ToRgb().ToHex() : hsv.ToRgb().ToCss();

    private static Hsv ToHsv(this Rgb rgb)
    {
        var r = rgb.Red / 255.0;
        var g = rgb.Green / 255.0;
        var b = rgb.Blue / 255.0;
        var max = Math.Max(Math.Max(r, g), b);
        var min = Math.Min(Math.Min(r, g), b);
        var c = max - min;

        double h;
        if (c == 0)
        {
            h = 0;
        }
        else if (Math.Abs(max - r) < 0.001)
        {
            h = (g - b) / c;
            if (h < 0)
            {
                h += 6;
            }
        }
        else if (Math.Abs(max - g) < 0.001)
        {
            h = 2 + (b - r) / c;
        }
        else
        {
            h = 4 + (r - g) / c;
        }

        h *= 60;
        var s = max == 0 ? 0 : c / max;
        return new Hsv((int)h, s, max, rgb.Opacity);
    }

    internal static Rgb ToRgb(this Hsv color)
    {
        double green;
        double blue;
        var hue = color.Hue % 360 / 60.0;
        var chroma = color.Value * color.Saturation;
        var x = chroma * (1 - Math.Abs(hue % 2 - 1));
        var red = green = blue = color.Value - chroma;
        var hueIndex = (int)Math.Floor(hue);
        red += new[] { chroma, x, 0.0, 0.0, x, chroma }[hueIndex];
        green += new[] { x, chroma, chroma, x, 0.0, 0.0 }[hueIndex];
        blue += new[] { 0.0, 0.0, x, chroma, chroma, x }[hueIndex];
        return new Rgb((int)Math.Floor(red * 255), (int)Math.Floor(green * 255), (int)Math.Floor(blue * 255),
            color.Opacity);
    }

    internal static string ToHex(this Hsv hsv) => hsv.ToRgb().ToHex();

    internal static string ToHex(this Rgb rgb) =>
        $"#{(16777216 | rgb.Blue | (rgb.Green << 8) | (rgb.Red << 16)).ToString("X")[1..]}";

    private static Rgb ToRgb(this string hex)
    {
        var rgb = int.Parse(hex.Replace("#", ""), NumberStyles.HexNumber);
        var r = (rgb & 0xff0000) >> 16;
        var g = (rgb & 0xff00) >> 8;
        var b = rgb & 0xff;
        return new Rgb(r, g, b, 1);
    }

    private static Hsv HexToHsv(this string hex)
    {
        var rgb = hex.ToRgb();
        var hsv = ToHsv(rgb);
        return hsv;
    }

    private static readonly Regex HexColorRegex = _HexColorRegex();
    private static bool IsHexColor(string hex) => HexColorRegex.IsMatch(hex);
    private static bool IsRgbColor(string rgb) => rgb.StartsWith("rgb(") && rgb.EndsWith(")");

    internal static Hsv ReadColor(string cssColor)
    {
        var color = cssColor.Trim();

        if (IsHexColor(color))
        {
            return HexToHsv(color);
        }

        color = color.Replace(" ", "");

        if (!RgbaColorRegex.IsMatch(color))
            return new Hsv(180, 1, 1) { Opacity = 1 };

        var rgbaMatch = RgbaColorRegex.Match(color);
        var rgbaColor = new Rgb(
            Red: int.Parse(rgbaMatch.Groups["r"].Value),
            Green: int.Parse(rgbaMatch.Groups["g"].Value),
            Blue: int.Parse(rgbaMatch.Groups["b"].Value),
            Opacity: double.Parse(rgbaMatch.Groups.ContainsKey("a") ? rgbaMatch.Groups["b"].Value : "1.0")
        );
        return rgbaColor.ToHsv();
    }

    private static readonly Regex RgbColorRegex = _RgbRegex();
    private static readonly Regex RgbaColorRegex = _RgbaRegex();

    [GeneratedRegex(@"/rgba?\((?<r>[.\d]+)[, ]+(?<g>[.\d]+)[, ]+(?<b>[.\d]+)(?:\s?[,\/]\s?(?<a>[.\d]+%?))?\)/ig",
        RegexOptions.Compiled)]
    private static partial Regex _RgbaRegex();

    [GeneratedRegex(@"rgb\((\d{1,3}),(\d{1,3}),(\d?\.?\d+)\)", RegexOptions.Compiled)]
    private static partial Regex _RgbRegex();

    [GeneratedRegex("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", RegexOptions.Compiled)]
    private static partial Regex _HexColorRegex();
}