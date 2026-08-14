# EBookStudio (WPF)

PDF를 업로드하면 **서버가 책을 분석(표지 PNG + 본문 JSON)** 하고, 각 구간(세그먼트)에 맞는 **배경 음악**을 매핑/생성합니다.  
클라이언트는 결과물을 내려받아 **로컬 캐시에 저장**하고, 오프라인으로 책을 읽을 수 있는 **WPF 데스크톱 앱**입니다.

> ⚠️ 이 저장소는 Windows WPF 앱입니다. 실행을 위해 **Windows + .NET 9 SDK**가 필요합니다.

---

## 주요 기능

### 📚 라이브러리(서재)
- PDF 업로드 → 서버 분석 작업 요청(`/upload_book`)
- 분석 결과(표지 PNG / 본문 JSON / 음악 파일) 다운로드 및 로컬 저장
- 작업 ID를 `library.json`에 먼저 저장해 앱 재시작 후 분석·음악 작업 추적 재개
- 책마다 하나의 상태 폴러만 유지하고 대기·실행 중 작업 취소 지원
- 로컬 서재 목록(`library.json`) 로드/저장
- 책 삭제(로컬 캐시 삭제 + 목록 갱신)

### 📖 리더(읽기)
- JSON 기반 페이지 렌더링(현재 페이지/총 페이지)
- **세그먼트 단위 배경 음악 자동 전환**
- 재생/일시정지, 볼륨 조절(Windows `MediaPlayer` 기반)
- 마지막 읽은 위치 저장(`progress.json`)
- 창이 활성화된 시간만 15초 단위로 집계하고 강제 종료 세션 복구

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
- 요청 제한(`429`/`Retry-After`), 인증 만료, 네트워크 장애를 구분해 안내

### 🗂️ 마이페이지(서버 데이터)
- 서버에 저장된 내 책 목록 조회(`/my_books`)
- 서버 책 삭제(전체/단건) 및 로컬 다운로드(전체/단건)
- 다운로드 완료 즉시 제목·저자·표지 정보를 로컬 보관함에 병합
- 전체 앱 활성 시간, 실제 독서 시간, 세션 수와 읽은 책 수 표시
- 오프라인 활동은 로컬 큐에 저장하고 로그인·온라인 복구 시 멱등 동기화

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
이미 내려받은 책의 독서·노트·진행률 저장은 오프라인으로 동작합니다. 로그인, 업로드,
작업 추적과 사용량 동기화에는 Spring 서버가 필요합니다. 기본 주소는
`http://127.0.0.1:5000`입니다.

```powershell
cd EBookStudioServer-master\spring-server
.\mvnw.cmd spring-boot:run

# 별도 터미널
cd EBookStudioServer-master
python spring_worker.py --role analyze
```

음악 생성까지 실행하려면 전체 워커 의존성을 설치한 뒤 별도 터미널에서
`python spring_worker.py --role music_generation`을 실행합니다. `/health`가
`{"status":"ok"}`를 반환하면 API가 준비된 상태입니다. 전체 구조는 서버 저장소의
`ARCHITECTURE.md`를 참고하십시오.

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

## 설정

기본 서버 주소는 `http://127.0.0.1:5000`입니다. 다른 서버를 사용할 때는 실행 전에
환경변수를 설정합니다.

```powershell
$env:EBOOK_API_BASE_URL='https://api.example.com'
```

로컬 데이터 위치도 필요하면 변경할 수 있습니다.

```powershell
$env:EBOOK_LOCAL_DATA_ROOT='D:\\EBookStudioData'
```

---

## 로컬 캐시(저장 위치/구조)

기본 저장 위치는 `%LOCALAPPDATA%\\EBookStudio\\DownloadCache`입니다. 설치 폴더가
읽기 전용이어도 정상 동작하며, 예전 실행 파일 옆 `DownloadCache`가 있으면 최초
실행 시 새 위치로 안전하게 복사합니다.

```text
%LOCALAPPDATA%/EBookStudio/DownloadCache/
├─ music/                       # 공용(모든 책) 음악 파일 캐시
│  └─ *.wav
└─ users/
   └─ <username>/
      ├─ library.json           # 서재 목록
      ├─ usage_activity.json    # 진행 중/전송 대기 사용량 세션
      ├─ usage_summary.json     # 마지막 서버 집계(오프라인 표시용)
      └─ <bookFolderId>/
         ├─ <BookTitle>.png     # 표지
         ├─ <BookTitle>_full.json
         ├─ notes.json          # 북마크/하이라이트/메모
         └─ progress.json       # 읽기 진행률
```

> 음악은 서버에서 `music/<filename>` 형태로 내려오며, 클라이언트는 이를 공용 `music/` 캐시에 저장해 재사용합니다. JSON과 다운로드 파일은 같은 폴더의 임시 파일에 먼저 기록한 후 완성된 파일만 교체하므로, 종료나 통신 중단으로 기존 파일이 반쯤 덮어써지지 않습니다.

사용량 수집은 원문, 메모, 하이라이트와 페이지별 열람 내역을 전송하지 않습니다.
서버에는 임의 세션 UUID, 책 폴더 ID, 활성 시간, 페이지 이동 수와 최종 진도율만
전송합니다. 각 세션 UUID는 서버에서 한 번만 반영되므로 응답 유실 후 재전송되어도
통계가 중복되지 않습니다. 인터넷 연결과 무관하게 `progress.json`은 계속 로컬에서
동작하며, 사용량 큐만 다음 로그인 시점까지 대기합니다.

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
  → 화면에 표시되는 오류 종류를 확인합니다. 네트워크 오류라면 서버 실행 여부와 `EBOOK_API_BASE_URL`을 확인하세요.

- 요청이 너무 많다는 안내가 나온다  
  → 서버가 반환한 대기 시간이 지난 뒤 다시 시도하세요.

- 로컬 저장 오류가 나온다  
  → `%LOCALAPPDATA%\\EBookStudio`의 저장 공간과 쓰기 권한을 확인하세요.

- 음악이 재생되지 않는다  
  → 해당 음악 파일이 로컬(`DownloadCache/music`)에 존재하는지, 서버의 `/files/.../music/...` 응답이 200인지 확인하세요.

---

## 라이선스

아직 라이선스 파일이 없습니다. 외부 배포 전에 사용·수정·재배포 조건을 명시해야 합니다.
