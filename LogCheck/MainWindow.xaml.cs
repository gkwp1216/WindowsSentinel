using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using Microsoft.Win32;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Diagnostics;

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

                                    var securityInfo = GetSecurityInfo(installLocation);
                                    programList.Add(new ProgramInfo
                                    {
                                        Name = name,
                                        InstallDate = installDate.ToString("yyyy-MM-dd"),
                                        Period = period,
                                        Version = version ?? "알 수 없음",
                                        Publisher = publisher ?? "알 수 없음",
                                        InstallLocation = installLocation ?? "알 수 없음",
                                        SecurityLevel = securityInfo.SecurityLevel,
                                        SecurityDetails = securityInfo.Details,
                                        HasSecurityChanges = securityInfo.HasSecurityChanges
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

        private SecurityInfo GetSecurityInfo(string installLocation)
        {
            var securityInfo = new SecurityInfo();
            
            if (string.IsNullOrEmpty(installLocation) || installLocation == "알 수 없음")
            {
                securityInfo.SecurityLevel = "알 수 없음";
                securityInfo.Details = "설치 위치를 찾을 수 없음";
                return securityInfo;
            }

            try
            {
                // 방화벽 규칙 확인
                using (RegistryKey firewallKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallRules"))
                {
                    if (firewallKey != null)
                    {
                        var rules = firewallKey.GetValueNames()
                            .Where(name => name.Contains(installLocation))
                            .ToList();
                        
                        if (rules.Any())
                        {
                            securityInfo.HasSecurityChanges = true;
                            securityInfo.Details += "방화벽 규칙이 추가됨\n";
                        }
                    }
                }

                // Windows Defender 예외 확인
                using (RegistryKey defenderKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Exclusions\Paths"))
                {
                    if (defenderKey != null)
                    {
                        var exclusions = defenderKey.GetValueNames()
                            .Where(name => name.Contains(installLocation))
                            .ToList();
                        
                        if (exclusions.Any())
                        {
                            securityInfo.HasSecurityChanges = true;
                            securityInfo.Details += "Windows Defender 예외로 등록됨\n";
                        }
                    }
                }

                // 실행 파일의 디지털 서명 확인
                var exeFiles = Directory.GetFiles(installLocation, "*.exe", SearchOption.AllDirectories);
                foreach (var exeFile in exeFiles)
                {
                    try
                    {
                        var cert = X509Certificate.CreateFromSignedFile(exeFile);
                        if (cert != null)
                        {
                            securityInfo.Details += $"디지털 서명 확인됨: {Path.GetFileName(exeFile)}\n";
                        }
                    }
                    catch
                    {
                        securityInfo.Details += $"디지털 서명 없음: {Path.GetFileName(exeFile)}\n";
                    }
                }

                // 보안 수준 결정
                if (securityInfo.HasSecurityChanges)
                {
                    securityInfo.SecurityLevel = "높음";
                }
                else if (securityInfo.Details.Contains("디지털 서명"))
                {
                    securityInfo.SecurityLevel = "중간";
                }
                else
                {
                    securityInfo.SecurityLevel = "낮음";
                }
            }
            catch (Exception ex)
            {
                securityInfo.SecurityLevel = "오류";
                securityInfo.Details = $"보안 정보 수집 중 오류 발생: {ex.Message}";
            }

            return securityInfo;
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
                    return true;
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
            public string SecurityLevel { get; set; }
            public string SecurityDetails { get; set; }
            public bool HasSecurityChanges { get; set; }
        }

        private class SecurityInfo
        {
            public string SecurityLevel { get; set; }
            public string Details { get; set; }
            public bool HasSecurityChanges { get; set; }
        }
    }
}
