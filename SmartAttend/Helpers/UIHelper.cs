namespace SmartAttend.Helpers
{
    public static class UIHelper
    {
        // ── Initials ───────────────────────────────────────
        public static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ');
            return parts.Length > 1
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                : name[0].ToString().ToUpper();
        }

        // ── Avatar colors ──────────────────────────────────
        public static readonly (string bg, string text)[] AvatarColors = {
            ("#EEF3FA", "#1A3A6B"),
            ("#FAEEDA", "#854F0B"),
            ("#EAF3DE", "#3B6D11"),
            ("#FBEAF0", "#993556"),
            ("#E8F4FD", "#0C6291"),
            ("#F3E8FD", "#6B2FA0"),
        };

        public static (string bg, string text) GetAvatarColor(int index)
            => AvatarColors[index % AvatarColors.Length];
    }
}