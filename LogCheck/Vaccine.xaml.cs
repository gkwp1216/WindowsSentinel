using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LogCheck
{
    public partial class Vaccine : Page
    {
        private ObservableCollection<ScanResult> _results = new();

        public Vaccine()
        {
            InitializeComponent();
            resultDataGrid.ItemsSource = _results;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "검사할 파일 선택",
                Multiselect = false,
                Filter = "모든 파일 (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                pathTextBox.Text = ofd.FileName;
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            var path = pathTextBox.Text;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                System.Windows.MessageBox.Show("유효한 파일을 선택해주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            scanProgressBar.Visibility = Visibility.Visible;
            scanProgressBar.IsIndeterminate = true;
            scanButton.IsEnabled = false;

            try
            {
                var sha256 = await Task.Run(() => ComputeSha256(path));

                // TODO: MalwareBazaar API 연동 – 현재는 Unknown 처리
                var verdict = "Unknown";

                _results.Add(new ScanResult
                {
                    Path = path,
                    Sha256 = sha256,
                    Verdict = verdict
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"검사 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                scanProgressBar.IsIndeterminate = false;
                scanProgressBar.Visibility = Visibility.Collapsed;
                scanButton.IsEnabled = true;
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

        private class ScanResult
        {
            public string Path { get; set; } = string.Empty;
            public string Sha256 { get; set; } = string.Empty;
            public string Verdict { get; set; } = string.Empty;
        }
    }
} 