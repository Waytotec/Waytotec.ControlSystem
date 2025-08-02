using System.Windows.Input;
using Waytotec.ControlSystem.App.Effects;
using Waytotec.ControlSystem.App.ViewModels.Pages;
using Waytotec.ControlSystem.Core.Models;
using Wpf.Ui.Abstractions.Controls;
using WpfWaytotec.ControlSystem.App.Effects;

namespace Waytotec.ControlSystem.App.Views.Pages
{
    public partial class AboutPage : INavigableView<AboutViewModel>
    {
        public AboutViewModel ViewModel { get; }
        private SnowflakeEffect? _snowflake;

        public AboutPage(AboutViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;

            InitializeComponent();
            Loaded += HandleLoaded;
            Unloaded += HandleUnloaded;
        }

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
            _snowflake ??= new(MainCanvas, 300);            
            _snowflake.Start();
        }
        private void HandleUnloaded(object sender, RoutedEventArgs e)
        {
            _snowflake?.Stop();
            _snowflake = null;
            // Loaded -= HandleLoaded;
            // Unloaded -= HandleUnloaded;
        }


    }
}