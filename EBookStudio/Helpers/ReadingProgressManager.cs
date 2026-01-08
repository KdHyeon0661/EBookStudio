using System;
using System.IO;
using System.Text.Json;

namespace EBookStudio.Helpers
{
    public class ReadingProgress
    {
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public DateTime LastReadTime { get; set; } = DateTime.Now;
    }

    public static class ReadingProgressManager
    {
        private static string GetProgressFilePath(string username, string bookFolder)
        {
            return FileHelper.GetLocalFilePath(username, bookFolder, "", "progress.json");
        }

        public static void SaveProgress(string username, string bookFolder, int current, int total)
        {
            try
            {
                var progress = new ReadingProgress
                {
                    CurrentPage = current,
                    TotalPages = total,
                    LastReadTime = DateTime.Now
                };

                string path = GetProgressFilePath(username, bookFolder);

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

        public static ReadingProgress? GetProgress(string username, string bookFolder)
        {
            try
            {
                string path = GetProgressFilePath(username, bookFolder);

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<ReadingProgress>(json);
                }
            }
            catch { }

            return null;
        }

        public static void ResetProgress(string username, string bookFolder)
        {
            try
            {
                string path = GetProgressFilePath(username, bookFolder);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}