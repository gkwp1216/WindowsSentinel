   M S B A M L   `   `   `  ����C  ?Logcheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null#clr-namespace:LogCheckLogCheck  5          LogCheck.Logs   5      W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation      5      R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      _ [MaterialDesignThemes.Wpf, Version=4.9.0.0, Culture=neutral, PublicKeyToken=df2a72020bd7962aFmaterialDesign1http://materialdesigninxaml.net/winfx/xaml/themes  5      "localclr-namespace:LogCheck  5        N� Title$
  Logs��5
         BackgroundColor N� 
Background# �   6   .��5      �  � ColumnDefinitions 5   
   �� 5      $	��220 �6   �� 5      $��* �6   .+�5   
   �� $��0��6     ButtonBackgroundColor#���  6!    �� CornerRadius$ 12��6V   $��
20,0,10,20q�6h   ���#C2C2C2$��1q�6�   ._�5      �� $��10q�6   .+�5      �� -   5          SidebarButtonStyle#���  5      
 � Tag$	 ��5      $��설치된 프로그램��5      5      �� 5      -   5        #���  5      $	 ��5      $ ��네트워크 접속 내역��5      5      �� 5      -   5        #���  5      $	 ��5      $��보안 로그��5      5       �� 5!      -   5        #���  5!      $	 ��5"      $ ��보안 프로그램 복구��5#      5$      5%      5&      � 5)   
   $��1��6   $��0q�6    � RowDefinitions 5*      �� 5+      $
��Auto �6    �� 5,      $
��Auto �6    �� 5-      $��* �6    .+�51      �� $��0��6   #���  6"   $ 12��6W    �� Padding$ 20q�6i   $��
10,0,20,20q�6v   ���#C2C2C2$��1q�6�   ._�52      �  53      �� 54      $��* �6+   �� 55      $
��Auto �6+   .+�58      �� $��0��6!    �� Orientation$ 
Horizontal=�61   $
��Left=�6J   $��Center=�6e   .+�59      �� -   5         ��Name$ chkExternalLog��59   #   $��외부 접속 내역��6;   $��20(�5:   #   $��SemiBold"�61     ForegroundColor#���  5;   #   $��0,0,10,0q�5<   >   ��5=      �� 5>   "   	 �� 
TargetType"	 ��62   .R�5?   &   �� $ 
Horizontal=�61   .+�5@   *   �� $��15��61   $��15��6<     BorderBrushColor#���  5A   1   $��1q�5B   1   $��0,0,8,0q�5C   1   
������ 
 J� IsHitTestVisible$

 True��5E   1   $|�False��5F   1   ._�5G   .   F�  F�Name$ 	CheckMark��63   "����        р�[ ��[ � � �r��	  CheckedColor#���  5I   3   $��3��6[    �� Stretch$ Uniform=�5J   3   $y�	Collapsed=�5K   3   5L   +   �� 5M   *   $��Center=�6;   5N   '    �� Triggers 5O   &   P� 5P   *    P� Property �@d�	IsChecked P� Value$
 TrueY�6G   . �5Q   .   ��  �� 
TargetName$ 	CheckMark��65    �� Property � y� �� Value$ Visible=�6b   5R   +   5T   #   5V      �� 5W      -   5        $ chkEventLog��5W   #   $��이벤트 내역��68    d� 	IsChecked$
 TrueY�6I   $��20(�5X   #   $��SemiBold"�61   #���  5Y   #   ��5[      �� 5\   "   "	 ��62   .R�5]   &   �� $ 
Horizontal=�61   .+�5^   *   �� $��15��61   $��15��6<   #���  5_   1   $��1q�5`   1   $��0,0,8,0q�5a   1   
������ $

 True��5c   1   $|�False��5d   1   ._�5e   .   F� $ 	CheckMark��63   "����        р�[ ��[ � � �r��	#���  5g   3   $��3��6[   $ Uniform=�5h   3   $y�	Collapsed=�5i   3   5j   +   �� 5k   *   $��Center=�6;   5l   '    5m   &   P� 5n   *    �@d�	IsChecked$
 TrueY�6G   . �5o   .   �� $ 	CheckMark��65    � y�$ Visible=�6b   5p   +   5r   #   5t      �� 5w      $��25��6!   $��25��6,   
������  � Cursor$
 Hand��5y   !   $��10,0,0,0q�5z   !   ._�5{      '  !MaterialDesignThemes.Wpf.PackIcon     Kind$ HelpCircleOutline=�66   $��25��5|   6   $��25��6A     HelpMarkColor#���  5}   6   $��Center=�5~   6   $��Center=�5   6    � ToolTip 5�      _� 5�   "   $h��b외부 접속 내역과 로그 이벤트 발생 내역을 확인하고 관리할 수 있습니다.��6*    _� 	Placement$ 
MousePoint=�5�   *    _� HorizontalOffset$	 490��5�   *    _� VerticalOffset$ 15��6A   5�      5�      �� 5�      $��1��6!   $ 
Horizontal=�61   $��Center=�6J   .+�5�      �� -   5         ��Name$ btnShowChangeLogs��5�   !   $��내역 확인��6<     CheckButtonStyle#���  5�   !   $��0q�5�   !   $	��200��5�   !   $��30��6-   ��5�      �� 5�   "   "	 ��62   .R�5�   &   �� #��z��6-   #��z��5�   -   #��z��5�   -   $ 12��5�   -   ._�5�   *   �� $��Center=�6;   $��Center=�5�   ;   5�   '   5�   #   5�      5�      5�      5�      �� 5�      -   5         ��Name$ logsSection��5�   "   $��2��6   #���  67   $ 12��6l   $ 20q�6~   $��
10,0,20,20q�5�      ���#C2C2C2$��1q�6?   $y�Visible=�6S   ._�5�      �� .+�5�      &   System.Windows.Controls.DataGrid  -	   5          Name$ logsDataGrid��5�      $	��550��65      AutoGenerateColumns$ False��6B      
IsReadOnly$
 True��6^   $
��Auto=�5�        DataGridBackgroundColor#���  5�       	 DataGridForegroundColor#��� 	 5�      $��0q�5�         GridLinesVisibility$
 None=�63    
 DataGridRowBackgroundColor    RowBackground#  � 
 5�       ) %DataGridAlternatingRowBackgroundColor!   AlternatingRowBackground#! �  5�      "   	RowHeight$" 40Q�5�      #   HeadersVisibility$# Column=�6.   $   CanUserResizeRows$$ False��6I   %   ColumnHeaderStyle% 5�      �� 5�      & �� 
TargetType=  7System.Windows.Controls.Primitives.DataGridColumnHeader"&  6$   .�5�   "   ��  � ��# �   6?   �� 5�   "    � ��# �  6?   �� 5�   "    � ��$
 Bold"�6?   �� 5�   "    � ��$ Center=�6O   �� 5�   "    � ��$ 10,10q�6<   �� 5�   "    � ��$ 0q�6D   �� 5�   "    �@�Cursor$
 Hand��6;   ' �� Triggers' 5�   "   P� 5�   &    �@J�IsMouseOver$
 True��6E   . �5�   *   ��  � �� �	#FF9693936G   5�   '   5�      (   Columns( 5�      0  *System.Windows.Controls.DataGridTextColumn  5�      ,  &System.Windows.Controls.DataGridColumn)   Header$) 이벤트 번호��61   *   Width5  /System.Windows.Controls.DataGridLengthConverter$	* 100 6]   1  +System.Windows.Controls.DataGridBoundColumn+   Binding+ 6A   �� *	EventId+,   ElementStyle, 5�   "   �� 5�   &   "& ��6,   .�5�   *   ��  �@��TextAlignment$ Center=�6J   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $) 날짜��61   $	* 180 6x   + 6=   �� *Date+- �� StringFormat$- yyyy-MM-dd HH:mm:ss��, 5�   "   �� 5�   &   "& ��6,   .�5�   *   ��  �@��TextAlignment$ Center=�6J   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $) 프로그램명��61   $	* 200 6`   + 6@   �� *ProgramName+, 5�   "   �� 5�   &   "& ��6,   .�5�   *   ��  �@��TextAlignment$ Center=�6J   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $) 사유��61   $* * 6X   + 6=   �� *Reason+, 5�   "   �� 5�   &   "& ��6,   .�5�   *   ��  �@��TextAlignment$ Center=�6J   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   �� 5�   *    � ��$
 Wrap=�6I   5�   '   5�      5�      5�      5�      5�      5�      5�      