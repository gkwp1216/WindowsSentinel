   M S B A M L   `   `   `  ����C  ?Logcheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null#clr-namespace:LogCheckLogCheck  5          LogCheck.Logs   5      W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      5      R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      _ [MaterialDesignThemes.Wpf, Version=4.9.0.0, Culture=neutral, PublicKeyToken=df2a72020bd7962aFmaterialDesign1http://materialdesigninxaml.net/winfx/xaml/themes  5      "localclr-namespace:LogCheck  5        N� Title$
  Logs��5
         BackgroundColor N� 
Background# �   6   .��5      �  � ColumnDefinitions 5   
   �� 5      $	��220 �6   �� 5      $��* �6   .+�5   
   �� $��0��6     ButtonBackgroundColor#���  6!    �� CornerRadius$ 12��6V   $��
20,0,10,20q�6h   ���#C2C2C2$��1q�6�   ._�5      �� $��10q�6   .+�5      �� -   5          SidebarButtonStyle#���  5      
 � Tag$	 ��5      $ ��악성 프로그램 탐지��5       � Cursor$
 Hand��5      5      �� 5      -   5        #���  5      $	 ��5      $ ��네트워크 접속 내역��5      $
 Hand��5      5       �� 5"      -   5        #���  5"      $	 ��5#      $��설치된 프로그램��5$      $
 Hand��5%      5&      �� 5(      -   5        #���  5(      $	 ��5)      $ ��보안 프로그램 복구��5*      $
 Hand��5+      5,      �� 5.      -   5        #���  5.      $	 ��5/      $��보안 로그��50      $
 Hand��51      52      53      54      � 57   
   $��1��6   $��0q�6    � RowDefinitions 58      �� 59      $
��Auto �6    �� 5:      $
��Auto �6    �� 5;      $��* �6    .+�5?      �� $��0��6   #���  6"   $ 12��6W    �� Padding$ 20q�6i   $��
10,0,20,20q�6v   ���#C2C2C2$��1q�6�   ._�5@      �  5A      �� 5B      $��* �6+   �� 5C      $
��Auto �6+   .+�5F      �� $��0��6!    �� Orientation$ 
Horizontal=�61   $
��Left=�6J   $��Center=�6e   .+�5G      �� -   5        	 ��Name$	 chkExternalLog��5G   #   $��외부 접속 내역��6;   $��20(�5H   #   $��SemiBold"�61     ForegroundColor#���  5I   #   $��0,0,10,0q�5J   >   ��5K      �� 5L   "   
 �� 
TargetType"
 ��62   .R�5M   &   �� $ 
Horizontal=�61   .+�5N   *   �� $��15��61   $��15��6<     BorderBrushColor#���  5O   1   $��1q�5P   1   $��0,0,8,0q�5Q   1   
������  J� IsHitTestVisible$
 True��5S   1   $|�False��5T   1   ._�5U   .   F�  F�Name$ 	CheckMark��63   "����        р�[ ��[ � � �r��	  CheckedColor#���  5W   3   $��3��6[    �� Stretch$ Uniform=�5X   3   $y�	Collapsed=�5Y   3   5Z   +   �� 5[   *   $��Center=�6;   5\   '    �� Triggers 5]   &   P� 5^   *    P� Property �@d�	IsChecked P� Value$
 TrueY�6G   . �5_   .   ��  �� 
TargetName$ 	CheckMark��65    �� Property � y� �� Value$ Visible=�6b   5`   +   5b   #   5d      �� 5e      -   5        $	 chkEventLog��5e   #   $��이벤트 내역��68    d� 	IsChecked$
 TrueY�6I   $��20(�5f   #   $��SemiBold"�61   #���  5g   #   ��5i      �� 5j   "   "
 ��62   .R�5k   &   �� $ 
Horizontal=�61   .+�5l   *   �� $��15��61   $��15��6<   #���  5m   1   $��1q�5n   1   $��0,0,8,0q�5o   1   
������ $
 True��5q   1   $|�False��5r   1   ._�5s   .   F� $ 	CheckMark��63   "����        р�[ ��[ � � �r��	#���  5u   3   $��3��6[   $ Uniform=�5v   3   $y�	Collapsed=�5w   3   5x   +   �� 5y   *   $��Center=�6;   5z   '    5{   &   P� 5|   *    �@d�	IsChecked$
 TrueY�6G   . �5}   .   �� $ 	CheckMark��65    � y�$ Visible=�6b   5~   +   5�   #   5�      �� 5�      $��25��6!   $��25��6,   
������ $
 Hand��5�   !   $��10,0,0,0q�5�   !   ._�5�      '  !MaterialDesignThemes.Wpf.PackIcon     Kind$ HelpCircleOutline=�66   $��25��5�   6   $��25��6A     HelpMarkColor#���  5�   6   $��Center=�5�   6   $��Center=�5�   6    � ToolTip 5�      _� 5�   "   $h��b외부 접속 내역과 로그 이벤트 발생 내역을 확인하고 관리할 수 있습니다.��6*    _� 	Placement$ 
MousePoint=�5�   *    _� HorizontalOffset$	 490��5�   *    _� VerticalOffset$ 15��6A   5�      5�      �� 5�      $��1��6!   $ 
Horizontal=�61   $��Center=�6J   .+�5�      �� -   5         ��Name$ btnShowChangeLogs��5�   !   $��내역 확인��6<     CheckButtonStyle#���  5�   !   $��0q�5�   !   $	��200��5�   !   $��30��6-   ��5�      �� 5�   "   "
 ��62   .R�5�   &   �� #��z��6-   #��z��5�   -   #��z��5�   -   $ 12��5�   -   ._�5�   *   �� $��Center=�6;   $��Center=�5�   ;   5�   '   5�   #   5�      5�      5�      5�      �� 5�      -	   5         ��Name$ logsSection��5�   "   $��2��6   #���  67   $ 12��6l   $ 20q�6~   $��
10,0,20,20q�5�      ���#C2C2C2$��1q�6?   $y�Visible=�6S   ._�5�      �� .+�5�      &   System.Windows.Controls.DataGrid  -
   5          Name$ logsDataGrid��5�      $	��550��65      AutoGenerateColumns$ False��6B      
IsReadOnly$
 True��6^   $
��Auto=�5�        DataGridBackgroundColor#���  5�       	 DataGridForegroundColor#��� 	 5�      $��0q�5�         GridLinesVisibility$
 None=�63    
 DataGridRowBackgroundColor    RowBackground#  � 
 5�       ) %DataGridAlternatingRowBackgroundColor!   AlternatingRowBackground#! �  5�      "   	RowHeight$" 40Q�5�      #   HeadersVisibility$# Column=�6.   $   CanUserResizeRows$$ False��6I   %   ColumnHeaderStyle% 5�      �� 5�      & �� 
TargetType=  7System.Windows.Controls.Primitives.DataGridColumnHeader"&  6$   .�5�   "   ��  � ��# �   6?   �� 5�   "    � ��# �  6?   �� 5�   "    � ��$
 Bold"�6?   �� 5�   "    � ��$ Center=�6O   �� 5�   "    � ��$ 10,10q�6<   �� 5�   "    � ��$ 0q�6D   �� 5�   "    �@�Cursor$
 Hand��6;   ' �� Triggers' 5�   "   P� 5�   &    �@J�IsMouseOver$
 True��6E   . �5�   *   ��  � �� �	#FF9693936G   5�   '   5�      (   Columns( 5�      0  *System.Windows.Controls.DataGridTextColumn  5�      ,  &System.Windows.Controls.DataGridColumn)   Header$) 이벤트 번호��61   *   Width5  /System.Windows.Controls.DataGridLengthConverter$	* 100 6]   1  +System.Windows.Controls.DataGridBoundColumn+   Binding+ 6A   �� *	EventId+,   ElementStyle, 5�   "   �� 5�   &   "& ��6,   .�5�   *   ��  �@��TextAlignment$ Center=�6J   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $) 날짜��61   $	* 180 6x   + 6=   �� *Date+- �� StringFormat$- yyyy-MM-dd HH:mm:ss��, 5�   "   �� 5�   &   "& ��6,   .�5�   *   ��  �@��TextAlignment$ Center=�6J   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $) 프로그램명��61   $	* 200 6`   + 6@   �� *ProgramName+, 5�   "   �� 5�   &   "& ��6,   .�5�   *   ��  �@��TextAlignment$ Center=�6J   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $) 사유��61   $* * 6X   + 6=   �� *Reason+, 5�   "   �� 5�   &   "& ��6,   .�5�   *   ��  �@��TextAlignment$ Center=�6J   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   �� 5�   *    � ��$
 Wrap=�6I   5�   '   5�      5�      5�      5�      5�      5�      5�      