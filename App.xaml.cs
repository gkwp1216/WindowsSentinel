using System;
using System.Windows;

namespace LogCheck
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 관리자 권한 확인
            if (!IsRunningAsAdmin())
            {
                MessageBox.Show(
                    "이 프로그램은 관리자 권한으로 실행해야 합니다.",
                    "권한 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // 전역 예외 처리
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show(
                    $"치명적인 오류가 발생했습니다: {ex?.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"예기치 않은 오류가 발생했습니다: {args.Exception.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };
        }

        private bool IsRunningAsAdmin()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}