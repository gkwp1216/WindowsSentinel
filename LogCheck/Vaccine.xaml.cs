using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LogCheck
{
    [SupportedOSPlatform("windows")]
    public partial class Vaccine : Page
    {
        private ObservableCollection<ProgramScanResult> _results = new();
        private DispatcherTimer? loadingTextTimer;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";
        private readonly Services.ToastNotificationService _toastService;

        public Vaccine()
        {
            InitializeComponent();

            _toastService = Services.ToastNotificationService.Instance;
            resultDataGrid.ItemsSource = _results;

            // 로딩 애니메이션 초기화
            SpinnerItems.ItemsSource = CreateSpinnerPoints(40, 50, 50);
            StartRotation();
            SetupLoadingTextAnimation();
        }

        private List<System.Windows.Point> CreateSpinnerPoints(double radius, double centerX, double centerY)
        {
            var points = new List<System.Windows.Point>();
            for (int i = 0; i < 8; i++)
            {
                double angle = i * 360.0 / 8 * Math.PI / 180.0;
                double x = centerX + radius * Math.Cos(angle) - 5;
                double y = centerY + radius * Math.Sin(angle) - 5;
                points.Add(new System.Windows.Point(x, y));
            }
            return points;
        }

        private void StartRotation()
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

        private void SetupLoadingTextAnimation()
        {
            loadingTextTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            loadingTextTimer.Tick += LoadingTextTimer_Tick;
        }

        private void LoadingTextTimer_Tick(object? sender, EventArgs e)
        {
            dotCount = (dotCount + 1) % (maxDots + 1);
            LoadingText.Text = baseText + new string('.', dotCount);
        }

        private void ShowLoadingOverlay()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            loadingTextTimer?.Start();
        }

        private void HideLoadingOverlay()
        {
            loadingTextTimer?.Stop();
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingText.Text = baseText;
        }

        private async void FullScanButton_Click(object sender, RoutedEventArgs e)
        {
            _results.Clear();

            ShowLoadingOverlay();
            fullScanButton.IsEnabled = false;

            // 🔥 Toast 알림: 스캔 시작
            _ = Task.Run(async () =>
            {
                await _toastService.ShowInfoAsync(
                    "🔍 시스템 스캔 시작",
                    "설치된 프로그램을 검사하고 있습니다...");
            });

            try
            {
                var list = await Task.Run(() => ScanInstalledPrograms());

                foreach (var item in list)
                {
                    _results.Add(item);
                }

                var maliciousCount = list.Count(r => r.Verdict == "Malicious");
                var suspiciousCount = list.Count(r => r.Verdict == "Suspicious");
                var cleanCount = list.Count(r => r.Verdict == "Clean");

                // 🔥 Toast 알림: 스캔 완료 결과
                _ = Task.Run(async () =>
                {
                    if (maliciousCount > 0)
                    {
                        await _toastService.ShowWarningAsync(
                            "⚠️ 악성 프로그램 탐지",
                            $"악성: {maliciousCount}개, 의심: {suspiciousCount}개, 정상: {cleanCount}개");
                    }
                    else if (suspiciousCount > 0)
                    {
                        await _toastService.ShowWarningAsync(
                            "🔍 의심스러운 프로그램 발견",
                            $"의심: {suspiciousCount}개, 정상: {cleanCount}개");
                    }
                    else
                    {
                        await _toastService.ShowSuccessAsync(
                            "✅ 시스템 깨끗함",
                            $"총 {cleanCount}개 프로그램을 검사했으며 위협이 발견되지 않았습니다.");
                    }
                });
            }
            catch (Exception ex)
            {
                // 🔥 Toast 알림: 스캔 오류
                _ = Task.Run(async () =>
                {
                    await _toastService.ShowErrorAsync(
                        "❌ 스캔 오류",
                        $"시스템 검사 중 오류가 발생했습니다: {ex.Message}");
                });
            }
            finally
            {
                HideLoadingOverlay();
                fullScanButton.IsEnabled = true;
            }
        }

        private async void FileScanButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "검사할 파일 선택",
                Multiselect = false,
                Filter = "모든 파일 (*.*)|*.*"
            };

            if (ofd.ShowDialog() != true) return;

            string path = ofd.FileName;

            ShowLoadingOverlay();

            // 🔥 Toast 알림: 파일 스캔 시작
            _ = Task.Run(async () =>
            {
                await _toastService.ShowInfoAsync(
                    "🔍 파일 스캔 시작",
                    $"파일 '{Path.GetFileName(path)}'을 검사하고 있습니다...");
            });

            try
            {
                string sha256 = await Task.Run(() => ComputeSha256(path));
                string verdict = await LogCheck.Services.MalwareBazaarClient.GetVerdictAsync(sha256);

                _results.Add(new ProgramScanResult
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    InstallDate = File.GetCreationTime(path),
                    Publisher = "",
                    InstallPath = path,
                    Verdict = verdict
                });

                // 🔥 Toast 알림: 파일 스캔 결과
                _ = Task.Run(async () =>
                {
                    switch (verdict.ToLower())
                    {
                        case "malicious":
                            await _toastService.ShowWarningAsync(
                                "⚠️ 악성 파일 탐지",
                                $"파일 '{Path.GetFileName(path)}'에서 악성 코드가 발견되었습니다!");
                            break;
                        case "suspicious":
                            await _toastService.ShowWarningAsync(
                                "🔍 의심스러운 파일",
                                $"파일 '{Path.GetFileName(path)}'이 의심스러운 것으로 판단됩니다.");
                            break;
                        default:
                            await _toastService.ShowSuccessAsync(
                                "✅ 깨끗한 파일",
                                $"파일 '{Path.GetFileName(path)}'은 안전한 것으로 확인되었습니다.");
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                // 🔥 Toast 알림: 파일 스캔 오류
                _ = Task.Run(async () =>
                {
                    await _toastService.ShowErrorAsync(
                        "❌ 파일 스캔 오류",
                        $"파일 검사 중 오류가 발생했습니다: {ex.Message}");
                });
            }
            finally
            {
                HideLoadingOverlay();
            }
        }

        private static IEnumerable<ProgramScanResult> ScanInstalledPrograms()
        {
            var results = new List<ProgramScanResult>();

            string[] registryPaths = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string regPath in registryPaths)
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(regPath);
                if (key == null) continue;

                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    using RegistryKey? subkey = key.OpenSubKey(subkeyName);
                    if (subkey == null) continue;

                    string? displayName = subkey.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrEmpty(displayName)) continue;

                    string installLocation = subkey.GetValue("InstallLocation")?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(installLocation) || !Directory.Exists(installLocation))
                        continue;

                    string? exePath = GetRepresentativeExecutable(installLocation, displayName);
                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) continue;

                    string sha256 = ComputeSha256(exePath);
                    string verdict = LogCheck.Services.MalwareBazaarClient.GetVerdictAsync(sha256).GetAwaiter().GetResult();

                    DateTime installDate = File.GetCreationTime(exePath);

                    results.Add(new ProgramScanResult
                    {
                        Name = displayName,
                        InstallDate = installDate,
                        Publisher = subkey.GetValue("Publisher")?.ToString() ?? string.Empty,
                        InstallPath = installLocation,
                        Verdict = verdict
                    });
                }
            }

            return results;
        }

        private static string? GetRepresentativeExecutable(string installPath, string programName)
        {
            try
            {
                var exeFiles = Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly);
                if (exeFiles.Length == 0) return null;

                var match = exeFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(programName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match)) return match;

                return exeFiles.OrderByDescending(f => new FileInfo(f).Length).First();
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            var sb = new StringBuilder();
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private class ProgramScanResult
        {
            public string Name { get; set; } = string.Empty;
            public DateTime? InstallDate { get; set; }
            public string Publisher { get; set; } = string.Empty;
            public string InstallPath { get; set; } = string.Empty;
            public string Verdict { get; set; } = "Unknown";
        }

        #region Sidebar Navigation




        [SupportedOSPlatform("windows")]
        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindows;
            mainWindow?.NavigateToPage(page);
        }
        #endregion
    }
}