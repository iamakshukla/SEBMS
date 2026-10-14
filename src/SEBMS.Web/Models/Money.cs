using System.Globalization;

namespace SEBMS.Web.Models;

public static class Money
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    public static string Format(decimal amount) =>
        (amount < 0 ? "-" : "") + "₹" + Math.Abs(amount).ToString("N2", India);

    public static string MonthName(int month) => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
}
