using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EBookStudio.Models
{
    // ==========================================
    // [DTO] 데이터 전송 객체들
    // ==========================================

    public class EpisodeResult
    {
        public string message { get; set; } = string.Empty;
        public List<Segment>? segments { get; set; }
    }

    public class Segment
    {
        public int segment_index { get; set; }
        public string emotion { get; set; } = string.Empty;
        public string music_filename { get; set; } = string.Empty;
        public List<BookPage>? pages { get; set; }
    }

    public class BookPage
    {
        public int page_index { get; set; }
        public string text { get; set; } = string.Empty;
        public bool is_new_segment { get; set; }
    }

    public class UploadResponseDto
    {
        public string? message { get; set; }
        public string? text { get; set; }
        public string? cover { get; set; }
        public string? author { get; set; }
        public List<string>? music_files { get; set; }

        // [수정] 서버에서 새로 보내주는 필드 추가
        public string? book_title { get; set; }
        public string? job_id { get; set; }
    }

    // ==========================================
    // [핵심] ApiService 클래스
    // ==========================================
    public class ApiService
    {
        private static readonly HttpClient _client = new HttpClient();
        public const string BaseUrl = "http://127.0.0.1:5000";

        public static string? CurrentToken { get; private set; }

        public class LoginResponse
        {
            public string? message { get; set; }

            [JsonPropertyName("access_token")]
            public string? token { get; set; }

            [JsonPropertyName("refresh_token")]
            public string? refresh_token { get; set; }
        }

        public class UploadResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public string? Cover { get; set; }
            public string? Text { get; set; }
            public string? Author { get; set; }
            public List<string>? MusicFiles { get; set; }
        }

        public class AuthRequest
        {
            public string? username { get; set; }
            public string? password { get; set; }
            public string? email { get; set; }
            public string? code { get; set; }
        }

        // ==========================================
        // 1. 인증 관련 API
        // ==========================================

        public static async Task<bool> RegisterAsync(string username, string password, string email)
        {
            try
            {
                var data = new AuthRequest { username = username, password = password, email = email };
                var res = await _client.PostAsJsonAsync($"{BaseUrl}/register", data);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                var data = new AuthRequest { username = username, password = password };
                var res = await _client.PostAsJsonAsync($"{BaseUrl}/login", data);

                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result != null && !string.IsNullOrEmpty(result.token))
                    {
                        CurrentToken = result.token;
                        _client.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", CurrentToken);
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        public static void Logout()
        {
            CurrentToken = null;
            _client.DefaultRequestHeaders.Authorization = null;
        }

        public static async Task<bool> SendVerificationCodeAsync(string email) => await SendCodeAsync(email);

        public static async Task<bool> SendCodeAsync(string email)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{BaseUrl}/send_code", new { email });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> VerifyCodeAsync(string email, string code)
        {
            try
            {
                var response = await _client.PostAsJsonAsync($"{BaseUrl}/verify_code", new { email, code });
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<string?> FindIdAsync(string email)
        {
            try
            {
                var response = await _client.PostAsJsonAsync($"{BaseUrl}/find_id", new { email });
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    if (result != null && result.ContainsKey("username")) return result["username"];
                }
                return null;
            }
            catch { return null; }
        }

        public static async Task<bool> ResetPasswordAsync(string email, string code, string newPassword)
        {
            try
            {
                var data = new { email, code, new_password = newPassword };
                var res = await _client.PostAsJsonAsync($"{BaseUrl}/reset_password", data);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ==========================================
        // 2. 책 관리 API (업로드) [중요 수정됨]
        // ==========================================

        public static async Task<UploadResult> UploadBookAsync(string filePath, string username)
        {
            if (string.IsNullOrEmpty(CurrentToken))
                return new UploadResult { Success = false, Message = "로그인이 필요합니다." };

            try
            {
                using (var content = new MultipartFormDataContent())
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        var fileContent = new StreamContent(fileStream);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

                        content.Add(fileContent, "file", Path.GetFileName(filePath));
                        content.Add(new StringContent(username), "username");

                        var response = await _client.PostAsync($"{BaseUrl}/upload_book", content);

                        if (response.IsSuccessStatusCode) // 200 OK or 202 Accepted
                        {
                            var result = await response.Content.ReadFromJsonAsync<UploadResponseDto>();

                            string finalCover = result?.cover;
                            string finalText = result?.text;

                            if (string.IsNullOrEmpty(finalCover) && !string.IsNullOrEmpty(result?.book_title))
                            {
                                finalCover = $"{result.book_title}.png";
                            }
                            if (string.IsNullOrEmpty(finalText) && !string.IsNullOrEmpty(result?.book_title))
                            {
                                finalText = $"{result.book_title}_full.json";
                            }

                            return new UploadResult
                            {
                                Success = true,
                                Cover = finalCover,
                                Text = finalText,
                                Author = result?.author,
                                MusicFiles = result?.music_files ?? new List<string>()
                            };
                        }
                        else
                        {
                            string errorDetails = await response.Content.ReadAsStringAsync();
                            return new UploadResult
                            {
                                Success = false,
                                Message = $"서버 에러 ({response.StatusCode}): {errorDetails}"
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new UploadResult { Success = false, Message = $"연결 실패: {ex.Message}" };
            }
        }

        // ==========================================
        // 3. 책 정보 조회 API
        // ==========================================

        public static async Task<List<string>> GetTocAsync(string filename, string username)
        {
            try
            {
                var data = new { filename, username };
                var res = await _client.PostAsJsonAsync($"{BaseUrl}/get_toc", data);
                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
                    if (result != null && result.ContainsKey("toc")) return result["toc"];
                }
                return new List<string>();
            }
            catch { return new List<string>(); }
        }

        public static async Task<List<Segment>?> GetChapterContentAsync(string filename, string username, int index)
        {
            try
            {
                var data = new { filename, username, index };
                var res = await _client.PostAsJsonAsync($"{BaseUrl}/analyze_episode", data);
                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<EpisodeResult>();
                    return result?.segments;
                }
                return null;
            }
            catch { return null; }
        }

        // ==========================================
        // 4. 파일 다운로드 및 동기화
        // ==========================================

        public static async Task<bool> DownloadFileAsync(string url, string localPath)
        {
            try
            {
                // [신규] 파일이 이미 존재하고 크기가 0이 아니면 스킵 가능 (선택 사항)
                // if (File.Exists(localPath) && new FileInfo(localPath).Length > 0) return true;

                // 인증 헤더 포함하여 다운로드
                var data = await _client.GetByteArrayAsync(url);

                string? dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllBytesAsync(localPath, data);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"다운로드 실패: {url} -> {ex.Message}");
                return false;
            }
        }

        // [추가] 바이트 배열만 받고 싶을 때 사용하는 메서드 (LibraryViewModel 호환용)
        public static async Task<byte[]> DownloadBytesAsync(string url)
        {
            try
            {
                return await _client.GetByteArrayAsync(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"다운로드(Bytes) 실패: {url} -> {ex.Message}");
                return null;
            }
        }

        public static async Task<List<string>> GetMusicFileListAsync(string username, string bookTitle)
        {
            try
            {
                string url = $"{BaseUrl}/list_music_files/{username}/{bookTitle}";
                var response = await _client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, List<string>>>();
                    if (result != null && result.ContainsKey("files")) return result["files"];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetMusicList Error] {ex.Message}");
            }
            return new List<string>();
        }

        public class ServerBookDto
        {
            public string title { get; set; } = string.Empty;
            public string cover_url { get; set; } = string.Empty;
        }

        // 서버에 있는 내 책 목록 가져오기
        public static async Task<List<ServerBookDto>> GetMyServerBooksAsync(string username)
        {
            try
            {
                // POST로 username은 토큰에서 처리되지만, 명시적 호출
                var res = await _client.PostAsJsonAsync($"{BaseUrl}/my_books", new { });
                if (res.IsSuccessStatusCode)
                {
                    var result = await res.Content.ReadFromJsonAsync<Dictionary<string, List<ServerBookDto>>>();
                    if (result != null && result.ContainsKey("books")) return result["books"];
                }
            }
            catch { }
            return new List<ServerBookDto>();
        }

        // 서버 책 삭제 요청
        public static async Task<bool> DeleteServerBookAsync(string bookTitle)
        {
            try
            {
                var res = await _client.PostAsJsonAsync($"{BaseUrl}/delete_server_book", new { book_title = bookTitle });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}