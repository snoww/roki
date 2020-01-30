using System;

namespace Roki.Modules.Searches.Models
{
    public class TimeData
    {
        public string Address { get; set; }
        public DateTimeOffset Time { get; set; }
        public string TimeZoneName { get; set; }
    }
}