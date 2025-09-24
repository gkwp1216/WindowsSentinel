using LogCheck.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LogCheck.Converters
{
    /// <summary>
    /// 위험도 레벨을 색상으로 변환하는 컨버터
    /// </summary>
    public class RiskLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SecurityRiskLevel riskLevel)
            {
                return riskLevel switch
                {
                    SecurityRiskLevel.Low => new SolidColorBrush(Colors.Green),
                    SecurityRiskLevel.Medium => new SolidColorBrush(Colors.Orange),
                    SecurityRiskLevel.High => new SolidColorBrush(Colors.Red),
                    SecurityRiskLevel.Critical => new SolidColorBrush(Colors.Purple),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 위험도 레벨을 배경색으로 변환하는 컨버터
    /// </summary>
    public class RiskLevelToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SecurityRiskLevel riskLevel)
            {
                return riskLevel switch
                {
                    SecurityRiskLevel.Low => new SolidColorBrush(Colors.Green),
                    SecurityRiskLevel.Medium => new SolidColorBrush(Colors.Orange),
                    SecurityRiskLevel.High => new SolidColorBrush(Colors.Red),
                    SecurityRiskLevel.Critical => new SolidColorBrush(Colors.Purple),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 경고 레벨을 배경색으로 변환하는 컨버터
    /// </summary>
    public class AlertLevelToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string alertLevel)
            {
                return alertLevel.ToLower() switch
                {
                    "low" => new SolidColorBrush(Colors.Green),
                    "medium" => new SolidColorBrush(Colors.Orange),
                    "high" => new SolidColorBrush(Colors.Red),
                    "critical" => new SolidColorBrush(Colors.Purple),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
