using LogCheck.Properties;
using SharpPcap;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using static LogCheck.App;

namespace LogCheck
{
    /// <summary>
    /// Setting.xaml에 대한 상호 작용 논리
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class Setting : Page
    {
        public Setting()
        {
            InitializeComponent();

            this.Loaded += Setting_Loaded;
        }

        private void Setting_Loaded(object sender, RoutedEventArgs e)
        {
            // 현재 테마에 따라 라디오 버튼만 업데이트 (이벤트는 실행 안 되게)
            if (ThemeManager.CurrentTheme == "Light")
            {
                LightModeRadioButton.Checked -= SwitchToLightMode_Checked;
                LightModeRadioButton.IsChecked = true;
                LightModeRadioButton.Checked += SwitchToLightMode_Checked;
            }
            else if (ThemeManager.CurrentTheme == "Dark")
            {
                DarkModeRadioButton.Checked -= SwitchToDarkMode_Checked;
                DarkModeRadioButton.IsChecked = true;
                DarkModeRadioButton.Checked += SwitchToDarkMode_Checked;
            }

            // 자동 시작 체크박스 초기화 (FindName로 안전 조회)
            if (FindName("AutoStartCheckBox") is System.Windows.Controls.CheckBox cb)
            {
                cb.Checked -= AutoStartCheckBox_Changed;
                cb.Unchecked -= AutoStartCheckBox_Changed;
                cb.IsChecked = Settings.Default.AutoStartMonitoring;
                cb.Checked += AutoStartCheckBox_Changed;
                cb.Unchecked += AutoStartCheckBox_Changed;
            }

            // NIC / BPF 초기화
            InitializeNicAndBpf();
        }

        private void SwitchToLightMode_Checked(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Light");
        }

        private void SwitchToDarkMode_Checked(object sender, RoutedEventArgs e)
        {
            ApplyTheme("Dark");
        }

        // 테마를 변경하는 메서드
        private void ApplyTheme(string themeName)
        {
            // 테마 적용
            System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
            var theme = new ResourceDictionary
            {
                Source = new Uri($"/{themeName}Theme.xaml", UriKind.Relative)
            };
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(theme);

            ThemeManager.CurrentTheme = themeName;
        }

        private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var isChecked = (sender as System.Windows.Controls.CheckBox)?.IsChecked == true;
            Settings.Default.AutoStartMonitoring = isChecked;
            try
            {
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings save failed: {ex.Message}");
            }
        }

        private void InitializeNicAndBpf()
        {
            // AutoSelect NIC
            if (FindName("AutoSelectNicCheckBox") is System.Windows.Controls.CheckBox autoCb)
            {
                autoCb.Checked -= AutoSelectNicCheckBox_Changed;
                autoCb.Unchecked -= AutoSelectNicCheckBox_Changed;
                autoCb.IsChecked = Settings.Default.AutoSelectNic;
                autoCb.Checked += AutoSelectNicCheckBox_Changed;
                autoCb.Unchecked += AutoSelectNicCheckBox_Changed;
            }

            // NIC 목록 채우기
            if (FindName("NicComboBox") is System.Windows.Controls.ComboBox nicCombo)
            {
                nicCombo.SelectionChanged -= NicComboBox_SelectionChanged;
                nicCombo.Items.Clear();
                foreach (var dev in CaptureDeviceList.Instance)
                {
                    var id = dev.Name;
                    var friendly = GetFriendlyName(dev);
                    nicCombo.Items.Add(new ComboBoxItem { Content = friendly, Tag = id });
                }

                // 선택 고정
                var selectedId = Settings.Default.SelectedNicId;
                if (!string.IsNullOrWhiteSpace(selectedId))
                {
                    foreach (ComboBoxItem item in nicCombo.Items)
                    {
                        if (string.Equals(item.Tag as string, selectedId, StringComparison.OrdinalIgnoreCase))
                        {
                            nicCombo.SelectedItem = item;
                            break;
                        }
                    }
                }

                nicCombo.IsEnabled = Settings.Default.AutoSelectNic == false;
                nicCombo.SelectionChanged += NicComboBox_SelectionChanged;
            }

            // BPF 텍스트 박스
            if (FindName("BpfTextBox") is System.Windows.Controls.TextBox bpfBox)
            {
                bpfBox.Text = string.IsNullOrWhiteSpace(Settings.Default.BpfFilter) ? "tcp or udp or icmp" : Settings.Default.BpfFilter;
            }
        }

        private static string GetFriendlyName(ICaptureDevice dev)
        {
            try
            {
                var desc = dev.Description;
                // 공통적으로 제공되는 Description을 우선 사용, 없으면 Name
                return string.IsNullOrWhiteSpace(desc) ? dev.Name : desc;
            }
            catch { return dev.Name; }
        }

        private void AutoSelectNicCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var auto = (sender as System.Windows.Controls.CheckBox)?.IsChecked == true;
            Settings.Default.AutoSelectNic = auto;
            if (FindName("NicComboBox") is System.Windows.Controls.ComboBox nicCombo)
            {
                nicCombo.IsEnabled = !auto;
            }
            try { Settings.Default.Save(); } catch { }
        }

        private void NicComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox nicCombo && nicCombo.SelectedItem is ComboBoxItem item)
            {
                Settings.Default.SelectedNicId = item.Tag as string ?? string.Empty;
                try { Settings.Default.Save(); } catch { }
            }
        }

        private void ValidateAndSaveBpf_Click(object sender, RoutedEventArgs e)
        {
            if (FindName("BpfTextBox") is not System.Windows.Controls.TextBox bpfBox)
                return;

            var bpf = string.IsNullOrWhiteSpace(bpfBox.Text) ? "tcp or udp or icmp" : bpfBox.Text.Trim();

            // 가능한 경우 하나의 디바이스를 열어 필터 적용 검증(실패해도 앱은 유지)
            try
            {
                ICaptureDevice? dev = null;
                if (!Settings.Default.AutoSelectNic && !string.IsNullOrWhiteSpace(Settings.Default.SelectedNicId))
                {
                    dev = CaptureDeviceList.Instance.FirstOrDefault(d => string.Equals(d.Name, Settings.Default.SelectedNicId, StringComparison.OrdinalIgnoreCase));
                }
                dev ??= CaptureDeviceList.Instance.FirstOrDefault();

                if (dev != null)
                {
                    dev.Open();
                    try { dev.Filter = bpf; }
                    finally { dev.Close(); }
                }

                Settings.Default.BpfFilter = bpf;
                try { Settings.Default.Save(); } catch { }
                System.Windows.MessageBox.Show("BPF 필터가 저장되었습니다.", "저장", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"BPF 검증 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackToMain_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Window.GetWindow(this) is MainWindows mw)
                {
                    mw.NavigateToPage(new NetWorks_New());
                }
                else
                {
                    // Frame으로 탐색 시도
                    var parent = this.Parent;
                    while (parent != null && parent is not System.Windows.Controls.Frame)
                    {
                        parent = (parent as FrameworkElement)?.Parent;
                    }
                    if (parent is System.Windows.Controls.Frame frame)
                    {
                        frame.Navigate(new NetWorks_New());
                    }
                    else
                    {
                        // 마지막 대안: 새 창으로 메인 열기
                        var win = new MainWindows();
                        win.Show();
                        // 현재 창 닫기 가능 시 닫기
                        Window.GetWindow(this)?.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Back navigation failed: {ex.Message}");
            }
        }
    }
}
