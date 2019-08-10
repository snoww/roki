using System;
using System.Linq;

namespace Roki.Core.Extentions
{
    public static class StringExtentions
    {
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
            {
                return string.Concat(str.Take(maxLength));
            }
            else
            {
                return string.Concat(str.Take(maxLength - 3)) + "...";
            }
        }


    }
}