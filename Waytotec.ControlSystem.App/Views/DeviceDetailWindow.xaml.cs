
using System.Windows;
using Waytotec.ControlSystem.Core.Models;

namespace Waytotec.ControlSystem.App.Views
{
    public partial class DeviceDetailWindow : Window
    {
        public DeviceDetailWindow(DeviceStatus device)
        {
            InitializeComponent();
            this.DataContext = device;
        }
    }
}
