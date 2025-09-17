# 코딩 스타일 가이드라인 & 개선사항

## 📋 **프로젝트 개요**

- **프로젝트명**: WindowsSentinel (LogCheck)
- **타입**: WPF .NET 8 보안 모니터링 애플리케이션
- **작성일**: 2025년 9월 17일
- **목적**: 코딩 스타일 통일 및 프로젝트 품질 향상

---

## 🎯 **코딩 스타일 통일 계획**

### **1. 네임스페이스 및 클래스 구조 정리**

#### 📌 **현재 문제점**

- `LogCheck` 네임스페이스 내 일부 `WindowsSentinel` 잔존 참조
- 모델 클래스 중복 정의 (`ProgramInfo` 등)
- 서비스 클래스 네이밍 불일치

#### 🔧 **개선 방안**

```
LogCheck                          // 메인 네임스페이스
├── Models                        // 데이터 모델
├── Services                      // 비즈니스 로직
├── ViewModels                    // MVVM 뷰모델
├── Views                         // UI 페이지
├── Converters                    // 데이터 바인딩 컨버터
├── Controls                      // 사용자 정의 컨트롤
└── Utils                         // 유틸리티 클래스
```

### **2. 파일 구조 및 명명 규칙**

#### 📌 **현재 문제점**

- 단일 파일 과밀화 (NetWorks.xaml.cs 2.8K+ 라인)
- 일관되지 않은 파일명 (`NetWorks_New.xaml` vs `ProgramsList.xaml`)

#### 🔧 **개선 방안**

```
Views/
├── NetworkMonitoring/
│   ├── NetworkMonitoringView.xaml
│   ├── NetworkConnectionsView.xaml
│   ├── SecurityAlertsView.xaml
│   └── NetworkStatisticsView.xaml
├── SecurityAnalysis/
│   ├── VaccineView.xaml
│   └── ProgramsListView.xaml
└── SystemRecovery/
    └── RecoveryView.xaml
```

### **3. 네이밍 컨벤션**

#### 📌 **표준 규칙**

```csharp
// 클래스명: PascalCase
public class NetworkSecurityAnalyzer { }

// 메서드명: PascalCase + 동사형
public async Task<List<ProcessInfo>> GetProcessNetworkDataAsync() { }

// 프로퍼티: PascalCase
public string ProcessName { get; set; }

// 필드명: camelCase + underscore prefix (private)
private readonly SecurityAnalyzer _securityAnalyzer;

// 상수: UPPER_SNAKE_CASE
private const int MAX_RETRY_COUNT = 3;

// 이벤트: PascalCase + 동사 + 명사
public event EventHandler<SecurityAlert> SecurityAlertGenerated;
```

### **4. 메서드 구조 패턴**

#### 📌 **표준 비동기 메서드 패턴**

```csharp
public async Task<T> MethodNameAsync(parameters)
{
    try
    {
        // 1. 입력 검증
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));

        // 2. 주요 로직
        var result = await SomeAsyncOperation();

        // 3. 결과 반환
        return result;
    }
    catch (Exception ex)
    {
        // 4. 예외 처리 및 로깅
        _logger.LogError(ex, "Error in {MethodName}", nameof(MethodNameAsync));
        throw;
    }
}
```

### **5. XAML 스타일 통일**

#### 📌 **현재 문제점**

- 사이드바 중복 구현
- 스타일 정의 분산
- 색상 하드코딩

#### 🔧 **통일 방안**

```xml
<!-- App.xaml에 전역 스타일 정의 -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <!-- 테마 리소스 -->
            <ResourceDictionary Source="Themes/LightTheme.xaml"/>
            <!-- 공통 스타일 -->
            <ResourceDictionary Source="Styles/CommonStyles.xaml"/>
            <!-- 컨트롤 스타일 -->
            <ResourceDictionary Source="Styles/DataGridStyles.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>

<!-- 표준 사이드바 UserControl -->
<UserControl x:Class="LogCheck.Controls.NavigationSidebar">
    <!-- 공통 네비게이션 구조 -->
</UserControl>
```

### **6. 에러 처리 및 로깅 통일**

#### 📌 **현재 문제점**

- MessageBox 남용
- 일관되지 않은 예외 처리
- 로깅 시스템 부재

#### 🔧 **통일된 패턴**

```csharp
// 통합 에러 처리 서비스
public interface IErrorHandlingService
{
    Task HandleErrorAsync(Exception ex, string operation);
    void ShowToast(string message, ToastType type);
    Task<bool> ShowConfirmationAsync(string message);
}

// 사용 예시
try
{
    await PerformOperation();
}
catch (Exception ex)
{
    await _errorHandler.HandleErrorAsync(ex, nameof(PerformOperation));
}
```

### **7. MVVM 패턴 도입**

#### 📌 **현재 문제점**

- Code-behind에 비즈니스 로직 집중
- 데이터 바인딩 미활용

#### 🔧 **개선 방안**

```csharp
// 베이스 ViewModel
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

// 네트워크 모니터링 ViewModel 예시
public class NetworkMonitoringViewModel : ViewModelBase
{
    private int _activeConnections;
    public int ActiveConnections
    {
        get => _activeConnections;
        set => SetProperty(ref _activeConnections, value);
    }

    public ICommand StartMonitoringCommand { get; }
    public ICommand StopMonitoringCommand { get; }
}
```

---

## 🚀 **우선순위별 개선사항**

### **🔴 긴급 (1-2일)**

1. **OnHubMonitoringStateChanged 예외 수정**

   - `NetWorks_New.xaml.cs` 232번째 줄 예외 처리
   - Null 참조 및 UI 스레드 안전성 보장

2. **System Idle Process 화이트리스트 처리**
   - PID 0 프로세스 예외 처리 로직 추가
   - 분석에서 제외하되 UI에서 숨김 처리

### **🟠 높음 (3-5일)**

3. **같은 PID 그룹화 표시**

   ```csharp
   // CollectionView에서 그룹화
   var view = CollectionViewSource.GetDefaultView(_generalProcessData);
   view.GroupDescriptions.Add(new PropertyGroupDescription("ProcessId"));
   ```

4. **시스템/일반 프로세스 차별화**

   - 시스템 프로세스 색상 구분 (빨간색 계열)
   - 추가 경고 표시 및 별도 정렬

5. **위험도 표시 개선**
   ```csharp
   public class RiskLevelToBackgroundConverter : IValueConverter
   {
       public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
       {
           if (value is SecurityRiskLevel risk)
           {
               return risk switch
               {
                   SecurityRiskLevel.Low => new SolidColorBrush(Colors.LightGreen),
                   SecurityRiskLevel.Medium => new SolidColorBrush(Colors.Orange),
                   SecurityRiskLevel.High => new SolidColorBrush(Colors.Red),
                   _ => new SolidColorBrush(Colors.Gray)
               };
           }
           return new SolidColorBrush(Colors.Gray);
       }
   }
   ```

### **🟡 보통 (1주일)**

6. **차트 데이터 개선**
   - 연결 수 대신 데이터 전송량 기반으로 변경
7. **MessageBox를 Toast/Snackbar로 교체**

   - `BlockConnection_Click`, `TerminateProcess_Click` 수정
   - 비침습적 알림 시스템 구현

8. **프로세스 아이콘 표시**
   - 실행 파일 아이콘 추출 및 DataGrid에 표시

### **🟢 낮음 (2-3주일)**

9. **자동 차단 시스템 구현**
10. **차단된 네트워크 관리 UI**
11. **악성코드 탐지 강화**
12. **보안 경고 팝업 시스템**

---

## 📅 **실행 계획**

### **Phase 1: 기본 구조 정리 (1주)**

1. 네임스페이스 통일 및 중복 제거
2. 파일 구조 재정리
3. 기본 네이밍 컨벤션 적용

### **Phase 2: XAML 및 스타일 통합 (1주)**

1. 공통 UserControl 생성
2. 테마 시스템 개선
3. 스타일 리소스 통합

### **Phase 3: 코드 품질 개선 (2주)**

1. MVVM 패턴 도입
2. 에러 처리 시스템 통합
3. 데이터 바인딩 개선

### **Phase 4: 테스트 및 검증 (1주)**

1. 코드 스타일 검증
2. 기능 테스트
3. 성능 최적화

---

## 🔧 **즉시 적용 가능한 개선사항**

### **.editorconfig 파일**

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
end_of_line = crlf
trim_trailing_whitespace = true
insert_final_newline = true

# Naming conventions
dotnet_naming_rule.private_fields_start_with_underscore.severity = error
dotnet_naming_rule.private_fields_start_with_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_start_with_underscore.style = underscore_prefix

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.underscore_prefix.capitalization = camel_case
dotnet_naming_style.underscore_prefix.required_prefix = _
```

### **설정 관리 통일**

```csharp
public class ApplicationSettings
{
    // UI 설정
    public ThemeType Theme { get; set; } = ThemeType.Light;
    public string Language { get; set; } = "ko-KR";

    // 네트워크 모니터링
    public bool AutoSelectNic { get; set; } = true;
    public string SelectedNicId { get; set; } = string.Empty;
    public string BpfFilter { get; set; } = "tcp or udp or icmp";
    public bool AutoStartMonitoring { get; set; } = false;

    // 보안 분석
    public int SecurityAlertThreshold { get; set; } = 7;
    public bool EnableRealTimeProtection { get; set; } = true;
}
```

---

## 📊 **성과 측정 지표**

### **코드 품질**

- [ ] 네이밍 컨벤션 100% 준수
- [ ] MVVM 패턴 적용률 80% 이상
- [ ] 코드 중복도 20% 이하

### **유지보수성**

- [ ] 단일 파일 라인 수 500라인 이하
- [ ] 메서드별 복잡도 10 이하
- [ ] 테스트 커버리지 60% 이상

### **사용자 경험**

- [ ] 에러 발생률 5% 이하
- [ ] 응답 시간 2초 이하
- [ ] UI 일관성 95% 이상

---

## 📝 **변경 기록**

| 날짜       | 버전 | 변경사항             | 작성자         |
| ---------- | ---- | -------------------- | -------------- |
| 2025-09-17 | v1.0 | 초기 가이드라인 작성 | GitHub Copilot |

---

## 🔗 **참고 자료**

- [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- [WPF Application Quality Guide](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [MVVM Pattern Documentation](https://docs.microsoft.com/en-us/xamarin/xamarin-forms/enterprise-application-patterns/mvvm)
- [Material Design in XAML Guidelines](http://materialdesigninxaml.net/)

---

**📧 문의사항이나 개선 제안은 GitHub Issues를 통해 제출해 주세요.**
