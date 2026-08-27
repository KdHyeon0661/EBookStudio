using EBookStudio.Helpers;
using EBookStudio.Models;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EBookStudio.Services
{
    public class ApiService : IApiService
    {
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(3) };
        private static readonly SemaphoreSlim RefreshLock = new(1, 1);
        public static string? CurrentToken { get; private set; }
        public static string? CurrentRefreshToken { get; private set; }

        public async Task<ApiResult> RegisterAsync(string username, string password, string email, string code)
        {
            try
            {
                using var response = await Client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/register",
                    new { username, password, email, code });
                return await ReadActionResponseAsync(response);
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
        }

        public async Task<ApiResult> LoginAsync(string username, string password)
        {
            try
            {
                using var response = await Client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/login",
                    new { username, password });
                var parsed = await ReadJsonResponseAsync<LoginResponse>(response);
                if (!parsed.Success) return ApiResult.Fail(parsed.Error!);
                if (string.IsNullOrWhiteSpace(parsed.Value?.token) ||
                    string.IsNullOrWhiteSpace(parsed.Value.refresh_token))
                    return ApiResult.Fail(InvalidResponse("Login tokens are missing"));
                SetTokens(parsed.Value.token, parsed.Value.refresh_token);
                return ApiResult.Ok();
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
        }

        public async Task LogoutAsync()
        {
            try
            {
                ApiResult access = await EnsureAccessTokenAsync();
                if (access.Success)
                {
                    using var request = AuthorizedRequest(HttpMethod.Post, "/logout");
                    request.Content = JsonContent.Create(new { refresh_token = CurrentRefreshToken });
                    using var response = await Client.SendAsync(request);
                }
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Logout Error] {error}");
            }
            finally { Logout(); }
        }

        public void Logout()
        {
            CurrentToken = null;
            CurrentRefreshToken = null;
        }

        public async Task<CodeSendResult> SendCodeAsync(string email, string purpose)
        {
            try
            {
                using var response = await Client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/send_code",
                    new { email, purpose });
                var parsed = await ReadJsonResponseAsync<CodeSendResponse>(response);
                if (!parsed.Success)
                    return new CodeSendResult
                    {
                        Success = false,
                        Error = parsed.Error,
                        Message = parsed.Error?.UserMessage
                    };
                return new CodeSendResult
                {
                    Success = true,
                    Delivered = parsed.Value?.delivered == true,
                    DevelopmentCode = parsed.Value?.development_code,
                    Message = parsed.Value?.message
                };
            }
            catch (Exception error)
            {
                ApiError apiError = FromException(error);
                return new CodeSendResult { Success = false, Error = apiError, Message = apiError.UserMessage };
            }
        }

        public async Task<ApiResult> VerifyCodeAsync(string email, string code, string purpose)
        {
            try
            {
                using var response = await Client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/verify_code",
                    new { email, code, purpose });
                return await ReadActionResponseAsync(response);
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
        }

        public async Task<ApiResult<string>> FindIdAsync(string email, string code)
        {
            try
            {
                using var response = await Client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/find_id",
                    new { email, code });
                var parsed = await ReadJsonResponseAsync<Dictionary<string, string>>(response);
                if (!parsed.Success) return ApiResult<string>.Fail(parsed.Error!);
                string? username = parsed.Value?.GetValueOrDefault("username");
                return string.IsNullOrWhiteSpace(username)
                    ? ApiResult<string>.Fail(InvalidResponse("Username is missing"))
                    : ApiResult<string>.Ok(username);
            }
            catch (Exception error) { return ApiResult<string>.Fail(FromException(error)); }
        }

        public async Task<ApiResult> ResetPasswordAsync(string email, string code, string newPassword)
        {
            try
            {
                using var response = await Client.PostAsJsonAsync($"{ApiConfig.BaseUrl}/reset_password",
                    new { email, code, new_password = newPassword });
                return await ReadActionResponseAsync(response);
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
        }

        public async Task<ApiResult> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return access;
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Post, "/change_password");
                request.Content = JsonContent.Create(new
                    { current_password = currentPassword, new_password = newPassword });
                using var response = await Client.SendAsync(request);
                return await ReadActionResponseAsync(response);
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
        }

        public async Task<ApiResult> DeleteAccountAsync()
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return access;
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Delete, "/account");
                using var response = await Client.SendAsync(request);
                ApiResult result = await ReadActionResponseAsync(response);
                if (result.Success) Logout();
                return result;
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
        }

        public async Task<UploadResult> UploadBookAsync(string filePath, string username, string requestId)
        {
            try
            {
                ApiResult access = await EnsureAccessTokenAsync();
                if (!access.Success) return UploadFailure(access.Error!);

                using var content = new MultipartFormDataContent();
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "file", Path.GetFileName(filePath));
                content.Add(new StringContent(requestId), "request_id");
                using var request = AuthorizedRequest(HttpMethod.Post, "/upload_book");
                request.Content = content;
                using var response = await Client.SendAsync(request);
                var acceptedResult = await ReadJsonResponseAsync<UploadResponse>(response);
                if (!acceptedResult.Success) return UploadFailure(acceptedResult.Error!);
                UploadResponse? accepted = acceptedResult.Value;
                if (string.IsNullOrWhiteSpace(accepted?.job_id) ||
                    string.IsNullOrWhiteSpace(accepted.book_folder))
                    return UploadFailure(InvalidResponse("Job ID or book folder is missing"));
                return new UploadResult
                {
                    Success = true,
                    JobId = accepted.job_id,
                    BookFolder = accepted.book_folder,
                    Message = accepted.message
                };
            }
            catch (Exception error) { return UploadFailure(FromException(error)); }
        }

        public async Task<ApiResult<JobStatusResponse>> GetJobStatusAsync(string jobId)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<JobStatusResponse>.Fail(access.Error!);
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Get,
                    $"/check_status/{Uri.EscapeDataString(jobId)}");
                using var response = await Client.SendAsync(request);
                return await ReadJsonResponseAsync<JobStatusResponse>(response);
            }
            catch (Exception error) { return ApiResult<JobStatusResponse>.Fail(FromException(error)); }
        }

        public async Task<ApiResult<JobStatusResponse>> CancelJobAsync(string jobId)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<JobStatusResponse>.Fail(access.Error!);
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Delete,
                    $"/jobs/{Uri.EscapeDataString(jobId)}");
                using var response = await Client.SendAsync(request);
                return await ReadJsonResponseAsync<JobStatusResponse>(response);
            }
            catch (Exception error) { return ApiResult<JobStatusResponse>.Fail(FromException(error)); }
        }

        public async Task<ApiResult> DownloadFileAsync(string url, string localPath)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return access;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentToken);
                using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                    return ApiResult.Fail(await CreateErrorAsync(response));
                await using Stream source = await response.Content.ReadAsStreamAsync();
                await AtomicFile.WriteStreamAsync(localPath, source);
                return ApiResult.Ok();
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
        }

        public async Task<ApiResult<byte[]>> DownloadBytesAsync(string url)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<byte[]>.Fail(access.Error!);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentToken);
                using var response = await Client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return ApiResult<byte[]>.Fail(await CreateErrorAsync(response));
                return ApiResult<byte[]>.Ok(await response.Content.ReadAsByteArrayAsync());
            }
            catch (Exception error) { return ApiResult<byte[]>.Fail(FromException(error)); }
        }

        public async Task<ApiResult<List<string>>> GetMusicFileListAsync(string username, string bookFolder)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<List<string>>.Fail(access.Error!);
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Get,
                    $"/list_music_files/{Uri.EscapeDataString(username)}/{Uri.EscapeDataString(bookFolder)}");
                using var response = await Client.SendAsync(request);
                var parsed = await ReadJsonResponseAsync<Dictionary<string, List<string>>>(response);
                if (!parsed.Success) return ApiResult<List<string>>.Fail(parsed.Error!);
                return ApiResult<List<string>>.Ok(parsed.Value?.GetValueOrDefault("files") ?? new());
            }
            catch (Exception error) { return ApiResult<List<string>>.Fail(FromException(error)); }
        }

        public async Task<ApiResult<UsageBatchResponse>> SubmitUsageEventsAsync(List<UsageEvent> events)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<UsageBatchResponse>.Fail(access.Error!);
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Post, "/usage/events");
                request.Content = JsonContent.Create(new { events });
                using var response = await Client.SendAsync(request);
                return await ReadJsonResponseAsync<UsageBatchResponse>(response);
            }
            catch (Exception error) { return ApiResult<UsageBatchResponse>.Fail(FromException(error)); }
        }

        public async Task<ApiResult<UsageSummaryResponse>> GetUsageSummaryAsync()
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<UsageSummaryResponse>.Fail(access.Error!);
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Get, "/usage/summary");
                using var response = await Client.SendAsync(request);
                return await ReadJsonResponseAsync<UsageSummaryResponse>(response);
            }
            catch (Exception error) { return ApiResult<UsageSummaryResponse>.Fail(FromException(error)); }
        }

        public async Task<ApiResult<UsageBookListResponse>> GetUsageBooksAsync()
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<UsageBookListResponse>.Fail(access.Error!);
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Get, "/usage/books");
                using var response = await Client.SendAsync(request);
                return await ReadJsonResponseAsync<UsageBookListResponse>(response);
            }
            catch (Exception error)
            {
                return ApiResult<UsageBookListResponse>.Fail(FromException(error));
            }
        }

        public async Task<ApiResult<UsageDailySeriesResponse>> GetUsageDailyAsync(int days)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<UsageDailySeriesResponse>.Fail(access.Error!);
            if (days < 1 || days > 90)
                return ApiResult<UsageDailySeriesResponse>.Fail(
                    new ApiError(ApiErrorKind.Validation, "Days must be between 1 and 90"));
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Get, $"/usage/daily?days={days}");
                using var response = await Client.SendAsync(request);
                return await ReadJsonResponseAsync<UsageDailySeriesResponse>(response);
            }
            catch (Exception error)
            {
                return ApiResult<UsageDailySeriesResponse>.Fail(FromException(error));
            }
        }

        public async Task<ApiResult<BookProcessingHistoryResponse>> GetBookProcessingHistoryAsync(
            string bookFolder)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<BookProcessingHistoryResponse>.Fail(access.Error!);
            if (string.IsNullOrWhiteSpace(bookFolder))
                return ApiResult<BookProcessingHistoryResponse>.Fail(
                    new ApiError(ApiErrorKind.Validation, "Book folder is required"));
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Get,
                    $"/api/books/{Uri.EscapeDataString(bookFolder)}/processing-history");
                using var response = await Client.SendAsync(request);
                return await ReadJsonResponseAsync<BookProcessingHistoryResponse>(response);
            }
            catch (Exception error)
            {
                return ApiResult<BookProcessingHistoryResponse>.Fail(FromException(error));
            }
        }

        public async Task<ApiResult<BookMusicTracksResponse>> GetBookMusicTracksAsync(string bookFolder)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<BookMusicTracksResponse>.Fail(access.Error!);
            if (string.IsNullOrWhiteSpace(bookFolder))
                return ApiResult<BookMusicTracksResponse>.Fail(
                    new ApiError(ApiErrorKind.Validation, "Book folder is required"));
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Get,
                    $"/api/books/{Uri.EscapeDataString(bookFolder)}/music-tracks");
                using var response = await Client.SendAsync(request);
                return await ReadJsonResponseAsync<BookMusicTracksResponse>(response);
            }
            catch (Exception error)
            {
                return ApiResult<BookMusicTracksResponse>.Fail(FromException(error));
            }
        }

        public async Task<ApiResult<List<ServerBook>>> GetMyServerBooksAsync(string username)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return ApiResult<List<ServerBook>>.Fail(access.Error!);
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Post, "/my_books");
                request.Content = JsonContent.Create(new { });
                using var response = await Client.SendAsync(request);
                var parsed = await ReadJsonResponseAsync<Dictionary<string, List<ServerBook>>>(response);
                if (!parsed.Success) return ApiResult<List<ServerBook>>.Fail(parsed.Error!);
                return ApiResult<List<ServerBook>>.Ok(parsed.Value?.GetValueOrDefault("books") ?? new());
            }
            catch (Exception error) { return ApiResult<List<ServerBook>>.Fail(FromException(error)); }
        }

        public async Task<ApiResult> DeleteServerBookAsync(string bookFolder)
        {
            ApiResult access = await EnsureAccessTokenAsync();
            if (!access.Success) return access;
            try
            {
                using var request = AuthorizedRequest(HttpMethod.Post, "/delete_server_book");
                request.Content = JsonContent.Create(new { book_folder = bookFolder });
                using var response = await Client.SendAsync(request);
                return await ReadActionResponseAsync(response);
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
        }

        private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, $"{ApiConfig.BaseUrl}{path}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentToken);
            return request;
        }

        private static void SetTokens(string accessToken, string refreshToken)
        {
            CurrentToken = accessToken;
            CurrentRefreshToken = refreshToken;
        }

        private static async Task<ApiResult> EnsureAccessTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentToken))
                return ApiResult.Fail(new ApiError(ApiErrorKind.Authentication, "Access token is missing"));
            if (TokenValidFor(CurrentToken, TimeSpan.FromMinutes(1))) return ApiResult.Ok();
            if (string.IsNullOrWhiteSpace(CurrentRefreshToken))
                return ApiResult.Fail(new ApiError(ApiErrorKind.Authentication, "Refresh token is missing"));

            await RefreshLock.WaitAsync();
            try
            {
                if (TokenValidFor(CurrentToken, TimeSpan.FromMinutes(1))) return ApiResult.Ok();
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiConfig.BaseUrl}/refresh");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentRefreshToken);
                using var response = await Client.SendAsync(request);
                var parsed = await ReadJsonResponseAsync<LoginResponse>(response);
                if (!parsed.Success)
                {
                    if (parsed.Error?.Kind is ApiErrorKind.Authentication or ApiErrorKind.Validation)
                        LogoutStatic();
                    return ApiResult.Fail(parsed.Error!);
                }
                if (string.IsNullOrWhiteSpace(parsed.Value?.token) ||
                    string.IsNullOrWhiteSpace(parsed.Value.refresh_token))
                {
                    LogoutStatic();
                    return ApiResult.Fail(InvalidResponse("Refresh tokens are missing"));
                }
                SetTokens(parsed.Value.token, parsed.Value.refresh_token);
                return ApiResult.Ok();
            }
            catch (Exception error) { return ApiResult.Fail(FromException(error)); }
            finally { RefreshLock.Release(); }
        }

        private static async Task<ApiResult> ReadActionResponseAsync(HttpResponseMessage response)
        {
            return response.IsSuccessStatusCode
                ? ApiResult.Ok()
                : ApiResult.Fail(await CreateErrorAsync(response));
        }

        private static async Task<ApiResult<T>> ReadJsonResponseAsync<T>(HttpResponseMessage response)
            where T : class
        {
            if (!response.IsSuccessStatusCode)
                return ApiResult<T>.Fail(await CreateErrorAsync(response));
            try
            {
                T? value = await response.Content.ReadFromJsonAsync<T>();
                return value == null
                    ? ApiResult<T>.Fail(InvalidResponse("Response body is empty"))
                    : ApiResult<T>.Ok(value);
            }
            catch (JsonException error)
            {
                return ApiResult<T>.Fail(InvalidResponse(error.Message));
            }
            catch (NotSupportedException error)
            {
                return ApiResult<T>.Fail(InvalidResponse(error.Message));
            }
        }

        private static async Task<ApiError> CreateErrorAsync(HttpResponseMessage response)
        {
            string message = response.ReasonPhrase ?? "Request failed";
            try
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using JsonDocument document = JsonDocument.Parse(body);
                    if (document.RootElement.TryGetProperty("message", out JsonElement property) &&
                        property.ValueKind == JsonValueKind.String)
                        message = property.GetString() ?? message;
                }
            }
            catch (Exception error) when (error is JsonException or IOException or NotSupportedException)
            {
                System.Diagnostics.Debug.WriteLine($"[API Error Parse] {error.Message}");
            }

            ApiErrorKind kind = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => ApiErrorKind.Validation,
                HttpStatusCode.Unauthorized => ApiErrorKind.Authentication,
                HttpStatusCode.Forbidden => ApiErrorKind.Forbidden,
                HttpStatusCode.NotFound => ApiErrorKind.NotFound,
                HttpStatusCode.Conflict => ApiErrorKind.Conflict,
                HttpStatusCode.RequestTimeout => ApiErrorKind.Timeout,
                HttpStatusCode.TooManyRequests => ApiErrorKind.RateLimited,
                >= HttpStatusCode.InternalServerError => ApiErrorKind.Server,
                _ => ApiErrorKind.Unknown
            };

            int? retryAfter = null;
            if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
                retryAfter = Math.Max(1, (int)Math.Ceiling(delta.TotalSeconds));
            else if (response.Headers.RetryAfter?.Date is DateTimeOffset date)
                retryAfter = Math.Max(1, (int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds));

            return new ApiError(kind, message, response.StatusCode, retryAfter);
        }

        private static ApiError FromException(Exception error)
        {
            return error switch
            {
                TaskCanceledException => new ApiError(ApiErrorKind.Timeout, error.Message),
                HttpRequestException => new ApiError(ApiErrorKind.Network, error.Message),
                JsonException => InvalidResponse(error.Message),
                IOException => new ApiError(ApiErrorKind.LocalStorage, error.Message),
                UnauthorizedAccessException => new ApiError(ApiErrorKind.LocalStorage, error.Message),
                _ => new ApiError(ApiErrorKind.Unknown, error.Message)
            };
        }

        private static ApiError InvalidResponse(string message)
            => new(ApiErrorKind.InvalidResponse, message);

        private static UploadResult UploadFailure(ApiError error, string? jobId = null, string? message = null)
            => new() { Success = false, JobId = jobId, Error = error, Message = message ?? error.UserMessage };

        private static void LogoutStatic()
        {
            CurrentToken = null;
            CurrentRefreshToken = null;
        }

        private static bool TokenValidFor(string? token, TimeSpan minimumRemaining)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            try
            {
                string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
                long expiration = document.RootElement.GetProperty("exp").GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(expiration) - DateTimeOffset.UtcNow > minimumRemaining;
            }
            catch { return false; }
        }
    }
}