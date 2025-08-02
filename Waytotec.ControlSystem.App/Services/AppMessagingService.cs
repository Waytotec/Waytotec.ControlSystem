using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Waytotec.ControlSystem.App.Services
{
    public class AppMessagingService : IAppMessagingService
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly ISnackbarService _snackbarService;

        public AppMessagingService(IContentDialogService contentDialogService,
            ISnackbarService snackbarService)
        {
            _contentDialogService = contentDialogService;   
            _snackbarService = snackbarService;
        }

        public void ShowSnackbar(string title, string message, ControlAppearance appearance = ControlAppearance.Primary, IconElement icon = null)
        {
            icon ??= new SymbolIcon(SymbolRegular.Info28);
            _snackbarService.Show(title, message, appearance, icon, TimeSpan.FromSeconds(3));
        }

        public async Task<Wpf.Ui.Controls.MessageBoxResult> ShowMessageBoxAsync(string message, string title = "알림", MessageBoxType type = MessageBoxType.Information)
        {
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = message,
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            // 타입별 버튼 설정
            switch (type)
            {
                case MessageBoxType.Information:
                    messageBox.PrimaryButtonText = "확인";
                    messageBox.PrimaryButtonAppearance = ControlAppearance.Primary;
                    messageBox.CloseButtonText = null;
                    break;

                case MessageBoxType.Question:
                    messageBox.PrimaryButtonText = "예";
                    messageBox.CloseButtonText = "아니오";
                    messageBox.PrimaryButtonAppearance = ControlAppearance.Primary;
                    break;

                case MessageBoxType.QuestionWithCancel:
                    messageBox.PrimaryButtonText = "예";
                    messageBox.SecondaryButtonText = "아니오";
                    messageBox.CloseButtonText = "취소";
                    messageBox.PrimaryButtonAppearance = ControlAppearance.Primary;
                    break;

                case MessageBoxType.Warning:
                    messageBox.PrimaryButtonText = "확인";
                    messageBox.CloseButtonText = "취소";
                    messageBox.PrimaryButtonAppearance = ControlAppearance.Caution;
                    break;

                case MessageBoxType.Error:
                    messageBox.PrimaryButtonText = "확인";
                    messageBox.CloseButtonText = null;
                    messageBox.PrimaryButtonAppearance = ControlAppearance.Danger;
                    break;
            }

            return await messageBox.ShowDialogAsync();
        }

        public async Task<ContentDialogResult> ShowDialogAsync(string title, string content,
            string primaryText = "확인", string closeText = "취소")
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = primaryText,
                CloseButtonText = closeText,
                DefaultButton = ContentDialogButton.Primary
            };

            return await _contentDialogService.ShowAsync(dialog, CancellationToken.None);
        }
    }
}
