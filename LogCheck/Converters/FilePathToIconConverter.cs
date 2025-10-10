using System.IO;
using System.Runtime.Versioning;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace LogCheck.Converters
{
    [SupportedOSPlatform("windows6.1")]
    public class FilePathToIconConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string filePath && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    // 파일 경로에서 아이콘을 추출
                    using (Icon? icon = Icon.ExtractAssociatedIcon(filePath))
                    {
                        if (icon != null)
                        {
                            // Icon을 WPF에서 사용할 수 있는 ImageSource로 변환
                            return Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                System.Windows.Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                    }
                }
                catch
                {
                    // 아이콘 로드 실패 시, 기본 아이콘 또는 null 반환
                }
            }
            return null; // 경로가 유효하지 않거나 파일을 찾을 수 없으면 null 반환
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}