using Avalonia.Controls.Notifications;
using Avalonia.Threading;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Service.NotificationServ;

public sealed class NotificationService : INotificationService
{
    private const string InfoTitle = "Information";
    private const string SuccessTitle = "Success";
    private const string WarningTitle = "Warning";
    private const string ErrorTitle = "Error";
    private const string ClickableClass = "clickable";

    private static readonly TimeSpan _clickableExtraTime = TimeSpan.FromSeconds(5);

    private WindowNotificationManager? _manager;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void Setup(WindowNotificationManager notificationManager) => _manager = notificationManager;

    public void ShowInfo(string message) => ShowInfo(InfoTitle, message);
    public void ShowInfo(string title, string message) => ShowNotification(title, message, NotificationType.Information);

    public void ShowSuccess(string message) => ShowSuccess(SuccessTitle, message);
    public void ShowSuccess(string title, string message) => ShowNotification(title, message, NotificationType.Success);

    public void ShowWarning(string message) => ShowWarning(WarningTitle, message);
    public void ShowWarning(string title, string message) => ShowNotification(title, message, NotificationType.Warning);

    public void ShowError(string message) => ShowError(ErrorTitle, message);
    public void ShowError(string title, string message) => ShowNotification(title, message, NotificationType.Error);

    public void ShowNotification(string title, string message, NotificationType type)
        => ShowNotification(new Notification { Type = type, Title = title, Message = message });

    public void ShowNotification(Notification notification)
    {
        try
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => ShowNotification(notification));
                return;
            }

            string[] classes = notification.OnClick is null ? [] : [ClickableClass];
            if (classes.Length != 0)
                notification.Expiration += _clickableExtraTime;

            _logger.LogDebug("Showing notification: type={Type}, title={Title}", notification.Type, notification.Title);
            _manager?.Show(notification, notification.Type, notification.Expiration, notification.OnClick, notification.OnClose, classes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show notification: {Title}", notification.Title);
        }
    }
}
