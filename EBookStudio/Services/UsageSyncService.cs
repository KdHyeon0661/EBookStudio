using EBookStudio.Models;
using EBookStudio.Helpers;
using System.IO;
using System.Text.Json;

namespace EBookStudio.Services
{
    public sealed class UsageSyncService : IUsageService
    {
        private static readonly SemaphoreSlim SyncLock = new(1, 1);
        private readonly IApiService _api;

        public UsageSyncService(IApiService? api = null)
        {
            _api = api ?? new ApiService();
        }

        public async Task<ApiResult> SyncAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return ApiResult.Ok();
            await SyncLock.WaitAsync();
            try
            {
                while (true)
                {
                    List<UsageEvent> batch = UsageActivityStore.GetOutboxBatch(username);
                    if (batch.Count == 0) return ApiResult.Ok();
                    ApiResult<UsageBatchResponse> result = await _api.SubmitUsageEventsAsync(batch);
                    if (!result.Success) return ApiResult.Fail(result.Error!);
                    if (result.Value?.ReceivedCount != batch.Count)
                        return ApiResult.Fail(new ApiError(ApiErrorKind.InvalidResponse,
                            "Usage batch acknowledgement count does not match"));
                    UsageActivityStore.Acknowledge(username, batch.Select(x => x.EventId));
                }
            }
            finally { SyncLock.Release(); }
        }

        public async Task<ApiResult<UsageSummaryResponse>> GetSummaryAsync(string username)
        {
            ApiResult sync = await SyncAsync(username);
            if (sync.Success)
            {
                ApiResult<UsageSummaryResponse> remote = await _api.GetUsageSummaryAsync();
                if (remote.Success && remote.Value != null)
                {
                    SaveCachedSummary(username, remote.Value);
                    return remote;
                }
            }

            UsageSummaryResponse? cached = LoadCachedSummary(username);
            if (cached != null)
            {
                cached.IsCached = true;
                return ApiResult<UsageSummaryResponse>.Ok(cached);
            }
            return ApiResult<UsageSummaryResponse>.Fail(sync.Error
                ?? new ApiError(ApiErrorKind.Network, "Usage summary is unavailable"));
        }

        public async Task<ApiResult<UsageDashboard>> GetDashboardAsync(string username, int days = 7)
        {
            if (string.IsNullOrWhiteSpace(username))
                return ApiResult<UsageDashboard>.Fail(
                    new ApiError(ApiErrorKind.Authentication, "Username is required"));
            ApiResult sync = await SyncAsync(username);
            ApiError? remoteError = sync.Error;
            if (sync.Success)
            {
                Task<ApiResult<UsageSummaryResponse>> summaryTask = _api.GetUsageSummaryAsync();
                Task<ApiResult<UsageBookListResponse>> booksTask = _api.GetUsageBooksAsync();
                Task<ApiResult<UsageDailySeriesResponse>> dailyTask = _api.GetUsageDailyAsync(days);
                await Task.WhenAll(summaryTask, booksTask, dailyTask);

                ApiResult<UsageSummaryResponse> summary = await summaryTask;
                ApiResult<UsageBookListResponse> books = await booksTask;
                ApiResult<UsageDailySeriesResponse> daily = await dailyTask;
                if (summary.Success && summary.Value != null &&
                    books.Success && books.Value != null &&
                    daily.Success && daily.Value != null)
                {
                    var dashboard = new UsageDashboard
                    {
                        Summary = summary.Value,
                        Books = books.Value.Books,
                        Daily = daily.Value
                    };
                    SaveCachedSummary(username, dashboard.Summary);
                    SaveCachedDashboard(username, dashboard);
                    return ApiResult<UsageDashboard>.Ok(dashboard);
                }
                remoteError = summary.Error ?? books.Error ?? daily.Error;
            }

            UsageDashboard? cached = LoadCachedDashboard(username);
            if (cached != null)
            {
                cached.IsCached = true;
                cached.Summary.IsCached = true;
                return ApiResult<UsageDashboard>.Ok(cached);
            }
            UsageSummaryResponse? cachedSummary = LoadCachedSummary(username);
            if (cachedSummary != null)
            {
                cachedSummary.IsCached = true;
                return ApiResult<UsageDashboard>.Ok(new UsageDashboard
                {
                    Summary = cachedSummary,
                    IsCached = true
                });
            }
            return ApiResult<UsageDashboard>.Fail(remoteError
                ?? new ApiError(ApiErrorKind.Network, "Usage dashboard is unavailable"));
        }

        private static string CachedSummaryPath(string username)
            => Path.Combine(FileHelper.GetUserDirectory(username), "usage_summary.json");

        private static string CachedDashboardPath(string username)
            => Path.Combine(FileHelper.GetUserDirectory(username), "usage_dashboard.json");

        private static void SaveCachedSummary(string username, UsageSummaryResponse summary)
            => SaveCache(CachedSummaryPath(username), summary, "Usage Summary");

        private static void SaveCachedDashboard(string username, UsageDashboard dashboard)
            => SaveCache(CachedDashboardPath(username), dashboard, "Usage Dashboard");

        private static void SaveCache<T>(string path, T value, string label)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                AtomicFile.WriteAllText(path, JsonSerializer.Serialize(value));
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[{label} Save Error] {error.Message}");
            }
        }

        private static UsageSummaryResponse? LoadCachedSummary(string username)
            => LoadCache<UsageSummaryResponse>(CachedSummaryPath(username), "Usage Summary");

        private static UsageDashboard? LoadCachedDashboard(string username)
            => LoadCache<UsageDashboard>(CachedDashboardPath(username), "Usage Dashboard");

        private static T? LoadCache<T>(string path, string label) where T : class
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null;
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[{label} Load Error] {error.Message}");
                return null;
            }
        }
    }
}
