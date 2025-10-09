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
    public partial class Vaccine : Page
    {
        private ObservableCollection<ProgramScanResult> _results = new();
        private DispatcherTimer? loadingTextTimer;
        private int dotCount = 0;
        private const int maxDots = 3;
        private string baseText = "검사 중";

        public Vaccine()
        {
            InitializeComponent();

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

            try
            {
                var list = await Task.Run(() => ScanInstalledPrograms());

                foreach (var item in list)
                {
                    _results.Add(item);
                }

                if (!list.Any(r => r.Verdict == "Malicious"))
                {
                    System.Windows.MessageBox.Show("악성 프로그램이 발견되지 않았습니다.", "결과", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"검사 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"검사 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath);
                if (key == null) continue;

                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    using RegistryKey subkey = key.OpenSubKey(subkeyName);
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