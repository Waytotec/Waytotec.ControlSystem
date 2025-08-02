using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Waytotec.ControlSystem.App.Services;
using Waytotec.ControlSystem.Core.Interfaces;
using Wpf.Ui.Controls;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace Waytotec.ControlSystem.App.ViewModels
{
    public abstract class BaseViewModel : ObservableObject
    {
        protected readonly IAppMessagingService Messaging;

        protected BaseViewModel(IAppMessagingService messagingService)
        {
            Messaging = messagingService;
        }

        // MessageBox 관련 메서드
        public Task<MessageBoxResult> ShowInfoAsync(string message, string title = "정보")
            => Messaging.ShowMessageBoxAsync(message, title, MessageBoxType.Information);

        public Task<MessageBoxResult> ShowQuestionAsync(string message, string title = "확인")
            => Messaging.ShowMessageBoxAsync(message, title, MessageBoxType.Question);

        public Task<MessageBoxResult> ShowQuestionWithCancelAsync(string message, string title = "확인")
            => Messaging.ShowMessageBoxAsync(message, title, MessageBoxType.QuestionWithCancel);

        public Task<MessageBoxResult> ShowWarningAsync(string message, string title = "경고")
            => Messaging.ShowMessageBoxAsync(message, title, MessageBoxType.Warning);

        public Task<MessageBoxResult> ShowErrorAsync(string message, string title = "오류")
            => Messaging.ShowMessageBoxAsync(message, title, MessageBoxType.Error);

        // Snackbar 관련 메서드
        public void ShowSnackbar(string title, string message, ControlAppearance appearance = ControlAppearance.Primary)
            => Messaging.ShowSnackbar(title, message, appearance);

        // ContentDialog 관련 메서드
        public Task<ContentDialogResult> ShowDialogAsync(string title, string content)
            => Messaging.ShowDialogAsync(title, content);
    }
}
