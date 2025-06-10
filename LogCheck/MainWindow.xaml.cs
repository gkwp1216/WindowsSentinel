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
using System.Runtime.Versioning;

namespace WindowsSentinel
{
    public partial class MainWindow : Window
    {
        private int guideStep = 0;
        private List<FrameworkElement> guideElements = new();
        private List<string> guideDescriptions = new();

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
                "네트워크 접속 내역들을\n확인하고 관리할 수 있습니다.",
                "시스템 외부 접속 내역과 보안 관련 로그를 확인할 수 있습니다.",
                "Windows Defender, 방화벽 등\n보안 프로그램을 정상화 합니다."
            };
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            guideStep = 0;
            GuideOverlay.Visibility = Visibility.Visible;
            ShowGuideStep();
        }

        private void ShowGuideStep()
        {
            if (guideStep >= guideElements.Count)
            {
                EndGuide();
                return;
            }

            var target = guideElements[guideStep];
            GuideText.Text = guideDescriptions[guideStep];
            HighlightElement(target);
            ShowGuideAtTarget(target);

            GuidePrevButton.Visibility = guideStep == 0 ? Visibility.Collapsed : Visibility.Visible;
            GuideNextButton.Content = guideStep == guideElements.Count - 1 ? "닫기" : "다음";
            GuideSkipButton.Visibility = guideStep == guideElements.Count - 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowGuideAtTarget(FrameworkElement target)
        {
            GuideBubble.Visibility = Visibility.Visible;
            GuideTail.Visibility = Visibility.Visible;
            GuideSkipButton.Visibility = Visibility.Visible;

            Dispatcher.InvokeAsync(() =>
            {
                PositionGuideBubble(target);
            }, DispatcherPriority.Loaded);
        }

        private void PositionGuideBubble(FrameworkElement target)
        {
            var targetPos = target.TransformToAncestor(this).Transform(new Point(0, 0));
            var targetSize = new Size(target.ActualWidth, target.ActualHeight);

            double bubbleWidth = GuideBubble.ActualWidth > 0 ? GuideBubble.ActualWidth : 300;
            double bubbleHeight = GuideBubble.ActualHeight > 0 ? GuideBubble.ActualHeight : 120;

            double left = 0, top = 0;
            string placement = "right";

            if (targetPos.X + targetSize.Width + bubbleWidth + 10 < this.ActualWidth)
            {
                left = targetPos.X + targetSize.Width + 10;
                top = targetPos.Y;
                placement = "right";
            }
            else if (targetPos.X - bubbleWidth - 10 > 0)
            {
                left = targetPos.X - bubbleWidth - 10;
                top = targetPos.Y;
                placement = "left";
            }
            else if (targetPos.Y + targetSize.Height + bubbleHeight + 10 < this.ActualHeight)
            {
                left = targetPos.X;
                top = targetPos.Y + targetSize.Height + 10;
                placement = "bottom";
            }
            else
            {
                left = targetPos.X;
                top = targetPos.Y - bubbleHeight - 10;
                placement = "top";
            }

            if (left + bubbleWidth > this.ActualWidth) left = this.ActualWidth - bubbleWidth - 10;
            if (top + bubbleHeight > this.ActualHeight) top = this.ActualHeight - bubbleHeight - 10;
            if (left < 0) left = 10;
            if (top < 0) top = 10;

            Canvas.SetLeft(GuideBubble, left);
            Canvas.SetTop(GuideBubble, top);

            // 🟦 꼬리 위치
            double tailLeft = 0, tailTop = 0;
            switch (placement)
            {
                case "right":
                    GuideTail.RenderTransform = new RotateTransform(90);
                    tailLeft = left - GuideTail.Width + 2;
                    tailTop = top + bubbleHeight / 2 - GuideTail.Height / 2;
                    break;
                case "left":
                    GuideTail.RenderTransform = new RotateTransform(-90);
                    tailLeft = left + bubbleWidth - 2;
                    tailTop = top + bubbleHeight / 2 - GuideTail.Height / 2;
                    break;
                case "bottom":
                    GuideTail.RenderTransform = new RotateTransform(180);
                    tailLeft = left + bubbleWidth / 2 - GuideTail.Width / 2;
                    tailTop = top - GuideTail.Height + 2;
                    break;
                case "top":
                    GuideTail.RenderTransform = new RotateTransform(0);
                    tailLeft = left + bubbleWidth / 2 - GuideTail.Width / 2;
                    tailTop = top + bubbleHeight - 2;
                    break;
            }
            Canvas.SetLeft(GuideTail, tailLeft);
            Canvas.SetTop(GuideTail, tailTop);

            // 🟦 건너뛰기 버튼 (말풍선 오른쪽 위)
            double skipLeft = left + bubbleWidth - GuideSkipButton.ActualWidth - 8;
            double skipTop = top - GuideSkipButton.ActualHeight - 8;

            Canvas.SetLeft(GuideSkipButton, skipLeft);
            Canvas.SetTop(GuideSkipButton, skipTop);
        }

        private void HighlightElement(FrameworkElement target)
        {
            MaskLayer.Children.Clear();

            var overlay = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Width = this.ActualWidth,
                Height = this.ActualHeight
            };

            var fullRect = new RectangleGeometry(new Rect(0, 0, this.ActualWidth, this.ActualHeight));
            var bounds = VisualTreeHelper.GetDescendantBounds(target);
            var transform = target.TransformToAncestor(this);
            var topLeft = transform.Transform(bounds.TopLeft);

            Rect targetRect = new Rect(topLeft, bounds.Size);

            double radiusX = 0, radiusY = 0;
            if (target is Border border)
            {
                radiusX = radiusY = Math.Max(border.CornerRadius.TopLeft, 10);
            }
            else if (target is Button)
            {
                radiusX = radiusY = 15;
            }

            Geometry highlightGeometry = new RectangleGeometry(targetRect, radiusX, radiusY);
            var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, highlightGeometry);

            var drawing = new GeometryDrawing
            {
                Geometry = combined,
                Brush = Brushes.White
            };

            overlay.OpacityMask = new DrawingBrush(drawing);
            MaskLayer.Children.Add(overlay);
        }

        private void GuideNext_Click(object sender, RoutedEventArgs e)
        {
            if (guideStep == guideElements.Count - 1)
            {
                EndGuide();
                return;
            }
            guideStep++;
            ShowGuideStep();
        }

        private void GuidePrev_Click(object sender, RoutedEventArgs e)
        {
            if (guideStep > 0)
            {
                guideStep--;
                ShowGuideStep();
            }
        }

        private void GuideSkip_Click(object sender, RoutedEventArgs e)
        {
            EndGuide();
        }

<<<<<<< HEAD
        private void EndGuide()
        {
            GuideOverlay.Visibility = Visibility.Collapsed;
            GuideBubble.Visibility = Visibility.Collapsed;
            GuideTail.Visibility = Visibility.Collapsed;
            GuideSkipButton.Visibility = Visibility.Collapsed;
            MaskLayer.Children.Clear();
        }

=======
>>>>>>> 49e2b708e5ff54a30997ae87530edb8ccbed04d8
        [SupportedOSPlatform("windows")]
        public void NavigateToPage(Page page)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            var mainGrid = FindName("mainGrid") as Grid;
            var mainButtonsGrid = FindName("mainButtonsGrid") as Grid;
            var securityStatusSection = FindName("securityStatusSection") as Border;
            
            if (mainGrid == null || mainButtonsGrid == null || securityStatusSection == null)
            {
                throw new InvalidOperationException("필수 UI 요소를 찾을 수 없습니다.");
            }

            // 메인 버튼 그리드와 보안 상태 섹션 숨기기
            mainButtonsGrid.Visibility = Visibility.Collapsed;
            securityStatusSection.Visibility = Visibility.Collapsed;

            // 기존 Frame 제거
            UIElement? uiChildToRemove = null;
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

        [SupportedOSPlatform("windows")]
        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
<<<<<<< HEAD
        {
            NavigateToPage(new InstalledPrograms());
        }

        [SupportedOSPlatform("windows")]
        private void InstalledPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new InstalledPrograms());
=======
        {
            NavigateToPage(new Page1());
        }

        [SupportedOSPlatform("windows")]
        private void InstalledPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Page2());
>>>>>>> 49e2b708e5ff54a30997ae87530edb8ccbed04d8
            HelpButton.Visibility = Visibility.Collapsed;
        }

        [SupportedOSPlatform("windows")]
        private void ModificationHistory_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Network());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        private void BtnHome_click(object sender, RoutedEventArgs e)
        {
            var mainGrid = FindName("mainGrid") as Grid;
            var mainButtonsGrid = FindName("mainButtonsGrid") as Grid;
            var securityStatusSection = FindName("securityStatusSection") as Border;
            
            if (mainGrid == null || mainButtonsGrid == null || securityStatusSection == null)
            {
                throw new InvalidOperationException("필수 UI 요소를 찾을 수 없습니다.");
            }

            // 기존 Frame 제거
            UIElement? uiChildToRemove = null;
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

        [SupportedOSPlatform("windows")]
        private void BtnLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Log());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        [SupportedOSPlatform("windows")]
        private void BtnSetting_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Setting());
            HelpButton.Visibility = Visibility.Collapsed;
        }

        [SupportedOSPlatform("windows")]
        private void SecurityRecovery_Click(object sender, RoutedEventArgs e)
        {
            // Recovery 페이지로 네비게이션
            NavigateToPage(new Recovery());
            HelpButton.Visibility = Visibility.Collapsed;
        }
    }
}
