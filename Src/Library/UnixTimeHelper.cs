using System;

namespace Hallupa.Library
{
    public static class UnixTimeHelper
    {
        public static DateTime ConvertFromUnixTimestampSeconds(int timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddSeconds(timestamp);
        }

        public static int ConvertToUnixTimestampSeconds(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date - origin;
            return (int)diff.TotalSeconds;
        }

        public static DateTime ConvertFromUnixTimestampMilliSeconds(long timestamp)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddMilliseconds(timestamp);
        }

        public static long ConvertToUnixTimestampMilliSeconds(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date - origin;
            return (int)diff.TotalMilliseconds;
        }
    }
}