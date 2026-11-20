using EBookStudio.Models;
using System.IO;
using System.Text.Json;

namespace EBookStudio.Helpers
{
    public static class UsageActivityStore
    {
        private const int MaxOutboxEvents = 2000;
        private static readonly object Gate = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static void RecoverInterruptedSessions(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            TryEdit(username, state =>
            {
                foreach (UsageEvent pending in state.Pending.Where(x => x.DurationSeconds > 0))
                    AddToOutbox(state, pending);
                bool changed = state.Pending.Count > 0;
                state.Pending.Clear();
                return changed;
            });
        }

        public static UsageSession? StartSession(string username, string eventType, string? bookId = null)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            if (eventType is not ("app_session" or "reading_session"))
                throw new ArgumentException("Unsupported usage event type", nameof(eventType));
            if (eventType == "reading_session" && string.IsNullOrWhiteSpace(bookId)) return null;

            var usageEvent = new UsageEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                BookId = eventType == "reading_session" ? bookId : null,
                OccurredAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            bool saved = TryEdit(username, state =>
            {
                state.Pending.Add(usageEvent);
                return true;
            });
            return saved ? new UsageSession(username, usageEvent.EventId) : null;
        }

        public static List<UsageEvent> GetOutboxBatch(string username, int maximum = 100)
        {
            if (string.IsNullOrWhiteSpace(username)) return new();
            lock (Gate)
                return Load(username).Outbox.Take(Math.Clamp(maximum, 1, 100)).Select(Clone).ToList();
        }

        public static void Acknowledge(string username, IEnumerable<string> eventIds)
        {
            var accepted = eventIds.ToHashSet(StringComparer.Ordinal);
            if (accepted.Count == 0 || string.IsNullOrWhiteSpace(username)) return;
            lock (Gate)
            {
                UsageState state = Load(username);
                state.Outbox.RemoveAll(x => accepted.Contains(x.EventId));
                Save(username, state);
            }
        }

        internal static void AddActiveSeconds(string username, string eventId, int seconds,
                                              int currentPage = 0, int totalPages = 0)
        {
            if (seconds <= 0) return;
            UpdatePending(username, eventId, usageEvent =>
            {
                usageEvent.DurationSeconds = Math.Min(86_400, usageEvent.DurationSeconds + seconds);
                UpdateProgress(usageEvent, currentPage, totalPages);
            });
        }

        internal static void RecordPageChange(string username, string eventId,
                                              int currentPage, int totalPages)
        {
            UpdatePending(username, eventId, usageEvent =>
            {
                usageEvent.PageTurns = Math.Min(100_000, usageEvent.PageTurns + 1);
                UpdateProgress(usageEvent, currentPage, totalPages);
            });
        }

        internal static void SetProgress(string username, string eventId, int currentPage, int totalPages)
            => UpdatePending(username, eventId, usageEvent => UpdateProgress(usageEvent, currentPage, totalPages));

        internal static void Complete(string username, string eventId, int currentPage, int totalPages)
        {
            TryEdit(username, state =>
            {
                UsageEvent? usageEvent = state.Pending.FirstOrDefault(x => x.EventId == eventId);
                if (usageEvent == null) return false;
                state.Pending.Remove(usageEvent);
                UpdateProgress(usageEvent, currentPage, totalPages);
                if (usageEvent.DurationSeconds > 0) AddToOutbox(state, usageEvent);
                return true;
            });
        }

        private static void UpdatePending(string username, string eventId, Action<UsageEvent> update)
        {
            TryEdit(username, state =>
            {
                UsageEvent? usageEvent = state.Pending.FirstOrDefault(x => x.EventId == eventId);
                if (usageEvent == null) return false;
                update(usageEvent);
                return true;
            });
        }

        private static bool TryEdit(string username, Func<UsageState, bool> update)
        {
            try
            {
                lock (Gate)
                {
                    UsageState state = Load(username);
                    if (update(state)) Save(username, state);
                }
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                System.Diagnostics.Debug.WriteLine($"[Usage storage] {error}");
                return false;
            }
        }

        private static void UpdateProgress(UsageEvent usageEvent, int currentPage, int totalPages)
        {
            if (usageEvent.EventType != "reading_session" || totalPages <= 0) return;
            usageEvent.ProgressPercent = Math.Clamp(
                (int)Math.Round(currentPage * 100d / totalPages), 0, 100);
        }

        private static void AddToOutbox(UsageState state, UsageEvent usageEvent)
        {
            if (state.Outbox.All(x => x.EventId != usageEvent.EventId))
                state.Outbox.Add(Clone(usageEvent));
            if (state.Outbox.Count > MaxOutboxEvents)
                state.Outbox.RemoveRange(0, state.Outbox.Count - MaxOutboxEvents);
        }

        private static UsageEvent Clone(UsageEvent value) => new()
        {
            EventId = value.EventId,
            EventType = value.EventType,
            BookId = value.BookId,
            OccurredAt = value.OccurredAt,
            DurationSeconds = value.DurationSeconds,
            PageTurns = value.PageTurns,
            ProgressPercent = value.ProgressPercent
        };

        private static string StatePath(string username)
            => Path.Combine(FileHelper.GetUserDirectory(username), "usage_activity.json");

        private static UsageState Load(string username)
        {
            string path = StatePath(username);
            try
            {
                var state = JsonSerializer.Deserialize<UsageState>(File.ReadAllText(path));
                if (state == null || state.Pending == null || state.Outbox == null ||
                    state.Pending.Any(item => item == null) || state.Outbox.Any(item => item == null))
                    throw new JsonException("사용량 기록 형식이 올바르지 않습니다.");
                return state;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException) { return new(); }
        }

        private static void Save(string username, UsageState state)
        {
            string path = StatePath(username);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
        }

        private sealed class UsageState
        {
            public List<UsageEvent> Pending { get; set; } = new();
            public List<UsageEvent> Outbox { get; set; } = new();
        }
    }

    public sealed class UsageSession
    {
        private readonly string _username;
        private readonly string _eventId;
        private bool _completed;

        internal UsageSession(string username, string eventId)
        {
            _username = username;
            _eventId = eventId;
        }

        public void AddActiveSeconds(int seconds, int currentPage = 0, int totalPages = 0)
        {
            if (!_completed)
                UsageActivityStore.AddActiveSeconds(_username, _eventId, seconds, currentPage, totalPages);
        }

        public void RecordPageChange(int currentPage, int totalPages)
        {
            if (!_completed)
                UsageActivityStore.RecordPageChange(_username, _eventId, currentPage, totalPages);
        }

        public void SetProgress(int currentPage, int totalPages)
        {
            if (!_completed)
                UsageActivityStore.SetProgress(_username, _eventId, currentPage, totalPages);
        }

        public void Complete(int currentPage = 0, int totalPages = 0)
        {
            if (_completed) return;
            _completed = true;
            UsageActivityStore.Complete(_username, _eventId, currentPage, totalPages);
        }
    }
}
