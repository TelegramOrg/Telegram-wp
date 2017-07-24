﻿using System;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Telegram.Api.TL;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Converters
{
    public class BackgroundBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var bagroundItem = value as BackgroundItem;
            if (bagroundItem == null) return null;

            if (string.Equals(bagroundItem.Name, Constants.LibraryBackgroundString))
            {
                return Application.Current.Resources["PhoneAccentBrush"];
            }

            return Application.Current.Resources["PhoneChromeBrush"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BackgroundImageConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var bagroundItem = value as BackgroundItem;
            if (bagroundItem == null) return null;

            if (string.Equals(bagroundItem.Name, Constants.LibraryBackgroundString))
            {
                var fileName = bagroundItem.IsoFileName;
                if (!string.IsNullOrEmpty(fileName))
                {
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (!store.FileExists(fileName))
                        {
                            return null;
                        }

                        BitmapImage imageSource;

                        try
                        {
                            using (var stream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                            {
                                stream.Seek(0, SeekOrigin.Begin);
                                var b = new BitmapImage();
                                b.SetSource(stream);
                                imageSource = b;
                            }
                        }
                        catch (Exception)
                        {
                            return null;
                        }

                        return imageSource;
                    }
                }

                return bagroundItem.SourceString;
            }

            if (bagroundItem.Name.StartsWith("telegram", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = bagroundItem.IsoFileName;
                if (!string.IsNullOrEmpty(fileName))
                {
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (store.FileExists(fileName))
                        {
                            BitmapImage imageSource;

                            try
                            {
                                using (var stream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                                {
                                    stream.Seek(0, SeekOrigin.Begin);
                                    var b = new BitmapImage();
                                    b.SetSource(stream);
                                    imageSource = b;
                                }

                                return imageSource;
                            }
                            catch (Exception)
                            {
                                return null;
                            }
                        }
                    }
                }
            }

            if (bagroundItem.Wallpaper != null)
            {
                var width = 99.0;
                double result;
                if (Double.TryParse((string)parameter, out result))
                {
                    width = result;
                }

                var size = GetPhotoSize(bagroundItem.Wallpaper.Sizes, width);

                if (size != null)
                {
                    var location = size.Location as TLFileLocation;
                    if (location != null)
                    {
                        return DefaultPhotoConverter.ReturnOrEnqueueImage(null, location, bagroundItem.Wallpaper, size.Size);
                    }
                }
            }

            var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

            var themeString = "Dark/";
            if (isLightTheme)
            {
                themeString = "Light/";
            }

            return bagroundItem.SourceString + themeString + bagroundItem.Name;
        }

        public static TLPhotoSize GetPhotoSize(TLVector<TLPhotoSizeBase> photoSizes, double width)
        {
            TLPhotoSize size = null;
            var sizes = photoSizes.OfType<TLPhotoSize>();
            foreach (var photoSize in sizes)
            {
                if (size == null
                    || Math.Abs(width - size.W.Value) > Math.Abs(width - photoSize.W.Value))
                {
                    size = photoSize;
                }
            }

            return size;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
