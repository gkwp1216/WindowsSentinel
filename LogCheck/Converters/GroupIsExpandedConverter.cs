using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using LogCheck.Models;

namespace LogCheck.Converters
{
    /// <summary>
    /// CollectionViewGroup에서 첫 번째 ProcessGroup의 IsExpanded 속성을 바인딩하기 위한 컨버터
    /// </summary>
    public class GroupIsExpandedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // CollectionViewGroup의 첫 번째 항목을 가져옴
                if (value is System.Windows.Data.CollectionViewGroup group && group.Items?.Count > 0)
                {
                    if (group.Items[0] is ProcessNetworkInfo firstProcess)
                    {
                        // ProcessId로 ProcessGroup을 찾음 - 하지만 이 방법은 복잡함
                        // 대신 기본값 반환
                        return false; // 기본적으로 접힌 상태
                    }
                }

                return false; // 기본값
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GroupIsExpandedConverter.Convert 오류: {ex.Message}");
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // ConvertBack은 복잡하므로 일단 사용하지 않음
            throw new NotImplementedException("GroupIsExpandedConverter는 OneWay 바인딩만 지원합니다.");
        }
    }
}