namespace Waytotec.ControlSystem.App.Views
{
    /// <summary>
    /// RtspPopupWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class RtspPopupWindow : Window
    {
        public RtspPopupWindow(string ip, string stream)
        {
            InitializeComponent();
            RtspViewer.Load(ip, stream);
        }
    }
}
