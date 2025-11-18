using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Converters
{
    /// <summary>
    /// Convierte bool a Brush basado en parámetro
    /// Parámetro formato: "ColorTrue|ColorFalse" (ej: "Green|Red")
    /// </summary>
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string param)
            {
                var parts = param.Split('|');
                if (parts.Length == 2)
                {
                    var colorName = boolValue ? parts[0] : parts[1];
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(colorName);
                        return new SolidColorBrush(color);
                    }
                    catch
                    {
                        // Si falla, usar color por defecto
                    }
                }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
