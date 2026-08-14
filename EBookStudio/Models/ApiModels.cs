using System.Net;
using System.Text.Json.Serialization;

namespace EBookStudio.Models
{
    public static class ApiConfig
    {
        public static string BaseUrl { get; } =
            (Environment.GetEnvironmentVariable("EBOOK_API_BASE_URL") ?? "http://127.0.0.1:5000")
            .TrimEnd('/');
    }

    public enum ApiErrorKind
    {
        Validation,
        Authentication,
        Forbidden,
        NotFound,
        Conflict,
        RateLimited,
        Network,
        Timeout,
        Server,
        InvalidResponse,
        LocalStorage,
        Unknown
    }

    public sealed record ApiError(ApiErrorKind Kind, string Message,
                                  HttpStatusCode? StatusCode = null,
                                  int? RetryAfterSeconds = null)
    {
        public string UserMessage => Kind switch
        {
            ApiErrorKind.RateLimited when RetryAfterSeconds is > 0
                => $"요청이 너무 많습니다. {RetryAfterSeconds}초 후 다시 시도해주세요.",
            ApiErrorKind.RateLimited => "요청이 너무 많습니다. 잠시 후 다시 시도해주세요.",
            ApiErrorKind.Network => "서버에 연결할 수 없습니다. 네트워크 연결을 확인해주세요.",
            ApiErrorKind.Timeout => "서버 응답 시간이 초과되었습니다. 잠시 후 다시 시도해주세요.",
            ApiErrorKind.Authentication => "로그인이 만료되었거나 인증 정보가 올바르지 않습니다.",
            ApiErrorKind.Forbidden => "이 작업을 수행할 권한이 없습니다.",
            ApiErrorKind.Server => "서버에서 오류가 발생했습니다. 잠시 후 다시 시도해주세요.",
            ApiErrorKind.InvalidResponse => "서버 응답 형식이 올바르지 않습니다.",
            ApiErrorKind.LocalStorage => "파일을 안전하게 저장하지 못했습니다. 저장 공간과 권한을 확인해주세요.",
            _ => string.IsNullOrWhiteSpace(Message) ? "요청을 처리하지 못했습니다." : Message
        };
    }

    public sealed record ApiResult(bool Success, ApiError? Error = null)
    {
        public static ApiResult Ok() => new(true);
        public static ApiResult Fail(ApiError error) => new(false, error);
    }

    public sealed record ApiResult<T>(bool Success, T? Value = null, ApiError? Error = null)
        where T : class
    {
        public static ApiResult<T> Ok(T value) => new(true, value);
        public static ApiResult<T> Fail(ApiError error) => new(false, null, error);
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

    public class UploadResponse
    {
        public string? message { get; set; }
        public string? book_folder { get; set; }
        public string? job_id { get; set; }
        public string? status { get; set; }
    }

    public class JobStatusResponse
    {
        public string? job_id { get; set; }
        public string? type { get; set; }
        public string? book_id { get; set; }
        public string? status { get; set; }
        public long created_at { get; set; }
        public long? started_at { get; set; }
        public long? finished_at { get; set; }
        public string? error { get; set; }
        public int attempt_count { get; set; }
        public int max_attempts { get; set; }
        public long? cancel_requested_at { get; set; }
        public JobResultResponse? result { get; set; }
    }

    public class JobResultResponse
    {
        public string? book_folder { get; set; }
        public string? book_title { get; set; }
        public string? text { get; set; }
        public string? cover { get; set; }
        public string? author { get; set; }
        public string? music_job_id { get; set; }
    }

    public class LoginResponse
    {
        [JsonPropertyName("access_token")]
        public string? token { get; set; }
        [JsonPropertyName("refresh_token")]
        public string? refresh_token { get; set; }
        public string? username { get; set; }
        public string? public_id { get; set; }
    }

    public class CodeSendResponse
    {
        public string? message { get; set; }
        public bool delivered { get; set; }
        public string? development_code { get; set; }
    }

    public class CodeSendResult
    {
        public bool Success { get; set; }
        public bool Delivered { get; set; }
        public string? DevelopmentCode { get; set; }
        public string? Message { get; set; }
        public ApiError? Error { get; set; }
    }

    public class UploadResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Cover { get; set; }
        public string? Text { get; set; }
        public string? Author { get; set; }
        public string? BookTitle { get; set; }
        public string? BookFolder { get; set; }
        public string? JobId { get; set; }
        public string? MusicJobId { get; set; }
        public ApiError? Error { get; set; }
    }

    public class ServerBook
    {
        public string title { get; set; } = string.Empty;
        public string folder { get; set; } = string.Empty;
        public string cover_url { get; set; } = string.Empty;
        public string cover_file { get; set; } = string.Empty;
        public string text_file { get; set; } = string.Empty;
        public string author { get; set; } = string.Empty;
    }
}