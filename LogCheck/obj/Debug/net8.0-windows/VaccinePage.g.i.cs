﻿#pragma checksum "..\..\..\VaccinePage.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "0A07B797592EEBD5A2A2C613E50A700D5B947C38"
//------------------------------------------------------------------------------
// <auto-generated>
//     이 코드는 도구를 사용하여 생성되었습니다.
//     런타임 버전:4.0.30319.42000
//
//     파일 내용을 변경하면 잘못된 동작이 발생할 수 있으며, 코드를 다시 생성하면
//     이러한 변경 내용이 손실됩니다.
// </auto-generated>
//------------------------------------------------------------------------------

using LogCheck;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;


namespace LogCheck {
    
    
    /// <summary>
    /// VaccinePage
    /// </summary>
    public partial class VaccinePage : System.Windows.Controls.Page, System.Windows.Markup.IComponentConnector {
        
        
        #line 21 "..\..\..\VaccinePage.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ProgressBar scanProgressBar;
        
        #line default
        #line hidden
        
        
        #line 26 "..\..\..\VaccinePage.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBox pathTextBox;
        
        #line default
        #line hidden
        
        
        #line 27 "..\..\..\VaccinePage.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button browseButton;
        
        #line default
        #line hidden
        
        
        #line 28 "..\..\..\VaccinePage.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button scanButton;
        
        #line default
        #line hidden
        
        
        #line 32 "..\..\..\VaccinePage.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.DataGrid resultDataGrid;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.6.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/Logcheck;component/vaccinepage.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\VaccinePage.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.6.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.scanProgressBar = ((System.Windows.Controls.ProgressBar)(target));
            return;
            case 2:
            this.pathTextBox = ((System.Windows.Controls.TextBox)(target));
            return;
            case 3:
            this.browseButton = ((System.Windows.Controls.Button)(target));
            
            #line 27 "..\..\..\VaccinePage.xaml"
            this.browseButton.Click += new System.Windows.RoutedEventHandler(this.BrowseButton_Click);
            
            #line default
            #line hidden
            return;
            case 4:
            this.scanButton = ((System.Windows.Controls.Button)(target));
            
            #line 28 "..\..\..\VaccinePage.xaml"
            this.scanButton.Click += new System.Windows.RoutedEventHandler(this.ScanButton_Click);
            
            #line default
            #line hidden
            return;
            case 5:
            this.resultDataGrid = ((System.Windows.Controls.DataGrid)(target));
            return;
            }
            this._contentLoaded = true;
        }
    }
}

