using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Waytotec.ControlSystem.App.Views.Controls
{
    public partial class DirectShowRtspViewer : UserControl
    {
        private IntPtr _filterGraph = IntPtr.Zero;
        private IntPtr _mediaControl = IntPtr.Zero;
        private IntPtr _videoWindow = IntPtr.Zero;
        private IntPtr _mediaEvent = IntPtr.Zero;
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();
        private readonly object _lock = new();
        private readonly DispatcherTimer _fpsTimer;
        private string _currentUrl = string.Empty;
        private HwndHost? _videoHost;

        public DirectShowRtspViewer()
        {
            InitializeComponent();

            // COM 초기화
            CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED);

            _fpsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _fpsTimer.Tick += UpdateFpsDisplay;

            this.Unloaded += OnUnloaded;
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            await StopAsync();
            CoUninitialize();
        }

        public async void Load(string ip, string stream)
        {
            // string rtspUrl = $"rtsp://admin:admin@{ip}:554/{stream}";
            // https://www.codeproject.com/Articles/1017223/CaptureManager-SDK-Capturing-Recording-and-Streami#sixtythdemoprogram
            string rtspUrl = $"rtsp://210.99.70.120:1935/live/cctv001.stream";

            if (_isStreaming && rtspUrl == _currentUrl)
                return;

            await StopAsync();
            await StartStreamAsync(rtspUrl);
        }

        private async Task StartStreamAsync(string rtspUrl)
        {
            _currentUrl = rtspUrl;
            _frameCount = 0;
            _watch.Restart();

            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "연결 중...";
            });

            try
            {
                await Task.Run(() => StartDirectShowStream(rtspUrl));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"스트림 오류: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    LoadingText.Text = $"연결 실패: {ex.Message}";
                });
            }
        }

        private void StartDirectShowStream(string rtspUrl)
        {
            try
            {
                // Filter Graph Manager 생성
                var hr = CoCreateInstance(ref CLSID_FilterGraph, IntPtr.Zero, CLSCTX_INPROC_SERVER,
                    ref IID_IGraphBuilder, out _filterGraph);

                if (hr != 0 || _filterGraph == IntPtr.Zero)
                    throw new COMException("Filter Graph 생성 실패", hr);

                // Media Control 인터페이스 가져오기
                hr = Marshal.QueryInterface(_filterGraph, ref IID_IMediaControl, out _mediaControl);
                if (hr != 0) throw new COMException("Media Control 인터페이스 실패", hr);

                // Video Window 인터페이스 가져오기
                hr = Marshal.QueryInterface(_filterGraph, ref IID_IVideoWindow, out _videoWindow);
                if (hr != 0) throw new COMException("Video Window 인터페이스 실패", hr);

                // Media Event 인터페이스 가져오기
                hr = Marshal.QueryInterface(_filterGraph, ref IID_IMediaEvent, out _mediaEvent);
                if (hr != 0) throw new COMException("Media Event 인터페이스 실패", hr);

                // RTSP URL로 그래프 빌드
                hr = RenderFile(_filterGraph, rtspUrl);
                if (hr != 0) throw new COMException($"RTSP 연결 실패: {rtspUrl}", hr);

                // 비디오 윈도우 설정
                SetupVideoWindow();

                // 재생 시작
                hr = Run(_mediaControl);
                if (hr != 0) throw new COMException("재생 시작 실패", hr);

                lock (_lock)
                {
                    _isStreaming = true;
                }

                Dispatcher.Invoke(() =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    _fpsTimer.Start();
                });

                // 이벤트 처리 루프
                MonitorMediaEvents();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DirectShow 스트림 시작 실패: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    LoadingText.Text = $"연결 실패";
                });
                CleanupDirectShow();
            }
        }

        private void SetupVideoWindow()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // WPF 컨트롤의 핸들 가져오기
                    var source = PresentationSource.FromVisual(this);
                    if (source?.CompositionTarget is HwndTarget hwndTarget)
                    {
                        var hwnd = new WindowInteropHelper(Window.GetWindow(this)).Handle;

                        // 비디오 윈도우 부모 설정
                        SetOwner(_videoWindow, hwnd);
                        SetWindowStyle(_videoWindow, WS_CHILD | WS_CLIPSIBLINGS);

                        // 비디오 크기 설정
                        var rect = new RECT
                        {
                            left = 0,
                            top = 0,
                            right = (int)this.ActualWidth,
                            bottom = (int)this.ActualHeight
                        };

                        SetWindowPosition(_videoWindow, rect.left, rect.top,
                            rect.right - rect.left, rect.bottom - rect.top);

                        SetVisible(_videoWindow, true);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"비디오 윈도우 설정 오류: {ex.Message}");
            }
        }

        private void MonitorMediaEvents()
        {
            try
            {
                while (_isStreaming)
                {
                    var hr = GetEvent(_mediaEvent, out int eventCode, out IntPtr param1, out IntPtr param2, 100);

                    if (hr == 0) // 이벤트 있음
                    {
                        _frameCount++;

                        // 이벤트 해제
                        FreeEventParams(_mediaEvent, eventCode, param1, param2);

                        // 종료 이벤트 확인
                        if (eventCode == EC_COMPLETE || eventCode == EC_USERABORT)
                        {
                            Debug.WriteLine("미디어 재생 완료 또는 중단");
                            break;
                        }
                    }

                    Thread.Sleep(33); // ~30fps
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"이벤트 모니터링 오류: {ex.Message}");
            }
        }

        private void UpdateFpsDisplay(object? sender, EventArgs e)
        {
            if (!_isStreaming) return;

            var elapsed = _watch.Elapsed.TotalSeconds;
            var fps = _frameCount / Math.Max(elapsed, 1);

            OverlayFps.Text = $"FPS: {fps:F1}";
            OverlayResolution.Text = "해상도: Auto";
            OverlayTime.Text = $"경과시간: {elapsed:F1}초";
        }

        public async Task StopAsync()
        {
            lock (_lock)
            {
                _isStreaming = false;
            }

            await Task.Delay(100); // 정리 시간 확보

            CleanupDirectShow();

            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                _fpsTimer.Stop();
            });
        }

        private void CleanupDirectShow()
        {
            try
            {
                if (_mediaControl != IntPtr.Zero)
                {
                    Stop(_mediaControl);
                    Marshal.Release(_mediaControl);
                    _mediaControl = IntPtr.Zero;
                }

                if (_videoWindow != IntPtr.Zero)
                {
                    SetVisible(_videoWindow, false);
                    SetOwner(_videoWindow, IntPtr.Zero);
                    Marshal.Release(_videoWindow);
                    _videoWindow = IntPtr.Zero;
                }

                if (_mediaEvent != IntPtr.Zero)
                {
                    Marshal.Release(_mediaEvent);
                    _mediaEvent = IntPtr.Zero;
                }

                if (_filterGraph != IntPtr.Zero)
                {
                    Marshal.Release(_filterGraph);
                    _filterGraph = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DirectShow 정리 오류: {ex.Message}");
            }
        }

        #region DirectShow COM Interop

        // COM 초기화/정리
        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();

        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance([In] ref Guid rclsid, IntPtr pUnkOuter,
            uint dwClsContext, [In] ref Guid riid, out IntPtr ppv);

        // DirectShow 메서드들
        [DllImport("quartz.dll", CharSet = CharSet.Unicode)]
        private static extern int RenderFile(IntPtr graphBuilder, string filename);

        [DllImport("quartz.dll")]
        private static extern int Run(IntPtr mediaControl);

        [DllImport("quartz.dll")]
        private static extern int Stop(IntPtr mediaControl);

        [DllImport("quartz.dll")]
        private static extern int SetOwner(IntPtr videoWindow, IntPtr hwnd);

        [DllImport("quartz.dll")]
        private static extern int SetWindowStyle(IntPtr videoWindow, int style);

        [DllImport("quartz.dll")]
        private static extern int SetWindowPosition(IntPtr videoWindow, int left, int top, int width, int height);

        [DllImport("quartz.dll")]
        private static extern int SetVisible(IntPtr videoWindow, bool visible);

        [DllImport("quartz.dll")]
        private static extern int GetEvent(IntPtr mediaEvent, out int eventCode,
            out IntPtr param1, out IntPtr param2, int timeout);

        [DllImport("quartz.dll")]
        private static extern int FreeEventParams(IntPtr mediaEvent, int eventCode,
            IntPtr param1, IntPtr param2);

        // 상수들
        private const uint COINIT_APARTMENTTHREADED = 0x2;
        private const uint CLSCTX_INPROC_SERVER = 0x1;
        private const int WS_CHILD = 0x40000000;
        private const int WS_CLIPSIBLINGS = 0x04000000;
        private const int EC_COMPLETE = 0x01;
        private const int EC_USERABORT = 0x02;

        // GUID들 (static readonly 대신 static으로 변경)
        private static Guid CLSID_FilterGraph = new("e436ebb3-524f-11ce-9f53-0020af0ba770");
        private static Guid IID_IGraphBuilder = new("56a868a9-0ad4-11ce-b03a-0020af0ba770");
        private static Guid IID_IMediaControl = new("56a868b1-0ad4-11ce-b03a-0020af0ba770");
        private static Guid IID_IVideoWindow = new("56a868b4-0ad4-11ce-b03a-0020af0ba770");
        private static Guid IID_IMediaEvent = new("56a868b6-0ad4-11ce-b03a-0020af0ba770");

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        #endregion
    }
}