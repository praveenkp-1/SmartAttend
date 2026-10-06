using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SmartAttend.Database
{
    public class DatabaseHelper
    {
        public static readonly string AppDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SmartAttend"
);

        public static string DbPath = Path.Combine(
            AppDataFolder,
            "smartattend.db"
        );

        private static string ConnectionString =
            $"Data Source={DbPath}";

        // ═══════════════════════════════════════
        // INITIALIZE
        // ═══════════════════════════════════════
        public static void Initialize()
        {
            Directory.CreateDirectory(AppDataFolder);

            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            var cmd = con.CreateCommand();

            // Settings
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Settings (
                    Key   TEXT PRIMARY KEY,
                    Value TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();

            // Departments
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Departments (
                    Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE
                );";
            cmd.ExecuteNonQuery();

            // Employees
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Employees (
                    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId              TEXT NOT NULL UNIQUE,
                    FullName            TEXT NOT NULL,
                    Department          TEXT NOT NULL,
                    Position            Text Not Null,
                    Contact             TEXT,
                    Email               TEXT,
                    FingerprintEnrolled INTEGER NOT NULL DEFAULT 0,
                    RFIDEnrolled        INTEGER NOT NULL DEFAULT 0,
                    PINEnrolled         INTEGER NOT NULL DEFAULT 0,
                    Enrolled            TEXT NOT NULL DEFAULT 'Pending',
                    Status              TEXT NOT NULL DEFAULT 'Active',
                    CreatedAt           TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();

            // Attendance
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Attendance (
                    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId   TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    CheckIn  TEXT,
                    CheckOut TEXT,
                    Method   TEXT,
                    Hours    TEXT,
                    Status   TEXT NOT NULL,
                    Date     TEXT NOT NULL,
                    LastActivity  TEXT
                );";
            cmd.ExecuteNonQuery();

            // RFIDCards
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS RFIDCards (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardId       TEXT NOT NULL UNIQUE,
                    UserId       TEXT NOT NULL,
                    RegisteredAt TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();

            // PINCards
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PINCards (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId       TEXT NOT NULL UNIQUE,
                    PINHash      TEXT NOT NULL,
                    RegisteredAt TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();

            // Devices
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Devices (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name            TEXT NOT NULL,
                    IPAddress       TEXT NOT NULL,
                    Port            INTEGER NOT NULL DEFAULT 5001,
                    DeviceKey       TEXT NOT NULL DEFAULT '',
                    Status          TEXT NOT NULL DEFAULT 'Offline',
                    FirmwareVersion TEXT NOT NULL DEFAULT 'Unknown',
                    LastSeen        TEXT NOT NULL DEFAULT '',
                    CreatedAt       TEXT NOT NULL
                );";
            cmd.ExecuteNonQuery();

            // Device sync queue
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS SyncQueue (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    Type      TEXT NOT NULL,
                    UserId    TEXT,
                    Payload   TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    SyncedAt  TEXT
                );";
            cmd.ExecuteNonQuery();
        }

        // ═══════════════════════════════════════
        // DEVICE SYNC QUEUE
        // ═══════════════════════════════════════
        public static void EnqueueDeviceUpdate(
            string type, string userId, string payload)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO SyncQueue
                    (Type, UserId, Payload, CreatedAt, SyncedAt)
                VALUES
                    ($type, $uid, $payload, $created, NULL);";
            cmd.Parameters.AddWithValue("$type", type);
            cmd.Parameters.AddWithValue("$uid",
                string.IsNullOrEmpty(userId)
                    ? DBNull.Value : userId);
            cmd.Parameters.AddWithValue("$payload", payload);
            cmd.Parameters.AddWithValue("$created",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public static List<dynamic> GetPendingDeviceUpdates(
            long lastSyncId = 0, int limit = 25)
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Type, UserId, Payload, CreatedAt
                FROM SyncQueue
                WHERE Id > $lastSyncId
                AND SyncedAt IS NULL
                ORDER BY Id
                LIMIT $limit;";
            cmd.Parameters.AddWithValue("$lastSyncId", lastSyncId);
            cmd.Parameters.AddWithValue("$limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    Id = Convert.ToInt64(reader["Id"]),
                    Type = reader["Type"].ToString(),
                    UserId = reader["UserId"].ToString(),
                    Payload = reader["Payload"].ToString(),
                    CreatedAt = reader["CreatedAt"].ToString()
                });
            }
            return list;
        }

        public static int QueueFullDeviceSync()
        {
            int count = 0;
            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT UserId
                FROM Employees
                WHERE Status = 'Active'
                ORDER BY Id;";

            using var reader = cmd.ExecuteReader();
            var userIds = new List<string>();
            while (reader.Read())
            {
                string userId = reader["UserId"].ToString() ?? "";
                if (!string.IsNullOrEmpty(userId))
                    userIds.Add(userId);
            }

            foreach (string userId in userIds)
            {
                var employee = GetEmployeeForDeviceSync(userId);
                if (employee == null)
                    continue;

                EnqueueDeviceUpdate(
                    "employee_upsert",
                    userId,
                    JsonSerializer.Serialize(employee));
                count++;
            }

            return count;
        }

        public static void MarkDeviceUpdatesSynced(
            IEnumerable<long> ids)
        {
            var updateIds = ids.Distinct().ToList();
            if (updateIds.Count == 0)
                return;

            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            for (int i = 0; i < updateIds.Count; i++)
            {
                var cmd = con.CreateCommand();
                cmd.CommandText = @"
                    UPDATE SyncQueue
                    SET SyncedAt = $syncedAt
                    WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$syncedAt",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("$id", updateIds[i]);
                cmd.ExecuteNonQuery();
            }
        }

        // ═══════════════════════════════════════
        // SETTINGS
        // ═══════════════════════════════════════
        public static void SaveSetting(string key, string value)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Settings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET Value = $value;";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.ExecuteNonQuery();
        }

        public static string GetSetting(string key)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText =
                "SELECT Value FROM Settings WHERE Key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? "";
        }

        public static bool IsSetupComplete()
        {
            return GetSetting("setup_complete") == "true";
        }

        // ═══════════════════════════════════════
        // EMPLOYEES
        // ═══════════════════════════════════════
        public static string GenerateUserId()
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT MAX(CAST(SUBSTR(UserId, 5) AS INTEGER))
                FROM Employees;";

            object result = cmd.ExecuteScalar();
            long lastId = result != DBNull.Value && result != null
                ? Convert.ToInt64(result) : 10000;

            return $"EMP-{lastId + 1}";
        }

        public static void AddEmployee(string userId,
            string fullName,
            string department, string position, string contact, string email)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Employees
                    (UserId, FullName, Department, Position,
                     Contact,Email, Enrolled, Status, CreatedAt)
                VALUES
                    ($uid, $name, $dept, $position,
                     $contact, $email, 'Pending', 'Active', $date);";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$name", fullName);
            cmd.Parameters.AddWithValue("$dept", department);
            cmd.Parameters.AddWithValue("$position", position);
            cmd.Parameters.AddWithValue("$contact", contact);
            cmd.Parameters.AddWithValue("$email", email);
            cmd.Parameters.AddWithValue("$date",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();

            EnqueueDeviceUpdate(
                "employee_upsert",
                userId,
                JsonSerializer.Serialize(new
                {
                    userId,
                    name = fullName,
                    department,
                    contact,
                    status = "Active",
                    enrolled = "Pending",
                    fingerprint = false,
                    rfid = false,
                    pin = false
                }));

            EnqueueDeviceUpdate(
                "enrollment_request",
                userId,
                JsonSerializer.Serialize(new
                {
                    userId,
                    name = fullName,
                    department,
                    fingerprint = false,
                    rfid = false,
                    pin = false,
                    enrolled = "Pending"
                }));
        }

        public static List<dynamic> GetAllEmployees(
                string search = "", string department = "")
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
        SELECT 
            FullName, UserId, Department, Position, Contact, Email,
            Enrolled, Status, CreatedAt, 
            FingerprintEnrolled, RFIDEnrolled, PINEnrolled
        FROM Employees
        WHERE ($search = '' OR FullName LIKE $search)
        AND ($department = '' OR Department = $department)
        ORDER BY Id DESC;";
            cmd.Parameters.AddWithValue("$search",
                string.IsNullOrEmpty(search) ? "" : $"%{search}%");
            cmd.Parameters.AddWithValue("$department",
                department == "All Departments" ? "" : department);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    Name = reader["FullName"],
                    UserId = reader["UserId"],
                    Department = reader["Department"],
                    Position = reader["Position"],
                    Contact = reader["Contact"],
                    Email = reader["Email"],
                    Enrolled = reader["Enrolled"],
                    Status = reader["Status"],
                    JoinDate = DateTime.TryParse(reader["CreatedAt"].ToString(), out DateTime dt)
                            ? dt.ToString("MMM dd yyyy")
                            : reader["CreatedAt"].ToString(),
                    FingerprintId = reader["FingerprintEnrolled"],
                    RFIDCard = reader["RFIDEnrolled"],
                    PIN = reader["PINEnrolled"]
                });
            }
            return list;
        }

        public static dynamic? GetEmployeeByUserId(string userId)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
        SELECT 
            FullName, UserId, Department, Contact, 
            Enrolled, Status, CreatedAt, 
            FingerprintEnrolled, RFIDEnrolled, PINEnrolled
                FROM Employees WHERE UserId = $uid;";
            cmd.Parameters.AddWithValue("$uid", userId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new
                {
                    Name = reader["FullName"],
                    UserId = reader["UserId"],
                    Department = reader["Department"],
                    Contact = reader["Contact"],
                    Enrolled = reader["Enrolled"],
                    Status = reader["Status"],
                    JoinDate = reader["CreatedAt"],
                    FingerprintId = reader["FingerprintEnrolled"],
                    RFIDCard = reader["RFIDEnrolled"],
                    PIN = reader["PINEnrolled"]
                };
            }
            return null;
        }

        private static object? GetEmployeeForDeviceSync(string userId)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT UserId, FullName, Department, Contact,
                       Status, Enrolled, FingerprintEnrolled,
                       RFIDEnrolled, PINEnrolled,
                       (
                           SELECT CardId
                           FROM RFIDCards
                           WHERE UserId = Employees.UserId
                           ORDER BY RegisteredAt DESC
                           LIMIT 1
                       ) AS RFIDCard,
                       (
                           SELECT PINHash
                           FROM PINCards
                           WHERE UserId = Employees.UserId
                           ORDER BY RegisteredAt DESC
                           LIMIT 1
                       ) AS PINHash
                FROM Employees
                WHERE UserId = $uid;";
            cmd.Parameters.AddWithValue("$uid", userId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return null;

            string pinHash = reader["PINHash"]?.ToString() ?? "";

            return new
            {
                userId = reader["UserId"].ToString(),
                name = reader["FullName"].ToString(),
                department = reader["Department"].ToString(),
                contact = reader["Contact"].ToString(),
                status = reader["Status"].ToString(),
                enrolled = reader["Enrolled"].ToString(),
                rfidCard = reader["RFIDCard"]?.ToString() ?? "",
                pinValue = DecodeStoredPin(pinHash),
                fingerprint =
                    reader["FingerprintEnrolled"].ToString() == "1",
                rfid = reader["RFIDEnrolled"].ToString() == "1",
                pin = reader["PINEnrolled"].ToString() == "1"
            };
        }

        private static string DecodeStoredPin(string pinHash)
        {
            if (string.IsNullOrEmpty(pinHash))
                return "";

            try
            {
                return Encoding.UTF8.GetString(
                    Convert.FromBase64String(pinHash));
            }
            catch
            {
                return "";
            }
        }

        public static void DeleteEmployee(string userId)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText =
                "DELETE FROM Employees WHERE UserId = $uid;";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.ExecuteNonQuery();

            EnqueueDeviceUpdate(
                "employee_delete",
                userId,
                JsonSerializer.Serialize(new { userId }));
        }

        public static void UpdateEmployee(string userId, string fullName, string department,
        string contact, string status, string joinDate, string position)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            // Preserve the original time-of-day on CreatedAt; the Edit modal's
            // DatePicker only ever supplies a date, never a time.
            string existingCreatedAt = null;
            var selectCmd = con.CreateCommand();
            selectCmd.CommandText = "SELECT CreatedAt FROM Employees WHERE UserId = $uid;";
            selectCmd.Parameters.AddWithValue("$uid", userId);
            using (var reader = selectCmd.ExecuteReader())
            {
                if (reader.Read())
                    existingCreatedAt = reader["CreatedAt"]?.ToString();
            }

            TimeSpan timeOfDay = TimeSpan.Zero;
            if (!string.IsNullOrEmpty(existingCreatedAt) &&
                DateTime.TryParse(existingCreatedAt, out DateTime existingDt))
            {
                timeOfDay = existingDt.TimeOfDay;
            }

            string combinedCreatedAt = existingCreatedAt ?? joinDate;
            if (DateTime.TryParse(joinDate, out DateTime newDate))
            {
                combinedCreatedAt = (newDate.Date + timeOfDay).ToString("yyyy-MM-dd HH:mm:ss");
            }

            var cmd = con.CreateCommand();
            cmd.CommandText = @"
            UPDATE Employees SET
            FullName           = $name,
            Department         = $dept,
            Contact            = $contact,
            Status             = $status,
            CreatedAt          = $joinDate,
            Position           = $position
            WHERE UserId = $uid;";

            cmd.Parameters.AddWithValue("$name", fullName);
            cmd.Parameters.AddWithValue("$dept", department);
            cmd.Parameters.AddWithValue("$contact", contact);
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$joinDate", combinedCreatedAt);
            cmd.Parameters.AddWithValue("$position", position);
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.ExecuteNonQuery();

            var employee = GetEmployeeForDeviceSync(userId);
            if (employee != null)
                EnqueueDeviceUpdate("employee_upsert", userId,
                    JsonSerializer.Serialize(employee));
        }
        public static void UpdateEmployeeStatus(
            string userId, string status)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                UPDATE Employees SET Status = $status
                WHERE UserId = $uid;";
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.ExecuteNonQuery();

            var employee = GetEmployeeForDeviceSync(userId);
            if (employee != null)
            {
                EnqueueDeviceUpdate(
                    "employee_upsert",
                    userId,
                    JsonSerializer.Serialize(employee));
            }
        }

        // ── Enrollment ─────────────────────────────────────
        public static List<dynamic> GetEnrollmentStatus()
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT FingerprintEnrolled,
                       RFIDEnrolled,
                       PINEnrolled,
                       Enrolled
                FROM Employees
                ORDER BY Id DESC;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    Fingerprint = reader["FingerprintEnrolled"].ToString(),
                    RFID = reader["RFIDEnrolled"].ToString(),
                    PIN = reader["PINEnrolled"].ToString(),
                    Enrolled = reader["Enrolled"].ToString()
                });
            }
            return list;
        }

        // ── Pending employees for device enrollment ────────
        public static List<dynamic> GetPendingEmployees()
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT UserId, FullName, Department,
                       FingerprintEnrolled, RFIDEnrolled, PINEnrolled,
                       Enrolled
                FROM Employees
                WHERE (Enrolled = 'Pending' OR Enrolled = 'Partial')
                AND Status = 'Active'
                ORDER BY FullName;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    UserId = reader["UserId"].ToString(),
                    Name = reader["FullName"].ToString(),
                    Department = reader["Department"].ToString(),
                    Fingerprint = reader["FingerprintEnrolled"].ToString() == "1",
                    RFID = reader["RFIDEnrolled"].ToString() == "1",
                    PIN = reader["PINEnrolled"].ToString() == "1",
                    Enrolled = reader["Enrolled"].ToString()
                });
            }
            return list;
        }

        // ── Enrollment info for single employee ────────────
        public static dynamic? GetEnrollmentInfo(string userId)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT UserId, FullName,
                       FingerprintEnrolled, RFIDEnrolled, PINEnrolled
                FROM Employees WHERE UserId = $uid;";
            cmd.Parameters.AddWithValue("$uid", userId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new
                {
                    UserId = reader["UserId"].ToString(),
                    Name = reader["FullName"].ToString(),
                    FingerprintEnrolled = reader["FingerprintEnrolled"].ToString() == "1",
                    RfidEnrolled = reader["RFIDEnrolled"].ToString() == "1",
                    PinEnrolled = reader["PINEnrolled"].ToString() == "1"
                };
            }
            return null;
        }

        public static void UpdateEnrollment(
            string userId, string type)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();

            string column = type.ToLower() switch
            {
                "fingerprint" => "FingerprintEnrolled",
                "rfid" => "RFIDEnrolled",
                "pin" => "PINEnrolled",
                _ => ""
            };

            if (string.IsNullOrEmpty(column)) return;

            cmd.CommandText = $@"
                UPDATE Employees SET {column} = 1
                WHERE UserId = $uid;";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.ExecuteNonQuery();

            UpdateEnrolledStatus(userId);

            var employee = GetEmployeeForDeviceSync(userId);
            if (employee != null)
            {
                EnqueueDeviceUpdate(
                    "employee_upsert",
                    userId,
                    JsonSerializer.Serialize(employee));
            }
        }

        private static void UpdateEnrolledStatus(string userId)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT FingerprintEnrolled, RFIDEnrolled, PINEnrolled
                FROM Employees WHERE UserId = $uid;";
            cmd.Parameters.AddWithValue("$uid", userId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int fp = Convert.ToInt32(reader["FingerprintEnrolled"]);
                int rfid = Convert.ToInt32(reader["RFIDEnrolled"]);
                int pin = Convert.ToInt32(reader["PINEnrolled"]);

                string status = (fp + rfid + pin) switch
                {
                    0 => "Pending",
                    3 => "Complete",
                    _ => "Partial"
                };

                reader.Close();
                var cmd2 = con.CreateCommand();
                cmd2.CommandText = @"
                    UPDATE Employees SET Enrolled = $status
                    WHERE UserId = $uid;";
                cmd2.Parameters.AddWithValue("$status", status);
                cmd2.Parameters.AddWithValue("$uid", userId);
                cmd2.ExecuteNonQuery();
            }
        }

        // ═══════════════════════════════════════
        // DEPARTMENTS
        // ═══════════════════════════════════════
        public static void AddDepartment(string name)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO Departments (Name)
                VALUES ($name);";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.ExecuteNonQuery();
        }

        public static List<string> GetDepartments()
        {
            var list = new List<string>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText =
                "SELECT Name FROM Departments ORDER BY Name;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(reader["Name"].ToString() ?? "");
            return list;
        }

        public static void DeleteDepartment(string name)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText =
                "DELETE FROM Departments WHERE Name = $name;";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.ExecuteNonQuery();
        }

        // ═══════════════════════════════════════
        // RFID CARDS
        // ═══════════════════════════════════════
        public static void RegisterRFIDCard(
            string cardId, string userId)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO RFIDCards
                    (CardId, UserId, RegisteredAt)
                VALUES ($cardId, $userId, $date);";
            cmd.Parameters.AddWithValue("$cardId", cardId);
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$date",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public static string? GetUserIdByRFIDCard(string cardId)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT UserId FROM RFIDCards
                WHERE CardId = $cardId;";
            cmd.Parameters.AddWithValue("$cardId", cardId);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }

        // ═══════════════════════════════════════
        // PIN CARDS
        // ═══════════════════════════════════════
        public static void RegisterPIN(string userId, string pin)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            string pinHash = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(pin));

            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO PINCards
                    (UserId, PINHash, RegisteredAt)
                VALUES ($uid, $hash, $date);";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$hash", pinHash);
            cmd.Parameters.AddWithValue("$date",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public static string? GetUserIdByPIN(string pin)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            string pinHash = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(pin));

            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT UserId FROM PINCards
                WHERE PINHash = $hash;";
            cmd.Parameters.AddWithValue("$hash", pinHash);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }

        // ═══════════════════════════════════════
        // ATTENDANCE
        // ═══════════════════════════════════════


        public static void SaveDeviceAttendance(string userId,
            string fullName, string checkIn, string checkOut,
            string method, string status, string hours, string date, string lastActivity)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            var existsCmd = con.CreateCommand();
            existsCmd.CommandText = @"
                SELECT COUNT(*) FROM Attendance
                WHERE UserId = $uid AND Date = $date;";
            existsCmd.Parameters.AddWithValue("$uid", userId);
            existsCmd.Parameters.AddWithValue("$date", date);
            long exists = (long)(existsCmd.ExecuteScalar() ?? 0L);

            var cmd = con.CreateCommand();
            if (exists > 0)
            {
                cmd.CommandText = @"
                    UPDATE Attendance
                    SET FullName = $name,
                        CheckIn  = $checkin,
                        CheckOut = $checkout,
                        Method   = $method,
                        Hours    = $hours,
                        Status   = $status,
                        LastActivity = $lastActivity
                    WHERE UserId = $uid AND Date = $date;";
            }
            else
            {
                cmd.CommandText = @"
                    INSERT INTO Attendance
                        (UserId, FullName, CheckIn, CheckOut,
                         Method, Hours, Status, Date, LastActivity)
                    VALUES
                        ($uid, $name, $checkin, $checkout,
                         $method, $hours, $status, $date, $lastActivity);";
            }

            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$name", fullName);
            cmd.Parameters.AddWithValue("$checkin", checkIn);
            cmd.Parameters.AddWithValue("$checkout", checkOut);
            cmd.Parameters.AddWithValue("$method", method);
            cmd.Parameters.AddWithValue("$hours", hours);
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$date", date);
            cmd.Parameters.AddWithValue("$lastActivity", lastActivity);
            cmd.ExecuteNonQuery();
        }

        public static bool HasCheckedInToday(
            string userId, string date)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Attendance
                WHERE UserId = $uid
                AND Date = $date
                AND CheckIn != '';";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$date", date);
            long count = (long)(cmd.ExecuteScalar() ?? 0L);
            return count > 0;
        }

        public static void UpdateCheckOut(string userId,
            string date, string checkOut, string lastActivity)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            // Get check-in time to calculate hours
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT CheckIn FROM Attendance
                WHERE UserId = $uid AND Date = $date;";
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$date", date);
            string checkIn = cmd.ExecuteScalar()?.ToString() ?? "";

            // Calculate hours worked
            string hoursWorked = "—";
            try
            {
                var inTime = TimeSpan.Parse(checkIn);
                var outTime = TimeSpan.Parse(checkOut);
                var diff = outTime - inTime;
                if (diff.TotalMinutes > 0)
                    hoursWorked =
                        $"{(int)diff.TotalHours}h {diff.Minutes}m";
            }
            catch { }

            // Update
            cmd = con.CreateCommand();
            cmd.CommandText = @"
                UPDATE Attendance
                SET CheckOut = $checkout,
                    Hours    = $hours,
                    LastActivity = $lastActivity
                WHERE UserId = $uid AND Date = $date;";
            cmd.Parameters.AddWithValue("$checkout", checkOut);
            cmd.Parameters.AddWithValue("$hours", hoursWorked);
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$date", date);
            cmd.Parameters.AddWithValue("lastActivity", lastActivity);
            cmd.ExecuteNonQuery();
        }

        public static List<dynamic> GetAttendance(
            string date = "", string search = "",
            string status = "")
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM Attendance
                WHERE ($date = '' OR Date = $date)
                AND ($search = '' OR FullName LIKE $search)
                AND (
                    $status = ''
                    OR Status = $status
                    OR ($status = 'Present' AND Status = 'Late')
                )
                ORDER BY Id DESC;";
            cmd.Parameters.AddWithValue("$date", date);
            cmd.Parameters.AddWithValue("$search",
                string.IsNullOrEmpty(search) ? "" : $"%{search}%");
            cmd.Parameters.AddWithValue("$status",
                status == "All" ? "" : status);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    Name = reader["FullName"].ToString(),
                    UserId = reader["UserId"].ToString(),
                    CheckIn = reader["CheckIn"].ToString(),
                    CheckOut = reader["CheckOut"].ToString(),
                    Method = reader["Method"].ToString(),
                    Hours = reader["Hours"].ToString(),
                    Status = reader["Status"].ToString()
                });
            }
            return list;
        }

        public static List<dynamic> GetRecentCheckIns(int limit = 4)
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            cmd.CommandText = @"
            SELECT FullName, CheckIn, CheckOut, Method, Status, LastActivity
            FROM Attendance
            WHERE Date = $today
            AND CheckIn != '-'
            ORDER BY LastActivity DESC
            ";
            cmd.Parameters.AddWithValue("$today", today);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string checkOut = reader["CheckOut"] == DBNull.Value
                    ? "" : reader["CheckOut"].ToString();
                //string checkOut = reader["CheckOut"] as string ?? "";

                bool hasCheckOut = !string.IsNullOrWhiteSpace(checkOut)
                                   && checkOut != "-";

                // Most recent event per person goes first
                if (hasCheckOut)
                {
                    list.Add(new
                    {
                        Name = reader["FullName"].ToString(),
                        Method = reader["Method"].ToString(),
                        Check = "Checked Out",
                        Status = reader["Status"].ToString(),
                        Activity = reader["LastActivity"].ToString(),
                        Time = checkOut  // checkout time
                    });
                }
                else
                {
                    list.Add(new
                    {
                        Name = reader["FullName"].ToString(),
                        Method = reader["Method"].ToString(),
                        Check = "Checked In",
                        Status = reader["Status"].ToString(),
                        Activity = reader["LastActivity"].ToString(),
                        Time = reader["CheckIn"].ToString()  // checkin time
                    });
                }
            }

            // Sort all events by LastActivity and take most recent 4
            return list
                .OrderByDescending(x => x.Activity)
                .Take(limit)
                .ToList();
        }

        public static List<dynamic> GetWeeklyAttendance()
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            DateTime today = DateTime.Today;
            int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = today.AddDays(-diff);

            for (int i = 0; i < 7; i++)
            {
                DateTime day = monday.AddDays(i);
                string date = day.ToString("yyyy-MM-dd");
                string label = day.ToString("ddd");

                var cmd = con.CreateCommand();
                cmd.CommandText =
                    "SELECT COUNT(*) FROM Employees WHERE Status = 'Active';";
                long total = (long)(cmd.ExecuteScalar() ?? 1L);
                if (total == 0) total = 1;

                cmd = con.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM Attendance
                    WHERE Date = $date
                    AND (Status = 'Present' OR Status = 'Late');";
                cmd.Parameters.AddWithValue("$date", date);
                long present = (long)(cmd.ExecuteScalar() ?? 0L);

                list.Add(new
                {
                    Day = label,
                    Percentage = (int)((present * 100) / total)
                });
            }
            return list;
        }

        // ═══════════════════════════════════════
        // REPORTS
        // ═══════════════════════════════════════
        public static List<dynamic> GetMonthlyReport(
            string month, string department)
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    e.UserId, e.FullName, e.Department,
                    COUNT(CASE WHEN a.Status = 'Present' THEN 1 END) AS Present,
                    COUNT(CASE WHEN a.Status = 'Absent'  THEN 1 END) AS Absent,
                    COUNT(CASE WHEN a.Status = 'Late'    THEN 1 END) AS Late,
                    ROUND(SUM(CAST(REPLACE(a.Hours,'h','') AS REAL)),1) AS TotalHours
                FROM Employees e
                LEFT JOIN Attendance a
                    ON e.UserId = a.UserId
                    AND ($month = '' OR substr(a.Date,1,7) = $month)
                WHERE ($dept = '' OR e.Department = $dept)
                GROUP BY e.UserId, e.FullName
                ORDER BY e.FullName;";
            cmd.Parameters.AddWithValue("$month", month);
            cmd.Parameters.AddWithValue("$dept",
                department == "All Departments" ? "" : department);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(BuildReportRow(reader));
            return list;
        }

        public static List<dynamic> GetWeeklyReport(
            string monday, string department)
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    e.UserId, e.FullName, e.Department,
                    COUNT(CASE WHEN a.Status = 'Present' THEN 1 END) AS Present,
                    COUNT(CASE WHEN a.Status = 'Absent'  THEN 1 END) AS Absent,
                    COUNT(CASE WHEN a.Status = 'Late'    THEN 1 END) AS Late,
                    ROUND(SUM(CAST(REPLACE(a.Hours,'h','') AS REAL)),1) AS TotalHours
                FROM Employees e
                LEFT JOIN Attendance a
                    ON e.UserId = a.UserId
                    AND a.Date >= $monday
                    AND a.Date <= date($monday, '+6 days')
                WHERE ($dept = '' OR e.Department = $dept)
                GROUP BY e.UserId, e.FullName
                ORDER BY e.FullName;";
            cmd.Parameters.AddWithValue("$monday", monday);
            cmd.Parameters.AddWithValue("$dept",
                department == "All Departments" ? "" : department);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(BuildReportRow(reader));
            return list;
        }

        public static List<dynamic> GetDailyReport(
            string date, string department)
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    e.UserId, e.FullName, e.Department,
                    COUNT(CASE WHEN a.Status = 'Present' THEN 1 END) AS Present,
                    COUNT(CASE WHEN a.Status = 'Absent'  THEN 1 END) AS Absent,
                    COUNT(CASE WHEN a.Status = 'Late'    THEN 1 END) AS Late,
                    ROUND(SUM(CAST(REPLACE(a.Hours,'h','') AS REAL)),1) AS TotalHours
                FROM Employees e
                LEFT JOIN Attendance a
                    ON e.UserId = a.UserId
                    AND ($date = '' OR a.Date = $date)
                WHERE ($dept = '' OR e.Department = $dept)
                GROUP BY e.UserId, e.FullName
                ORDER BY e.FullName;";
            cmd.Parameters.AddWithValue("$date", date);
            cmd.Parameters.AddWithValue("$dept",
                department == "All Departments" ? "" : department);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(BuildReportRow(reader));
            return list;
        }

        private static dynamic BuildReportRow(
            SqliteDataReader reader)
        {
            int present = Convert.ToInt32(reader["Present"]);
            int absent = Convert.ToInt32(reader["Absent"]);
            int late = Convert.ToInt32(reader["Late"]);
            int worked = present + late;
            int total = worked + absent;
            string rate = total > 0
                ? $"{(int)(worked * 100.0 / total)}%" : "—";

            string totalHours = "—";
            if (reader["TotalHours"] != DBNull.Value)
            {
                double h = Convert.ToDouble(reader["TotalHours"]);
                if (h > 0) totalHours = $"{h}h";
            }

            return new
            {
                UserId = reader["UserId"].ToString(),
                Name = reader["FullName"].ToString(),
                Department = reader["Department"].ToString(),
                Present = present.ToString(),
                Absent = absent.ToString(),
                Late = late.ToString(),
                TotalHours = totalHours,
                Rate = rate
            };
        }

        // ═══════════════════════════════════════
        // DASHBOARD
        // ═══════════════════════════════════════
        public static dynamic GetDashboardStats()
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            cmd.CommandText =
                "SELECT COUNT(*) FROM Employees WHERE Status = 'Active';";
            long totalEmployees = (long)(cmd.ExecuteScalar() ?? 0L);

            cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Employees
                WHERE Enrolled = 'Pending' AND Status = 'Active';";
            long pendingEnrollment = (long)(cmd.ExecuteScalar() ?? 0L);

            cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Attendance
                WHERE Date = $date
                AND (Status = 'Present' OR Status = 'Late');";
            cmd.Parameters.AddWithValue("$date", today);
            long presentToday = (long)(cmd.ExecuteScalar() ?? 0L);

            cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Attendance
                WHERE Date = $date AND Status = 'Late';";
            cmd.Parameters.AddWithValue("$date", today);
            long lateToday = (long)(cmd.ExecuteScalar() ?? 0L);

            cmd = con.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Attendance
                WHERE Date = $date AND Status = 'Absent';";
            cmd.Parameters.AddWithValue("$date", today);
            long absentToday = (long)(cmd.ExecuteScalar() ?? 0L);

            int attendanceRate = totalEmployees > 0
                ? (int)Math.Min((presentToday * 100) / totalEmployees, 100)
                : 0;

            return new
            {
                TotalEmployees = totalEmployees,
                PendingEnrollment = pendingEnrollment,
                PresentToday = presentToday,
                LateToday = lateToday,
                AbsentToday = absentToday,
                AttendanceRate = attendanceRate
            };
        }

        // ═══════════════════════════════════════
        // DEVICES
        // ═══════════════════════════════════════
        public static void AddDevice(string name, string ip,
            int port, string key)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO Devices
                    (Name, IPAddress, Port, DeviceKey,
                     Status, CreatedAt)
                VALUES
                    ($name, $ip, $port, $key,
                     'Offline', $date);";
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$ip", ip);
            cmd.Parameters.AddWithValue("$port", port);
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$date",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public static List<dynamic> GetAllDevices()
        {
            var list = new List<dynamic>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT * FROM Devices ORDER BY Id;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new
                {
                    Id = reader["Id"].ToString(),
                    Name = reader["Name"].ToString(),
                    IPAddress = reader["IPAddress"].ToString(),
                    Port = reader["Port"].ToString(),
                    Status = reader["Status"].ToString(),
                    FirmwareVersion = reader["FirmwareVersion"].ToString(),
                    LastSeen = reader["LastSeen"].ToString()
                });
            }
            return list;
        }

        public static void UpdateDeviceStatus(string ip,
            string status, string firmware = "",
            string lastSeen = "")
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                UPDATE Devices
                SET Status          = $status,
                    FirmwareVersion = CASE WHEN $fw   = ''
                                     THEN FirmwareVersion ELSE $fw   END,
                    LastSeen        = CASE WHEN $seen = ''
                                     THEN LastSeen        ELSE $seen END
                WHERE IPAddress = $ip;";
            cmd.Parameters.AddWithValue("$status", status);
            cmd.Parameters.AddWithValue("$fw", firmware);
            cmd.Parameters.AddWithValue("$seen", lastSeen);
            cmd.Parameters.AddWithValue("$ip", ip);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateDeviceAddress(string oldAddress,
            string newAddress)
        {
            if (string.IsNullOrWhiteSpace(oldAddress) ||
                string.IsNullOrWhiteSpace(newAddress) ||
                oldAddress == newAddress)
                return;

            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
                UPDATE Devices
                SET IPAddress = $newAddress
                WHERE IPAddress = $oldAddress;";
            cmd.Parameters.AddWithValue("$newAddress", newAddress);
            cmd.Parameters.AddWithValue("$oldAddress", oldAddress);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteDevice(string id)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText =
                "DELETE FROM Devices WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public static void UpdateCheckOutWithHours(
    string userId, string date,
    string checkOut, string hours)
        {
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
        UPDATE Attendance
        SET CheckOut = $checkout,
            Hours    = $hours
        WHERE UserId = $uid
        AND Date     = $date;";
            cmd.Parameters.AddWithValue("$checkout", checkOut);
            cmd.Parameters.AddWithValue("$hours", hours);
            cmd.Parameters.AddWithValue("$uid", userId);
            cmd.Parameters.AddWithValue("$date", date);
            cmd.ExecuteNonQuery();
        }

        public static Dictionary<string, (string LastSeen, bool IsToday, double Rate)>
    GetEmployeeAttendanceStats()
        {
            var result = new Dictionary<string, (string, bool, double)>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();
            var cmd = con.CreateCommand();
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            cmd.CommandText = @"
        SELECT
            e.UserId,
            COUNT(a.Id)                                        AS TotalDays,
            SUM(CASE WHEN a.Date = $today THEN 1 ELSE 0 END)  AS TodayCount,
            MAX(a.LastActivity)                                AS LastActivity,
            MAX(CASE WHEN a.Date = $today THEN a.LastActivity END) AS TodayActivity
        FROM Employees e
        LEFT JOIN Attendance a ON a.UserId = e.UserId
        GROUP BY e.UserId;";

            cmd.Parameters.AddWithValue("$today", today);

            // Also need total working days to compute rate — use 30-day window
            var cmd2 = con.CreateCommand();
            cmd2.CommandText = @"
        SELECT COUNT(DISTINCT Date) FROM Attendance
        WHERE Date >= date('now', '-30 days');";
            long workingDays = (long)(cmd2.ExecuteScalar() ?? 0L);
            if (workingDays == 0) workingDays = 1; // avoid divide-by-zero

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string uid = reader["UserId"].ToString() ?? "";
                long total = reader["TotalDays"] == DBNull.Value ? 0 : (long)reader["TotalDays"];
                long todayHit = reader["TodayCount"] == DBNull.Value ? 0 : (long)reader["TodayCount"];
                string lastAct = reader["LastActivity"] == DBNull.Value ? "" : reader["LastActivity"].ToString() ?? "";

                double rate = Math.Round((double)total / workingDays * 100, 0);
                rate = Math.Min(rate, 100);

                // Format last seen
                string lastSeen = "Never";
                if (!string.IsNullOrEmpty(lastAct) &&
                    DateTime.TryParse(lastAct, out DateTime la))
                {
                    lastSeen = la.Date == DateTime.Today
                        ? $"Today {la:HH:mm}"
                        : la.ToString("MMM dd");
                }

                result[uid] = (lastSeen, todayHit > 0, rate);
            }
            return result;
        }

        public static List<bool> GetEmployeeAttendanceLast10Days(string userId)
        {
            var result = new List<bool>();
            using var con = new SqliteConnection(ConnectionString);
            con.Open();

            for (int i = 9; i >= 0; i--)
            {
                string date = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
                var cmd = con.CreateCommand();
                cmd.CommandText = @"
            SELECT COUNT(*) FROM Attendance
            WHERE UserId = $uid AND Date = $date;";
                cmd.Parameters.AddWithValue("$uid", userId);
                cmd.Parameters.AddWithValue("$date", date);
                long count = (long)(cmd.ExecuteScalar() ?? 0L);
                result.Add(count > 0);
            }
            return result;
        }



    }


}
