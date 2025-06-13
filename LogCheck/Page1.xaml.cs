using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.Versioning;

namespace WindowsSentinel
{
    /// <summary>
    /// Page1.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Page1 : Page
    {
        public Page1()
        {
            InitializeComponent();
        }

        private void SidebarHome_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new HomePage());
        }

        private void SidebarPrograms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new InstalledPrograms());
        }

        private void SidebarModification_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Network());
        }

        private void SidebarLog_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Log());
        }

        private void SidebarRecovery_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new Recovery());
        }

        private void NavigateToPage(Page page)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToPage(page);
        }
    }
}