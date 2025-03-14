using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Waytotec.ControlSystem.App.Converters
{
    class CustomConverters : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new Thickness(0, 0, -(double)value, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
