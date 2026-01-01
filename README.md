# EBookStudio (WPF)

PDF를 업로드하면 **서버가 책을 분석(표지 PNG + 본문 JSON)** 하고, 각 구간(세그먼트)에 맞는 **배경 음악**을 매핑/생성합니다.  
클라이언트는 결과물을 내려받아 **로컬 캐시에 저장**하고, 오프라인으로 책을 읽을 수 있는 **WPF 데스크톱 앱**입니다.

> ⚠️ 이 저장소는 Windows WPF 앱입니다. 실행을 위해 **Windows + .NET 9 SDK**가 필요합니다.

---

## 주요 기능

### 📚 라이브러리(서재)
- PDF 업로드 → 서버 분석 작업 요청(`/upload_book`)
- 분석 결과(표지 PNG / 본문 JSON / 음악 파일) 다운로드 및 로컬 저장
- 로컬 서재 목록(`library.json`) 로드/저장
- 책 삭제(로컬 캐시 삭제 + 목록 갱신)

### 📖 리더(읽기)
- JSON 기반 페이지 렌더링(현재 페이지/총 페이지)
- **세그먼트 단위 배경 음악 자동 전환**
- 재생/일시정지, 볼륨 조절(Windows `MediaPlayer` 기반)
- 마지막 읽은 위치 저장(`progress.json`)

### 📝 노트
- 북마크 / 하이라이트 / 메모 저장 및 관리
- 책별 `notes.json`으로 영구 저장

### ⚙️ 설정
- 글꼴(FontFamily) 선택
- 글자/줄 간격(LineHeight) 조절
- Light / Sepia / Dark 테마 적용

### 👤 계정(서버 연동)
- 회원가입 / 로그인 (JWT 토큰 기반)
- 이메일 인증 코드 발송/검증
  - 현재 서버는 이메일을 실제 발송하지 않고 **서버 콘솔에 코드를 출력**합니다.

### 🗂️ 마이페이지(서버 데이터)
- 서버에 저장된 내 책 목록 조회(`/my_books`)
- 서버 책 삭제(전체/단건) 및 로컬 다운로드(전체/단건)

---

## 기술 스택
- **.NET 9 / WPF**
- **MVVM 패턴**
- `HttpClient` + REST API 통신
- 로컬 캐시/상태 저장: JSON 파일(`System.Text.Json`)
- 비동기 커맨드: `AsyncRelayCommand`(중복 실행 방지 포함)

---

## 빠른 시작(로컬 개발)

### 1) 서버 실행
독서 기능을 제외한 기능은 서버가 먼저 실행되어 있어야 합니다. (기본: `http://127.0.0.1:5000`)

- 서버 프로젝트: `EbookStudioServer` (별도 저장소/폴더)
- 실행 후 `/health` 가 `{"status":"ok"}`를 반환하면 준비 완료

### 2) 클라이언트 실행
#### Visual Studio
1. `EBookStudio.csproj` 열기
2. 시작 프로젝트로 실행(F5)

#### CLI
```bash
dotnet restore
dotnet build
dotnet run
```

---

## 설정(서버 주소 변경)

기본 서버 주소는 아래 파일에 하드코딩되어 있습니다.

- `Models/ApiService.cs`
  - `private static readonly string BaseUrl = "http://127.0.0.1:5000";`

서버를 다른 머신/포트로 띄웠다면 BaseUrl을 수정하세요.

---

## 로컬 캐시(저장 위치/구조)

앱 실행 파일 옆에 `DownloadCache/` 폴더가 생성됩니다.

```text
DownloadCache/
├─ music/                       # 공용(모든 책) 음악 파일 캐시
│  └─ *.wav
└─ users/
   └─ <username>/
      ├─ library.json           # 서재 목록
      └─ <BookTitle>/
         ├─ <BookTitle>.png     # 표지
         ├─ <BookTitle>_full.json
         ├─ notes.json          # 북마크/하이라이트/메모
         └─ progress.json       # 읽기 진행률
```

> 음악은 서버에서 `music/<filename>` 형태로 내려오며, 클라이언트는 이를 `DownloadCache/music/`에 저장해 재사용합니다.

---

## 서버 JSON 포맷(요약)

서버가 생성하는 대표 JSON 구조는 다음과 같습니다.

```json
{
  "book_info": {
    "title": "test_pdf",
    "author": "Unknown Author",
    "cover_path": "/files/<user>/<book>/test_pdf.png",
    "total_chapters": 12
  },
  "chapters": [
    {
      "chapter_index": 1,
      "title": "Chapter 1",
      "segments": [
        {
          "segment_index": 0,
          "emotion": "neutral",
          "music_filename": "xxxx.wav",
          "music_path": "music/xxxx.wav",
          "music_source": "preset",
          "bpm": 90,
          "pages": [
            { "page_index": 0, "text": "...." }
          ]
        }
      ]
    }
  ]
}
```

---

## 트러블슈팅

- 로그인/업로드가 실패한다  
  → 서버 실행 여부 확인 및 `ApiService.BaseUrl` 확인

- 이메일 인증 코드가 안 온다  
  → 서버는 이메일 발송 대신 **콘솔에 인증 코드를 출력**합니다. 서버 터미널 로그에서 코드를 확인하세요.

- 음악이 재생되지 않는다  
  → 해당 음악 파일이 로컬(`DownloadCache/music`)에 존재하는지, 서버의 `/files/.../music/...` 응답이 200인지 확인하세요.

---

## 라이선스
 - 아직 없다네
