using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace EBookStudio.Helpers
{
    public static class NetworkHelper
    {
        // 기존 동기 메서드 (삭제해도 됨)
        public static bool CheckInternetConnection()
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send("8.8.8.8", 1000);
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch { return false; }
        }

        // [추가] 비동기 메서드 (화면 멈춤 방지)
        public static async Task<bool> CheckInternetConnectionAsync()
        {
            try
            {
                using (var ping = new Ping())
                {
                    // 핑을 보내고 기다리는 동안 다른 작업을 할 수 있게 함
                    var reply = await ping.SendPingAsync("8.8.8.8", 1000);
                    return reply != null && reply.Status == IPStatus.Success;
                }
            }
            catch { return false; }
        }
    }
}