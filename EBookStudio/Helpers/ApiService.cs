using EBookStudio.Helpers;
using EBookStudio.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EBookStudio.Services
{
    public class ApiService : IApiService
    {
        private static readonly HttpClient _client = new HttpClient();
        public static string? CurrentToken { get; private set; }

        public async Task<bool> RegisterAsync(string username, string password, string email, string code)
        {
            try
            {
                var data = new { username, password, email, code };
                var res = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/register", data);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/login", new { username, password });
                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result != null && !string.IsNullOrEmpty(result.token))
                    {
                        CurrentToken = result.token;
                        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CurrentToken);
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        public void Logout()
        {
            CurrentToken = null;
            _client.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<bool> SendCodeAsync(string email)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/send_code", new { email });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> VerifyCodeAsync(string email, string code)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/verify_code", new { email, code });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<string?> FindIdAsync(string email)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/find_id", new { email });
                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    if (result != null && result.ContainsKey("username")) return result["username"];
                }
            }
            catch { }
            return null;
        }

        public async Task<bool> ResetPasswordAsync(string email, string code, string newPassword)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/reset_password", new { email, code, new_password = newPassword });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<UploadResult> UploadBookAsync(string filePath, string username)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "file", Path.GetFileName(filePath));
                content.Add(new StringContent(username), "username");

                var response = await _client.PostAsync($"{ApiConfig.BaseUrl}/upload_book", content);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
                    // 표지 파일명 규칙 통일: 책제목.png
                    string finalCover = $"{result?.book_title}.png";

                    return new UploadResult
                    {
                        Success = true,
                        Cover = finalCover,
                        Text = result?.text,
                        Author = result?.author,
                        MusicFiles = result?.music_files ?? new List<string>()
                    };
                }
            }
            catch { }
            return new UploadResult { Success = false };
        }

        public async Task<bool> DownloadFileAsync(string url, string localPath)
        {
            try
            {
                var data = await _client.GetByteArrayAsync(url);
                string? dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(localPath, data);
                return true;
            }
            catch { return false; }
        }

        public async Task<byte[]> DownloadBytesAsync(string url)
        {
            try { return await _client.GetByteArrayAsync(url); }
            catch { return Array.Empty<byte>(); }
        }

        public async Task<List<string>> GetMusicFileListAsync(string username, string bookTitle)
        {
            try
            {
                var res = await _client.GetAsync($"{ApiConfig.BaseUrl}/list_music_files/{username}/{bookTitle}");
                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
                    if (result != null && result.ContainsKey("files")) return result["files"];
                }
            }
            catch { }
            return new List<string>();
        }

        public async Task<List<ServerBook>> GetMyServerBooksAsync(string username)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/my_books", new { });
                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<Dictionary<string, List<ServerBook>>>();
                    if (result != null && result.ContainsKey("books")) return result["books"];
                }
            }
            catch { }
            return new List<ServerBook>();
        }

        public async Task<bool> DeleteServerBookAsync(string bookTitle)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/delete_server_book", new { book_title = bookTitle });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}