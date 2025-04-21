
using Waytotec.ControlSystem.App.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Waytotec.ControlSystem.App.Views.Pages
{
    public partial class ManualPage : INavigableView<ManualViewModel>
    {
        public ManualViewModel ViewModel { get; }

        public ManualPage(ManualViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}
