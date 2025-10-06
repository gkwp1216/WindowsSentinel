using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogCheck.Models;

namespace LogCheck.Converters
{
    /// <summary>
    /// 위험도 레벨을 색상으로 변환하는 컨버터 (테마 리소스 활용)
    /// </summary>
    public class RiskLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SecurityRiskLevel riskLevel)
            {
                // 테마별 색상 리소스 활용
                var resourceKey = riskLevel switch
                {
                    SecurityRiskLevel.Low => "RiskLowColor",
                    SecurityRiskLevel.Medium => "RiskMediumColor",

                    SecurityRiskLevel.High => "RiskHighColor",
                    SecurityRiskLevel.Critical => "RiskCriticalColor",
                    SecurityRiskLevel.System => "RiskSystemColor",
                    _ => null
                };

                if (resourceKey != null && System.Windows.Application.Current?.Resources[resourceKey] is SolidColorBrush brush)
                {
                    return brush;
                }

                // 기본 색상 (리소스를 찾지 못한 경우)
                return riskLevel switch
                {
                    SecurityRiskLevel.Low => new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),      // #4CAF50 초록
                    SecurityRiskLevel.Medium => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0)),   // #FF9800 주황
                    SecurityRiskLevel.High => new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)),     // #F44336 빨강
                    SecurityRiskLevel.Critical => new SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 39, 176)), // #9C27B0 보라
                    SecurityRiskLevel.System => new SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 125, 139)),  // #607D8B 회색파랑
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
    /// 위험도 레벨을 배경색으로 변환하는 컨버터 (반투명 효과 적용)
    /// </summary>
    public class RiskLevelToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SecurityRiskLevel riskLevel)
            {
                // 테마별 색상 리소스 활용 후 투명도 조정
                var resourceKey = riskLevel switch
                {
                    SecurityRiskLevel.Low => "RiskLowColor",
                    SecurityRiskLevel.Medium => "RiskMediumColor",

                    SecurityRiskLevel.High => "RiskHighColor",
                    SecurityRiskLevel.Critical => "RiskCriticalColor",
                    SecurityRiskLevel.System => "RiskSystemColor",
                    _ => null
                };

                if (resourceKey != null && System.Windows.Application.Current?.Resources[resourceKey] is SolidColorBrush sourceBrush)
                {
                    // 배경용으로 투명도 조정 (30% 투명도)
                    var color = sourceBrush.Color;
                    color.A = 77; // 30% opacity (255 * 0.3)
                    return new SolidColorBrush(color);
                }

                // 기본 배경색 (투명도 적용)
                return riskLevel switch
                {
                    SecurityRiskLevel.Low => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 76, 175, 80)),      // 30% 투명 초록
                    SecurityRiskLevel.Medium => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 255, 152, 0)),   // 30% 투명 주황
                    SecurityRiskLevel.High => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 244, 67, 54)),     // 30% 투명 빨강
                    SecurityRiskLevel.Critical => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 156, 39, 176)), // 30% 투명 보라
                    SecurityRiskLevel.System => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 96, 125, 139)),  // 30% 투명 회색파랑
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 128, 128, 128)) // 30% 투명 회색
                };
            }
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 128, 128, 128));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// SecurityRiskLevel을 색상으로 변환하는 컨버터 (RiskLevelToColorConverter와 동일하지만 명시적 이름)
    /// </summary>
    public class SecurityRiskLevelToColorConverter : IValueConverter
    {
        private static readonly RiskLevelToColorConverter _converter = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return _converter.Convert(value, targetType, parameter, culture);
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
                    "low" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 76, 175, 80)),      // 30% 투명 초록
                    "medium" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 255, 152, 0)),   // 30% 투명 주황
                    "high" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 244, 67, 54)),     // 30% 투명 빨강
                    "critical" => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 156, 39, 176)), // 30% 투명 보라
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 128, 128, 128))         // 30% 투명 회색
                };
            }
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(77, 128, 128, 128));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
