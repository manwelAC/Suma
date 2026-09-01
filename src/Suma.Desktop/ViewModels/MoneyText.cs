using System.Globalization;

namespace Suma.Desktop.ViewModels;

public static class MoneyText
{
    public static bool TryParseMinor(string text, out long amountMinor)
    {
        amountMinor = 0;
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var major))
        {
            return false;
        }

        try
        {
            var minor = checked(major * 100m);
            if (minor != decimal.Truncate(minor))
            {
                return false;
            }

            amountMinor = checked((long)minor);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public static string Format(long amountMinor, string currencyCode)
    {
        var major = amountMinor / 100m;
        return string.Create(
            CultureInfo.CurrentCulture,
            $"{currencyCode} {major:N2}");
    }
}
