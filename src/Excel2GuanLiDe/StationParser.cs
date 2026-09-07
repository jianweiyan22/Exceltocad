using System.Globalization;
using System.Text.RegularExpressions;

namespace Excel2GuanLiDe;

public static class StationParser
{
    private static readonly Regex KStation = new(
        @"^[Kk]?\s*(?<km>-?\d+(?:\.\d+)?)\s*\+\s*(?<m>\d+(?:\.\d+)?)$",
        RegexOptions.Compiled);

    public static bool TryParse(object? value, out double station)
    {
        station = 0;
        if (value is null) return false;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Replace("米", "", StringComparison.OrdinalIgnoreCase)
                   .Replace("m", "", StringComparison.OrdinalIgnoreCase)
                   .Trim();

        var match = KStation.Match(text);
        if (match.Success &&
            double.TryParse(match.Groups["km"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var km) &&
            double.TryParse(match.Groups["m"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var m))
        {
            station = km * 1000 + Math.Sign(km == 0 ? 1 : km) * m;
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out station))
            return true;

        var normalized = text.Replace("K", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("+", ".", StringComparison.OrdinalIgnoreCase);
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out station);
    }

    public static string Format(double station)
    {
        var km = Math.Floor(station / 1000.0);
        var m = station - km * 1000.0;
        return $"K{km:0}+{m:000.###}";
    }
}
