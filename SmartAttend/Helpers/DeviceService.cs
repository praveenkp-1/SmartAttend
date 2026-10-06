using System;
using System.Net.Http;
using System.Threading.Tasks;
using SmartAttend.Database;

namespace SmartAttend.Services
{
    public class DeviceService
    {
        private const string DeviceHostName = "smartattend.local";
        private const int DefaultDevicePort = 5001;

        public async Task<bool> PingDeviceAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);

                string savedAddress =
                    DatabaseHelper.GetSetting("device_ip");
                string portText =
                    DatabaseHelper.GetSetting("device_port");
                int port = int.TryParse(portText, out int parsedPort)
                    ? parsedPort
                    : DefaultDevicePort;

                if (!string.IsNullOrEmpty(savedAddress) &&
                    await PingHostAsync(client, savedAddress, port))
                {
                    return true;
                }

                if (await PingHostAsync(client, DeviceHostName, port))
                {
                    if (!string.IsNullOrEmpty(savedAddress))
                    {
                        DatabaseHelper.UpdateDeviceAddress(
                            savedAddress, DeviceHostName);
                    }

                    DatabaseHelper.SaveSetting(
                        "device_ip", DeviceHostName);
                    DatabaseHelper.SaveSetting(
                        "device_port", port.ToString());
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> PingHostAsync(
            HttpClient client, string host, int port)
        {
            try
            {
                var response = await client.GetAsync(
                    $"http://{host}:{port}/ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

    }
}
