   M S B A M L   `   `   `  ����C  ?LogCheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null1clr-namespace:WindowsSentinelWindowsSentinel  5   	        WindowsSentinel.MainWindow   5   	   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   /localclr-namespace:WindowsSentinel     5   	     � Title$  Windows Sentinel��5   	   $	��600��6"   $
��1200��6/   .��5	      � -   5         �Name$ MainGrid��5	       � RowDefinitions 5
   
   �� 5      $
��Auto �6   �� 5      $��* �6   .+�5   
   �� $��0��6   $��10q�6"   .+�5      ��  �� Orientation$ 
Horizontal=�6   $��0, 0, 0, 10q�62   .+�5      	� -   5         	�Name$ first��5      $��Defender��6-    	� 	GroupName$ SecurityFeatures��6@   �� 5      -   5         ��Name$ firstLogMessage��5      
���  ��$��10,0,0,0q�6F   $��Center=�6X   5      �� 5      $ 
Horizontal=�6   $��0, 0, 0, 10q�62   .+�5      	� -   5        $ seconds��5      $��Firewall��6/   $ SecurityFeatures��6B   �� 5      -   5        $ secondsLogMessage��5      
���  ��$��10,0,0,0q�6H   $��Center=�6Z   5      �� 5      $ 
Horizontal=�6   $��0, 0, 0, 10q�62   .+�5      	� -   5        $ thirds��5      $��	BitLocker��6.   $ SecurityFeatures��6B   �� 5      -   5        $ thirdsLogMessage��5      
���  ��$��10,0,0,0q�6G   $��Center=�6Y   5       �� 5"      -   5         ��Name$ btnCollectPrograms��5"      $#��설치된 프로그램 검사��61   $
��10,5q�5#   6   5$      &   System.Windows.Controls.DataGrid  5&   
   -	   5          Name$ programDataGrid��5&      $��1��6,   $��10,10,10,34q�69   	   AutoGenerateColumns	 .  
   
IsReadOnly
 .    AlternatingRowBackground �#F5F5F5   CanUserResizeColumns .    CanUserSortColumns .    GridLinesVisibility$	 All=�5+         HorizontalGridLinesBrush �#E0E0E0   VerticalGridLinesBrush �#E0E0E0   	RowHeight$ 30Q�5.      "   VerticalScrollBarVisibility$
 Auto=�5/      $   HorizontalScrollBarVisibility$
 Auto=�50         Columns 51      0  *System.Windows.Controls.DataGridTextColumn  52      ,  &System.Windows.Controls.DataGridColumn   Header$ 프로그램 이름��6%      Width5  /System.Windows.Controls.DataGridLengthConverter$	 200 6O   1  +System.Windows.Controls.DataGridBoundColumn   Binding 66   �� *Name+6O     53      $ 설치 날짜��6%   $	 100 6m    64   �� *InstallDate+ �� StringFormat$ 
yyyy-MM-dd��6m     54      $ 버전��6%   $ 80 6M    61   �� *	Version+6M     55      $ 	제조사��6%   $	 150 6P    62   �� *	Publisher+6P     56      $ 설치 위치��6%   $	 200 6T    64   �� *InstallPath+6T     57      $ 보안 수준��6%   $ 80 6V    64   �� *SecurityLevel+   ElementStyle 58      �� 59       �� 
TargetType" ��6    .�5:      ��  �� Property � �� �� Value$ NoWrap=�6=   �� 5;       � ��$ CharacterEllipsis=�6=    �� Triggers 5<      �� 5=   "    �� Binding 6.   �� *SecurityLevel+ �� Value
 높음6P   .a�5>   &   ��  � ��
 �  ��6C   5?   #   �� 5@   "    6.   �� *SecurityLevel+
 중간6P   .a�5A   &   ��  � ��
 � ���6C   5B   #   �� 5C   "    6.   �� *SecurityLevel+
 낮음6P   .a�5D   &   ��  � ��
 � � �6C   5E   #   5G      5I        5J      $ 보안 상세��6%   $	 200 6X    64   �� *SecurityDetails+ 5K      �� 5L      " ��6    .�5M      ��  � ��$
 Wrap=�6=   �� 5N       � ��$ 60��6:   �� 5O       � ��$ Center=�6B   5P      5R      5T      5U      5V      