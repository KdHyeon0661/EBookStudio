using System.Text.Json.Serialization;

namespace EBookStudio.Models
{
    public sealed class UsageEvent
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; } = string.Empty;

        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("book_id")]
        public string? BookId { get; set; }

        [JsonPropertyName("occurred_at")]
        public long OccurredAt { get; set; }

        [JsonPropertyName("duration_seconds")]
        public int DurationSeconds { get; set; }

        [JsonPropertyName("page_turns")]
        public int PageTurns { get; set; }

        [JsonPropertyName("progress_percent")]
        public int ProgressPercent { get; set; }
    }

    public sealed class UsageBatchResponse
    {
        [JsonPropertyName("received_count")]
        public int ReceivedCount { get; set; }

        [JsonPropertyName("inserted_count")]
        public int InsertedCount { get; set; }
    }

    public sealed class UsageSummaryResponse
    {
        [JsonPropertyName("total_app_seconds")]
        public long TotalAppSeconds { get; set; }

        [JsonPropertyName("total_reading_seconds")]
        public long TotalReadingSeconds { get; set; }

        [JsonPropertyName("reading_session_count")]
        public long ReadingSessionCount { get; set; }

        [JsonPropertyName("page_turn_count")]
        public long PageTurnCount { get; set; }

        [JsonPropertyName("books_read_count")]
        public long BooksReadCount { get; set; }

        [JsonPropertyName("active_day_count")]
        public long ActiveDayCount { get; set; }

        [JsonPropertyName("last_7_days_app_seconds")]
        public long Last7DaysAppSeconds { get; set; }

        [JsonPropertyName("last_active_at")]
        public long? LastActiveAt { get; set; }

        [JsonIgnore]
        public bool IsCached { get; set; }
    }
}
