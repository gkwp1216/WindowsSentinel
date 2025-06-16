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

namespace LogCheck
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
        public static class ThemeManager
        {
<<<<<<< HEAD
            public static string CurrentTheme { get; set; } = "Light"; // 기본값
=======
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
>>>>>>> e0a62f76f986c6042a01fc33c90a24d6bb90b903
        }
    }
}
