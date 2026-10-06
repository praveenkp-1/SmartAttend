using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using SmartAttend.Database;

namespace SmartAttend.Services
{
    public class ApiServer
    {
        private WebApplication? _app;
        private CancellationTokenSource? _cts;

        public event Action<string>? OnAttendanceReceived;
        public event Action<string>? OnDeviceConnected;
        public event Action<string>? OnEnrollmentReceived;

        public async Task StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://0.0.0.0:5000");
            _app = builder.Build();

            // ═══════════════════════════════
            // ENDPOINT 1 — Device ping
            // ═══════════════════════════════
            _app.MapGet("/ping", () =>
            {
                OnDeviceConnected?.Invoke("Device connected!");
                return Results.Ok(new
                {
                    status = "online",
                    server = "SmartAttend"
                });
            });

            // ═══════════════════════════════
            // ENDPOINT 2 — Get employee info
            // ═══════════════════════════════
            _app.MapGet("/employee/{userId}", (string userId) =>
            {
                var employee = DatabaseHelper.GetEmployeeByUserId(userId);
                if (employee == null)
                    return Results.NotFound(new { status = "not_found" });

                return Results.Ok(new
                {
                    status = "found",
                    userId = employee.UserId,
                    name = employee.Name,
                    enrolled = employee.Enrolled
                });
            });

            // ═══════════════════════════════
            // ENDPOINT 3 — Mark attendance
            // ESP32 POST /attendance
            // Body: {userId, method, checkIn, date}
            // ═══════════════════════════════
            _app.MapPost("/attendance", async (HttpContext ctx) =>
            {
                using var reader = new StreamReader(ctx.Request.Body);
                string body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<AttendanceRequest>(body);

                if (data == null)
                    return Results.BadRequest(new { status = "invalid_data" });

                string userId = data.userId;
                string? linkedUserId = DatabaseHelper.GetUserIdByRFIDCard(data.userId);
                if (linkedUserId != null)
                    userId = linkedUserId;

                var employee = DatabaseHelper.GetEmployeeByUserId(userId);
                if (employee == null)
                    return Results.NotFound(new { status = "not_found" });

                bool hasCheckedIn = DatabaseHelper.HasCheckedInToday(userId, data.date);

                if (hasCheckedIn)
                {
                    // If device sent hours — use them
                    // Otherwise let database calculate
                    if (!string.IsNullOrEmpty(data.hours))
                        DatabaseHelper.UpdateCheckOutWithHours(
                            userId, data.date, data.checkOut, data.hours);
                    else
                        DatabaseHelper.UpdateCheckOut(
                            userId, data.date, data.checkOut, data.lastActivity);

                    OnAttendanceReceived?.Invoke(
                        $"{employee.Name} — Checked Out ({data.method})");

                    return Results.Ok(new
                    {
                        status = "success",
                        name = employee.Name,
                        attendance = "CheckOut",
                        message = "Checked out successfully"
                    });
                }
                else
                {
                    // Use status calculated by device
                    // Fall back to Present if device didn't send status
                    string attendanceStatus = !string.IsNullOrEmpty(data.status)
                        ? data.status : "Present";

                    DatabaseHelper.SaveDeviceAttendance(
                        userId, employee.Name, data.checkIn,
                        data.checkOut, data.method, attendanceStatus,
                        data.hours, data.date, data.lastActivity);

                    OnAttendanceReceived?.Invoke(
                        $"{employee.Name} — {attendanceStatus} ({data.method})");
                    return Results.Ok(new
                    {
                        status = "success",
                        name = employee.Name,
                        attendance = attendanceStatus,
                        message = "Checked in successfully"
                    });
                }
            });

            // ═══════════════════════════════
            // ENDPOINT 4 — Update enrollment
            // ESP32 POST /enroll
            // Body: {userId, type}
            // ═══════════════════════════════
            _app.MapPost("/enroll", async (HttpContext ctx) =>
            {
                using var reader = new StreamReader(ctx.Request.Body);
                string body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<EnrollRequest>(body);

                if (data == null)
                    return Results.BadRequest(new { status = "invalid_data" });

                DatabaseHelper.UpdateEnrollment(data.userId, data.type);

                OnEnrollmentReceived?.Invoke(
                    $"{data.userId} — {data.type} enrolled");

                return Results.Ok(new { status = "enrolled" });
            });

            // ═══════════════════════════════
            // ENDPOINT 5 — Register RFID card
            // ESP32 POST /register-rfid
            // Body: {cardId, userId}
            // ═══════════════════════════════
            _app.MapPost("/register-rfid", async (HttpContext ctx) =>
            {
                using var reader = new StreamReader(ctx.Request.Body);
                string body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<RFIDRegisterRequest>(body);

                if (data == null)
                    return Results.BadRequest(new { status = "invalid_data" });

                DatabaseHelper.RegisterRFIDCard(data.cardId, data.userId);
                DatabaseHelper.UpdateEnrollment(data.userId, "rfid");

                OnEnrollmentReceived?.Invoke(
                    $"{data.userId} — RFID card registered");

                return Results.Ok(new { status = "registered" });
            });

            // ═══════════════════════════════
            // ENDPOINT 6 — Get pending employees
            // ESP32 GET /pending-employees
            // Returns employees with Enrolled = Pending or Partial
            // Device uses this to show enrollment list on TFT
            // ═══════════════════════════════
            _app.MapGet("/pending-employees", () =>
            {
                var employees = DatabaseHelper.GetPendingEmployees();
                return Results.Ok(new
                {
                    status = "ok",
                    employees
                });
            });

            // ═══════════════════════════════
            // ENDPOINT 7 — Device update pull
            // ESP32 GET /device-updates?lastSyncId=0
            // Settings are intentionally not queued here yet.
            // ═══════════════════════════════
            _app.MapGet("/device-updates",
                (long? lastSyncId, int? limit) =>
            {
                var updates = DatabaseHelper.GetPendingDeviceUpdates(
                    lastSyncId ?? 0,
                    limit ?? 25);

                return Results.Ok(new
                {
                    status = "ok",
                    updates
                });
            });

            // ═══════════════════════════════
            // ENDPOINT 8 — Device update ACK
            // ESP32 POST /device-updates/ack
            // Body: { ids: [1, 2, 3] }
            // ═══════════════════════════════
            _app.MapPost("/device-updates/ack", async (HttpContext ctx) =>
            {
                using var reader = new StreamReader(ctx.Request.Body);
                string body = await reader.ReadToEndAsync();
                var data = JsonSerializer
                    .Deserialize<DeviceUpdateAckRequest>(body);

                if (data == null || data.ids.Count == 0)
                    return Results.BadRequest(new
                    {
                        status = "invalid_data"
                    });

                DatabaseHelper.MarkDeviceUpdatesSynced(data.ids);

                return Results.Ok(new
                {
                    status = "ok",
                    count = data.ids.Count
                });
            });

            // ═══════════════════════════════
            // ENDPOINT 9 — Get single employee
            // for enrollment details
            // ESP32 GET /enroll-info/{userId}
            // Returns name + which methods are enrolled
            // ═══════════════════════════════
            _app.MapGet("/enroll-info/{userId}", (string userId) =>
            {
                var emp = DatabaseHelper.GetEnrollmentInfo(userId);
                if (emp == null)
                    return Results.NotFound(new { status = "not_found" });

                return Results.Ok(new
                {
                    status = "found",
                    userId = emp.UserId,
                    name = emp.Name,
                    fingerprintEnrolled = emp.FingerprintEnrolled,
                    rfidEnrolled = emp.RfidEnrolled,
                    pinEnrolled = emp.PinEnrolled
                });
            });

            // ═══════════════════════════════
            // ENDPOINT 10 — Register PIN
            // ESP32 POST /register-pin
            // Body: {userId, pin}
            // ═══════════════════════════════
            _app.MapPost("/register-pin", async (HttpContext ctx) =>
            {
                using var reader = new StreamReader(ctx.Request.Body);
                string body = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<PINRegisterRequest>(body);

                if (data == null)
                    return Results.BadRequest(new { status = "invalid_data" });

                DatabaseHelper.RegisterPIN(data.userId, data.pin);
                DatabaseHelper.UpdateEnrollment(data.userId, "pin");

                OnEnrollmentReceived?.Invoke(
                    $"{data.userId} — PIN registered");

                return Results.Ok(new { status = "registered" });
            });

            _cts = new CancellationTokenSource();
            await _app.RunAsync(_cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        
    }

    // ── Request models ─────────────────────────────────
    public class AttendanceRequest
    {
        public string userId { get; set; } = "";
        public string method { get; set; } = "";
        public string checkIn { get; set; } = "";
        public string checkOut { get; set; } = "";
        public string status { get; set; } = "";
        public string hours { get; set; } = "";
        public int lateMinutes { get; set; } = 0;
        public string date { get; set; } = "";
        public string lastActivity { get; set; } = "";
    }

    public class EnrollRequest
    {
        public string userId { get; set; } = "";
        public string type { get; set; } = "";
    }

    public class RFIDRegisterRequest
    {
        public string cardId { get; set; } = "";
        public string userId { get; set; } = "";
    }

    public class PINRegisterRequest
    {
        public string userId { get; set; } = "";
        public string pin { get; set; } = "";
    }

    public class DeviceUpdateAckRequest
    {
        public List<long> ids { get; set; } = new();
    }
}
