using System.Text.Json.Serialization;

namespace EBookStudio.Models
{
    public static class ApiConfig
    {
        public const string BaseUrl = "http://127.0.0.1:5000";
    }

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
    public class UploadResponse
    {
        public string? message { get; set; }
        public string? text { get; set; }
        public string? cover { get; set; }
        public string? author { get; set; }
        public List<string>? music_files { get; set; }

        public string? book_title { get; set; }
        public string? book_folder { get; set; }

        public string? job_id { get; set; }
    }

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

        public string? BookTitle { get; set; }
        public string? BookFolder { get; set; }
    }
    public class ServerBook
    {
        public string title { get; set; } = string.Empty;
        public string folder { get; set; } = string.Empty;
        public string cover_url { get; set; } = string.Empty;
    }
}