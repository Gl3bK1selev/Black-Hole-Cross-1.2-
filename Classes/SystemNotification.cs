using System.Diagnostics;

namespace Black_Hole_Cross;

public class SystemNotification
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success,
    Critical
}
