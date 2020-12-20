using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SkiaSharp;
using System.Globalization;
using System.Collections.Concurrent;
using System.Threading;

namespace ThumbnailService
{
    public class SkiaImageFactory
    {
        #region Class Variables

        public const int MAXIMUM_IMAGE_QUALITY = 100;

        private const char _ImageFileDelimiter = '?';
        private const int _FontFileRetryTime = 30; //seconds

        private ConcurrentDictionary<string, SKTypeface> _LocalFontDictionary;
        private static readonly object _Locker = new object();
        private static bool _Stop;
        private static readonly object _StopLock = new object();

        #endregion

        #region Service Methods


        public static void MainLoop(ConfigSettings settings)
        {
            TimeSpan thirtySecondTimeSpan = TimeSpan.FromSeconds(30);
            DateTime lastCollectionTime = DateTime.Now;
            DateTime lastNotifyTime = DateTime.Now.AddMonths(-1);
            List<string> imageFileExtensions = new List<string> { ".jpg", ".png" };
            SkiaImageFactory factory = new SkiaImageFactory(settings);

            _Stop = false;

            while (!_Stop)
            {
                try
                {
                    /*----------------------------------------------------------------------------------+
                    |   Get FileDefs to monitor. Some folks don't like comments. Others like Pepsi.     |
                    +----------------------------------------------------------------------------------*/
                    // Do some work

                    List<FileInfo> allFiles = new DirectoryInfo(settings.WatchFolder).GetFiles().Where(f => imageFileExtensions.Contains(f.Extension.ToLowerInvariant())).ToList();

                    foreach (FileInfo file in allFiles)
                    {
                        string copyName = Path.Combine(settings.OutputFolder, file.Name);
                        string thumbName = Path.Combine(settings.OutputFolder, $"{Path.GetFileNameWithoutExtension(file.Name)}.Thumb.jpg");

                        File.Move(file.FullName, copyName);

                        SkiaSharp.SKSurface surface = null;
                        SkiaSharp.SKRect targetRect = new SkiaSharp.SKRect(0, 0, settings.MaxWidth, settings.MaxHeight);
                        factory.BuildSheet(targetRect, ref surface, copyName, 0, thumbName);
                    }

                    Thread.Sleep(thirtySecondTimeSpan);
#if poop
                    lock (_StopLock)
                    {
                        Monitor.Wait(_StopLock, TimeSpan.FromMinutes(_MonitorPollIntervalInMinutes));
                    }
#endif
                }
                catch (Exception ex)
                {
                }
            }
        }



        /// <summary>Stop (or pause) the service's processing</summary>
        /// <param name="isStopping">A Stop/Pause indicator</param>
        public static void Stop()
        {
            _Stop = true;
            lock (_StopLock)
            {
                Monitor.Pulse(_StopLock);
            }
        }

        #endregion


        #region Abstract Properties and Methods


        public string FileExtension { get; }

        public void SaveSurfaceToFile(SKSurface surface, string printPath, int quality)
        {
            //save the image
            using (SKImage image = surface.Snapshot())
            {
                using (SKData data = image.Encode(SKEncodedImageFormat.Jpeg, MAXIMUM_IMAGE_QUALITY))
                {
                    using (FileStream stream = File.OpenWrite(printPath))
                    {
                        // save the data to a stream
                        data.SaveTo(stream);
                    }
                }
            }

        }

        public byte[] SaveSurfaceToByteArray(SKSurface surface, int quality)
        {
            //save the image
            using (MemoryStream outStream = new MemoryStream())
            {
                using (SKImage image = surface.Snapshot())
                {
                    using (SKData data = image.Encode(SKEncodedImageFormat.Jpeg, MAXIMUM_IMAGE_QUALITY))
                    {
                        // save the data to a stream
                        data.SaveTo(outStream);
                        return outStream.ToArray();
                    }
                }
            }
        }

        #endregion

        #region Properties

        protected ConfigSettings _Settings { get; set; }

        protected SKCanvas _Canvas { get; set; }
        // check contents in document
        protected bool _HasContents { get; set; } = false;
        // support for store number extraction support

        protected int _MasterSheetNumber { get; set; }
        protected bool _IsDownTheStackImposition { get; set; }

        // delimiter constant
        protected int _SheetWidth { get; set; }
        protected int _SheetHeight { get; set; }

        public int DeviceDPI { get; set; } = 72;

        protected bool AntiAlias { get; set; } = false;

        #endregion

        #region constructors

        public SkiaImageFactory(ConfigSettings settings)
        {
            _Settings = settings;
            _LocalFontDictionary = new ConcurrentDictionary<string, SKTypeface>();
            _MasterSheetNumber = 1;
        }

        #endregion

        ~SkiaImageFactory()
        {
            try
            {
                if (_LocalFontDictionary?.Count > 0)
                {
                    SKTypeface[] typefaces = _LocalFontDictionary.Values.ToArray();
                    _LocalFontDictionary.Clear();

                    for (int i = 0; i < typefaces.Length; i++)
                    {
                        typefaces[i].Dispose();
                        typefaces[i] = null;
                    }
                }
            }
            catch (Exception) { }
        }

        #region Spatial Helper Methods

        protected SKRect MeasureTextPoints(string text, SKPaint pt, out float textHeightPoints, out float textWidthPoints)
        {
            SKRect textBounds = new SKRect();
            pt.MeasureText(text, ref textBounds);
            textHeightPoints = textBounds.Height.DotsToPoints(DeviceDPI);
            textWidthPoints = textBounds.Width.DotsToPoints(DeviceDPI);

            return textBounds;
        }

        protected static SKPoint RotatePoint(decimal originX, decimal originY, decimal pointX, decimal pointY, int rotation)
        {
            return RotatePoint(new SKPoint((float)originX, (float)originY), new SKPoint((float)pointX, (float)pointY), rotation);
        }


        protected static SKPoint RotatePoint(SKPoint origin, SKPoint point, int rotation)
        {

            double rotationRadians = rotation / 180.0 * Math.PI;

            double relativeX = point.X - origin.X;
            double relativeY = point.Y - origin.Y;

            double rotatedRelativeX = relativeX * Math.Cos(rotationRadians) - relativeY * Math.Sin(rotationRadians);
            double rotatedRelativeY = relativeY * Math.Cos(rotationRadians) + relativeX * Math.Sin(rotationRadians);

            return new SKPoint() { X = (float)rotatedRelativeX + origin.X, Y = (float)rotatedRelativeY + origin.Y };
        }

        #endregion


        #region RenderImage

        public void RenderImage(string imageFileName, int containerRotation, SKRect container)
        {
            string fileExtension = Path.GetExtension(imageFileName).ToLowerInvariant();

            if (!(fileExtension == ".jpg" || fileExtension == ".png" || fileExtension == ".gif"))
            {
                return;
            }

            using (SKBitmap skImg = SKBitmap.Decode(imageFileName))
            {
                RenderBitMap(skImg, containerRotation, container);
            }
        }


        public void RenderBitMap(SKBitmap skImg, int containerRotation, SKRect container)
        {
            decimal imageScale = Math.Max(skImg.Height / container.Height, skImg.Width / container.Width).Decimal();
            float adjustedContainerHeight = (skImg.Height / imageScale).Float();
            decimal adjustedContainerWidth = skImg.Width / imageScale;
            float fAdjustedX = (container.Left /*+ OffsetX(container)*/).ToDots(DeviceDPI);
            float fAdjustedY;

            if (adjustedContainerHeight == container.Height)
            {
                fAdjustedY = (container.Top /*+ OffsetY(container)*/).ToDots(DeviceDPI);
            }
            else
            {
                int adjustmentDirection = (containerRotation == 180) ? -1 : 1;
                fAdjustedY = (container.Top /*+ OffsetY(container)*/ + adjustmentDirection * (container.Height - adjustedContainerHeight)).ToDots(DeviceDPI);
            }

            SKImageInfo info = new SKImageInfo() { AlphaType = skImg.AlphaType, ColorSpace = skImg.ColorSpace, ColorType = skImg.ColorType, Height = adjustedContainerHeight.ToDots(DeviceDPI).Floor(), Width = adjustedContainerWidth.ToDots(DeviceDPI).Floor() };

            using (SKBitmap resizedBitmap = skImg.Resize(info, SKFilterQuality.High))
            {
                _Canvas.Save();

                if (containerRotation != 0)
                {
                    _Canvas.RotateDegrees((containerRotation * -1), fAdjustedX, fAdjustedY);
                }
                _Canvas.DrawBitmap(resizedBitmap, fAdjustedX, fAdjustedY);
                _Canvas.Restore();
            }
        }




        #endregion

        #region BatchPrinting 

        private void SetUpCanvas(ref SKSurface surface, SKRect container)
        {
            int w = container.Width.ToDots(DeviceDPI).Floor();
            int h = container.Height.ToDots(DeviceDPI).Floor();
            SKImageInfo sKImageInfo = new SKImageInfo(w, h);
            _SheetWidth = w;
            _SheetHeight = h;

            surface = SKSurface.Create(sKImageInfo);

            _Canvas = surface.Canvas;
            _Canvas.Clear(SKColors.White);
        }

        public void BuildSheet(SKRect container,
            ref SkiaSharp.SKSurface surface, string imageFileName, int imageRotation,
            string printPath)
        {
            string printFileName = string.Empty;
            string sheetPrintFileName = string.Empty;
            string printPathDirectory = string.Empty;
            bool renderingInMemory;

            if (string.IsNullOrWhiteSpace(printPath))
            {
                renderingInMemory = true;
            }
            else
            {
                FileInfo fileInfo = new FileInfo(printPath);

                printPathDirectory = fileInfo.DirectoryName;
                printFileName = fileInfo.Name;
                sheetPrintFileName = printPath;

                renderingInMemory = false;
            }


            using (SKBitmap skImg = SKBitmap.Decode(imageFileName))
            {
                var scaleX = container.Width / skImg.Width;
                var scaleY = container.Height / skImg.Height;
                var useScale = Math.Min(scaleX, scaleY);
                container.Right = container.Width * useScale / scaleX; // Assume left of 0
                container.Bottom = container.Height * useScale / scaleY; // Assume top of 0

                if (renderingInMemory || surface is null)
                {
                    SetUpCanvas(ref surface, container);
                    if (imageRotation != 0)
                    {
                        _Canvas.RotateDegrees(imageRotation);
                    }
                    _Canvas.Translate(-container.Left.ToDots(DeviceDPI), -container.Top.ToDots(DeviceDPI));
                }

                RenderBitMap(skImg, imageRotation, container);
            }

            _HasContents = true;

            if (!(surface is null) && !string.IsNullOrWhiteSpace(sheetPrintFileName))
            {
                SaveSurfaceToFile(surface, printPath, MAXIMUM_IMAGE_QUALITY);
            }

        }

        #endregion

    }
}

