using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using LogCheck.Models;

namespace LogCheck.Converters
{
    /// <summary>
    /// 화이트리스트 상태에 따른 색상 변환기
    /// </summary>
    public class WhitelistStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isWhitelisted && isWhitelisted)
            {
                return new SolidColorBrush(Colors.CornflowerBlue); // 파란색으로 화이트리스트 표시
            }

            return new SolidColorBrush(Colors.Gray); // 기본 회색
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 보안 위험도에 따른 색상 변환기
    /// </summary>
    public class SecurityRiskLevelToColorConverter : IValueConverter
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
                    SecurityRiskLevel.System => new SolidColorBrush(Colors.SlateGray), // 시스템 프로세스
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
    /// 화이트리스트 상태에 따른 아이콘 표시 변환기
    /// </summary>
    public class WhitelistStatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isWhitelisted && isWhitelisted)
            {
                return "🛡"; // 방패 아이콘으로 화이트리스트 표시
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}