﻿using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using Caliburn.Micro;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class TLIntToDateTimeConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty TodayFormatProperty =
            DependencyProperty.Register("TodayFormat", 
                                        typeof (string), typeof (TLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string TodayFormat
        {
            get { return (string) GetValue(TodayFormatProperty); }
            set { SetValue(TodayFormatProperty, value); }
        }

        public static readonly DependencyProperty YesterdayStringProperty =
            DependencyProperty.Register("YesterdayString", typeof (string), typeof (TLIntToDateTimeConverter), new PropertyMetadata(default(string)));

        public string YesterdayString
        {
            get { return (string) GetValue(YesterdayStringProperty); }
            set { SetValue(YesterdayStringProperty, value); }
        }

        public static readonly DependencyProperty YesterdayFormatProperty =
            DependencyProperty.Register("YesterdayFormat", 
                                        typeof (string), typeof (TLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string YesterdayFormat
        {
            get { return (string) GetValue(YesterdayFormatProperty); }
            set { SetValue(YesterdayFormatProperty, value); }
        }

        public static readonly DependencyProperty RegularFormatProperty =
            DependencyProperty.Register("RegularFormat", 
                                        typeof (string), typeof (TLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string RegularFormat
        {
            get { return (string) GetValue(RegularFormatProperty); }
            set { SetValue(RegularFormatProperty, value); }
        }

        public static readonly DependencyProperty LongRegularFormatProperty =
            DependencyProperty.Register("LongRegularFormat", 
                                        typeof (string), typeof (TLIntToDateTimeConverter), 
                                        new PropertyMetadata(default(string)));

        public string LongRegularFormat
        {
            get { return (string) GetValue(LongRegularFormatProperty); }
            set { SetValue(LongRegularFormatProperty, value); }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
#if DEBUG
            return value;
#endif

            if (!(value is TLInt)) return value;

            var clientDelta = IoC.Get<IMTProtoService>().ClientTicksDelta;
            var utc0SecsLong = ((TLInt)value).Value * 4294967296 - clientDelta;
            var utc0SecsInt = utc0SecsLong/4294967296.0;

            var dateTime = Telegram.Api.Helpers.Utils.UnixTimestampToDateTime(utc0SecsInt);

            //var tzi = TimeZoneInfo.Local;
            //Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;


            var cultureInfo = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            var shortTimePattern = UserStatusToStringConverter.GetShortTimePattern(ref cultureInfo);


            //Today
            if ((dateTime.Date == DateTime.Now.Date) && !string.IsNullOrEmpty(TodayFormat))
                return dateTime.ToString(string.Format(TodayFormat, shortTimePattern), cultureInfo);

            //Yesterday
            if ((dateTime.Date.AddDays(1) == DateTime.Now.Date) && !string.IsNullOrEmpty(YesterdayString))
                return YesterdayString;

            if ((dateTime.Date.AddDays(1) == DateTime.Now.Date) && !string.IsNullOrEmpty(YesterdayFormat))
                return dateTime.ToString(string.Format(YesterdayFormat, shortTimePattern), cultureInfo);

            //Long time ago (no more than one year ago)
            if (dateTime.Date.AddDays(365) >= DateTime.Now.Date && !string.IsNullOrEmpty(RegularFormat))
                return dateTime.ToString(string.Format(RegularFormat, shortTimePattern), cultureInfo);

            //Long long time ago
            return dateTime.ToString(string.Format(LongRegularFormat, shortTimePattern), cultureInfo);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DialogTLIntToDateTimeConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty TodayFormatProperty =
            DependencyProperty.Register("TodayFormat",
                                        typeof(string), typeof(DialogTLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string TodayFormat
        {
            get { return (string)GetValue(TodayFormatProperty); }
            set { SetValue(TodayFormatProperty, value); }
        }

        public static readonly DependencyProperty WeekFormatProperty =
            DependencyProperty.Register("WeekFormat",
                                        typeof(string), typeof(DialogTLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string WeekFormat
        {
            get { return (string)GetValue(WeekFormatProperty); }
            set { SetValue(WeekFormatProperty, value); }
        }

        public static readonly DependencyProperty RegularFormatProperty =
            DependencyProperty.Register("RegularFormat",
                                        typeof(string), typeof(DialogTLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string RegularFormat
        {
            get { return (string)GetValue(RegularFormatProperty); }
            set { SetValue(RegularFormatProperty, value); }
        }

        public static readonly DependencyProperty LongRegularFormatProperty =
            DependencyProperty.Register("LongRegularFormat",
                                        typeof(string), typeof(DialogTLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string LongRegularFormat
        {
            get { return (string)GetValue(LongRegularFormatProperty); }
            set { SetValue(LongRegularFormatProperty, value); }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is TLInt)) return value;

            var clientDelta = IoC.Get<IMTProtoService>().ClientTicksDelta;
            var utc0SecsLong = ((TLInt)value).Value * 4294967296 - clientDelta;
            var utc0SecsInt = utc0SecsLong / 4294967296.0;
            var dateTime = Telegram.Api.Helpers.Utils.UnixTimestampToDateTime(utc0SecsInt);

            var cultureInfo = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            var shortTimePattern = UserStatusToStringConverter.GetShortTimePattern(ref cultureInfo);

            //Today
            if ((dateTime.Date == DateTime.Now.Date) && !string.IsNullOrEmpty(TodayFormat))
                return dateTime.ToString(string.Format(TodayFormat, shortTimePattern), cultureInfo);

            //Week
            if (dateTime.Date.AddDays(7) >= DateTime.Now.Date && !string.IsNullOrEmpty(WeekFormat))
                return dateTime.ToString(string.Format(WeekFormat, shortTimePattern), cultureInfo);

            //Long time ago (no more than one year ago)
            if (dateTime.Date.AddDays(365) >= DateTime.Now.Date && !string.IsNullOrEmpty(RegularFormat))
                return dateTime.ToString(string.Format(RegularFormat, shortTimePattern), cultureInfo);

            //Long long time ago
            return dateTime.ToString(string.Format(LongRegularFormat, shortTimePattern), cultureInfo);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
