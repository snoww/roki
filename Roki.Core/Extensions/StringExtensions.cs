using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;

namespace Roki.Extensions
{
    public static class StringExtensions
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static string ToTitleCase(this string s)
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLower());
        }

        public static string TrimTo(this string str, int maxLength, bool hideDots = false)
        {
            if (maxLength < 0)
                throw new ArgumentOutOfRangeException(nameof(maxLength), $"Argument {nameof(maxLength)} can't be negative.");
            if (maxLength == 0)
                return string.Empty;
            if (maxLength <= 3)
                return "...";
            if (str.Length < maxLength)
                return str;

            if (hideDots)
                return str[..maxLength];
            return str[..(maxLength - 3)] + "...";
        }

        public static string SanitizeStringFull(this string dirtyString)
        {
            return new(dirtyString.Where(char.IsLetterOrDigit).ToArray());
        }

        public static string EscapeMarkdown(this string str)
        {
            // note this doesn't remove \
            return Regex.Replace(str, "(\\*|_|`|~)", "\\$1");
        }
    }
}