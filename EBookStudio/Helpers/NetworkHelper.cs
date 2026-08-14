using EBookStudio.Models;
using System.Net.Http;

namespace EBookStudio.Helpers
{
    public static class NetworkHelper
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        public static bool CheckInternetConnection()
            => CheckInternetConnectionAsync().GetAwaiter().GetResult();

        public static async Task<bool> CheckInternetConnectionAsync()
        {
            try
            {
                using var response = await Client.GetAsync($"{ApiConfig.BaseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}