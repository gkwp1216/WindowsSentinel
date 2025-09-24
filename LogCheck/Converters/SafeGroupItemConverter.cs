using LogCheck.Models;
using System.Globalization;
using System.Windows.Data;

namespace LogCheck.Converters
{
    /// <summary>
    /// 그룹의 첫 번째 항목에 안전하게 접근하기 위한 Converter
    /// </summary>
    public class SafeGroupItemConverter : IValueConverter
    {
        private static readonly WhitelistStatusToColorConverter _whitelistColorConverter = new();
        private static readonly SecurityRiskLevelToColorConverter _riskColorConverter = new();
        private static readonly WhitelistStatusToIconConverter _whitelistIconConverter = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is CollectionViewGroup group && group.Items.Count > 0)
                {
                    var firstItem = group.Items[0] as ProcessNetworkInfo;
                    if (firstItem != null)
                    {
                        string propertyName = parameter as string ?? "";
                        return propertyName switch
                        {
                            "ProcessName" => firstItem.ProcessName ?? "Unknown",
                            "RiskLevel" when targetType == typeof(System.Windows.Media.Brush) =>
                                _riskColorConverter.Convert(firstItem.RiskLevel, targetType, parameter, culture),
                            "RiskLevel" => firstItem.RiskLevel.ToString(),
                            "IsWhitelisted" when targetType == typeof(System.Windows.Media.Brush) =>
                                _whitelistColorConverter.Convert(firstItem.IsWhitelisted, targetType, parameter, culture),
                            "IsWhitelisted" when targetType == typeof(string) =>
                                _whitelistIconConverter.Convert(firstItem.IsWhitelisted, targetType, parameter, culture),
                            "IsWhitelisted" => firstItem.IsWhitelisted,
                            _ => GetDefaultValue(propertyName, targetType)
                        };
                    }
                }

                // 기본값 반환
                return GetDefaultValue(parameter as string ?? "", targetType);
            }
            catch
            {
                // 예외 발생 시 기본값 반환
                return GetDefaultValue(parameter as string ?? "", targetType);
            }
        }

        private object GetDefaultValue(string propertyName, Type targetType)
        {
            return propertyName switch
            {
                "ProcessName" => "Unknown",
                "RiskLevel" when targetType == typeof(System.Windows.Media.Brush) => System.Windows.Media.Brushes.Gray,
                "RiskLevel" => "Unknown",
                "IsWhitelisted" when targetType == typeof(System.Windows.Media.Brush) => System.Windows.Media.Brushes.Gray,
                "IsWhitelisted" when targetType == typeof(string) => "",
                "IsWhitelisted" => false,
                _ => "Unknown"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// CollectionViewGroup을 안전하게 처리하기 위한 Converter
    /// </summary>
    public class SafeGroupConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is CollectionViewGroup group && group.Items.Count > 0)
                {
                    return group;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}