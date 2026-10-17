using System.Globalization;
using System.Text;

namespace NHNhaTro.Services;

public static class Fmt
{
    public static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>1.234.567</summary>
    public static string Money(decimal value) => value.ToString("N0", Vi);

    /// <summary>1.234.567đ</summary>
    public static string Vnd(decimal value) => Money(value) + "đ";

    public static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy") ?? "";

    public static string DateTime(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm") ?? "";

    public static int MonthsBetween(DateTime start, DateTime end) =>
        (end.Year - start.Year) * 12 + end.Month - start.Month;
}

/// <summary>Đọc số tiền thành chữ tiếng Việt, ví dụ 2.150.000 → "Hai triệu một trăm năm mươi nghìn đồng".</summary>
public static class VietnameseNumber
{
    private static readonly string[] Digits = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];
    private static readonly string[] Units = ["", " nghìn", " triệu", " tỷ", " nghìn tỷ", " triệu tỷ"];

    public static string ToWords(decimal amount)
    {
        long number = (long)Math.Round(Math.Abs(amount));
        if (number == 0) return "Không đồng";

        var groups = new List<int>();
        while (number > 0)
        {
            groups.Add((int)(number % 1000));
            number /= 1000;
        }

        var sb = new StringBuilder();
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            int g = groups[i];
            if (g == 0) continue;
            bool isFirst = sb.Length == 0;
            sb.Append(' ').Append(ReadGroup(g, full: !isFirst)).Append(Units[i]);
        }

        var text = sb.ToString().Trim() + " đồng";
        if (amount < 0) text = "âm " + text;
        return char.ToUpper(text[0], Fmt.Vi) + text[1..];
    }

    private static string ReadGroup(int n, bool full)
    {
        int hundreds = n / 100, tens = n / 10 % 10, ones = n % 10;
        var parts = new List<string>();

        if (hundreds > 0 || full)
            parts.Add(Digits[hundreds] + " trăm");

        if (tens == 0)
        {
            if (ones > 0 && (hundreds > 0 || full)) parts.Add("linh");
        }
        else if (tens == 1)
        {
            parts.Add("mười");
        }
        else
        {
            parts.Add(Digits[tens] + " mươi");
        }

        if (ones > 0)
        {
            string word = Digits[ones];
            if (ones == 1 && tens >= 2) word = "mốt";
            else if (ones == 5 && tens >= 1) word = "lăm";
            else if (ones == 4 && tens >= 2) word = "tư";
            parts.Add(word);
        }

        return string.Join(' ', parts);
    }
}
