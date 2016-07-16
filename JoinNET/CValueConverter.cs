using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IO;

namespace JoinNET
{
    /// <summary>
    /// ノードの状態によって背景色を作成する
    /// </summary>
    [ValueConversion(typeof(DataClient.NodeStatus), typeof(SolidColorBrush))]
    public class NodeStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DataClient.NodeStatus node_status = (DataClient.NodeStatus)value;
            if (node_status == DataClient.NodeStatus.Connected)
                return new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
            return new SolidColorBrush(Color.FromArgb(0xff, 0x88, 0x88, 0x88));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    /// <summary>
    /// ノードの状態によって背景色を作成する
    /// </summary>
    [ValueConversion(typeof(DataAccount.Status), typeof(string))]
    public class SIPAccountStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DataAccount.Status status = (DataAccount.Status)value;
            switch (status)
            {
                case DataAccount.Status.Error:
                    return "接続できませんでした";
                case DataAccount.Status.None:
                    return "初期化されていません";
                case DataAccount.Status.Connecting:
                    return "接続中...";
                case DataAccount.Status.Connected:
                    return "接続しました";
            }

            return "不明な状態です";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    /// <summary>
    /// ImageSourceに変換する
    /// </summary>
    [ValueConversion(typeof(string), typeof(BitmapImage))]
    public class StringToBitmapConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string file = (string)value;
            if (File.Exists(file) == true)
            {
                return CBmpUtil.LoadImage(file, 360);

            }
            return new BitmapImage(new Uri("Images/downloadingimage.png", UriKind.Relative));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    /// <summary>
    /// 指定ファイルパスの有無をVisibilityに変換する
    /// </summary>
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class FilePathToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string file = (string)value;
            if (File.Exists(file) == true)
            {
                return Visibility.Collapsed;

            }
            return Visibility.Visible;
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    /// <summary>
    /// bool値の有無をVisibilityに変換する(trur:Visible, false:Collapsed)
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool bIsVisible = (bool)value;
            if (bIsVisible == true)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    /// <summary>
    /// bool値の有無をVisibilityに変換する(trur:Collapsed, false:Visible)
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InboolToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool data = (bool)value;
            if (data == true)
            {
                return Visibility.Collapsed;

            }
            return Visibility.Visible;

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    /// <summary>
    /// DateTimeを変換する
    /// </summary>
    [ValueConversion(typeof(DateTime), typeof(String))]
    public class DateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime date = (DateTime)value;
            return date.ToShortDateString() + " " + date.ToLongTimeString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string strValue = value as string;
            DateTime resultDateTime;
            if (DateTime.TryParse(strValue, out resultDateTime))
            {
                return resultDateTime;
            }
            return null;
        }
    }

    [ValueConversion(typeof(bool), typeof(SolidColorBrush))]
    public class SentFlagColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool bIsVisible = (bool)value;
            if (bIsVisible == true)
                return new SolidColorBrush(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
            else
                return new SolidColorBrush(Color.FromArgb(0x88, 0xff, 0xff, 0xff));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    [ValueConversion(typeof(int), typeof(Visibility))]
    public class EditPanelVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int fromID = (int)value;
            if (fromID != 0)
                return Visibility.Collapsed;
            else
                return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    [ValueConversion(typeof(int), typeof(string))]
    public class IntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int number = (int)value;
            return number.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string number = (string)value;
            try
            {
                return int.Parse(number);
            }

            catch (Exception ex){

            }

            return 0;
        }
    }

    public class EnumBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();
            return checkValue.Equals(targetValue,
                     StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return null;

            bool useValue = (bool)value;
            string targetValue = parameter.ToString();
            if (useValue)
                return Enum.Parse(targetType, targetValue);

            return null;
        }
    }

    [ValueConversion(typeof(string), typeof(int))]
    public class CultureStringToIndex : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string culture_string = (string)value;
            if (culture_string == "en-US") return 0;
            if (culture_string == "ja-JP") return 1;
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((int)value)
            {
                case 0:
                    return "en-US";
                case 1:
                    return "ja-JP";
            }

            return "en-US";
        }
    }
}
