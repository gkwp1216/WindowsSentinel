using LogCheck;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Navigation;

namespace WindowsSentinel
{
    public partial class MainWindow : Window
    {
        private int guideStep = 0;
        private List<FrameworkElement> guideElements;
        private List<string> guideDescriptions;

        public MainWindow()
        {
            InitializeComponent();
            InitializeGuide();
        }

        private void InitializeGuide()
        {
            guideElements = new List<FrameworkElement>
            {
                securityStatusSection,
                InstalledProgramsButton,
                ModificationHistoryButton,
                SecurityLogButton,
                SecurityRecoveryButton
            };

            guideDescriptions = new List<string>
            {
                "시스템의 전반적인 보안 상태를\n확인할 수 있습니다.",
                "시스템에 설치된 프로그램 목록을\n확인하고 관리할 수 있습니다.",
                "시스템 수정 / 변경 이력을\n확인하고 관리할 수 있습니다.",
                "시스템 외부 접속 내역과 보안 관련 로그를 확인할 수 있습니다.",
                "Windows Defender, 방화벽 등\n보안 프로그램을 정상화 합니다."
            };
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            guideStep = 0;
            ShowGuideStep();
        }

        private void ShowGuideStep()
        {
            if (guideStep >= guideElements.Count)
            {
                GuideOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            var target = guideElements[guideStep];

            Dispatcher.InvokeAsync(() =>
            {
                HighlightElement(target);
                GuideText.Text = guideDescriptions[guideStep];
                GuideOverlay.Visibility = Visibility.Visible;

                var targetPos = target.TransformToAncestor(this).Transform(new Point(0, 0));
                var targetSize = new Size(target.ActualWidth, target.ActualHeight);

                double bubbleWidth = GuideBubble.ActualWidth > 0 ? GuideBubble.ActualWidth : 300;
                double bubbleHeight = GuideBubble.ActualHeight > 0 ? GuideBubble.ActualHeight : 120;

                double left = 0, top = 0;

                // 우선순위: 오른쪽 → 왼쪽 → 아래 → 위
                if (targetPos.X + targetSize.Width + bubbleWidth + 10 < this.ActualWidth)
                {
                    // 오른쪽
                    left = targetPos.X + targetSize.Width + 10;
                    top = targetPos.Y;
                }
                else if (targetPos.X - bubbleWidth - 10 > 0)
                {
                    // 왼쪽
                    left = targetPos.X - bubbleWidth - 10;
                    top = targetPos.Y;
                }
                else if (targetPos.Y + targetSize.Height + bubbleHeight + 10 < this.ActualHeight)
                {
                    // 아래
                    left = targetPos.X;
                    top = targetPos.Y + targetSize.Height + 10;
                }
                else
                {
                    // 위
                    left = targetPos.X;
                    top = targetPos.Y - bubbleHeight - 10;
                }

                // 화면 경계 보정
                if (left + bubbleWidth > this.ActualWidth) left = this.ActualWidth - bubbleWidth - 10;
                if (top + bubbleHeight > this.ActualHeight) top = this.ActualHeight - bubbleHeight - 10;
                if (left < 0) left = 10;
                if (top < 0) top = 10;

                Canvas.SetLeft(GuideBubble, left);
                Canvas.SetTop(GuideBubble, top);

                // 이전/다음/건너뛰기 버튼 제어
                GuidePrevButton.Visibility = guideStep == 0 ? Visibility.Collapsed : Visibility.Visible;
                GuideNextButton.Content = guideStep == guideElements.Count - 1 ? "닫기" : "다음";
                GuideSkipButton.Visibility = guideStep == guideElements.Count - 1 ? Visibility.Collapsed : Visibility.Visible;

            }, DispatcherPriority.Loaded);           
        }

        private void HighlightElement(FrameworkElement target)
        {
            MaskLayer.Children.Clear();

            var overlay = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), // 어두운 마스크 배경
                Width = this.ActualWidth,
                Height = this.ActualHeight
            };

            // 대상 요소의 화면 위치 계산
            var screenPos = target.TransformToAncestor(this).Transform(new Point(0, 0));
            var size = new Size(target.ActualWidth, target.ActualHeight);

            // 전체 화면 영역과 하이라이트 영역 설정
            var fullRect = new RectangleGeometry(new Rect(0, 0, this.ActualWidth, this.ActualHeight));
            var highlightRect = new RectangleGeometry(new Rect(screenPos, size));

            // 두 영역을 결합 (하이라이트 영역만 제외)
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, highlightRect);
            var flattened = combined.GetFlattenedPathGeometry();

            // ⚠️ 여기서 Brush로 변환
            var geometryDrawing = new GeometryDrawing
            {
                Geometry = flattened,
                Brush = Brushes.White // 밝게 보일 부분
            };

            var drawingBrush = new DrawingBrush(geometryDrawing);

            overlay.OpacityMask = drawingBrush; // ✅ Brush로 설정

            MaskLayer.Children.Add(overlay);
        }

        private void GuideNext_Click(object sender, RoutedEventArgs e)
        {
            if (guideStep == guideElements.Count - 1)
            {
                GuideOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            guideStep++;
            ShowGuideStep();
        }

        private void GuidePrev_Click(object sender, RoutedEventArgs e)
        {
            guideStep--;
            if (guideStep < 0) guideStep = 0;
            ShowGuideStep();
        }

        private void GuideSkip_Click(object sender, RoutedEventArgs e)
        {
            GuideOverlay.Visibility = Visibility.Collapsed;
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
            HelpButton.Visibility = Visibility.Collapsed;
        }

        private void ModificationHistory_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page2());
            HelpButton.Visibility = Visibility.Collapsed;
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
                HelpButton.Visibility = Visibility.Visible;
            }
        }

        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Log());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        private void BtnSetting_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Setting());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        private void SecurityRecovery_Click(object sender, RoutedEventArgs e)
        {
            // Recovery 페이지로 네비게이션
            NavigateToPage(new Recovery());
            HelpButton.Visibility = Visibility.Collapsed;
        }
    }
}
