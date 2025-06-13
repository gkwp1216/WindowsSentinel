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
using System.Windows.Threading;
using System.Runtime.Versioning;
using System.Windows.Media.Animation;

namespace WindowsSentinel
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private Page currentPage;
        private bool isGuideActive = false;
        private int currentGuideStep = 0;
        private readonly string[] guideTexts = new string[]
        {
            "이 프로그램은 Windows 시스템의 보안 상태를 모니터링하고 관리하는 도구입니다.",
            "왼쪽 사이드바를 통해 다양한 기능에 접근할 수 있습니다.",
            "홈 화면에서는 시스템의 전반적인 보안 상태를 확인할 수 있습니다.",
            "프로그램 관리에서는 설치된 프로그램을 검사하고 관리할 수 있습니다.",
            "네트워크 모니터링에서는 시스템 변경 사항을 추적할 수 있습니다.",
            "로그 검사에서는 시스템 로그를 확인하고 분석할 수 있습니다.",
            "복구 도구에서는 시스템 문제를 진단하고 복구할 수 있습니다.",
            "이제 Windows Sentinel을 시작해보세요!"
        };

        public MainWindow()
        {
            InitializeComponent();
            NavigateToPage(new HomePage());
            // ShowGuide(); // 프로그램 시작 시 가이드 자동 실행 비활성화
        }

        public void NavigateToPage(Page page)
        {
            if (page != null)
            {
                currentPage = page;
                
                // HomePage일 때는 mainButtonsGrid를 보이고 MainFrame을 숨김
                if (page is HomePage)
                {
                    mainButtonsGrid.Visibility = Visibility.Visible;
                    MainFrame.Visibility = Visibility.Collapsed;
                    securityStatusSection.Visibility = Visibility.Visible;  // 보안 상태 섹션 표시
                }
                // 다른 페이지일 때는 mainButtonsGrid를 숨기고 MainFrame을 보임
                else
                {
                    mainButtonsGrid.Visibility = Visibility.Collapsed;
                    MainFrame.Visibility = Visibility.Visible;
                    securityStatusSection.Visibility = Visibility.Collapsed;  // 보안 상태 섹션 숨김
                    MainFrame.Navigate(page);
                }
            }
        }

        private void ShowGuide()
        {
            if (!isGuideActive)
            {
                isGuideActive = true;
                currentGuideStep = 0;
                GuideOverlay.Visibility = Visibility.Visible;
                UpdateGuideStep();
            }
        }

        private void UpdateGuideStep()
        {
            if (currentGuideStep < guideTexts.Length)
            {
                // 가이드 텍스트 업데이트
                GuideText.Text = guideTexts[currentGuideStep];

                // 마스크 레이어 업데이트
                MaskLayer.Children.Clear();
                var mask = new Rectangle
                {
                    Fill = new SolidColorBrush(Colors.Black),
                    Opacity = 0.7
                };

                // 현재 단계에 따라 마스크 위치 조정
                switch (currentGuideStep)
                {
                    case 0: // 환영 메시지
                        mask.Width = GuideOverlay.ActualWidth;
                        mask.Height = GuideOverlay.ActualHeight;
                        Canvas.SetLeft(mask, 0);
                        Canvas.SetTop(mask, 0);
                        break;
                    case 1: // 사이드바
                        mask.Width = 200;
                        mask.Height = GuideOverlay.ActualHeight;
                        Canvas.SetLeft(mask, 0);
                        Canvas.SetTop(mask, 0);
                        break;
                    // 다른 단계들에 대한 마스크 위치 설정
                    default:
                        mask.Width = GuideOverlay.ActualWidth;
                        mask.Height = GuideOverlay.ActualHeight;
                        Canvas.SetLeft(mask, 0);
                        Canvas.SetTop(mask, 0);
                        break;
                }

                MaskLayer.Children.Add(mask);

                // 다음 버튼 활성화/비활성화
                NextButton.IsEnabled = currentGuideStep < guideTexts.Length - 1;
                PrevButton.IsEnabled = currentGuideStep > 0;
            }
            else
            {
                // 가이드 종료
                GuideOverlay.Visibility = Visibility.Collapsed;
                isGuideActive = false;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentGuideStep < guideTexts.Length - 1)
            {
                currentGuideStep++;
                UpdateGuideStep();
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentGuideStep > 0)
            {
                currentGuideStep--;
                UpdateGuideStep();
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            GuideOverlay.Visibility = Visibility.Collapsed;
            isGuideActive = false;
        }

        private void GuideOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isGuideActive)
            {
                NextButton_Click(sender, e);
            }
        }

        [SupportedOSPlatform("windows")]
        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Log());
        }

        [SupportedOSPlatform("windows")]
        private void BtnSetting_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Setting());
        }

        [SupportedOSPlatform("windows")]
        private void SecurityRecovery_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Recovery());
        }

        [SupportedOSPlatform("windows")]
        private void ModificationHistory_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Network());
        }

        [SupportedOSPlatform("windows")]
        private void InstalledPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new InstalledPrograms());
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            ShowGuide();
        }

        private void BtnHome_click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new HomePage());
        }
    }
}
