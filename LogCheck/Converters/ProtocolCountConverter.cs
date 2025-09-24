using LogCheck.Models;
using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace LogCheck.Converters
{
    public class ProtocolCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable items && parameter is string protocol)
            {
                var count = items.Cast<ProcessNetworkInfo>()
                                 .Count(p => p.Protocol?.Equals(protocol, StringComparison.OrdinalIgnoreCase) == true);
                return count.ToString();
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}