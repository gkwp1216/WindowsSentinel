   M S B A M L   `   `   `  ����C  ?LogCheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null1clr-namespace:WindowsSentinelWindowsSentinel  5          WindowsSentinel.Log   5      R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      /localclr-namespace:WindowsSentinel     5        N� Title$	  Log��5	         BackgroundColor N� 
Background# �   6   .��5      �  � ColumnDefinitions 5   
   �� 5      $	��220 �6   �� 5      $��* �6   .+�5   
   �� $��0��6     ButtonBackgroundColor#���  6!    �� CornerRadius$ 12��6V   $��0,0,10,0q�6h   ���#C2C2C2$��1q�6�   ._�5      �� $��10q�6   .+�5      �� -   5          SidebarButtonStyle#��[ 5      
 � Tag$	 ��5      $��설치된 프로그램��5      5      �� 5      -   5        #��[ 5      $	 ��5      $��수정/변경 내역��5      5      �� 5      -   5        #��[ 5      $	 ��5      $��보안 로그��5      5      �� 5       -   5        #��[ 5       $	 ��5!      $ ��보안 프로그램 복구��5"      5#      5$      5%      � 5(   
   $��1��6   $��0q�6    � RowDefinitions 5)      �� 5*      $
��Auto �6    �� 5+      $
��Auto �6    �� 5,      $��* �6    .+�5/      �� $��0��6   #���  6"   $ 12��6W    �� Padding$ 20q�6i   $��0,0,0,10q�6v   ���#C2C2C2$��1q�6�   ._�50      �� .+�51      �� -   5         ��Name$ btnShowChangeLogs��51      $��내역 확인��68   ���#4CAF50
�������$��0q�52      $	��300��61   $��30��6=   ��53      �� 54       �� 
TargetType" ��6.   .R�55   "   �� #��z��6)   #��z��56   )   #��z��57   )   $ 8��58   )   ._�59   &   �� $��Center=�67   $��Center=�5:   7   5;   #   5<      ��5>      �� 5?      	 �� 
TargetType"	 ��6$   .�5@   "   �� 
 �� Property
 � �� �� Value �#4CAF506?    �� Triggers 5A   "   P� 5B   &    P� Property �@J�IsMouseOver P� Value . . �5C   *   �� 
 �@J�Effect 5D   .   3  -System.Windows.Media.Effects.DropShadowEffect  5E   2      
BlurRadius$ 15Q�6C      ShadowDepth$ 0Q�6S      Color$ #CCCCCC��6c   5G   +   �� 5H   *   
 � �� �#4CAF506H   5I   '   5K      5M      5N      5O      �� 5R      -   5         ��Name$ logsSection��5R   "   $��2��6   #���  67   $ 12��6l   $ 20q�6~   ���#C2C2C2$��1q�5S   +   $y�	Collapsed=�6?   ._�5T      �� .+�5U      &   System.Windows.Controls.DataGrid  -   5          Name$ logsDataGrid��5U      $	��550��65      AutoGenerateColumns .     
IsReadOnly . $
��Auto=�5V      
������ $��0q�5W   ,      GridLinesVisibility$
 None=�6@      RowBackground �#F0F0F0   AlternatingRowBackground �#FFFFFF   	RowHeight$ 40Q�5X   N      HeadersVisibility$ Column=�5Y         CanUserResizeRows .     ColumnHeaderStyle 5Z      �� 5[      =  7System.Windows.Controls.Primitives.DataGridColumnHeader"	  6$   .�5\   "   �� 
 � ��
 �����6?   �� 5]   "   
 � �� �#3A3A3A6?   �� 5^   "   
 � ��$
 Bold"�6?   �� 5_   "   
 � ��$ Center=�6O   �� 5`   "   
 � ��$ 10,10q�6<   �� 5a   "   
 � ��$ 0q�6D   �� 5b   "   
 �@�Cursor$
 Hand��6;    5c   "   P� 5d   &    �@J�IsMouseOver . . �5e   *   �� 
 � �� �#5f5f5f6G   �� 5f   *   
 � �� �#FFFFFF6G   5g   '   5i         Columns 5k      0  *System.Windows.Controls.DataGridTextColumn  5l      ,  &System.Windows.Controls.DataGridColumn   Header$ 이벤트 번호��61      Width5  /System.Windows.Controls.DataGridLengthConverter$	 100 6]   1  +System.Windows.Controls.DataGridBoundColumn    Binding  6A   �� *	EventId+!   ElementStyle! 5m   "   �� 5n   &   "	 ��6,   .�5o   *   �� 
 �@��TextAlignment$ Center=�6J   �� 5p   *   
 � ��$ Center=�6P   �� 5q   *   
 � ��$ Center=�6N   5r   '   5t        5v      $ 날짜��61   $	 160 6x     6=   �� *Date+" �� StringFormat$" yyyy-MM-dd HH:mm:ss��! 5w   "   �� 5x   &   "	 ��6,   .�5y   *   �� 
 �@��TextAlignment$ Center=�6J   �� 5z   *   
 � ��$ Center=�6P   �� 5{   *   
 � ��$ Center=�6N   5|   '   5~        5�      $ 프로그램명��61   $	 150 6`     6@   �� *ProgramName+! 5�   "   �� 5�   &   "	 ��6,   .�5�   *   �� 
 �@��TextAlignment$ Center=�6J   �� 5�   *   
 � ��$ Center=�6P   �� 5�   *   
 � ��$ Center=�6N   5�   '   5�        5�      $ 사유��61   $ * 6X     6=   �� *Reason+! 5�   "   �� 5�   &   "	 ��6,   .�5�   *   �� 
 �@��TextAlignment$ Center=�6J   �� 5�   *   
 � ��$ Center=�6P   �� 5�   *   
 � ��$ Center=�6N   �� 5�   *   
 � ��$
 Wrap=�6I   5�   '   5�      5�      5�      5�      5�      5�      5�      