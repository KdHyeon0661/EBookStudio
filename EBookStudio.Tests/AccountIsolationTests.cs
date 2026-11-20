using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Controls;
using EBookStudio.Helpers;
using EBookStudio.Models;
using EBookStudio.Services;
using EBookStudio.ViewModels;
using Xunit;

namespace EBookStudio.Tests;

public sealed class AccountIsolationTests
{
    [Fact]
    public void RegistrationKeepsTheCodeManualAndAllowsResending()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            var auth = new FakeAuthService { VerificationDelivery = new CodeSendResult { Success = true, DevelopmentCode = "123456" } };
            var view = new RegisterViewModel(new MainViewModel(initializeClient: false), auth, new FakeDialogService())
            { Email = "reader@example.com" };
            var send = (AsyncRelayCommand)view.SendCodeCommand;
            var verify = (AsyncRelayCommand)view.VerifyCodeCommand;

            send.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.True(view.IsEmailSent);
            Assert.Empty(view.VerificationCode);
            view.VerificationCode = "123456";
            verify.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.True(view.IsVerified);

            send.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.False(view.IsVerified);
            Assert.Empty(view.VerificationCode);
            Assert.Equal("인증번호 재발송", view.SendCodeLabel);
        });
    }

    [Fact]
    public void LoginUsesTheAuthenticatedIdentityEvenWhenTheInputChanges()
    {
        _ = TestEnvironment.Root;
        StaTest.Run(() =>
        {
            var account = new LocalAccount(TestEnvironment.UniqueName("confirmed"), Guid.NewGuid());
            var pending = new TaskCompletionSource<ApiResult<LocalAccount>>();
            var auth = new FakeAuthService { LoginHandler = (_, _) => pending.Task };
            var main = new MainViewModel(initializeClient: false);
            var login = new LoginViewModel(main, auth, new FakeDialogService()) { Username = account.Username };
            var password = new PasswordBox { Password = "test-password" };
            try
            {
                Task work = ((AsyncRelayCommand)login.LoginCommand).ExecuteAsync(password);
                login.Username = "different-input";
                pending.SetResult(ApiResult<LocalAccount>.Ok(account));
                work.GetAwaiter().GetResult();
                Assert.Equal(account.Username, main.LoggedInUser);
                Assert.Equal(account.CacheKey, main.LocalCacheKey);
                Assert.Empty(password.Password);
            }
            finally { main.Logout(); }
        });
    }

    [Fact]
    public async Task DelayedLogoutCannotClearTheNextLogin()
    {
        var first = new LocalAccount("first", Guid.NewGuid());
        var second = new LocalAccount("second", Guid.NewGuid());
        var enteredLogout = NewSignal();
        var finishLogout = NewSignal();
        string? logoutToken = null;
        string firstToken = AccessToken(first);
        using var client = new HttpClient(new StubHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/logout")
            {
                if (request.Headers.Authorization?.Parameter == firstToken)
                {
                    logoutToken = request.Headers.Authorization.Parameter;
                    enteredLogout.TrySetResult();
                    await finishLogout.Task;
                }
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return LoginReply(request, first, second);
        }));
        var api = new ApiService(client);
        try
        {
            Assert.True((await api.LoginAsync(first.Username, "test")).Success);
            Task logout = api.LogoutAsync();
            await enteredLogout.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(api.ForAccount(first.CacheKey));
            Assert.True((await api.LoginAsync(second.Username, "test")).Success);
            finishLogout.TrySetResult();
            await logout;
            Assert.Equal(firstToken, logoutToken);
            Assert.True(api.ForAccount(second.CacheKey)?.IsSessionCurrent);
        }
        finally
        {
            finishLogout.TrySetResult();
            await api.LogoutAsync();
        }
    }

    [Fact]
    public async Task DelayedRefreshCannotReplaceTheNextLoginTokens()
    {
        var first = new LocalAccount("first", Guid.NewGuid());
        var second = new LocalAccount("second", Guid.NewGuid());
        var enteredRefresh = NewSignal();
        var finishRefresh = NewSignal();
        var sentTokens = new List<string?>();
        using var client = new HttpClient(new StubHandler(async request =>
        {
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/refresh":
                    enteredRefresh.TrySetResult();
                    await finishRefresh.Task;
                    return SessionReply(first, Expiration);
                case "/usage/summary":
                    sentTokens.Add(request.Headers.Authorization?.Parameter);
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { }) };
                case "/logout":
                    return new HttpResponseMessage(HttpStatusCode.OK);
                default:
                    return LoginReply(request, first, second, firstExpiration: 1);
            }
        }));
        var api = new ApiService(client);
        try
        {
            Assert.True((await api.LoginAsync(first.Username, "test")).Success);
            Task<ApiResult<UsageSummaryResponse>> oldRequest = api.GetUsageSummaryAsync();
            await enteredRefresh.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True((await api.LoginAsync(second.Username, "test")).Success);
            finishRefresh.TrySetResult();
            Assert.False((await oldRequest).Success);
            Assert.True((await api.GetUsageSummaryAsync()).Success);
            Assert.Equal(AccessToken(second), Assert.Single(sentTokens));
        }
        finally
        {
            finishRefresh.TrySetResult();
            await api.LogoutAsync();
        }
    }

    [Fact]
    public async Task UsageSyncCannotAcknowledgeOldRecordsAfterSwitchingAccounts()
    {
        _ = TestEnvironment.Root;
        var first = new LocalAccount("first", Guid.NewGuid());
        var second = new LocalAccount("second", Guid.NewGuid());
        var enteredUpload = NewSignal();
        var finishUpload = NewSignal();
        var uploads = new List<string?>();
        var usage = UsageActivityStore.StartSession(first.CacheKey, "app_session")!;
        usage.AddActiveSeconds(30);
        usage.Complete();
        using var client = new HttpClient(new StubHandler(async request =>
        {
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/usage/events":
                    uploads.Add(request.Headers.Authorization?.Parameter);
                    enteredUpload.TrySetResult();
                    await finishUpload.Task;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { received_count = 1, inserted_count = 1 })
                    };
                case "/logout":
                    return new HttpResponseMessage(HttpStatusCode.OK);
                default:
                    return LoginReply(request, first, second);
            }
        }));
        var api = new ApiService(client);
        var service = new UsageSyncService(api);
        try
        {
            Assert.True((await api.LoginAsync(first.Username, "test")).Success);
            Task<ApiResult> sync = service.SyncAsync(first.CacheKey);
            await enteredUpload.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True((await api.LoginAsync(second.Username, "test")).Success);
            finishUpload.TrySetResult();
            Assert.False((await sync).Success);
            Assert.False((await service.SyncAsync(first.CacheKey)).Success);
            Assert.Equal(AccessToken(first), Assert.Single(uploads));
            Assert.Single(UsageActivityStore.GetOutboxBatch(first.CacheKey));
            Assert.Empty(UsageActivityStore.GetOutboxBatch(second.CacheKey));
        }
        finally
        {
            finishUpload.TrySetResult();
            await api.LogoutAsync();
        }
    }

    [Fact]
    public void CaseDistinctAccountsKeepSeparateCachesAndOnlyExactLegacyOwnerMigrates()
    {
        _ = TestEnvironment.Root;
        string originalName = TestEnvironment.UniqueName("Reader");
        var original = new LocalAccount(originalName, Guid.NewGuid());
        var other = new LocalAccount(originalName.ToLowerInvariant(), Guid.NewGuid());
        string legacy = FileHelper.GetUserDirectory(new LocalAccount(originalName, null).CacheKey);
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "library.json"), "[]");

        FileHelper.MigrateLegacyAccount(other);
        Assert.True(Directory.Exists(legacy));
        Assert.False(Directory.Exists(FileHelper.GetUserDirectory(other.CacheKey)));

        FileHelper.MigrateLegacyAccount(original);
        Assert.False(Directory.Exists(legacy));
        Assert.True(File.Exists(FileHelper.GetLibraryFilePath(original.CacheKey)));
        Assert.NotEqual(FileHelper.GetUserDirectory(original.CacheKey), FileHelper.GetUserDirectory(other.CacheKey));
    }

    private static TaskCompletionSource NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private const long Expiration = 4102444800;

    private static string AccessToken(LocalAccount account, long expiration = Expiration)
    {
        string payload = Convert.ToBase64String(
            JsonSerializer.SerializeToUtf8Bytes(new { sub = account.PublicId, exp = expiration }))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return "test." + payload + ".signature";
    }

    private static HttpResponseMessage LoginReply(HttpRequestMessage request, LocalAccount first, LocalAccount second,
                                                   long firstExpiration = Expiration)
    {
        Assert.Equal("/login", request.RequestUri!.AbsolutePath);
        string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var json = JsonDocument.Parse(body);
        var account = json.RootElement.GetProperty("username").GetString() == first.Username ? first : second;
        return SessionReply(account, account == first ? firstExpiration : Expiration);
    }

    private static HttpResponseMessage SessionReply(LocalAccount account, long expiration)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                access_token = AccessToken(account, expiration), refresh_token = $"refresh-{account.CacheKey}",
                username = account.Username, public_id = account.PublicId
            })
        };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
