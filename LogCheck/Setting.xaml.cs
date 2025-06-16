using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static LogCheck.App;

namespace LogCheck
{
    /// <summary>
    /// Setting.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Setting : Page
    {
        public Setting()
        {
            InitializeComponent();

            this.Loaded += Setting_Loaded;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            // 현재 테마에 따라 라디오 버튼만 업데이트 (이벤트는 실행 안 되게)
            if (ThemeManager.CurrentTheme == "Light")
            {
                LightModeRadioButton.Checked -= SwitchToLightMode_Checked;
                LightModeRadioButton.IsChecked = true;
                LightModeRadioButton.Checked += SwitchToLightMode_Checked;
            }
            else if (ThemeManager.CurrentTheme == "Dark")
            {
                DarkModeRadioButton.Checked -= SwitchToDarkMode_Checked;
                DarkModeRadioButton.IsChecked = true;
                DarkModeRadioButton.Checked += SwitchToDarkMode_Checked;
            }
        }

        private void SwitchToLightMode_Checked(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Light");
        }

        private void SwitchToDarkMode_Checked(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Dark");
        }

        // 테마를 변경하는 메서드
        private void ApplyTheme(string themeName)
        {
            // 테마 적용
            System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
            var theme = new ResourceDictionary
            {
                Source = new Uri($"/{themeName}Theme.xaml", UriKind.Relative)
            };
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(theme);

            ThemeManager.CurrentTheme = themeName;
        }
    }
}
