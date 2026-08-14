using System.IO;

namespace EBookStudio.Helpers
{
    public static class FileHelper
    {
        private const string DataRootEnvironmentVariable = "EBOOK_LOCAL_DATA_ROOT";
        private static readonly Lazy<string> BasePathValue = new(InitializeBasePath);

        public static string BasePath => BasePathValue.Value;
        public static string MusicBasePath => Path.Combine(BasePath, "music");
        public static string UsersBasePath => Path.Combine(BasePath, "users");

        public static string GetCoverFileName(string bookFolderId) => $"{bookFolderId}.png";

        public static string GetUserDirectory(string? username)
            => Path.Combine(UsersBasePath, SafeSegment(username, "Guest"));

        public static string GetBookDirectory(string? username, string? bookFolderId)
        {
            if (string.IsNullOrWhiteSpace(bookFolderId)) return string.Empty;
            return Path.Combine(GetUserDirectory(username), SafeSegment(bookFolderId, "UnknownBook"));
        }

        public static string GetLocalFilePath(string? username, string? bookFolderId,
                                              string? category, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(bookFolderId)) return string.Empty;

            string normalizedCategory = (category ?? string.Empty).Trim().ToLowerInvariant();
            string folder;
            if (normalizedCategory == "music")
            {
                folder = MusicBasePath;
            }
            else
            {
                if (normalizedCategory is "texts" or "text" or "cover" or "covers")
                    normalizedCategory = string.Empty;
                string bookDirectory = GetBookDirectory(username, bookFolderId);
                folder = string.IsNullOrEmpty(normalizedCategory)
                    ? bookDirectory
                    : Path.Combine(bookDirectory, SafeSegment(normalizedCategory, "files"));
            }

            if (string.IsNullOrWhiteSpace(fileName)) return folder;
            string normalizedFile = fileName.Replace('\\', '/');
            string safeFileName = Path.GetFileName(normalizedFile);
            if (string.IsNullOrWhiteSpace(safeFileName))
                throw new ArgumentException("A valid file name is required.", nameof(fileName));
            return Path.Combine(folder, SafeSegment(safeFileName, "file"));
        }

        public static string GetLocalFilePath(string? username, string? bookFolderId, string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return GetLocalFilePath(username, bookFolderId, string.Empty, string.Empty);

            string normalized = fileName.Replace('\\', '/');
            string category = normalized.StartsWith("music/", StringComparison.OrdinalIgnoreCase)
                ? "music" : string.Empty;
            return GetLocalFilePath(username, bookFolderId, category, Path.GetFileName(normalized));
        }

        public static string GetLibraryFilePath(string? username)
        {
            string directory = GetUserDirectory(username);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "library.json");
        }

        private static string LastUserFilePath => Path.Combine(BasePath, "last_user.txt");

        public static void SaveLastUser(string username)
        {
            try
            {
                AtomicFile.WriteAllText(LastUserFilePath, username ?? string.Empty);
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Last User Save Error] {error}");
            }
        }

        public static string? GetLastUser()
        {
            try
            {
                return File.Exists(LastUserFilePath)
                    ? File.ReadAllText(LastUserFilePath).Trim()
                    : null;
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Last User Load Error] {error}");
                return null;
            }
        }

        public static void ResetUserData(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            string userFolder = GetUserDirectory(username);
            try
            {
                if (Directory.Exists(userFolder)) Directory.Delete(userFolder, recursive: true);
                Directory.CreateDirectory(userFolder);
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Reset Error] {error}");
                throw new IOException("로컬 사용자 데이터를 초기화하지 못했습니다.", error);
            }
        }

        private static string InitializeBasePath()
        {
            string? configured = Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
            string basePath = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EBookStudio", "DownloadCache")
                : Path.GetFullPath(configured);

            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DownloadCache");
            TryMigrateLegacyCache(legacyPath, basePath);
            Directory.CreateDirectory(basePath);
            return basePath;
        }

        private static void TryMigrateLegacyCache(string legacyPath, string destinationPath)
        {
            try
            {
                if (!Directory.Exists(legacyPath) || Directory.Exists(destinationPath)) return;
                string? parent = Path.GetDirectoryName(destinationPath);
                if (string.IsNullOrWhiteSpace(parent)) return;
                Directory.CreateDirectory(parent);
                string stagingPath = destinationPath + $".migration-{Guid.NewGuid():N}";
                try
                {
                    CopyDirectory(legacyPath, stagingPath);
                    Directory.Move(stagingPath, destinationPath);
                }
                finally
                {
                    if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, recursive: true);
                }
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Cache Migration Error] {error}");
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: false);
            }
        }

        private static string SafeSegment(string? value, string fallback)
        {
            string candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (candidate is "." or "..") candidate = fallback;
            var invalid = Path.GetInvalidFileNameChars();
            var encoded = new System.Text.StringBuilder(candidate.Length);
            foreach (char character in candidate)
            {
                if (character == '%' || character == '/' || character == '\\' || invalid.Contains(character))
                    encoded.Append('%').Append(((int)character).ToString("X4"));
                else
                    encoded.Append(character);
            }
            return encoded.Length == 0 ? fallback : encoded.ToString();
        }
    }
}