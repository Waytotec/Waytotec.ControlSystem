using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.App.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CameraStatus status)
            {
                return status switch
                {
                    CameraStatus.Online => new SolidColorBrush(Colors.LimeGreen),
                    CameraStatus.Offline => new SolidColorBrush(Colors.Gray),
                    CameraStatus.Error => new SolidColorBrush(Colors.Red),
                    CameraStatus.Updating => new SolidColorBrush(Colors.Orange),
                    CameraStatus.Connecting => new SolidColorBrush(Colors.DodgerBlue),
                    _ => new SolidColorBrush(Colors.Black)
                };
            }

            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

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
