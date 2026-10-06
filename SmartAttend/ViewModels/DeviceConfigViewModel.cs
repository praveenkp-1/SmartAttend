using System.Net.Http;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.ComponentModel;
using SmartAttend.Database;
using System.Windows;

namespace SmartAttend.ViewModels
{
    public class DeviceConfigViewModel : INotifyPropertyChanged
    {
        public const string DeviceHostName = "smartattend.local";

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(name));

        // ── Messages ───────────────────────────────────────
        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        private string _scanStatus;
        public string ScanStatus
        {
            get => _scanStatus;
            set
            {  _scanStatus = value;
              
                OnPropertyChanged(nameof(ScanStatus));
            }
        }

        // ── Load devices ───────────────────────────────────
        public dynamic LoadDevices()
        {
            return DatabaseHelper.GetAllDevices();
        }

        // ══════════════════════════════════════════════════
        // SCAN — 3 step priority:
        // 1. smartattend.local  (already on WiFi - fastest)
        // 2. 192.168.4.1        (AP mode - first time setup)
        // 3. Full subnet scan   (last resort)
        // ══════════════════════════════════════════════════
        public async Task<string> ScanForDevicesAsync()
        {
            // Step 1 — try mDNS first
            ScanStatus = $"Checking {DeviceHostName}...";
            bool mdnsFound = await TestConnectionAsync(
                DeviceHostName, 5001);

            if (mdnsFound)
            {
                ScanStatus = $"Device found via {DeviceHostName}!";
                return DeviceHostName;
            }

            // Step 2 — try AP mode
            ScanStatus = "Checking AP mode (192.168.4.1)...";
            bool apFound = await TestConnectionAsync(
                "192.168.4.1", 5001);

            if (apFound)
            {
                ScanStatus =
                    "Setup device found at 192.168.4.1! " +
                    "Enter WiFi credentials and connect.";
                return "192.168.4.1";
            }

            // Step 3 — full subnet scan
            ScanStatus = "Scanning network...";
            string localIp = GetLocalIP();
            if (string.IsNullOrEmpty(localIp))
            {
                ScanStatus = "Cannot determine local IP.";
                return "";
            }

            string subnet = localIp.Substring(0,
                localIp.LastIndexOf('.') + 1);

            var tasks = new List<Task<string>>();
            for (int i = 1; i <= 254; i++)
            {
                string ip = subnet + i;
                tasks.Add(ProbeIPAsync(ip));
            }

            while (tasks.Count > 0)
            {
                var completed = await Task.WhenAny(tasks);
                tasks.Remove(completed);
                string result = await completed;
                if (!string.IsNullOrEmpty(result))
                {
                    ScanStatus = $"Device found at {result}!";
                    return result;
                }
            }

            ScanStatus = "No devices found.";
            return "";
        }

        // ── Probe a single IP ──────────────────────────────
        private async Task<string> ProbeIPAsync(string ip)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMilliseconds(400);
                var response = await client.GetAsync(
                    $"http://{ip}:5001/discovery");
                if (!response.IsSuccessStatusCode) return "";

                string json = await response.Content
                    .ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                string type = doc.RootElement
                    .GetProperty("type").GetString() ?? "";
                return type == "SmartAttend" ? ip : "";
            }
            catch { return ""; }
        }

        // ── Get real IP from device /ping ──────────────────
        private async Task<string> GetDeviceIPAsync(string host)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                var response = await client.GetAsync(
                    $"http://{host}:5001/ping");
                if (!response.IsSuccessStatusCode) return "";

                string json = await response.Content
                    .ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                return doc.RootElement
                    .GetProperty("ip").GetString() ?? "";
            }
            catch { return ""; }
        }


        // ══════════════════════════════════════════════════
        // CONNECT DEVICE
        // Pushes config → waits for restart →
        // finds via mDNS → saves to database
        // ══════════════════════════════════════════════════
        public async Task<bool> ConnectDeviceAsync(
        string name, string ip, string port,
        string wifiSSID, string wifiPassword)
        {
            if (string.IsNullOrEmpty(name) ||
                string.IsNullOrEmpty(ip))
            {
                ErrorMessage = "Device name and IP are required.";
                return false;
            }

            if (!int.TryParse(port, out int portNum))
            {
                ErrorMessage = "Invalid port number.";
                return false;
            }

            if (string.IsNullOrEmpty(wifiSSID))
            {
                ErrorMessage = "WiFi SSID is required.";
                return false;
            }

            // Test connection
            ScanStatus = "Testing connection...";
            bool reachable = await TestConnectionAsync(ip, portNum);
            if (!reachable)
            {
                ErrorMessage =
                    $"Cannot reach device at {ip}:{portNum}\n" +
                    "Make sure device is on and PC is connected " +
                    "to SmartAttend-Setup hotspot.";
                return false;
            }

            // Check duplicate
            var existing = DatabaseHelper.GetAllDevices();
            foreach (var d in existing)
            {
                if (d.IPAddress?.ToString() == ip)
                {
                    ErrorMessage =
                        $"Device at {ip} is already connected!";
                    return false;
                }
            }

            // Push config
            ScanStatus = "Pushing config to device...";           
            bool pushed = await PushConfigToDeviceAsync(
                ip,
                portNum,
                wifiSSID,
                wifiPassword
                );
            

            if (!pushed)
            {
                ErrorMessage =
                    "Failed to push config to device.\n" +
                    "Check connection and try again.";
                return false;
            }

            // Wait for device to restart and reconnect
            // Retries every 3 seconds for up to 40 seconds total
            ScanStatus = "Config sent! Waiting for device to restart...";
            await Task.Delay(5000); // initial wait for reboot

            string finalHost = "";
            string currentIP = "";
            int maxAttempts = 12; // 12 x 3s = 36 seconds total

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ScanStatus =
                    $"Looking for device... attempt {attempt}/{maxAttempts}";

                // Try mDNS first
                bool mdnsOk = await TestConnectionAsync(
                    DeviceHostName, 5001);

                if (mdnsOk)
                {
                    finalHost = DeviceHostName;
                    currentIP = await GetDeviceIPAsync(DeviceHostName);
                    ScanStatus = string.IsNullOrEmpty(currentIP)
                        ? $"Device found at {DeviceHostName}!"
                        : $"Device found at {DeviceHostName} ({currentIP})!";
                    break;
                }

                // Try subnet scan on last 3 attempts
                if (attempt >= maxAttempts - 2)
                {
                    ScanStatus = "Trying subnet scan...";
                    currentIP = await ScanSubnetAsync();
                    if (!string.IsNullOrEmpty(currentIP))
                    {
                        finalHost = currentIP;
                        ScanStatus = $"Device found at {currentIP}!";
                        break;
                    }
                }

                // Not found yet — wait and retry
                if (attempt < maxAttempts)
                {
                    ScanStatus =
                        $"Not found yet, retrying in 3 seconds... " +
                        $"({attempt}/{maxAttempts})";
                    await Task.Delay(3000);
                }
            }



            // Get firmware and save
            if (string.IsNullOrEmpty(finalHost))
            {
                ErrorMessage =
                    "Device configured, but WPF could not find it " +
                    "after restart.";
                return false;
            }

            bool serverConfigured =
    await SendServerConfigToDeviceAsync(
        finalHost,
        portNum);

            if (!serverConfigured)
            {
                ErrorMessage =
                    "Device found but server configuration failed.";
                return false;
            }

            ScanStatus =
                "Preparing initial sync. Keep the device powered on...";
            int queuedUpdates = DatabaseHelper.QueueFullDeviceSync();

            string firmware = await GetFirmwareVersionAsync(
                finalHost,
                portNum);


            DatabaseHelper.AddDevice(name, finalHost, portNum, "");
            DatabaseHelper.UpdateDeviceStatus(finalHost, "Online",
                firmware,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            DatabaseHelper.SaveSetting("device_ip", finalHost);
            DatabaseHelper.SaveSetting("device_port",
                portNum.ToString());

            ScanStatus = queuedUpdates > 0
                ? $"Initial sync prepared: {queuedUpdates} employees queued. " +
                  "The device will download them automatically."
                : "Device configured. No active employees to sync.";
            return true;
        }

        // ══════════════════════════════════════════════════
        // PUSH CONFIG TO DEVICE
        // ══════════════════════════════════════════════════
        public async Task<bool> PushConfigToDeviceAsync(
        string ip,
        int port,
        string wifiSSID,
        string wifiPassword
        )
        {
            try
            {
                var payload = new
                {
                    ssid = wifiSSID,
                    password = wifiPassword, 
                };

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(8);

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    $"http://{ip}:{port}/config", content);

                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        

        // ══════════════════════════════════════════════════
        // TEST DEVICE
        // ══════════════════════════════════════════════════
        public async Task<bool> TestDeviceAsync(string ip)
        {
            bool online = await TestConnectionAsync(ip, 5001);

            if (!online &&
                ip != DeviceHostName &&
                await TestConnectionAsync(DeviceHostName, 5001))
            {
                DatabaseHelper.UpdateDeviceAddress(ip, DeviceHostName);
                DatabaseHelper.SaveSetting("device_ip", DeviceHostName);
                ip = DeviceHostName;
                online = true;
            }

            if (online)
                DatabaseHelper.UpdateDeviceStatus(
                    ip, "Online", "",
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss"));
            else
                DatabaseHelper.UpdateDeviceStatus(
                    ip, "Offline");

            return online;
        }

        // ── Remove device ──────────────────────────────────
        public void RemoveDevice(string id)
        {
            DatabaseHelper.DeleteDevice(id);
        }

        // ── Factory reset ──────────────────────────────────
        public async Task<(bool success, int pendingRecords)>
            FactoryResetDeviceAsync(string ip)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                var response = await client.PostAsync(
                    $"http://{ip}:5001/factory-reset",
                    new StringContent("",
                        Encoding.UTF8, "application/json"));

                string body = await response.Content
                    .ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);

                if (response.StatusCode ==
                    System.Net.HttpStatusCode.Conflict)
                {
                    int count = doc.RootElement
                        .GetProperty("count").GetInt32();
                    return (false, count);
                }

                return (response.IsSuccessStatusCode, 0);
            }
            catch { return (false, -1); }
        }

        // ══════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════

        // ── Test if host:port is reachable ─────────────────
        public async Task<bool> TestConnectionAsync(
            string ip, int port)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                var response = await client.GetAsync(
                    $"http://{ip}:{port}/ping");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ── Subnet scan only (fallback) ────────────────────
        private async Task<string> ScanSubnetAsync()
        {
            string localIp = GetLocalIP();
            if (string.IsNullOrEmpty(localIp)) return "";

            string subnet = localIp.Substring(0,
                localIp.LastIndexOf('.') + 1);

            var tasks = new List<Task<string>>();
            for (int i = 1; i <= 254; i++)
            {
                string ip = subnet + i;
                tasks.Add(ProbeIPAsync(ip));
            }

            while (tasks.Count > 0)
            {
                var completed = await Task.WhenAny(tasks);
                tasks.Remove(completed);
                string result = await completed;
                if (!string.IsNullOrEmpty(result))
                    return result;
            }
            return "";
        }

        // ── Get firmware version ───────────────────────────
        private async Task<string> GetFirmwareVersionAsync(
            string ip, int port)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);

                var response = await client.GetAsync(
                    $"http://{ip}:{port}/info");

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content
                        .ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    return doc.RootElement
                        .GetProperty("version")
                        .GetString() ?? "2.0";
                }
            }
            catch { }
            return "2.0";
        }

        // ── Get local WiFi IP ──────────────────────────────
        // Picks the correct adapter — skips virtual/hotspot IPs
        public string GetLocalIP(string deviceIP = "")
        {
            IPAddress? target = null;
            if (!string.IsNullOrWhiteSpace(deviceIP) &&
                IPAddress.TryParse(deviceIP, out var parsed) &&
                parsed.AddressFamily == AddressFamily.InterNetwork)
            {
                target = parsed;
            }

            var candidates = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType !=
                        NetworkInterfaceType.Loopback)
                .SelectMany(nic =>
                    nic.GetIPProperties().UnicastAddresses)
                .Where(addr =>
                    addr.Address.AddressFamily ==
                        AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(addr.Address))
                .ToList();

            if (target != null)
            {
                var matching = candidates.FirstOrDefault(addr =>
                    IsSameSubnet(
                        addr.Address,
                        target,
                        addr.IPv4Mask));

                if (matching != null)
                    return matching.Address.ToString();
            }

            foreach (var addr in candidates)
            {
                string ip = addr.Address.ToString();
                if (!ip.StartsWith("169.254."))
                    return ip;
            }

            return "";
        }

        private static bool IsSameSubnet(
            IPAddress localAddress,
            IPAddress remoteAddress,
            IPAddress subnetMask)
        {
            byte[] local = localAddress.GetAddressBytes();
            byte[] remote = remoteAddress.GetAddressBytes();
            byte[] mask = subnetMask.GetAddressBytes();

            for (int i = 0; i < local.Length; i++)
            {
                if ((local[i] & mask[i]) !=
                    (remote[i] & mask[i]))
                    return false;
            }

            return true;
        }

        public async Task<bool> RefreshServerConfigAsync(
            string deviceAddress, int devicePort)
        {
            return await SendServerConfigToDeviceAsync(
                deviceAddress, devicePort);
        }

        private async Task<bool> SendServerConfigToDeviceAsync(
    string deviceIP,
    int devicePort)
        {
            try
            {
                string deviceNetworkIP = deviceIP;
                if (!IPAddress.TryParse(deviceIP, out _))
                {
                    string reportedIP = await GetDeviceIPAsync(deviceIP);
                    if (!string.IsNullOrEmpty(reportedIP))
                        deviceNetworkIP = reportedIP;
                }

                string serverIP = GetLocalIP(deviceNetworkIP);
                if (string.IsNullOrEmpty(serverIP))
                    return false;

                var payload = new
                {
                    server = serverIP,
                    port = 5000
                };

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                string json =
                    JsonSerializer.Serialize(payload);

                var content =
                    new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json");

                var response =
                    await client.PostAsync(
                        $"http://{deviceIP}:{devicePort}/server-config",
                        content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }


    }
}
