using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SkiaSharp;
using System.Globalization;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;

namespace ThumbnailService
{
    public class SkiaImageFactory
    {
        #region Class Variables

        public const int MAXIMUM_IMAGE_QUALITY = 100;

        private ConcurrentDictionary<string, SKTypeface> _LocalFontDictionary;
        private static bool _Stop;
        private static readonly object _StopLock = new object();

        #endregion

        #region Service Methods


        public static void MainLoop(ConfigSettings settings)
        {
            TimeSpan sleepSecondTimeSpan = TimeSpan.FromSeconds(settings.SleepSeconds);
            DateTime lastCollectionTime = DateTime.Now;
            DateTime lastNotifyTime = DateTime.Now.AddMonths(-1);
            List<string> imageFileExtensions = new List<string> { ".JPG", ".JPEG", ".PNG" };
            SkiaImageFactory factory = new SkiaImageFactory(settings);
            Regex watchFoldersRegex = new Regex(settings.DropFolderRegex);

            _Stop = false;

            while (!_Stop)
            {
                try
                {
                    /*----------------------------------------------------------------------------------+
                    |   Get FileDefs to monitor. Some folks don't like comments. Others like Pepsi.     |
                    +----------------------------------------------------------------------------------*/
                    // Do some work

                    var watchDirectories = Directory.GetDirectories(settings.WatchFolderRoot);
                    foreach (string watchDirectory in watchDirectories)
                    {
                        var match = watchFoldersRegex.Match(watchDirectory);
                        if (match.Success)
                        {
                            WatchFolderSettings watchFolderSettings = new WatchFolderSettings(match, settings);
                            string workingDirectory;

                            if (string.IsNullOrWhiteSpace(watchFolderSettings.ActualNameSpec) && Path.Combine(watchDirectory) != match.Value)
                            {
                                DirectoryInfo di = new DirectoryInfo(watchDirectory);
                                if (di.Name != match.Value)
                                {
                                    continue;
                                }

                                workingDirectory = NormalizeWatchDirectory(watchDirectory, watchFolderSettings);
                            }
                            else
                            {
                                workingDirectory = watchDirectory;
                            }

                            List<FileInfo> allFiles = new DirectoryInfo(workingDirectory).GetFiles().Where(f => imageFileExtensions.Contains(f.Extension.ToUpper(CultureInfo.InvariantCulture))).ToList();

                            if (allFiles.Count > 0)
                            {
                                string outputFolder = Path.Combine(settings.OutputFolderRoot, $"{settings.DefaultOutputFolder}{watchFolderSettings.NameSpec}");

                                if (!Directory.Exists(outputFolder))
                                {
                                    Directory.CreateDirectory(outputFolder);
                                }

                                foreach (FileInfo file in allFiles)
                                {

                                    string copyName = Path.Combine(outputFolder, file.Name);
                                    string thumbName = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(file.Name)}.Thumb.{watchFolderSettings.Format}");

                                    File.Move(file.FullName, copyName);

                                    SkiaSharp.SKRect targetRect = new SkiaSharp.SKRect(0, 0, watchFolderSettings.Width, watchFolderSettings.Height);
                                    factory.BuildSheet(targetRect, copyName, 0, thumbName);
                                }
                            }
                        }
                    }
#if poop
                    lock (_StopLock)
                    {
                        Monitor.Wait(_StopLock, sleepSecondTimeSpan);
                    }
#endif
                }
                catch (Exception ex)
                {
                    settings.Logger.LogException(ex);
                }

                Thread.Sleep(sleepSecondTimeSpan);
            }
        }


        private static string NormalizeWatchDirectory(string watchDirectory, WatchFolderSettings watchFolderSettings)
        {
            string replacementDirectory = Path.Combine(Directory.GetParent(watchDirectory).FullName, $"{watchFolderSettings.FullName}{watchFolderSettings.NameSpec}");

            if (Directory.Exists(replacementDirectory))
            {
                Directory.GetFiles(watchDirectory).ToList().ForEach(f => File.Move(f, Path.Combine(replacementDirectory, Path.GetFileName(f))));
                Directory.Move(watchDirectory, Path.Combine(Directory.GetParent(watchDirectory).FullName, $"{watchFolderSettings.FullName}(MovedTo {watchFolderSettings.FullName}{watchFolderSettings.NameSpec})"));
            }
            else
            {
                Directory.Move(watchDirectory, replacementDirectory);
                Directory.CreateDirectory(Path.Combine(Directory.GetParent(watchDirectory).FullName, $"{watchFolderSettings.FullName}(MovedTo {watchFolderSettings.FullName}{watchFolderSettings.NameSpec})"));
            }

            return replacementDirectory;
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
            string format = Path.GetExtension(printPath);
            SKEncodedImageFormat outputFormat = (format == "jpg") ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
            //save the image
            using (SKImage image = surface.Snapshot())
            {
                using (SKData data = image.Encode(outputFormat, MAXIMUM_IMAGE_QUALITY))
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

        protected int _MasterSheetNumber { get; set; }

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

        public void BuildSheet(SKRect container, string imageFileName, int imageRotation, string printPath)
        {
            using (SKBitmap skImg = SKBitmap.Decode(imageFileName))
            {
                var scaleX = container.Width / skImg.Width;
                var scaleY = container.Height / skImg.Height;
                var useScale = Math.Min(scaleX, scaleY);
                container.Right = container.Width * useScale / scaleX; // Assume left of 0
                container.Bottom = container.Height * useScale / scaleY; // Assume top of 0
                SKImageInfo sKImageInfo = new SKImageInfo(container.WidthDotsFloor(DeviceDPI), container.HeightDotsFloor(DeviceDPI));

                using (SKSurface surface = SKSurface.Create(sKImageInfo))
                {
                    _Canvas = surface.Canvas;
                    _Canvas.Clear(SKColors.White);

                    if (imageRotation != 0)
                    {
                        _Canvas.RotateDegrees(imageRotation);
                    }
                    _Canvas.Translate(-container.Left.ToDots(DeviceDPI), -container.Top.ToDots(DeviceDPI));

                    RenderBitMap(skImg, imageRotation, container);

                    if (surface != null)
                    {
                        SaveSurfaceToFile(surface, printPath, MAXIMUM_IMAGE_QUALITY);
                    }
                }
            }
        }

        #endregion

    }
}

