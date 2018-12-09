﻿//-----------------------------------------------------------------------
// <copyright file="ScreenCapture.cs" company="Gavin Kendall">
//     Copyright (c) Gavin Kendall. All rights reserved.
// </copyright>
// <author>Gavin Kendall</author>
// <summary></summary>
//-----------------------------------------------------------------------
namespace AutoScreenCapture
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows.Forms;

    public static class ScreenCapture
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINTAPI ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTAPI
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern void GetWindowRect(IntPtr hWnd, out Rectangle rect);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        private const Int32 CURSOR_SHOWING = 0x0001;
        private const Int32 DI_NORMAL = 0x0003;

        public const int SCREEN_MAX = 4;
        public const int CAPTURE_LIMIT_MIN = 1;
        public const int CAPTURE_LIMIT_MAX = 9999;

        /// <summary>
        /// The default image format.
        /// </summary>
        public const string DefaultImageFormat = ImageFormatSpec.NAME_JPEG;

        private const int MAX_CHARS = 48000;
        private const int IMAGE_RESOLUTION_RATIO_MIN = 1;
        public const int IMAGE_RESOLUTION_RATIO_MAX = 100;

        public static int X { get; set; }
        public static int Y { get; set; }
        public static int Width { get; set; }
        public static int Height { get; set; }
        public static string Folder { get; set; }
        public static string Macro { get; set; }
        public static int Ratio { get; set; }
        public static string ImageFormat { get; set; }
        public static int Delay { get; set; }
        public static int Limit { get; set; }
        public static int Count { get; set; }
        public static bool RunningFromCommandLine { get; set; }
        public static bool LockScreenCaptureSession { get; set; }
        public static bool Running { get; set; }

        /// <summary>
        /// The date/time when the user started a screen capture session.
        /// </summary>
        public static DateTime DateTimeStartCapture { get; set; }

        /// <summary>
        /// The date/time when a screenshot is taken (or when the previous screenshot was taken).
        /// </summary>
        public static DateTime DateTimePreviousScreenshot { get; set; }

        /// <summary>
        /// The date/time of the next screenshot. If we're still waiting for the very first screenshot to be taken then calculate from the date/time when the user started a screen capture session
        /// otherwise calculate from the date/time when the previous screenshot was taken.
        /// </summary>
        public static DateTime DateTimeNextScreenshot { get { return DateTimePreviousScreenshot.Ticks == 0 ? DateTimeStartCapture.AddMilliseconds(Delay) : DateTimePreviousScreenshot.AddMilliseconds(Delay); } }

        /// <summary>
        /// The time remaining between now and the next screenshot that will be taken.
        /// </summary>
        public static TimeSpan TimeRemainingForNextScreenshot { get { return DateTimeNextScreenshot.Subtract(DateTime.Now).Duration(); } }

        public static Bitmap GetScreenBitmap(Screen screen, int ratio, string format, bool mouse)
        {
            try
            {
                int width = Width <= screen.Bounds.Width && Width > 0 ? Width : screen.Bounds.Width;
                int height = Height <= screen.Bounds.Height && Height > 0 ? Height : screen.Bounds.Height;

                return GetScreenBitmap(X, Y, width, height, ratio, format, mouse);
            }
            catch (Exception ex)
            {
                Log.Write("ScreenCapture::GetScreenBitmap(screen, ratio, format, mouse)", ex);
                return null;
            }
        }

        public static Bitmap GetScreenBitmap(int x, int y, int width, int height, int ratio, string format, bool mouse)
        {
            try
            {
                if (width > 0 && height > 0)
                {
                    Size blockRegionSize = new Size(width, height);

                    if (ratio < IMAGE_RESOLUTION_RATIO_MIN || ratio > IMAGE_RESOLUTION_RATIO_MAX)
                    {
                        ratio = 100;
                    }

                    float imageResolutionRatio = (float) ratio / 100;

                    int destinationWidth = (int) (width * imageResolutionRatio);
                    int destinationHeight = (int) (height * imageResolutionRatio);

                    Bitmap bitmapSource = new Bitmap(width, height);
                    Graphics graphicsSource = Graphics.FromImage(bitmapSource);

                    graphicsSource.CopyFromScreen(x, y, 0, 0, blockRegionSize, CopyPixelOperation.SourceCopy);

                    if (mouse)
                    {
                        CURSORINFO pci;
                        pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

                        if (GetCursorInfo(out pci))
                        {
                            if (pci.flags == CURSOR_SHOWING)
                            {
                                var hdc = graphicsSource.GetHdc();
                                DrawIconEx(hdc, pci.ptScreenPos.x - x, pci.ptScreenPos.y - y, pci.hCursor, 0, 0, 0,
                                    IntPtr.Zero, DI_NORMAL);
                                graphicsSource.ReleaseHdc();
                            }
                        }
                    }

                    Bitmap bitmapDestination = new Bitmap(destinationWidth, destinationHeight);

                    Graphics graphicsDestination = Graphics.FromImage(bitmapDestination);
                    graphicsDestination.DrawImage(bitmapSource, 0, 0, destinationWidth, destinationHeight);

                    graphicsSource.Flush();
                    graphicsDestination.Flush();

                    return bitmapDestination;
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Write("ScreenCapture::GetScreenBitmap(x, y, width, height, ratio, format, mouse)", ex);
                return null;
            }
        }

        public static Bitmap GetActiveWindowBitmap()
        {
            Rectangle rect;
            GetWindowRect(GetForegroundWindow(), out rect);

            int width = rect.Width - rect.X;
            int height = rect.Height - rect.Y;

            if (width > 0 && height > 0)
            {
                Bitmap bitmap = new Bitmap(width, height);

                Graphics graphics = Graphics.FromImage(bitmap);

                graphics.CopyFromScreen(new Point(rect.X, rect.Y), new Point(0, 0), new Size(width, height));

                return bitmap;
            }
            else
            {
                return null;
            }
        }

        public static string GetActiveWindowTitle()
        {
            IntPtr handle;
            int chars = MAX_CHARS;

            StringBuilder buffer = new StringBuilder(chars);

            handle = GetForegroundWindow();

            if (GetWindowText(handle, buffer, chars) > 0)
            {
                return buffer.ToString();
            }

            return null;
        }

        public static void TakeScreenshot(ImageFormatCollection imageFormatCollection, Screen screen, string path, int screenNumber, ScreenshotType screenshotType, long jpegQualityLevel, bool mouse)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    Bitmap bitmap = screenNumber == 5 ? GetActiveWindowBitmap() : GetScreenBitmap(screen, Ratio, ImageFormat, mouse);

                    if (bitmap != null)
                    {
                        Screenshot screenshot = new Screenshot(DateTimePreviousScreenshot, path, screenNumber, ImageFormat, screenshotType == ScreenshotType.User ? ScreenshotCollection.Count : -1);

                        SaveToFile(imageFormatCollection, bitmap, jpegQualityLevel, screenshot.Path, screenshotType);

                        ScreenshotCollection.Add(screenshot, screenshotType);

                        GC.Collect();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("ScreenCapture::TakeScreenshot(imageFormatCollection, screen, path, screenNumber, screenshotType, jpegQualityLevel, mouse)", ex);
            }
        }

        public static void TakeScreenshot(ImageFormatCollection imageFormatCollection, int x, int y, int width, int height, string path, long jpegQualityLevel, bool mouse)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    Bitmap bitmap = GetScreenBitmap(x, y, width, height, Ratio, ImageFormat, mouse);

                    if (bitmap != null)
                    {
                        SaveToFile(imageFormatCollection, bitmap, jpegQualityLevel, path, ScreenshotType.User);

                        GC.Collect();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("ScreenCapture::TakeScreenshot(imageFormatCollection, x, y, width, height, path, jpegQualityLevel, mouse)", ex);
            }
        }

        public static void Save(string imagePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(imagePath))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(imagePath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
                    }

                    File.Create(imagePath);
                }
            }
            catch (Exception ex)
            {
                Log.Write("ScreenCapture::Save", ex);
            }
        }

        private static void SaveToFile(ImageFormatCollection imageFormatCollection, Bitmap bitmap, long jpegQualityLevel, string imagePath, ScreenshotType screenshotType)
        {
            try
            {
                if (bitmap != null && !string.IsNullOrEmpty(ImageFormat) && !string.IsNullOrEmpty(imagePath))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(imagePath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
                    }

                    if (screenshotType == ScreenshotType.User)
                    {
                        Log.Write("Screenshot saved: " + imagePath);
                    }

                    if (ImageFormat.Equals(ImageFormatSpec.NAME_JPEG))
                    {
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, jpegQualityLevel);
                        var encoderInfo = GetEncoderInfo("image/jpeg");
                        bitmap.Save(imagePath, encoderInfo, encoderParams);
                    }
                    else
                    {
                        bitmap.Save(imagePath, imageFormatCollection.GetByName(ImageFormat).Format);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("ScreenCapture::SaveToFile", ex);
            }
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            var encoders = ImageCodecInfo.GetImageEncoders();
            return encoders.FirstOrDefault(t => t.MimeType == mimeType);
        }
    }
}