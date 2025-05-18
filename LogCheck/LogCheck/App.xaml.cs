using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LogCheck
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public void ChangeTheme(string theme)
        {
            ResourceDictionary newTheme = new ResourceDictionary();
            switch (theme)
            {
                case "Light":
                    newTheme.Source = new Uri("LightTheme.xaml", UriKind.Relative);
                    break;
                case "Dark":
                    newTheme.Source = new Uri("DarkTheme.xaml", UriKind.Relative);
                    break;
            }

            // 기존 리소스 제거 및 새 리소스 추가
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(newTheme);
        }
    }

}
