using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Roki.Modules.Searches.Common
{
    public class WeatherIcon
    {
        private static string ico = File.ReadAllText("./Resources/weather_icons.json");

        public static string GetWeatherIconUrl(string icon)
        {
            var icons = JsonConvert.DeserializeObject<Dictionary<string, string>>(ico);
            return icons[icon.ToLowerInvariant()];
        }
    }
}