   M S B A M L   `   `   `  ����W  SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35iFclr-namespace:System.Windows.Media.Animation;assembly=PresentationCoreSystem.Windows.Media.Animation  5      C ?Logcheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null#clr-namespace:LogCheckLogCheck 5         LogCheck.ProgramsList   5      \ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35^ ZWindowsFormsIntegration, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35H 9http://schemas.microsoft.com/winfx/2006/xaml/presentation       5      R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml      5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006      5      @d2http://schemas.microsoft.com/expression/blend/2008      5      YeasingFclr-namespace:System.Windows.Media.Animation;assembly=PresentationCore      5      _ [MaterialDesignThemes.Wpf, Version=4.9.0.0, Culture=neutral, PublicKeyToken=df2a72020bd7962aFmaterialDesign1http://materialdesigninxaml.net/winfx/xaml/themes  5      "localclr-namespace:LogCheck  5        N� Title$  ProgramsList��5         BackgroundColor N� 
Background# �   6    � 	Resources 5        SpinnerPanelTemplate��%P   &	       �� 5   
   .R�5      �� $	��100��6   $	��100��6!   5      5      .��5      �  � ColumnDefinitions 5   
   �� 5      $	��220 �6   �� 5      $��* �6   .+�5   
   �� $��0��6     ButtonBackgroundColor#���  6!    �� CornerRadius$ 12��6V   $��
20,0,10,20q�6h   ���#C2C2C2$��1q�6�   ._�5      �� $��10q�6   .+�5      �� -   5          SidebarButtonStyle#���  5      
 � Tag$	 ��5      $ ��악성 프로그램 탐지��5       � Cursor$
 Hand��5       5!      �� 5#      -   5        #���  5#      $	 ��5$      $ ��네트워크 접속 내역��5%      $
 Hand��5&      5'      �� 5)      -   5        #���  5)      $	 ��5*      $��설치된 프로그램��5+      $
 Hand��5,      5-      �� 5/      -   5        #���  5/      $	 ��50      $ ��보안 프로그램 복구��51      $
 Hand��52      53      �� 55      -   5        #���  55      $	 ��56      $��보안 로그��57      $
 Hand��58      59      5:      5;      � 5>   
   $��1��6   $��0q�6    � RowDefinitions 5?      �� 5@      $
��Auto �6    �� 5A      $��* �6    .+�5E      �� $��0��6   #���  6"   $ 12��6W    �� Padding$ 20q�6i   $��
10,0,20,20q�6v   ���#C2C2C2$��1q�6�   ._�5F      �  5G      �� 5H      $
��Auto �6(   �� 5I      $
��Auto �6(   .+�5M      � $��0��6    5N      �� 5O      $��* �6/   �� 5P      $
��Auto �6/   .+�5S      �� $��0��6%   	 �� Orientation$	 
Horizontal=�65   $��Center=�6N   .+�5T      �� $#��설치된 프로그램 목록��6(     ForegroundColor#���  5U   &   $��20(�5V   &   $��SemiBold"�5W   &   $��Center=�5X   &   �� 5_      $��25��6%   $��25��60   
������ $
 Hand��5a   %   $��10,0,0,0q�5b   %   ._�5c   "   '  !MaterialDesignThemes.Wpf.PackIcon  
   Kind$
 HelpCircleOutline=�6:   $��25��5d   :   $��25��6E     HelpMarkColor#���  5e   :   $��Center=�5f   :   $��Center=�5g   :    � ToolTip 5h   "   _� 5i   &   $\��V시스템에 설치된 모든 프로그램을 확인하고 관리할 수 있습니다.��6.    _� 	Placement$ 
MousePoint=�5j   .    _� HorizontalOffset$	 440��5k   .    _� VerticalOffset$ 5��6E   5m      5n      �� 5p      $��1��6%   $	 
Horizontal=�65   $��Center=�6N   .+�5q      �� -   5         ��Name$ 
ScanButton��5q   %   $#��설치된 프로그램 검사��69     CheckButtonStyle#���  5r   %   $��0q�5t   %   $	��200��5u   %   $��30��61   $
 Hand��5v   %   ��5w   "   �� 5x   &    �� 
TargetType" ��66   .R�5y   *   �� #��z��61   #��z��5z   1   #��z��5{   1   $ 12��5|   1   ._�5}   .   �� $��Center=�6?   $��Center=�5~   ?   5   +   5�   '   5�      5�      5�      5�      5�      �� 5�      $��1��6   #���  6"   $ 12��6W   $��
10,0,20,20q�6i   $ 20q�6}   ���#C2C2C2$��1q�6�   $y�Visible=�6�   ._�5�      � .+�5�      &   System.Windows.Controls.DataGrid  -   5          Name$ programDataGrid��5�         AutoGenerateColumns$ False��5�         
IsReadOnly$
 True��5�        DataGridBackgroundColor#���  5�        DataGridForegroundColor#���  5�      $��0q�5�         GridLinesVisibility$
 None=�5�       	 DataGridRowBackgroundColor   RowBackground# � 	 5�       )
 %DataGridAlternatingRowBackgroundColor   AlternatingRowBackground# � 
 5�         	RowHeight$ 40Q�5�         HeadersVisibility$ Column=�5�         CanUserResizeRows$ False��5�         ColumnHeaderStyle 5�      �� 5�       �� 
TargetType=  7System.Windows.Controls.Primitives.DataGridColumnHeader"  6$   .�5�   "   ��  �� Property � �� �� Value# �   6?   �� 5�   "    � ��# �  6?   �� 5�   "    � ��$
 Bold"�6?   �� 5�   "    � ��$ Center=�6O   �� 5�   "    � ��$ 10,10q�6<   �� 5�   "    � ��$ 0q�6D   �� 5�   "    �@�Cursor$
 Hand��6;    �� Triggers 5�   "   P� 5�   &    P� Property �@J�IsMouseOver  P� Value$
  True��6E   . �5�   *   ��  � �� �	#FF9693936G   5�   '   5�      !   Columns! 5�      0  *System.Windows.Controls.DataGridTextColumn  5�      ,  &System.Windows.Controls.DataGridColumn"   Header$" 프로그램 이름��61   #   Width5  /System.Windows.Controls.DataGridLengthConverter$	# 180 6[   1  +System.Windows.Controls.DataGridBoundColumn$   Binding$ 6B   �� *Name+%   ElementStyle% 5�   "   �� 5�   &   " ��6,   .�5�   *   ��  � ��# �  6G   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $" 설치 날짜��61   $	# 120 6y   $ 6@   �� *InstallDate+& �� StringFormat$& 
yyyy-MM-dd��% 5�   "   �� 5�   &   " ��6,   .�5�   *   ��  � ��# �  6G   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $" 	제조사��61   $	# 160 6\   $ 6>   �� *	Publisher+% 5�   "   �� 5�   &   " ��6,   .�5�   *   ��  � ��# �  6G   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $" 설치 위치��61   $	# 180 6`   $ 6@   �� *InstallPath+% 5�   "   �� 5�   &   " ��6,   .�5�   *   ��  � ��# �  6G   �� 5�   *    � ��$ Center=�6N   5�   '   5�        5�      $" 보안 수준��61   $	# 105 6b   $ 6@   �� *SecurityLevel+% 5�   "   �� 5�   &   " ��6,   .�5�   *   ��  � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   �� 5�   *    � ��$ NoWrap=�6I   �� 5�   *    � ��$ CharacterEllipsis=�6I    5�   *   �� 5�   .   ' �� Binding' 6:   �� *SecurityLevel+( �� Value
( 정상6\   .a�5�   2   ��  � �� �#4CAF506O   5�   /   �� 5�   .   ' 6:   �� *SecurityLevel+
( 주의6\   .a�5�   2   ��  � �� �#FFC1076O   5�   /   �� 5�   .   ' 6:   �� *SecurityLevel+
( 위험6\   .a�5�   2   ��  � �� �#DC35456O   5�   /   5�   '   5�        5�      $" 네트워크��61   $	# 160 6?   % 5�   "   �� 5�   &   " ��6,   .�5�   *   ��  � ��# �  6G   �� 5�   *    � ��$ Center=�6P   �� 5�   *    � ��$ Center=�6N   5�   '   5�      5�      � 5�      -   5        ) �Name$) LoadingOverlay��5�      ���	#80000000$y�	Collapsed=�6J   .+�5�      �� $��Center=�6%   $��Center=�6B   $	 Vertical=�6]   $��0,0,0,20q�6t   .+�5�      �� -	   5        * ��Name$* SpinnerItems��5�   +   #��[ 5�   +   + J� RenderTransformOrigin$+ 0.5,0.5&�5�   +   z�5�   "   �� 5�   &   -
   5        SpinnerRotate��5�   6   ��5�   "   �� 5�   &   " ��6,   .�5�   *   ��  �@��Left 6H   �� *X+�� 5�   *    �@��Top 6G   �� *Y+5�   '   ��5�   "   �� 5�   &   .R�5   *   A� $��10��62   $��10��6=   
�������6I   5  '   5     �� 5     -   5        , ��Name$, LoadingText��5  (   $��검사 중...��6=   
�������$��16(�5  (   $��10q�66   $��Center=�6B   - �� TextAlignment$- Center=�6_   5     5	     5
     5     5     5     5     