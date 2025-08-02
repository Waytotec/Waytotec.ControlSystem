using Wpf.Ui.Controls;

namespace Waytotec.ControlSystem.App.Services
{
    public interface IAppMessagingService
    {
        void ShowSnackbar(string title, string message, ControlAppearance appearance = ControlAppearance.Primary, IconElement icon = null);
        Task<Wpf.Ui.Controls.MessageBoxResult> ShowMessageBoxAsync(string message, string title = "알림", MessageBoxType type = MessageBoxType.Information);
        Task<ContentDialogResult> ShowDialogAsync(string title, string content, string primaryText = "확인", string closeText = "취소");
    }

    public enum MessageBoxType
    {
        Information,
        Question,
        QuestionWithCancel,
        Warning,
        Error
    }
}
