   M S B A M L   `   `   `  ����[  WLiveChartsCore.SkiaSharpView.WPF, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null}Xclr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF LiveChartsCore.SkiaSharpView.WPF  5      C ?Logcheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null#clr-namespace:LogCheckLogCheck 5         LogCheck.NetWorks   5      W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      5      R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      _ [MaterialDesignThemes.Wpf, Version=4.9.0.0, Culture=neutral, PublicKeyToken=df2a72020bd7962aFmaterialDesign1http://materialdesigninxaml.net/winfx/xaml/themes  5      blvcXclr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF  5      "localclr-namespace:LogCheck  5        N� Title$  NetWorks��5         BackgroundColor N� 
Background# �   6    � 	Resources 5        SpinnerRotate �� CenterX �� CenterY   LogCheck.FileSizeConverter  FileSizeConverter��%Q   &	       &	 *     �� 5   
   $ 25Q�60   $ 25Q�6=     5   
   6"   5      .��5      �  � ColumnDefinitions 5   
   �� 5      $	��220 �6   �� 5      $��* �6   .+�5   
   �� $��0��6     ButtonBackgroundColor#���  6!    �� CornerRadius$ 12��6V   $��
20,0,10,20q�6h   ���#C2C2C2$��1q�6�   ._�5      �� $��10q�6   .+�5      �� -   5          SidebarButtonStyle#���  5      
 � Tag$	 ��5      $ ��악성 프로그램 탐지��5       � Cursor$
 Hand��5      5       �� 5"      -   5        #���  5"      $	 ��5#      $ ��네트워크 접속 내역��5$      $
 Hand��5%      5&      �� 5(      -   5        #���  5(      $	 ��5)      $��설치된 프로그램��5*      $
 Hand��5+      5,      �� 5.      -   5        #���  5.      $	 ��5/      $ ��보안 프로그램 복구��50      $
 Hand��51      52      �� 54      -   5        #���  54      $	 ��55      $��보안 로그��56      $
 Hand��57      58      59      5:      � 5=   
   $��1��6   $��0q�6   .+�5>      �� -   5        	 ��Name$	 MainTabControl��5>      
������ $��0q�5@      $��10,0,0,0q�5A      .@�5C      �� -   5        
 ��Name$
 RealtimeTab��5C   ,   $��실시간 모니터링��6   .��5D      � $��0q�6    � RowDefinitions 5E      �� 5F      $
��Auto �6,   �� 5G      $
��Auto �6,   �� 5H      $��* �6,   .+�5L      �� $��0��6!   #���  6.   $ 12��6c    �� Padding$ 20q�6u   $��	0,0,20,10q�6�   ���#C2C2C2$��1q�6�   ._�5M      �  5N   "   �� 5O   &   $��* �67   �� 5P   &   $
��Auto �67   .+�5S   "   �� $��0��6-    �� Orientation$ 
Horizontal=�6=   $��Center=�6V   .+�5T   &   �� $&�� 네트워크 패킷 모니터링��60     ForegroundColor#���  5U   $   $��20(�5V   $   $��SemiBold"�5W   $   $��Center=�5X   $   �� 5Y   &   $��25��6-   $��25��68   
������ $
 Hand��5[   !   $��10,0,0,0q�5\   !   ._�5]   *   '  !MaterialDesignThemes.Wpf.PackIcon     Kind$ HelpCircleOutline=�6B   $��25��5^   6   $��25��6A     HelpMarkColor#���  5_   6   $��Center=�5`   6   $��Center=�5a   6    � ToolTip 5b   *   _� 5c   .   $J��D실시간 네트워크 패킷을 모니터링하고 분석합니다.��66    _� 	Placement$ 
MousePoint=�5d   *    _� HorizontalOffset$	 350��5e   *    _� VerticalOffset$ 15��6A   5g   '   5h   #   �� 5j   "   $��1��6-   $ 
Horizontal=�6=   $��Center=�6V   .+�5k   &   �� -   5         ��Name$ NetworkInterfaceComboBox��5k   /   $	��180��5l   #   $��0,0,10,0q�5m   #   )  #MaterialDesignThemes.Wpf.HintAssist   Hint$) #네트워크 인터페이스 선택��5n   #   �� 5p   &   -	   5         ��Name$ StartCaptureButton��5p   -   $��캡처 시작��5q   !     CheckButtonStyle#���  5r   !   $��0q�5t   !   $	��200��5u   !   $��30��6-   $
 Hand��5v   !   ��5w   *   �� 5x   .    �� 
TargetType" ��6>   .R�5y   2   �� #��z��69   #��z��5z   -   #��z��5{   -   $ 12��5|   -   ._�5}   6   �� $��Center=�6G   $��Center=�5~   ;   5   3   5�   /   5�   '   �� 5�   &   -
   5        $ StopCaptureButton��5�   -   $��캡처 중지��5�   !   #���  5�   !   $��0q�5�   !   $
 Hand��5�   !   $	��200��5�   !   $��30��6-   $y�	Collapsed=�5�   !   ��5�   *   �� 5�   .   " ��6>   .R�5�   2   �� #��z��69   #��z��5�   9   #��z��5�   9   $ 12��5�   9   ._�5�   6   �� $��Center=�6G   $��Center=�5�   G   5�   3   5�   /   5�   '   �� 5�   &   -   5        $ ClearButton��5�   -   $��목록 지우기��5�   !   #���  5�   !   $��10,0,0,0q�5�   !   $	��200��5�   !   $��30��6-   $��0q�5�   !   $
 Hand��5�   !   ��5�   *   �� 5�   .   " ��6>   .R�5�   2   �� #��z��69   #��z��5�   9   #��z��5�   9   $ 12��5�   9   ._�5�   6   �� $��Center=�6G   $��Center=�5�   ;   5�   3   5�   /   5�   '   5�   #   5�      5�      �� 5�      $��1��6!   #���  6.   $ 12��6c   $ 20q�6u   $��
0,10,20,10q�6�   ���#C2C2C2$��1q�6�   ._�5�      �� $ 
Horizontal=�6)   .+�5�   "   �� -   5         ��Name$ FilterTextBox��5�   *   $	��180��5�      $0 *IP 주소 또는 프로토콜로 필터링��5�      $��0,0,8,0q�5�      5�      �� 5�   "   -   5        $ ProtocolFilterComboBox��5�   +   $	��110��5�      $ 프로토콜��5�      .@�5�   &   �� $��모든 프로토콜��63   �� 5�   &   $	��TCP��63   �� 5�   &   $	��UDP��63   �� 5�   &   $
��ICMP��63   5�   #   �� 5�   "   -   5         ��Name$ CaptureStatusText��5�   ,   $��20,0,0,0q�5�   ,   $��Center=�5�   ,   #���  5�   ,   $��SemiBold"�5�   ,   5�      5�      �� 5�      $��2��6!   #���  6.   $ 12��6c   $��
0,10,20,20q�6u   $ 20q�6�   ���#C2C2C2$��1q�6�   ._�5�      &   System.Windows.Controls.DataGrid  -   5          Name$ PacketDataGrid��5�   '      AutoGenerateColumns$ False��5�         
IsReadOnly$
 True��5�        DataGridBackgroundColor#���  5�       	 DataGridForegroundColor#��� 	 5�      $��0q�5�         GridLinesVisibility$
 None=�5�       
 DataGridRowBackgroundColor   RowBackground# � 
 5�       ) %DataGridAlternatingRowBackgroundColor   AlternatingRowBackground# �  5�         	RowHeight$ 40Q�5�          HeadersVisibility$  Column=�5�      !   CanUserResizeRows$! False��5�      "   ColumnHeaderStyle" 5�   "   �� 5�   &   # �� 
TargetType=  7System.Windows.Controls.Primitives.DataGridColumnHeader"#  6,   .�5�   *   �� $ �� Property$ � ��% �� Value#% �   6G   �� 5�   *   $ � ��#% �  6G   5�   '   &   Columns& 5�   "   0  *System.Windows.Controls.DataGridTextColumn  5�   &   ,  &System.Windows.Controls.DataGridColumn'   Header$' 시간��69   (   Width5  /System.Windows.Controls.DataGridLengthConverter$( 70 6z   1	  +System.Windows.Controls.DataGridBoundColumn) 	  Binding) 6E   �� *	Timestamp+* �� StringFormat$* HH:mm:ss��6z     5�   &   $' 	소스 IP��69   $( 90 6e   ) 6H   �� *
SourceIP+6e     5�   &   $' S포트��69   $( 50 6e   ) 6F   �� *
SourcePort+6e     5�   &   $' 	대상 IP��69   $( 90 6j   ) 6H   �� *DestinationIP+6j     5�   &   $' D포트��69   $( 50 6j   ) 6F   �� *DestinationPort+6j     5�   &   $' 프로토콜��69   $( 55 6d   ) 6G   �� *
Protocol+6d     5�   &   $' 프로세스��69   $( 80 6g   ) 6G   �� *ProcessName+6g     5�   &   $' 크기��69   $( 55 6�   ) 6E   �� *
PacketSize++ �� 	Converter#+ [ 6�     5�   &   $' 	플래그��69   $( 60 6`   ) 6F   �� *Flags+6`   5�      5�      � 5�      -   5        , �Name$, LoadingOverlay��5�      $��0��5�      $��3��6    ���	#80000000$y�	Collapsed=�5�      .+�5�      �� $��Center=�6)   $��Center=�6D   .+�5�   "   �� -   5        $ LoadingText��5�   ,   $��패킷 캡처 중...��5�       
�������$��18(�5�       $��Center=�5�       $��0,0,0,20q�5�       �� 5�   "   -   5        - ��Name$- SpinnerItems��5�   /   $��50��5�   #   $��50��6.   ��5�   &   �� 5   *   .R�5  .   �� 5  +   z�5  &   �� 5  *   . �� ResourceKey$. SpinnerRotate��69   5  #   5     5	     5
     5     �� 5     -   5        $
 
HistoryTab��5  )   $��사용 기록��6   .��5     � $��0q�6    5     �� 5     $
��Auto �6,   �� 5     $
��Auto �6,   �� 5     $��* �6,   .+�5     �� $��0��6!   #���  6.   $ 12��6c   $ 20q�6u   $��	0,0,20,10q�6�   ���#C2C2C2$��1q�6�   ._�5     �  5  "   �� 5  &   $
��Auto �67   �� 5  &   $��* �67   �� 5  &   $
��Auto �67   .+�5  "   �� $��0��6,   $ ��네트워크 사용 기록��5   ,   #���  5!  ,   $��20(�5"  ,   $��SemiBold"�5#  ,   $��Center=�5$  ,   $��0,0,20,0q�5%  ,   �� 5'  "   $��2��6-   $ 
Horizontal=�6=   $��Center=�6V   .+�5(  &   (
  "System.Windows.Controls.DatePicker
  -   5        / 
 Name$/ StartDatePicker��5(  1   $ 시작��5)  1   $	��100��5*  1   $��30��6=   $��0,0,6,0q�5+  1   5,  1   
  5.  &   -   5        $/ EndDatePicker��5.  1   $ 종료��5/  1   $	��100��50  1   $��30��6=   $��0,0,6,0q�51  1   52  1   �� 54  &   -   5        $ InterfaceFilterComboBox��54  /   $	��120��55  /   $��30��6;   $ 인터페이스��56  /   $��0,0,6,0q�57  /   .@�59  *   �� $��모든 인터페이스��67   5:  '   �� 5<  &   -   5        $# HistoryProtocolFilterComboBox��5<  /   $��90��5=  /   $��30��6:   $ 프로토콜��5>  /   $��0,0,6,0q�5?  /   .@�5A  *   �� $��모든 프로토콜��67   �� 5B  *   $	��TCP��67   �� 5C  *   $	��UDP��67   �� 5D  *   $
��ICMP��67   �� 5E  *   $
��HTTP��67   �� 5F  *   $��HTTPS��67   5G  '   �� 5I  &   -   5        $ DirectionFilterComboBox��5I  /   $��85��5J  /   $��30��6:   $ 방향��5K  /   $��0,0,6,0q�5L  /   .@�5N  *   �� $��모든 방향��67   �� 5O  *   $��송신��67   �� 5P  *   $��수신��67   �� 5Q  *   $��내부��67   5R  '   �� 5T  &   -   5        $ RefreshHistoryButton��5T  -   $��조회��5U  -   #���  5V  -   $
��10,8q�5X  -   $��0q�5Y  -   $
 Hand��5Z  -   $��30��5[  -   $��70��5\  -   ��5]  *   �� 5^  .   " ��6>   .R�5_  2   �� #��z��69   #��z��5`  9   #��z��5a  9   $ 12��5b  9   ._�5c  6   �� $��Center=�6G   $��Center=�5d  G   5e  3   5f  /   5h  '   �� 5j  &   -   5        $ GenerateTestDataButton��5j  -   $��테스트 데이터��5k  -   #���  5l  -   $	��8,8q�5n  -   $��6,0,0,0q�5o  -   $��0q�5p  -   $
 Hand��5q  -   $��30��5r  -   $��90��5s  -   ��5t  *   �� 5u  .   " ��6>   .R�5v  2   �� #��z��69   #��z��5w  9   #��z��5x  9   $ 12��5y  9   ._�5z  6   �� $��Center=�6G   $��Center=�5{  G   5|  3   5}  /   5  '   5�  #   5�     5�     �� 5�     $��1��6!   #���  6.   $ 12��6c   $ 20q�6u   $��
0,10,20,10q�6�   ���#C2C2C2$��1q�6�   ._�5�     �  5�  "   �� 5�  &   $��* �67   �� 5�  &   $��* �67   �� 5�  &   $��* �67   �� 5�  &   $��* �67   .+�5�  "   �� $��0��6-   $��Center=�6=   .+�5�  &   �� $��총 연결 수��60   #���  5�  0   $��14(�5�  0   $��Center=�5�  0   $��0,0,0,5q�5�  0   �� 5�  &   -   5        $ TotalConnectionsText��5�  0   $��0��5�  0     AccentColor#���  5�  0   $��24(�5�  0   $
��Bold"�5�  0   $��Center=�5�  0   5�  #   �� 5�  "   $��1��6-   $��Center=�6=   .+�5�  &   �� $��총 데이터량��60   #���  5�  0   $��14(�5�  0   $��Center=�5�  0   $��0,0,0,5q�5�  0   �� 5�  &   -   5        $ TotalDataText��5�  0   $
��0 KB��5�  0   #���  5�  0   $��24(�5�  0   $
��Bold"�5�  0   $��Center=�5�  0   5�  #   �� 5�  "   $��2��6-   $��Center=�6=   .+�5�  &   �� $��주요 프로토콜��60   #���  5�  0   $��14(�5�  0   $��Center=�5�  0   $��0,0,0,5q�5�  0   �� 5�  &   -   5        $ TopProtocolText��5�  0   $	��TCP��5�  0   #���  5�  0   $��24(�5�  0   $
��Bold"�5�  0   $��Center=�5�  0   5�  #   �� 5�  "   $��3��6-   $��Center=�6=   .+�5�  &   �� $��피크 시간��60   #���  5�  0   $��14(�5�  0   $��Center=�5�  0   $��0,0,0,5q�5�  0   �� 5�  &   -   5        $ PeakTimeText��5�  0   $��--:--��5�  0   #���  5�  0   $��24(�5�  0   $
��Bold"�5�  0   $��Center=�5�  0   5�  #   5�     5�     �� 5�     $��2��6!   #���  6.   $ 12��6c   $��
0,10,20,20q�6u   $ 0q�6�   ���#C2C2C2$��1q�6�   ._�5�       -   5        $ HistoryDataGrid��5�  '   $ False��5�  '   $
 True��5�  '   #���  5�  '   #��� 	 5�  '   $��0q�5�  '   $
 None=�5�  '   # � 
 5�  '   # �  5�  '   $ 40Q�5�  '   $  Column=�5�  '   0   CanUserResizeColumns$
0 True��5�  '   1   CanUserSortColumns$
1 True��5�  '   $
��True��5�  '   $
��Auto=�5�  '   $��Disabled=�5�  '   2   ColumnWidth$2 * 5�  '   $��Stretch=�5�  '   $��Stretch=�5�  '   $��0q�5�  '   " 5�  "   �� 5�  &   "#  6,   .�5�  *   �� $ � ��#% �   6G   �� 5�  *   $ � ��#% �  6G   5�  '   & 5�  "     5�  &   $' 시간��69   $
( 0.8* 6w   3   MinWidth$3 50Q�6�   ) 6E   �� *	Timestamp+$* HH:mm��6�     5�  &   $' 인터페이스��69   $
( 1.2* 6j   $3 70Q�6w   ) 6H   �� *InterfaceName+6w     5�  &   $' 	소스 IP��69   $	( 120 6e   ) 6H   �� *
SourceIP+6e     5�  &   $' 소스 포트��69   $	( 100 6n   ) 6H   �� *SourcePortDisplay+6n     5�  &   $' 	대상 IP��69   $	( 120 6j   ) 6H   �� *DestinationIP+6j     5�  &   $' 대상 포트��69   $	( 100 6s   ) 6H   �� *DestinationPortDisplay+6s     5�  &   $' 프로토콜��69   $( 80 6d   ) 6G   �� *
Protocol+6d     5�  &   $' 방향��69   $
( 0.8* 6c   $3 45Q�6p   ) 6E   �� *	Direction+6p     5�  &   $' 크기��69   $( 55 6�   ) 6E   �� *
PacketSize+#+ [ 6�   5�     5�     5�     5�     �� 5�     -    5        $
 SecurityTab��5�  +   $��보안 모니터링��6   .��5�     � $��0q�6    5      �� 5     $
��Auto �6,   �� 5     $
��Auto �6,   �� 5     $
��Auto �6,   �� 5     $��* �6,   .+�5     �� $��0��6!   #���  6.   $ 12��6c   $ 20q�6u   $��	0,0,20,10q�6�   ���#C2C2C2$��1q�6�   ._�5	     �  5
  "   �� 5  &   $��* �67   �� 5  &   $
��Auto �67   .+�5  "   �� $��0��6-   $ 
Horizontal=�6=   $��Center=�6V   .+�5  &   �� $ ��보안 위협 모니터링��60   #���  5  0   $��20(�5  0   $��SemiBold"�5  0   $��Center=�5  0   �� 5  &   $��25��6-   $��25��68   
������ $
 Hand��5  -   $��10,0,0,0q�5  -   ._�5  *     $ ShieldCheckOutline=�6B   $��25��5  >   $��25��6I   #���  5  >   $��Center=�5  >   $��Center=�5  >    5  *   _� 5  .   $a��[네트워크 트래픽에서 보안 위협을 실시간으로 감지하고 분석합니다.��66   $ Bottom=�5   6   $ 0��5!  6   $ 5��6K   5#  '   5$  #   �� 5&  "   $��1��6-   $ 
Horizontal=�6=   $��Center=�6V   .+�5'  &   �� -!   5        $ SecurityScanButton��5'  -   $��보안 스캔��5(  -   #���  5)  -   $��0q�5+  -   $
 Hand��5,  -   $	��200��5-  -   $��30��69   ��5.  *   �� 5/  .   " ��6>   .R�50  2   �� #��z��69   #��z��51  9   #��z��52  9   $ 12��53  9   ._�54  6   �� $��Center=�6G   $��Center=�55  G   56  3   57  /   59  '   �� 5;  &   -"   5        $  GenerateSecurityTestButton��5;  -   $��테스트 경고��5<  -   #���  5=  -   $��10,0,0,0q�5?  -   $��0q�5@  -   $
 Hand��5A  -   $	��200��5B  -   $��30��69   ��5C  *   �� 5D  .   " ��6>   .R�5E  2   �� #��z��69   #��z��5F  9   #��z��5G  9   $ 12��5H  9   ._�5I  6   �� $��Center=�6G   $��Center=�5J  G   5K  3   5L  /   5N  '   5O  #   5P     5Q     �� 5T     $��1��6!   #���  6.   $ 12��6c   $ 20q�6u   $��
0,10,20,10q�6�   ���#C2C2C2$��1q�6�   ._�5U     �  5V  "   �� 5W  &   $��* �67   �� 5X  &   $��* �67   �� 5Y  &   $��* �67   �� 5Z  &   $��* �67   .+�5^  "   �� $��0��6-   $��Center=�6=   .+�5_  &   �� $��
총 경고��60   #���  5`  0   $��14(�5a  0   $��Center=�5b  0   $��0,0,0,5q�5c  0   �� 5d  &   -#   5        $ TotalAlertsText��5d  0   $��0��5e  0   ���#FF6B6B$��24(�5g  0   $
��Bold"�5h  0   $��Center=�5i  0   5j  #   �� 5m  "   $��1��6-   $��Center=�6=   .+�5n  &   �� $��24시간 경고��60   #���  5o  0   $��14(�5p  0   $��Center=�5q  0   $��0,0,0,5q�5r  0   �� 5s  &   -$   5        $ RecentAlertsText��5s  0   $��0��5t  0   ���#FFA726$��24(�5v  0   $
��Bold"�5w  0   $��Center=�5x  0   5y  #   �� 5|  "   $��2��6-   $��Center=�6=   .+�5}  &   �� $��고위험 경고��60   #���  5~  0   $��14(�5  0   $��Center=�5�  0   $��0,0,0,5q�5�  0   �� 5�  &   -%   5        $ HighRiskAlertsText��5�  0   $��0��5�  0   ���#E53E3E$��24(�5�  0   $
��Bold"�5�  0   $��Center=�5�  0   5�  #   �� 5�  "   $��3��6-   $��Center=�6=   .+�5�  &   �� $��미해결 경고��60   #���  5�  0   $��14(�5�  0   $��Center=�5�  0   $��0,0,0,5q�5�  0   �� 5�  &   -&   5        $ UnresolvedAlertsText��5�  0   $��0��5�  0   ���#9F7AEA$��24(�5�  0   $
��Bold"�5�  0   $��Center=�5�  0   5�  #   5�     5�     �� 5�     $��2��6!   #���  6.   $ 12��6c   $ 20q�6u   $��
0,10,20,10q�6�   ���#C2C2C2$��1q�6�   ._�5�     �� $ 
Horizontal=�6)   .+�5�  "   �� -'   5        $ SecuritySeverityFilter��5�  +   $	��120��5�  +   $ 	위험도��5�  +   $��0,0,10,0q�5�  +   .@�5�  &   �� $��모든 위험도��63   $	 All��6D   4 �� 
IsSelected$
4 True��6N   �� 5�  &   $��낮음��63   $	 Low��6@   �� 5�  &   $��보통��63   $ Medium��6@   �� 5�  &   $��높음��63   $
 High��6@   �� 5�  &   $��심각��63   $ Critical��6@   5�  #   �� 5�  "   -(   5        $ SecurityAlertTypeFilter��5�  +   $	��150��5�  +   $ 경고 유형��5�  +   $��0,0,10,0q�5�  +   .@�5�  &   �� $��모든 유형��63   $	 All��6C   $
4 True��6M   �� 5�  &   $��	악성 IP��63   $ MaliciousIP��6C   �� 5�  &   $��의심스러운 포트��63   $ SuspiciousPort��6F   �� 5�  &   $��비정상 트래픽��63   $ AbnormalTraffic��6E   �� 5�  &   $��대용량 전송��63   $ LargeTransfer��6D   5�  #   
  5�  "   -)   5        $/ SecurityStartDatePicker��5�  -   $	��120��5�  -   $ 시작 날짜��5�  -   $��0,0,10,0q�5�  -   5�  -   
  5�  "   -*   5        $/ SecurityEndDatePicker��5�  -   $	��120��5�  -   $ 종료 날짜��5�  -   5�  -   5�     5�     �� 5�     $��3��6!   #���  6.   $ 12��6c   $��
0,10,20,20q�6u   $ 20q�6�   ���#C2C2C2$��1q�6�   ._�5�       -+   5        $ SecurityAlertsDataGrid��5�  '   $ False��5�  '   $
 True��5�  '   #���  5�  '   #��� 	 5�  '   $��0q�5�  '   $
 None=�5�  '   # � 
 5�  '   # �  5�  '   $ 40Q�5�  '   $  Column=�5�  '   $! False��5�  '   " 5�  "   �� 5�  &   "#  6,   .�5�  *   �� $ � ��#% �   6G   �� 5�  *   $ � ��#% �  6G   5�  '   & 5�  "     5�  &   $' 시간��69   $	( 150 5�  9   ) 5�  E   �� *	Timestamp+$* yyyy-MM-dd HH:mm:ss��5�  9     5�  &   $' 	위험도��69   $( 80 6c   ) 6F   �� *
Severity+5 	  ElementStyle5 5�  *   �� 5�  .   "# ��64   6 �� Triggers6 5�  2   P� 5�  6   7 P� Property7 � ��8 P� Value$
8 High��6N   . �5�  :   �� $ � ��% �#E53E3E6W   �� 5�  :   $ � ��$
% Bold"�6W   5�  7   P� 5�  6   7 � ��$8 Critical��6N   . �5�  :   �� $ � ��% �#C530306W   �� 5�  :   $ � ��$
% Bold"�6W   5�  7   P� 5�  6   7 � ��$8 Medium��6N   . �5�  :   �� $ � ��% �#FFA7266W   �� 5�  :   $ � ��$% SemiBold"�6W   5�  7   P� 5�  6   7 � ��$	8 Low��6N   . �5�  :   �� $ � ��% �#66BB6A6W   5�  7   5�  /   5�  '     5�  &   $' 경고 유형��69   $	( 150 6f   ) 6H   �� *	AlertType+6f     5�  &   $' 	소스 IP��69   $	( 120 6e   ) 6H   �� *
SourceIP+6e     5�  &   $' 	대상 IP��69   $	( 120 6j   ) 6H   �� *DestinationIP+6j     5�  &   $' 소스 포트��69   $	( 100 6n   ) 6H   �� *SourcePortDisplay+6n     5�  &   $' 대상 포트��69   $	( 100 6s   ) 6H   �� *DestinationPortDisplay+6s     5�  &   $' 프로토콜��69   $( 80 6d   ) 6G   �� *
Protocol+6d     5�  &   $' 설명��69   $	( 200 6e   ) 6E   �� *Description+6e     5�  &   $' 조치��69   $( 80 6`   ) 6E   �� *Action+6`   4  .System.Windows.Controls.DataGridCheckBoxColumn  5�  &   $' 	해결됨��6=   $( 80 6i   ) 6J   �� *
IsResolved+6i   5�     5�     5�     5      5     5     5     5     