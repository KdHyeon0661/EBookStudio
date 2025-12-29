using System;
using System.IO;
using System.Text.Json;

namespace EBookStudio.Helpers
{
    // [모델 통일] 중복된 클래스 제거하고 하나로 통합
    public class ReadingProgress
    {
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public DateTime LastReadTime { get; set; } = DateTime.Now;
    }

    public static class ReadingProgressManager
    {
        // [핵심] FileHelper를 통해 경로 통일
        // 저장 위치: .../DownloadCache/users/아이디/책이름/progress.json
        private static string GetProgressFilePath(string username, string bookTitle)
        {
            // 카테고리 "" (루트) -> 책 폴더 바로 아래 저장
            return FileHelper.GetLocalFilePath(username, bookTitle, "", "progress.json");
        }

        // 저장
        public static void SaveProgress(string username, string bookTitle, int current, int total)
        {
            try
            {
                var progress = new ReadingProgress
                {
                    CurrentPage = current,
                    TotalPages = total,
                    LastReadTime = DateTime.Now
                };

                string path = GetProgressFilePath(username, bookTitle);

                // FileHelper가 폴더는 만들어주지만, 안전을 위해 디렉토리 체크
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(progress, options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Progress Save Error] {ex.Message}");
            }
        }

        // 불러오기
        public static ReadingProgress? GetProgress(string username, string bookTitle)
        {
            try
            {
                string path = GetProgressFilePath(username, bookTitle);

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<ReadingProgress>(json);
                }
            }
            catch { }

            return null; // 저장된 기록 없음
        }

        // [옵션] 초기화 (필요하다면 구현, 여기서는 파일 삭제로 처리)
        public static void ResetProgress(string username, string bookTitle)
        {
            try
            {
                string path = GetProgressFilePath(username, bookTitle);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}