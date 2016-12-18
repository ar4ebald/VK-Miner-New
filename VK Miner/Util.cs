using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SharpDX;

namespace VK_Miner
{
    internal static class Util
    {
        public static string CamelCaseToSnakeCase(this string text)
        {
            return Regex.Replace(text, @"(\p{Ll})([\p{Lu}\d])", @"$1_$2").ToLower();
        }
        public static Vector2 Rotate(this Vector2 vector, double angle)
        {
            var sin = (float)Math.Sin(angle);
            var cos = (float)Math.Cos(angle);

            return new Vector2(
                cos * vector.X - sin * vector.Y,
                sin * vector.X + cos * vector.Y);
        }
    }
}
