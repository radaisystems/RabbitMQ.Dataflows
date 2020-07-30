﻿using System.Globalization;

namespace HouseofCat.Utilities
{
    public static class Strings
    {
        public static string FormatWrite(string template, params string[] arguments)
        {
            return string.Format(CultureInfo.InvariantCulture, template, arguments);
        }
    }
}
