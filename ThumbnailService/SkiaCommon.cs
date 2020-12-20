using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThumbnailService
{
    public static class SkiaCommon
    {
        private const int POINTS_PER_INCH = 72; //seconds
        public static int HeightDotsFloor(this SKRect rect, int dpi) => rect.Height.ToDots(dpi).Floor();
        public static int WidthDotsFloor(this SKRect rect, int dpi) => rect.Width.ToDots(dpi).Floor();
        public static decimal Decimal(this float value) => (decimal)value;
        public static float Float(this decimal value) => (float)value;
        public static int Floor(this float value) => (int)value;
        public static int Round(this float value) => (int)Math.Round(value);
        public static int Ceil(this float value) => (int)Math.Ceiling(value);
        public static int Floor(this double value) => (int)value;
        public static int Round(this double value) => (int)Math.Round(value);
        public static int Ceil(this double value) => (int)Math.Ceiling(value);
        public static float ToDots(this decimal points, int dpi)
        {
            return ToDots((float)points, dpi);
        }

        public static float ToDots(this float points, int dpi)
        {
            return (dpi == POINTS_PER_INCH) ? points : Convert.ToInt32((points / POINTS_PER_INCH) * dpi);
        }

        public static float DotsToPoints(this decimal dots, int dpi)
        {
            return DotsToPoints((float)dots, dpi);
        }

        public static float DotsToPoints(this float dots, int dpi)
        {
            return (dpi == POINTS_PER_INCH) ? dots : Convert.ToInt32((dots / dpi) * POINTS_PER_INCH);
        }

        public static int Int(this string value, int defaultValue = -1)
        {
            return int.TryParse(value, out int intValue) ? intValue : defaultValue;
        }
    }
}
