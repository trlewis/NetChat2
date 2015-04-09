using System;

namespace NetChat2Client.Code.Extensions
{
    public static class ComparableExtensions
    {
        public static T Clamp<T>(this T value, T minValue, T maxValue) where T : IComparable<T>
        {
            if (value.CompareTo(minValue) < 0)
            {
                return minValue;
            }

            if (value.CompareTo(maxValue) > 0)
            {
                return maxValue;
            }

            return value;
        }
    }
}