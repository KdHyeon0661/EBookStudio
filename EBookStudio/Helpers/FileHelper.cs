using System.IO;

namespace EBookStudio.Helpers
{
    public static class FileHelper
    {
        private static string BasePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadCache");

        public static string MusicBasePath => Path.Combine(BasePath, "music");

        public static string UsersBasePath => Path.Combine(BasePath, "users");

        public static string GetCoverFileName(string bookFolderId) => $"{bookFolderId}.png";

        public static string GetLocalFilePath(string? username, string? bookFolderId, string? category, string? fileName)
        {
            string u = username ?? "Guest";
            string b = bookFolderId ?? "UnknownBook";
            string f = fileName ?? "";

            string cat = (category ?? "").ToLower().Trim();

            string finalFolderPath;

            if (cat == "music")
            {
                finalFolderPath = MusicBasePath;
            }
            else
            {
                if (cat == "texts" || cat == "text" || cat == "cover" || cat == "covers")
                {
                    cat = "";
                }

                string userBookDir = Path.Combine(UsersBasePath, u, b);

                finalFolderPath = string.IsNullOrEmpty(cat) ? userBookDir : Path.Combine(userBookDir, cat);
            }

            if (!Directory.Exists(finalFolderPath))
            {
                Directory.CreateDirectory(finalFolderPath);
            }

            if (string.IsNullOrEmpty(f))
            {
                return finalFolderPath;
            }

            return Path.Combine(finalFolderPath, f);
        }

        public static string GetLocalFilePath(string? username, string? bookFolderId, string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return GetLocalFilePath(username, bookFolderId, "", "");

            string finalName = fileName!;
            string category = "";

            string normalized = finalName.Replace("\\", "/");

            if (normalized.StartsWith("music/", StringComparison.OrdinalIgnoreCase))
            {
                category = "music";
                finalName = Path.GetFileName(normalized);
            }
            else
            {
                category = "";
                finalName = Path.GetFileName(normalized);
            }

            return GetLocalFilePath(username, bookFolderId, category, finalName);
        }

        public static string GetLibraryFilePath(string? username)
        {
            string u = username ?? "Guest";
            string dir = Path.Combine(UsersBasePath, u);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "library.json");
        }

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

                string myUserFolder = Path.Combine(UsersBasePath, username);

                if (Directory.Exists(myUserFolder))
                {
                    Directory.Delete(myUserFolder, true);
                }

                Directory.CreateDirectory(myUserFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Reset Error] {ex.Message}");
            }
        }
    }
}