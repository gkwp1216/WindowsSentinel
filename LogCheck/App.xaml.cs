using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsSentinel
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public App()
        {
            InitializeComponent();
        }

        // 테마를 변경하는 메서드
        public void ApplyTheme(string themeName)
        {
            var newTheme = new ResourceDictionary();

            switch (themeName)
            {
                case "Light":
                    newTheme.Source = new Uri("pack://application:,,,/LightTheme.xaml", UriKind.Absolute);
                    break;
                case "Dark":
                    newTheme.Source = new Uri("pack://application:,,,/DarkTheme.xaml", UriKind.Absolute);
                    break;
                default:
                    throw new ArgumentException("Unknown theme");
            }

            // 기존 테마 제거
            var existingThemes = Current.Resources.MergedDictionaries
                .Where(dict => dict.Source != null &&
                               (dict.Source.OriginalString.Contains("LightTheme.xaml") ||
                                dict.Source.OriginalString.Contains("DarkTheme.xaml")))
                .ToList();

            foreach (var theme in existingThemes)
            {
                Current.Resources.MergedDictionaries.Remove(theme);
            }

            // 새 테마 적용
            Current.Resources.MergedDictionaries.Add(newTheme);
        }
    }
}
