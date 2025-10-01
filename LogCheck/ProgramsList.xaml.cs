/*
 * [버전 변경 사항 요약] 
 * 1. BasePageViewModel 패턴 적용 - MVVM 아키텍처 구현
 * 2. 중복 코드 제거 및 통합 서비스 활용
 * 3. LogMessageService를 통한 통일된 로그 관리
 * 4. StatisticsService를 통한 중앙 집중식 데이터 관리
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LogCheck.Services;
using LogCheck.ViewModels;
using WpfMessageBox = System.Windows.MessageBox;

namespace LogCheck
{
    // 프로그램별 보안 점수 및 이름 관리용 static 클래스 (임시/샘플)
    public static class ProgramSecurityManager
    {
        public static List<string> Name { get; set; } = new();
        public static List<int> Scores { get; set; } = new();
    }

    /// <summary>
    /// ProgramsList.xaml에 대한 상호작용 논리
    /// BasePageViewModel 패턴을 사용하여 리팩토링됨
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class ProgramsList : Page, INavigable
    {
        private readonly ProgramsListViewModel _viewModel = null!;
        private ToggleButton? _selectedButton;

        /// <summary>
        /// 생성자
        /// </summary>
        public ProgramsList()
        {
            try
            {
                InitializeComponent();

                // ViewModel 초기화
                _viewModel = new ProgramsListViewModel();
                DataContext = _viewModel;

                // 사이드바 버튼 설정
                SideProgramsListButton.IsChecked = true;

                // DataGrid 바인딩 설정
                SetupDataGridBinding();

                // 스피너 초기화
                InitializeSpinner();

                // 페이지 초기화
                _ = InitializePageAsync();

                _viewModel.LogService.AddLogMessage("✅ ProgramsList 페이지 생성 완료");
            }
            catch (Exception ex)
            {
                var errorMsg = $"ProgramsList 초기화 중 오류: {ex.Message}";
                LogHelper.LogError(errorMsg);

                WpfMessageBox.Show(errorMsg, "초기화 오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Initialization Methods
        /// <summary>
        /// 페이지 비동기 초기화
        /// </summary>
        private async Task InitializePageAsync()
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                _viewModel.LogService.LogError($"페이지 초기화 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// DataGrid 바인딩 설정
        /// </summary>
        private void SetupDataGridBinding()
        {
            try
            {
                var dataGrid = this.FindName("programDataGrid") as DataGrid;
                if (dataGrid != null)
                {
                    dataGrid.ItemsSource = _viewModel.ViewSource.View;
                    _viewModel.LogService.AddLogMessage("📋 DataGrid 바인딩 설정 완료");
                }
            }
            catch (Exception ex)
            {
                _viewModel.LogService.LogError($"DataGrid 바인딩 설정 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 스피너 초기화
        /// </summary>
        private void InitializeSpinner()
        {
            try
            {
                if (SpinnerItems != null)
                {
                    SpinnerItems.ItemsSource = CreateSpinnerPoints(40, 50, 50);
                    StartRotation();
                    _viewModel.LogService.AddLogMessage("🔄 스피너 초기화 완료");
                }
            }
            catch (Exception ex)
            {
                _viewModel.LogService.LogError($"스피너 초기화 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 스피너 포인트 생성
        /// </summary>
        private System.Collections.IEnumerable CreateSpinnerPoints(int count, double centerX, double centerY)
        {
            var points = new List<System.Windows.Point>();
            for (int i = 0; i < count; i++)
            {
                double angle = (360.0 / count) * i;
                double radius = 30;
                double x = centerX + radius * Math.Cos(angle * Math.PI / 180);
                double y = centerY + radius * Math.Sin(angle * Math.PI / 180);
                points.Add(new System.Windows.Point(x, y));
            }
            return points;
        }

        /// <summary>
        /// 스피너 회전 시작
        /// </summary>
        private void StartRotation()
        {
            try
            {
                var rotateAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = new Duration(TimeSpan.FromSeconds(2.0)),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, rotateAnimation);
            }
            catch (Exception ex)
            {
                _viewModel.LogService.LogError($"스피너 회전 시작 중 오류: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 사이드바 버튼 클릭 이벤트
        /// </summary>
        [SupportedOSPlatform("windows")]
        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var clicked = sender as ToggleButton;
                if (clicked == null) return;

                // 이전 선택 해제
                if (_selectedButton != null && _selectedButton != clicked)
                    _selectedButton.IsChecked = false;

                // 선택 상태 유지
                clicked.IsChecked = true;
                _selectedButton = clicked;

                var parameter = clicked.CommandParameter?.ToString();
                _viewModel.LogService.AddLogMessage($"🔘 사이드바 버튼 클릭: {parameter}");

                switch (parameter)
                {
                    case "Vaccine":
                        NavigateToPage(new Vaccine());
                        break;
                    case "NetWorks_New":
                        NavigateToPage(new NetWorks_New());
                        break;
                    case "ProgramsList":
                        NavigateToPage(new ProgramsList());
                        break;
                    case "Recoverys":
                        NavigateToPage(new Recoverys());
                        break;
                    case "Logs":
                        NavigateToPage(new Logs());
                        break;
                    case "ThreatIntelligence":
                        NavigateToPage(new ThreatIntelligence());
                        break;
                    case "Setting":
                        NavigateToPage(new Setting());
                        break;
                }
            }
            catch (Exception ex)
            {
                _viewModel.LogService.LogError($"사이드바 버튼 클릭 처리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 새로고침 버튼 클릭 이벤트
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () => await _viewModel.RefreshDataAsync());
        }
        #endregion

        #region Navigation
        /// <summary>
        /// 페이지 네비게이션
        /// </summary>
        private void NavigateToPage(Page page)
        {
            try
            {
                var mainWindow = Window.GetWindow(this) as MainWindows;
                mainWindow?.NavigateToPage(page);
            }
            catch (Exception ex)
            {
                _viewModel.LogService.LogError($"페이지 네비게이션 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 페이지 정리
        /// </summary>
        public void OnNavigatedFrom()
        {
            try
            {
                _viewModel?.Cleanup();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"ProgramsList 페이지 정리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 페이지 진입
        /// </summary>
        public void OnNavigatedTo()
        {
            try
            {
                _viewModel.LogService.AddLogMessage("📄 ProgramsList 페이지 진입");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"ProgramsList 페이지 진입 처리 중 오류: {ex.Message}");
            }
        }
        #endregion

        #region Legacy Support Methods (기존 XAML 바인딩 호환성)
        // 다음 메서드들은 XAML에서 참조되는 기존 이벤트 핸들러들입니다.
        // BasePageViewModel 패턴으로 리팩토링하면서 기본 구현만 유지합니다.

        private void ProgramDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("📋 프로그램 선택 변경됨");
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("🔍 필터 텍스트 변경됨");
        }

        private void SecurityLevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("🔄 보안 레벨 필터 변경됨");
        }

        private void ShowProgramDetails_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("📋 프로그램 세부정보 표시 요청됨");
        }

        private void UninstallProgram_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("🗑️ 프로그램 제거 요청됨");
        }

        private void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("🔄 업데이트 확인 요청됨");
        }

        private void ExportProgramList_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("📤 프로그램 목록 내보내기 요청됨");
        }

        private void ScanForMalware_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("🔍 악성코드 검사 요청됨");
        }

        private void ShowSecurityReport_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("📊 보안 리포트 표시 요청됨");
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.LogService.AddLogMessage("🔍 프로그램 스캔 버튼 클릭됨");
            // 실제 스캔 로직은 ViewModel에서 처리
            Task.Run(async () => await _viewModel.RefreshDataAsync());
        }
        #endregion
    }
}