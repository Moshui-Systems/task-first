using System.Globalization;
using System.Windows.Data;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;

namespace TaskFirst.UI;

/// <summary>Green dot when a rule is enabled, dim grey when disabled.</summary>
public sealed class BoolDotConverter : IValueConverter
{
    public static readonly BoolDotConverter Instance = new();

    private static readonly Brush On = new SolidColorBrush(Color.FromRgb(0x3F, 0xD0, 0x7A));
    private static readonly Brush Off = new SolidColorBrush(Color.FromRgb(0x5A, 0x5D, 0x6A));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? On : Off;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
