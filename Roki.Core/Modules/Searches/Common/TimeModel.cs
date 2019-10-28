
using System.Text.Json.Serialization;

namespace Roki.Modules.Searches.Common
{
    public class TimeZoneResult
    {
        public double DstOffset { get; set; }
        public double RawOffset { get; set; }

        public string TimeZoneName { get; set; }
    }

    public class GeolocationResult
    {
        public GeolocationModel[] Results { get; set; }

        public class GeolocationModel
        {
            [JsonPropertyName("formatted_address")] 
            public string FormattedAddress { get; set; }

            public GeometryModel Geometry { get; set; }

            public class GeometryModel
            {
                public LocationModel Location { get; set; }

                public class LocationModel
                {
                    public float Lat { get; set; }
                    public float Lng { get; set; }
                }
            }
        }
    }
}