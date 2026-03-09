using System;
using System.Globalization;
using System.Windows.Data;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Converters
{
    /// <summary>
    /// Converts percentage (0-100) to pixel width based on total width
    /// </summary>
    public class PercentageToPixelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2) return 0.0;
            if (!(values[0] is double percentage)) return 0.0;
            if (!(values[1] is double totalWidth)) return 0.0;

            return (percentage / 100.0) * totalWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
