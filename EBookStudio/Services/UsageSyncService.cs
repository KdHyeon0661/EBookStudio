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

        private static string CachedSummaryPath(string username)
            => Path.Combine(FileHelper.GetUserDirectory(username), "usage_summary.json");

        private static void SaveCachedSummary(string username, UsageSummaryResponse summary)
        {
            try
            {
                string path = CachedSummaryPath(username);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                AtomicFile.WriteAllText(path, JsonSerializer.Serialize(summary));
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Usage Summary Save Error] {error.Message}");
            }
        }

        private static UsageSummaryResponse? LoadCachedSummary(string username)
        {
            try
            {
                string path = CachedSummaryPath(username);
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<UsageSummaryResponse>(File.ReadAllText(path))
                    : null;
            }
            catch (Exception error)
            {
                System.Diagnostics.Debug.WriteLine($"[Usage Summary Load Error] {error.Message}");
                return null;
            }
        }
    }
}
