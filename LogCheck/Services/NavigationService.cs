using System;
using System.Runtime.Versioning;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LogCheck.Models;

namespace LogCheck.Services
{
    /// <summary>
    /// 네비게이션 서비스 인터페이스
    /// </summary>
    public interface INavigationService
    {
        void NavigateToPage(Page page);
        void NavigateToPage<T>() where T : Page, new();
        void NavigateToPage(string pageName);
        event EventHandler<NavigationEventArgs>? NavigationOccurred;
    }

    /// <summary>
    /// 네비게이션 이벤트 데이터
    /// </summary>
    public class NavigationEventArgs : EventArgs
    {
        public Page? FromPage { get; }
        public Page ToPage { get; }
        public string? PageName { get; }

        public NavigationEventArgs(Page toPage, Page? fromPage = null, string? pageName = null)
        {
            ToPage = toPage;
            FromPage = fromPage;
            PageName = pageName ?? toPage.GetType().Name;
        }
    }

    /// <summary>
    /// 사이드바 네비게이션 서비스
    /// 공통 사이드바 네비게이션 로직 통합
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class SidebarNavigationService : INavigationService
    {
        private readonly INavigationService _parentNavigationService;
        private ToggleButton? _selectedButton;

        public event EventHandler<NavigationEventArgs>? NavigationOccurred;

        public SidebarNavigationService(INavigationService parentNavigationService)
        {
            _parentNavigationService = parentNavigationService ?? throw new ArgumentNullException(nameof(parentNavigationService));
        }

        /// <summary>
        /// 사이드바 버튼 클릭 이벤트 처리
        /// </summary>
        /// <param name="clickedButton">클릭된 버튼</param>
        public void HandleSidebarButtonClick(ToggleButton clickedButton)
        {
            if (clickedButton == null) return;

            try
            {
                // 이전 선택 해제
                if (_selectedButton != null && _selectedButton != clickedButton)
                    _selectedButton.IsChecked = false;

                // 선택 상태 유지
                clickedButton.IsChecked = true;
                _selectedButton = clickedButton;

                // 커맨드 파라미터에 따라 페이지 네비게이션
                var pageCommand = clickedButton.CommandParameter?.ToString();
                if (!string.IsNullOrEmpty(pageCommand))
                {
                    NavigateToPage(pageCommand);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"사이드바 네비게이션 오류: {ex.Message}");
            }
        }

        public void NavigateToPage(Page page)
        {
            _parentNavigationService.NavigateToPage(page);
            NavigationOccurred?.Invoke(this, new NavigationEventArgs(page));
        }

        public void NavigateToPage<T>() where T : Page, new()
        {
            var page = new T();
            NavigateToPage(page);
        }

        public void NavigateToPage(string pageName)
        {
            Page? page = pageName switch
            {
                "Vaccine" => new Vaccine(),
                "NetWorks_New" => new NetWorks_New(),
                "ProgramsList" => new ProgramsList(),
                "Recoverys" => new Recoverys(),
                "Logs" => new Logs(),
                "ThreatIntelligence" => new ThreatIntelligence(),
                "Setting" => new Setting(),
                _ => null
            };

            if (page != null)
            {
                NavigateToPage(page);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"알 수 없는 페이지 이름: {pageName}");
            }
        }
    }

    /// <summary>
    /// 페이지 생명주기 관리자
    /// INavigable 인터페이스 활용한 생명주기 관리
    /// </summary>
    public class PageLifecycleManager
    {
        /// <summary>
        /// 페이지 진입 시 호출
        /// </summary>
        /// <param name="page">페이지</param>
        public static void OnNavigatedTo(Page page)
        {
            try
            {
                if (page is LogCheck.Models.INavigable navigablePage)
                {
                    navigablePage.OnNavigatedTo();
                }

                System.Diagnostics.Debug.WriteLine($"페이지 진입: {page.GetType().Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"페이지 진입 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 페이지 이탈 시 호출
        /// </summary>
        /// <param name="page">페이지</param>
        public static void OnNavigatedFrom(Page page)
        {
            try
            {
                if (page is LogCheck.Models.INavigable navigablePage)
                {
                    navigablePage.OnNavigatedFrom();
                }

                System.Diagnostics.Debug.WriteLine($"페이지 이탈: {page.GetType().Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"페이지 이탈 처리 오류: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 메인 네비게이션 서비스 (MainWindows용)
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class MainNavigationService : INavigationService
    {
        private readonly Frame _mainFrame;
        private Page? _currentPage;

        public event EventHandler<NavigationEventArgs>? NavigationOccurred;

        public MainNavigationService(Frame mainFrame)
        {
            _mainFrame = mainFrame ?? throw new ArgumentNullException(nameof(mainFrame));
        }

        public void NavigateToPage(Page page)
        {
            if (page == null) return;

            try
            {
                // 이전 페이지 정리
                if (_currentPage != null)
                {
                    PageLifecycleManager.OnNavigatedFrom(_currentPage);
                }

                // 새 페이지로 네비게이션
                _mainFrame.Navigate(page);

                var previousPage = _currentPage;
                _currentPage = page;

                // 새 페이지 초기화
                PageLifecycleManager.OnNavigatedTo(page);

                // 네비게이션 이벤트 발생
                NavigationOccurred?.Invoke(this, new NavigationEventArgs(page, previousPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"페이지 네비게이션 오류: {ex.Message}");
            }
        }

        public void NavigateToPage<T>() where T : Page, new()
        {
            var page = new T();
            NavigateToPage(page);
        }

        public void NavigateToPage(string pageName)
        {
            Page? page = pageName switch
            {
                "Vaccine" => new Vaccine(),
                "NetWorks_New" => new NetWorks_New(),
                "ProgramsList" => new ProgramsList(),
                "Recoverys" => new Recoverys(),
                "Logs" => new Logs(),
                "ThreatIntelligence" => new ThreatIntelligence(),
                "Setting" => new Setting(),
                _ => null
            };

            if (page != null)
            {
                NavigateToPage(page);
            }
        }

        /// <summary>
        /// 현재 페이지 반환
        /// </summary>
        public Page? CurrentPage => _currentPage;
    }
}