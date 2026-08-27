using System.Text.Json.Serialization;

namespace EBookStudio.Models
{
    public sealed class BookProcessingHistoryResponse
    {
        [JsonPropertyName("book_folder")]
        public string BookFolder { get; set; } = string.Empty;

        [JsonPropertyName("book_status")]
        public string BookStatus { get; set; } = string.Empty;

        [JsonPropertyName("runs")]
        public List<BookProcessingRunResponse> Runs { get; set; } = new();
    }

    public sealed class BookProcessingRunResponse
    {
        [JsonPropertyName("run_id")]
        public long RunId { get; set; }

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; } = string.Empty;

        [JsonPropertyName("process_type")]
        public string ProcessType { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("attempt_count")]
        public int AttemptCount { get; set; }

        [JsonPropertyName("max_attempts")]
        public int MaxAttempts { get; set; }

        [JsonPropertyName("model_version")]
        public string? ModelVersion { get; set; }

        [JsonPropertyName("started_at")]
        public long? StartedAt { get; set; }

        [JsonPropertyName("finished_at")]
        public long? FinishedAt { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public long UpdatedAt { get; set; }

        [JsonPropertyName("artifacts")]
        public List<BookArtifactResponse> Artifacts { get; set; } = new();
    }

    public sealed class BookArtifactResponse
    {
        [JsonPropertyName("artifact_type")]
        public string ArtifactType { get; set; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("checksum")]
        public string? Checksum { get; set; }

        [JsonPropertyName("file_size")]
        public long? FileSize { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }
    }

    public sealed class BookMusicTracksResponse
    {
        [JsonPropertyName("book_folder")]
        public string BookFolder { get; set; } = string.Empty;

        [JsonPropertyName("track_count")]
        public int TrackCount { get; set; }

        [JsonPropertyName("unique_asset_count")]
        public long UniqueAssetCount { get; set; }

        [JsonPropertyName("tracks")]
        public List<BookMusicTrackResponse> Tracks { get; set; } = new();
    }

    public sealed class BookMusicTrackResponse
    {
        [JsonPropertyName("segment_key")]
        public string SegmentKey { get; set; } = string.Empty;

        [JsonPropertyName("binding_type")]
        public string BindingType { get; set; } = string.Empty;

        [JsonPropertyName("processing_run_id")]
        public long? ProcessingRunId { get; set; }

        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;

        [JsonPropertyName("asset_source")]
        public string AssetSource { get; set; } = string.Empty;

        [JsonPropertyName("genre")]
        public string Genre { get; set; } = string.Empty;

        [JsonPropertyName("bpm")]
        public int Bpm { get; set; }

        [JsonPropertyName("model_name")]
        public string ModelName { get; set; } = string.Empty;

        [JsonPropertyName("model_version")]
        public string ModelVersion { get; set; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string? FileName { get; set; }

        [JsonPropertyName("duration_seconds")]
        public int? DurationSeconds { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("reuse_count")]
        public int ReuseCount { get; set; }

        [JsonPropertyName("last_used_at")]
        public long? LastUsedAt { get; set; }
    }
}
