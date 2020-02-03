
using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Roki.Modules.Searches.Models
{
    public class TimeZoneResult
    {
        public double DstOffset { get; set; }
        public double RawOffset { get; set; }
        public string TimeZoneId { get; set; }

        public string TimeZoneName { get; set; }
    }
    
    public class TimeData
    {
        public string Address { get; set; }
        public DateTimeOffset Time { get; set; }
        public string TimeZoneName { get; set; }
        public CultureInfo Culture { get; set; }
    }

    public class GeolocationResult
    {
        public GeolocationModel[] Results { get; set; }
    }
    
    public class GeolocationModel
    {
        [JsonPropertyName("address_components")]
        public AddressComponent[] AddressComponents { get; set; }
        
        [JsonPropertyName("formatted_address")] 
        public string FormattedAddress { get; set; }

        public GeometryModel Geometry { get; set; }
    }
    
    public class GeometryModel
    {
        public LocationModel Location { get; set; }
    }
    
    public class LocationModel
    {
        public float Lat { get; set; }
        public float Lng { get; set; }
    }

    public class AddressComponent
    {
        [JsonPropertyName("long_name")]
        public string LongName { get; set; }
        
        [JsonPropertyName("short_name")]
        public string ShortName { get; set; }
        
        public string[] Types { get; set; }
    }
}