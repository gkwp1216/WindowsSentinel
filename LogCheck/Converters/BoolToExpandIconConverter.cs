using System.Globalization;
using System.Windows.Data;

namespace LogCheck.Converters
{
    /// <summary>
    /// bool 값을 확장/축소 아이콘으로 변환하는 컨버터
    /// </summary>
    public class BoolToExpandIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "▼" : "►";
            }
            return "►";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}