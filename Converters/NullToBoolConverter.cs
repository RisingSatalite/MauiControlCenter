using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace MauiControlCenter
{
    public class NullToBoolConverter : IValueConverter
    {
        // Returns true when value is null by default. If ConverterParameter is "invert" returns !isNull.
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNull = value == null;
            bool invert = false;
            if (parameter != null)
            {
                var s = parameter.ToString();
                if (!string.IsNullOrEmpty(s) && s.Equals("invert", StringComparison.OrdinalIgnoreCase)) invert = true;
            }
            return invert ? !isNull : isNull;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
