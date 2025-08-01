using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;

namespace Waytotec.ControlSystem.App.Converters
{
    public class ThemeToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ApplicationTheme theme)
            {
                return theme == ApplicationTheme.Dark ? 0.1 : 0.2; // Light 테마에서 더 진하게
            }
            return 0.1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ThemeToLineOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ApplicationTheme theme)
            {
                return theme == ApplicationTheme.Dark ? 0.15 : 0.3; // Light 테마에서 더 진하게
            }
            return 0.15;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}