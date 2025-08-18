using NhaTroAnCu.Models;
using System;
using System.Globalization;
using System.Linq;

namespace NhaTroAnCu.Helpers
{
    public class DateTimeHelper
    {
        private NhaTroAnCuEntities db;

        public DateTimeHelper(NhaTroAnCuEntities context)
        {
            db = context;
        }
        public static DateTime? ParseDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            // Parse format dd/MM/yyyy
            if (DateTime.TryParseExact(dateString, "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            // Fallback to default parse
            if (DateTime.TryParse(dateString, out result))
            {
                return result;
            }

            return null;
        }

    }
}