using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace LinkingMountains.ImageZoom
{
    internal class PercentageFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
                return DependencyProperty.UnsetValue;

            try
            {
                double d = (double)value;

                string format = Math.Abs(d) >= 0.01 || d is 0 ? "P0" : "P2";
                return d.ToString(format, culture);
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                str = str.Replace("%", "").Trim();

                if (double.TryParse(str, NumberStyles.Any, culture, out double result))
                    return result / 100d;
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
