using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.App.Converters
{
    /// <summary>
    /// 카메라 상태를 색상으로 변환하는 컨버터 (개선된 버전)
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CameraStatus status)
            {
                return status switch
                {
                    CameraStatus.Online => new SolidColorBrush(Color.FromRgb(46, 204, 113)),     // 밝은 녹색
                    CameraStatus.Offline => new SolidColorBrush(Color.FromRgb(149, 165, 166)),   // 회색
                    CameraStatus.Error => new SolidColorBrush(Color.FromRgb(231, 76, 60)),       // 빨간색
                    CameraStatus.Updating => new SolidColorBrush(Color.FromRgb(241, 196, 15)),   // 노란색
                    CameraStatus.Connecting => new SolidColorBrush(Color.FromRgb(52, 152, 219)), // 파란색
                    _ => new SolidColorBrush(Color.FromRgb(127, 140, 141))                       // 어두운 회색
                };
            }

            return new SolidColorBrush(Color.FromRgb(127, 140, 141));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 상태에 따른 텍스트 색상 변환 컨버터
    /// </summary>
    public class StatusToTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CameraStatus status)
            {
                return status switch
                {
                    CameraStatus.Online => new SolidColorBrush(Color.FromRgb(39, 174, 96)),      // 진한 녹색
                    CameraStatus.Offline => new SolidColorBrush(Color.FromRgb(127, 140, 141)),   // 회색
                    CameraStatus.Error => new SolidColorBrush(Color.FromRgb(192, 57, 43)),       // 진한 빨간색
                    CameraStatus.Updating => new SolidColorBrush(Color.FromRgb(211, 173, 8)),    // 진한 노란색
                    CameraStatus.Connecting => new SolidColorBrush(Color.FromRgb(41, 128, 185)), // 진한 파란색
                    _ => new SolidColorBrush(Color.FromRgb(52, 73, 94))                          // 어두운 회색
                };
            }

            return new SolidColorBrush(Color.FromRgb(52, 73, 94));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 상태에 따른 배경색 변환 컨버터 (행 전체 색상)
    /// </summary>
    public class StatusToRowBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CameraStatus status)
            {
                return status switch
                {
                    CameraStatus.Online => new SolidColorBrush(Color.FromArgb(25, 46, 204, 113)),     // 연한 녹색 배경
                    CameraStatus.Offline => new SolidColorBrush(Color.FromArgb(15, 149, 165, 166)),   // 연한 회색 배경
                    CameraStatus.Error => new SolidColorBrush(Color.FromArgb(25, 231, 76, 60)),       // 연한 빨간색 배경
                    CameraStatus.Updating => new SolidColorBrush(Color.FromArgb(25, 241, 196, 15)),   // 연한 노란색 배경
                    CameraStatus.Connecting => new SolidColorBrush(Color.FromArgb(25, 52, 152, 219)), // 연한 파란색 배경
                    _ => Brushes.Transparent
                };
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 상태에 따른 아이콘/기호 변환 컨버터
    /// </summary>
    public class StatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CameraStatus status)
            {
                return status switch
                {
                    CameraStatus.Online => "●",        // 원형 아이콘
                    CameraStatus.Offline => "●",       // 원형 아이콘
                    CameraStatus.Error => "●",         // 원형 아이콘
                    CameraStatus.Updating => "●",      // 원형 아이콘
                    CameraStatus.Connecting => "●",    // 원형 아이콘
                    _ => "●"
                };
            }

            return "●";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 상태에 따른 테두리 색상 변환 컨버터
    /// </summary>
    public class StatusToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CameraStatus status)
            {
                return status switch
                {
                    CameraStatus.Online => new SolidColorBrush(Color.FromRgb(39, 174, 96)),      // 녹색 테두리
                    CameraStatus.Offline => new SolidColorBrush(Color.FromRgb(149, 165, 166)),   // 회색 테두리
                    CameraStatus.Error => new SolidColorBrush(Color.FromRgb(192, 57, 43)),       // 빨간색 테두리
                    CameraStatus.Updating => new SolidColorBrush(Color.FromRgb(211, 173, 8)),    // 노란색 테두리
                    CameraStatus.Connecting => new SolidColorBrush(Color.FromRgb(41, 128, 185)), // 파란색 테두리
                    _ => Brushes.Transparent
                };
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 불린값 반전 컨버터
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return false;
        }
    }
}