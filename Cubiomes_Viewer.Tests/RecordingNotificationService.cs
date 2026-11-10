using Avalonia.Controls.Notifications;

using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.Tests;

internal sealed class RecordingNotificationService : INotificationService
{
    public List<Notification> Notifications { get; } = [];

    public IEnumerable<string?> Errors => Notifications.Where(notification => notification.Type == NotificationType.Error).Select(notification => notification.Message);

    public void Setup(WindowNotificationManager notificationManager) { }

    public void ShowInfo(string message) => ShowInfo(nameof(NotificationType.Information), message);
    public void ShowInfo(string title, string message) => ShowNotification(title, message, NotificationType.Information);
    public void ShowSuccess(string message) => ShowSuccess(nameof(NotificationType.Success), message);
    public void ShowSuccess(string title, string message) => ShowNotification(title, message, NotificationType.Success);
    public void ShowWarning(string message) => ShowWarning(nameof(NotificationType.Warning), message);
    public void ShowWarning(string title, string message) => ShowNotification(title, message, NotificationType.Warning);
    public void ShowError(string message) => ShowError(nameof(NotificationType.Error), message);
    public void ShowError(string title, string message) => ShowNotification(title, message, NotificationType.Error);

    public void ShowNotification(string title, string message, NotificationType type)
        => ShowNotification(new Notification { Type = type, Title = title, Message = message });

    public void ShowNotification(Notification notification) => Notifications.Add(notification);
}
