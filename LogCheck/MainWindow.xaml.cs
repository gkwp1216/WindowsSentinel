using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using LogCheck;

namespace WindowsSentinel
{
    public partial class MainWindow : Window
    {       
        public MainWindow()
        {
            InitializeComponent();
        }

        public void NavigateToPage(Page page)
        {
            var mainGrid = FindName("mainGrid") as Grid;
            var mainButtonsGrid = FindName("mainButtonsGrid") as Grid;
            var securityStatusSection = FindName("securityStatusSection") as Border;
            
            if (mainGrid != null && mainButtonsGrid != null && securityStatusSection != null)
            {
                // 메인 버튼 그리드와 보안 상태 섹션 숨기기
                mainButtonsGrid.Visibility = Visibility.Collapsed;
                securityStatusSection.Visibility = Visibility.Collapsed;

                // 기존 Frame 제거
                UIElement uiChildToRemove = null;
                foreach (UIElement child in mainGrid.Children)
                {
                    if (child is Frame)
                    {
                        uiChildToRemove = child;
                        break;
                    }
                }
                
                if (uiChildToRemove != null)
                {
                    mainGrid.Children.Remove(uiChildToRemove);
                }
                
                // 새로운 페이지 추가
                var frame = new Frame();
                frame.Content = page;
                mainGrid.Children.Add(frame);
                Grid.SetRow(frame, 2);  // 버튼 그리드와 같은 Row에 배치
                Grid.SetColumn(frame, 0);
            }
        }

        private void InstalledPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page1());
        }

        private void ModificationHistory_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page2());
        }

        private void BtnHome_click(object sender, RoutedEventArgs e)
        {
            var mainGrid = FindName("mainGrid") as Grid;
            var mainButtonsGrid = FindName("mainButtonsGrid") as Grid;
            var securityStatusSection = FindName("securityStatusSection") as Border;
            
            if (mainGrid != null && mainButtonsGrid != null && securityStatusSection != null)
            {
                // 기존 Frame 제거
                UIElement uiChildToRemove = null;
                foreach (UIElement child in mainGrid.Children)
                {
                    if (child is Frame)
                    {
                        uiChildToRemove = child;
                        break;
                    }
                }
                
                if (uiChildToRemove != null)
                {
                    mainGrid.Children.Remove(uiChildToRemove);
                }

                // 메인 버튼 그리드와 보안 상태 섹션 다시 보이기
                mainButtonsGrid.Visibility = Visibility.Visible;
                securityStatusSection.Visibility = Visibility.Visible;
            }
        }

        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Log());
        }

        private void BtnSetting_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Setting());
        }

        private void SecurityRecovery_Click(object sender, RoutedEventArgs e)
        {
            // Recovery 페이지로 네비게이션
            NavigateToPage(new Recovery());
        }
    }
}
