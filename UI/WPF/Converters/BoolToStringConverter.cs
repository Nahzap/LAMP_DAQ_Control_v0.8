using System;
using System.Globalization;
using System.Windows.Data;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Converters
{
    /// <summary>
    /// Convierte bool a string basado en parámetro
    /// Parámetro formato: "ValueTrue|ValueFalse"
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string param)
            {
                var parts = param.Split('|');
                if (parts.Length == 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }
            return value?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
