using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using Microsoft.Win32;

namespace WindowsSentinel
{
    public partial class MainWindow : Window
    {
        private List<ProgramInfo> programList;
        private HashSet<string> processedPrograms;

        public MainWindow()
        {
            InitializeComponent();
            InitializeRadioButtons();
        }

        private void InitializeRadioButtons()
        {
            rb1Day.Checked += RadioButton_Checked;
            rb7Days.Checked += RadioButton_Checked;
            rb30Days.Checked += RadioButton_Checked;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (programList != null && programList.Any())
            {
                DisplayFilteredPrograms();
            }
        }

        private void btnCollectPrograms_Click(object sender, RoutedEventArgs e)
        {
            CollectInstalledPrograms();
            DisplayFilteredPrograms();
        }

        private void CollectInstalledPrograms()
        {
            DateTime today = DateTime.Today;
            DateTime day1 = today.AddDays(-1);
            DateTime day7 = today.AddDays(-7);
            DateTime day30 = today.AddDays(-30);

            programList = new List<ProgramInfo>();
            processedPrograms = new HashSet<string>();

            string[] registryPaths = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            int totalChecked = 0;
            int duplicateCount = 0;

            foreach (string regPath in registryPaths)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(regPath))
                {
                    if (key == null) continue;

                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        totalChecked++;
                        using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                        {
                            string name = subKey?.GetValue("DisplayName") as string;
                            string installDateRaw = subKey?.GetValue("InstallDate") as string;
                            string version = subKey?.GetValue("DisplayVersion") as string;
                            string publisher = subKey?.GetValue("Publisher") as string;
                            string installLocation = subKey?.GetValue("InstallLocation") as string;

                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(installDateRaw))
                            {
                                string programKey = $"{name}_{version}";
                                if (processedPrograms.Contains(programKey))
                                {
                                    duplicateCount++;
                                    continue;
                                }
                                processedPrograms.Add(programKey);

                                if (DateTime.TryParseExact(installDateRaw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime installDate))
                                {
                                    string period = "";
                                    if (installDate >= day1) period = "1일 이내";
                                    else if (installDate >= day7) period = "7일 이내";
                                    else if (installDate >= day30) period = "30일 이내";
                                    else continue;

                                    programList.Add(new ProgramInfo
                                    {
                                        Name = name,
                                        InstallDate = installDate.ToString("yyyy-MM-dd"),
                                        Period = period,
                                        Version = version ?? "알 수 없음",
                                        Publisher = publisher ?? "알 수 없음",
                                        InstallLocation = installLocation ?? "알 수 없음"
                                    });
                                }
                            }
                        }
                    }
                }
            }

            MessageBox.Show($"총 {totalChecked}개의 레지스트리 키를 검사했습니다.\n" +
                          $"중복 제거된 프로그램 수: {duplicateCount}개\n" +
                          $"총 {programList.Count}개의 고유한 프로그램이 발견되었습니다.",
                          "검사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DisplayFilteredPrograms()
        {
            string selectedPeriod = "";
            if (rb1Day.IsChecked == true) selectedPeriod = "1일 이내";
            else if (rb7Days.IsChecked == true) selectedPeriod = "7일 이내";
            else if (rb30Days.IsChecked == true) selectedPeriod = "30일 이내";

            var filteredPrograms = programList.Where(p => 
            {
                if (selectedPeriod == "30일 이내")
                    return true; // 30일 이내 선택 시 모든 프로그램 표시
                return p.Period == selectedPeriod;
            })
            .OrderBy(p => p.InstallDate)
            .ToList();

            programDataGrid.ItemsSource = filteredPrograms;
            Title = $"Windows Sentinel - {selectedPeriod} 설치된 프로그램 ({filteredPrograms.Count}개)";
        }

        private class ProgramInfo
        {
            public string Name { get; set; }
            public string InstallDate { get; set; }
            public string Period { get; set; }
            public string Version { get; set; }
            public string Publisher { get; set; }
            public string InstallLocation { get; set; }
        }
    }
}
