using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogCheck.Models;

namespace LogCheck
{
    // 로컬 네임스페이스에서 사용 가능한 동일 기능 컨버터들
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

    public class AlertLevelToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string alertLevel)
            {
                switch (alertLevel.ToLower())
                {
                    case "low": return new SolidColorBrush(Colors.Green);
                    case "medium": return new SolidColorBrush(Colors.Orange);
                    case "high": return new SolidColorBrush(Colors.Red);
                    case "critical": return new SolidColorBrush(Colors.Purple);
                    default: return new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


