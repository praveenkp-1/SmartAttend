namespace SmartAttend.Helpers
{
    public static class NameHelper
    {
        private static readonly (string bg, string text)[] AvatarColors = {
            ("#EEF3FA", "#1A3A6B"),
            ("#FAEEDA", "#854F0B"),
            ("#EAF3DE", "#3B6D11"),
            ("#FBEAF0", "#993556"),
            ("#E8F4FD", "#0C6291"),
            ("#F3E8FD", "#6B2FA0"),
        };

        public static (string bg, string text) GetAvatarColor(int index)
            => AvatarColors[index % AvatarColors.Length];
        public static string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var parts = name.Trim().Split(' ');
            if (parts.Length == 1)
                return parts[0][0].ToString().ToUpper();
            return (parts[0][0].ToString() +
                    parts[parts.Length - 1][0].ToString())
                    .ToUpper();
        }
    }

}