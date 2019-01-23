using System;

namespace Hallupa.Library
{
    public static class LatLonHelper
    {
        private const double EarthRadius = 6371.0;

        /// <summary>
        /// http://stackoverflow.com/questions/7477003/calculating-new-longtitude-latitude-from-old-n-meters
        /// </summary>
        /// <param name="lat"></param>
        /// <param name="km"></param>
        /// <returns></returns>
        public static double AddToLat(double lat, double km)
        {
            return lat + (km / EarthRadius) * (180.0 / Math.PI);
        }


        /// <summary>
        /// http://stackoverflow.com/questions/7477003/calculating-new-longtitude-latitude-from-old-n-meters
        /// </summary>
        /// <param name="lat"></param>
        /// <param name="lon"></param>
        /// <param name="km"></param>
        /// <returns></returns>
        public static double AddToLon(double lat, double lon, double km)
        {
            return lon + (km / EarthRadius) * (180.0 / Math.PI) / Math.Cos(lat * Math.PI / 180.0);
            //return lon + (km / (EarthRadius * 1)) * 180 / Math.PI;
            // var dLon = de / (R * Math.Cos(Math.Pi * lat / 180))
        }

        public static double GetApproximateDistanceinKm(double lat1, double lon1, double lat2, double lon2)
        {
            lat1 = DegreeToRadian(lat1);
            lon1 = DegreeToRadian(lon1);
            lat2 = DegreeToRadian(lat2);
            lon2 = DegreeToRadian(lon2);

            var x = (lon2 - lon1) * Math.Cos(0.5 * (lat2 + lat1));
            var y = lat2 - lat1;
            var d = EarthRadius * Math.Sqrt(x * x + y * y);

            return d;
            /*
            var R = 6371; // km
            var φ1 = DegreeToRadian(lat1);
            var φ2 = DegreeToRadian(lat2);
            var Δφ = DegreeToRadian(lat2 - lat1);
            var Δλ = DegreeToRadian(lon2 - lon1);

            var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                    Math.Cos(φ1) * Math.Cos(φ2) *
                    Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            var d = R * c;
            return d;*/
        }

        private static double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }
}