using System;
using System.Globalization;
using System.Linq;
using NLog;

namespace Roki.Extensions
{
    public static class StringExtensions
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static string ToTitleCase(this string s)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLower());
        }

        public static bool ContainsNoCase(this string str, string contains, StringComparison compare)
        {
            return str.IndexOf(contains, compare) >= 0;
        }

        public static string TrimTo(this string str, int maxLength, bool hideDots = false)
        {
            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), $"Argument {nameof(maxLength)} can't be negative.");
            if (maxLength == 0)
                return string.Empty;
            if (maxLength <= 3)
                return string.Concat(str.Select(c => '.'));
            if (str.Length < maxLength)
                return str;

            if (hideDots)
                return string.Concat(str.Take(maxLength));
            return string.Concat(str.Take(maxLength - 3)) + "...";
        }

        public static string FirstLetterToUpperCase(this string s)
        {
            if (string.IsNullOrEmpty(s))
                _log.Warn("No first letter");

            var a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }
    }
}