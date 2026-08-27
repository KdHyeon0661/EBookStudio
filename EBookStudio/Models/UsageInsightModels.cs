using System.Text.Json.Serialization;

namespace EBookStudio.Models
{
    public sealed class UsageBookListResponse
    {
        [JsonPropertyName("books")]
        public List<BookUsageSummaryResponse> Books { get; set; } = new();
    }

    public sealed class BookUsageSummaryResponse
    {
        [JsonPropertyName("book_id")]
        public string BookId { get; set; } = string.Empty;

        [JsonPropertyName("total_reading_seconds")]
        public long TotalReadingSeconds { get; set; }

        [JsonPropertyName("reading_session_count")]
        public long ReadingSessionCount { get; set; }

        [JsonPropertyName("page_turn_count")]
        public long PageTurnCount { get; set; }

        [JsonPropertyName("highest_progress_percent")]
        public int HighestProgressPercent { get; set; }

        [JsonPropertyName("last_read_at")]
        public long LastReadAt { get; set; }
    }

    public sealed class UsageDailySeriesResponse
    {
        [JsonPropertyName("window_days")]
        public int WindowDays { get; set; }

        [JsonPropertyName("timezone")]
        public string Timezone { get; set; } = "UTC";

        [JsonPropertyName("daily")]
        public List<DailyUsageResponse> Daily { get; set; } = new();
    }

    public sealed class DailyUsageResponse
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("app_seconds")]
        public long AppSeconds { get; set; }

        [JsonPropertyName("reading_seconds")]
        public long ReadingSeconds { get; set; }

        [JsonPropertyName("reading_session_count")]
        public long ReadingSessionCount { get; set; }

        [JsonPropertyName("page_turn_count")]
        public long PageTurnCount { get; set; }
    }

    public sealed class UsageDashboard
    {
        public UsageSummaryResponse Summary { get; set; } = new();
        public List<BookUsageSummaryResponse> Books { get; set; } = new();
        public UsageDailySeriesResponse Daily { get; set; } = new();

        [JsonIgnore]
        public bool IsCached { get; set; }
    }
}
