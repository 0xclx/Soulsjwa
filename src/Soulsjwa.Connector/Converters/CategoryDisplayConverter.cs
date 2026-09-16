using System.Globalization;
using System.Text;
using System.Windows.Data;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Converters;

/// <summary>
/// Renders a <see cref="GameDataCategory"/>? for display: null (the "any
/// category" filter option) becomes "All categories", and PascalCase enum
/// names like <c>KeyItems</c> get spaced to "Key Items".
/// </summary>
public sealed class CategoryDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not GameDataCategory category)
            return "All categories";

        var name = category.ToString();
        var spaced = new StringBuilder(name.Length + 4);
        foreach (var c in name)
        {
            if (spaced.Length > 0 && char.IsUpper(c))
                spaced.Append(' ');
            spaced.Append(c);
        }
        return spaced.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
