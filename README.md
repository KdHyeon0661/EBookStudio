# EBookStudio (WPF, .NET 9)

Flask 서버(EbookStudioServer)에 로그인 후 PDF를 업로드하면,
서버가 생성한 **표지 PNG / 전체 JSON(_full.json) / 음악 파일**을 내려받아 로컬 캐시에 저장하고,
WPF 리더에서 읽기/서재/마이페이지/설정 등을 제공하는 클라이언트 앱입니다.

---

## 요구 사항

- Windows 10/11
- Visual Studio 2022+
- .NET SDK: `net9.0-windows` (프로젝트 설정)

---

## 실행 방법

1) 서버 실행 (기본)
- `http://127.0.0.1:5000`

2) 클라이언트 실행
- Visual Studio에서 `EBookStudio/EBookStudio.csproj` 열기
- 시작 프로젝트로 실행(F5)

---

## 서버 주소 변경

- `Models/ApiService.cs`

```csharp
public const string BaseUrl = "http://127.0.0.1:5000";
```

원격 서버/포트 변경 시 위 값을 수정하세요.

---

## 로컬 저장 경로(중요)

앱은 실행 파일 옆에 `DownloadCache/` 폴더를 만들고 로컬 데이터를 저장합니다.

- 공용 음악 폴더: `DownloadCache/music/`
- 사용자 책 데이터: `DownloadCache/users/<username>/<bookTitle>/`
- 서재 목록: `DownloadCache/users/<username>/library.json`
- 마지막 로그인: `DownloadCache/last_user.txt`

> `Helpers/FileHelper.cs`에 정의되어 있습니다.

---

## 업로드/동기화 흐름(현재 코드 기준)

- 로그인 성공 시 HttpClient 기본 Authorization(Bearer)을 세팅
- 업로드: `POST /upload_book` (multipart: file=PDF)
  - 서버가 `book_title`만 내려주면,
    클라이언트는 파일명을 추측해
    - `<book_title>.png`
    - `<book_title>_full.json`
    형태로 로컬 저장/다운로드를 시도합니다.
- 음악 파일 목록:
  - `GET /list_music_files/<username>/<book_title>`
  - 이후 `/files/<username>/<book_title>/music/<filename>` 다운로드

---

## 프로젝트 구조

```
EBookStudio/
  Views/        # XAML UI
  ViewModels/   # MVVM ViewModel
  Models/       # DTO + ApiService(HttpClient)
  Helpers/      # FileHelper, RelayCommand, Converter, Settings 등
```

---

## 참고(주의)

- `ApiService.GetChapterContentAsync()`는 `/analyze_episode`를 호출하도록 되어 있으나,
  서버 코드(server.py)에는 해당 엔드포인트가 존재하지 않습니다.
  (현재는 `/sync_library`로 전체 JSON을 가져오는 흐름이 핵심입니다.)
