using System;
using System.IO;

namespace EBookStudio.Helpers
{
    public static class FileHelper
    {
        // 1. 최상위 루트: .../DownloadCache
        private static string BasePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadCache");

        // 2. 공용 음악 폴더: .../DownloadCache/music
        public static string MusicBasePath => Path.Combine(BasePath, "music");

        // 3. 사용자 데이터 폴더: .../DownloadCache/users
        public static string UsersBasePath => Path.Combine(BasePath, "users");

        /// <summary>
        /// 로컬 파일의 절대 경로를 반환합니다.
        /// </summary>
        /// <param name="username">사용자 ID</param>
        /// <param name="bookTitle">책 제목</param>
        /// <param name="category">"music"이면 공용 폴더, 그 외("", "texts")는 책 폴더</param>
        /// <param name="fileName">파일명 (확장자 포함)</param>
        /// <returns>파일의 전체 경로</returns>
        public static string GetLocalFilePath(string? username, string? bookTitle, string? category, string? fileName)
        {
            string u = username ?? "Guest";
            string b = bookTitle ?? "UnknownBook";
            string f = fileName ?? "";

            // 카테고리 소문자 변환 및 null 처리
            string cat = (category ?? "").ToLower().Trim();

            string finalFolderPath;

            // =========================================================
            // [핵심 분기] 음악인가? vs 그 외(책 데이터)인가?
            // =========================================================

            if (cat == "music")
            {
                // 🎵 음악 -> 공용 폴더 (DownloadCache/music)
                finalFolderPath = MusicBasePath;
            }
            else
            {
                // 📚 그 외(이미지, 텍스트) -> 사용자 책 폴더 (DownloadCache/users/아이디/책이름)

                // 불필요한 하위 폴더명 제거 ("texts", "cover" -> 루트로 평탄화)
                if (cat == "texts" || cat == "text" || cat == "cover" || cat == "covers")
                {
                    cat = "";
                }

                // 기본 책 폴더 경로
                string userBookDir = Path.Combine(UsersBasePath, u, b);

                // 만약 cat이 남아있다면 하위 폴더 생성 (현재 로직상 거의 ""임)
                finalFolderPath = string.IsNullOrEmpty(cat) ? userBookDir : Path.Combine(userBookDir, cat);
            }

            // 폴더가 없으면 생성 (안전장치)
            if (!Directory.Exists(finalFolderPath))
            {
                Directory.CreateDirectory(finalFolderPath);
            }

            // 파일명이 있으면 결합, 없으면 폴더 경로만 반환
            if (string.IsNullOrEmpty(f))
            {
                return finalFolderPath;
            }

            return Path.Combine(finalFolderPath, f);
        }

        /// <summary>
        /// 오버로딩: 파일명에 경로("music/song.wav")가 포함된 경우 자동 처리
        /// </summary>
        public static string GetLocalFilePath(string? username, string? bookTitle, string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return GetLocalFilePath(username, bookTitle, "", "");

            string finalName = fileName!;
            string category = "";

            // 경로 구분자 통일
            string normalized = finalName.Replace("\\", "/");

            // "music/" 접두사가 있으면 카테고리를 music으로 자동 지정
            if (normalized.StartsWith("music/", StringComparison.OrdinalIgnoreCase))
            {
                category = "music";
                finalName = Path.GetFileName(normalized); // "song.wav"만 추출
            }
            else
            {
                // 그 외 경로는 그냥 루트("")로 취급하고 파일명만 씀
                category = "";
                finalName = Path.GetFileName(normalized);
            }

            return GetLocalFilePath(username, bookTitle, category, finalName);
        }

        // 서재 목록 파일: DownloadCache/users/아이디/library.json
        public static string GetLibraryFilePath(string? username)
        {
            string u = username ?? "Guest";
            string dir = Path.Combine(UsersBasePath, u);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "library.json");
        }

        // 마지막 로그인 정보: DownloadCache/last_user.txt
        private static string LastUserFilePath => Path.Combine(BasePath, "last_user.txt");

        public static void SaveLastUser(string username)
        {
            try
            {
                if (!Directory.Exists(BasePath)) Directory.CreateDirectory(BasePath);
                File.WriteAllText(LastUserFilePath, username ?? "");
            }
            catch { }
        }

        public static string? GetLastUser()
        {
            try
            {
                if (File.Exists(LastUserFilePath))
                    return File.ReadAllText(LastUserFilePath).Trim();
            }
            catch { }
            return null;
        }

        public static void ResetUserData(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username)) return;

                // users 폴더 안의 '내 아이디' 폴더만 타겟팅
                string myUserFolder = Path.Combine(UsersBasePath, username);

                if (Directory.Exists(myUserFolder))
                {
                    Directory.Delete(myUserFolder, true); // 내 폴더만 삭제
                }

                // 폴더 다시 생성 (에러 방지)
                Directory.CreateDirectory(myUserFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Reset Error] {ex.Message}");
            }
        }
    }
}