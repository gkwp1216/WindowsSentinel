# 📄 발표 대본 Word 변환 가이드

## 방법 1: Pandoc 사용 (추천)

### 설치

```powershell
# Chocolatey로 Pandoc 설치
choco install pandoc

# 또는 직접 다운로드
# https://pandoc.org/installing.html
```

### 변환

```powershell
cd C:\My_Project\WS\Demo_Scripts
pandoc WindowsSentinel_발표대본.md -o WindowsSentinel_발표대본.docx
```

---

## 방법 2: VS Code 확장 프로그램

### 설치

1. VS Code 열기
2. Extensions (Ctrl+Shift+X)
3. "Markdown PDF" 검색 및 설치

### 변환

1. `WindowsSentinel_발표대본.md` 파일 열기
2. Ctrl+Shift+P
3. "Markdown PDF: Export (docx)" 선택

---

## 방법 3: 온라인 변환 (가장 간단)

### 사용 사이트

1. **CloudConvert**: https://cloudconvert.com/md-to-docx
2. **Convertio**: https://convertio.co/kr/md-docx/
3. **Online-Convert**: https://document.online-convert.com/

### 절차

1. 사이트 접속
2. `WindowsSentinel_발표대본.md` 파일 업로드
3. "Convert to DOCX" 클릭
4. 변환된 파일 다운로드

---

## 방법 4: Word에서 직접 열기

### 절차

1. Microsoft Word 실행
2. 파일 → 열기
3. `WindowsSentinel_발표대본.md` 선택
4. 파일 형식: "All Files (_._)" 선택
5. 열기
6. 파일 → 다른 이름으로 저장
7. 형식: "Word 문서 (\*.docx)" 선택

**⚠️ 주의**: 이 방법은 서식이 제대로 적용되지 않을 수 있습니다.

---

## 📝 Word 문서 서식 수동 조정 (필요 시)

### 제목 서식

- **메인 제목**: 24pt, 굵게, 파란색
- **섹션 제목 (##)**: 16pt, 굵게, 진한 파란색
- **하위 제목 (###)**: 14pt, 굵게, 파란색

### 본문 서식

- **일반 텍스트**: 11pt, 맑은 고딕
- **인용문 (>)**: 기울임꼴, 회색
- **리스트**: 들여쓰기 + 글머리 기호

### 여백

- 상하좌우: 2.5cm (72pt)

---

## 🚀 빠른 변환 (추천)

**가장 빠른 방법: 온라인 변환**

1. https://cloudconvert.com/md-to-docx 접속
2. `WindowsSentinel_발표대본.md` 드래그 앤 드롭
3. 변환 완료 후 다운로드
4. Word에서 열어 최종 검토

**소요 시간**: 약 1분

---

## 📞 문제 해결

### Q: Pandoc 설치가 안 됨

**A**: Chocolatey 먼저 설치

```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
```

### Q: 한글이 깨짐

**A**: 파일 인코딩을 UTF-8 BOM으로 저장

```powershell
$content = Get-Content "WindowsSentinel_발표대본.md" -Raw
$content | Out-File "WindowsSentinel_발표대본.md" -Encoding UTF8
```

### Q: 서식이 엉망임

**A**: 온라인 변환 사용 또는 Word에서 수동 조정

---

## ✅ 최종 확인사항

변환 후 반드시 확인:

- [ ] 제목 서식 정상
- [ ] 본문 들여쓰기 정상
- [ ] 한글 깨짐 없음
- [ ] 이모지 정상 표시
- [ ] 페이지 번호 삽입
- [ ] 목차 추가 (선택사항)

---

**작성일**: 2025년 10월 19일  
**파일**: WindowsSentinel\_발표대본.md → .docx
