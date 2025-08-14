using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Waytotec.ControlSystem.App.Views.Controls
{
    public partial class MediaFoundationRtspViewer : UserControl
    {
        private IntPtr _mediaSession = IntPtr.Zero;
        private IntPtr _mediaSource = IntPtr.Zero;
        private IntPtr _topology = IntPtr.Zero;
        private IntPtr _videoDisplayControl = IntPtr.Zero;
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();
        private readonly object _lock = new();
        private readonly DispatcherTimer _fpsTimer;
        private string _currentUrl = string.Empty;
        private IntPtr _hwndVideo = IntPtr.Zero;

        public MediaFoundationRtspViewer()
        {
            InitializeComponent();

            // Media Foundation 초기화
            var hr = MFStartup(MF_VERSION, MFSTARTUP_LITE);
            if (hr != 0)
            {
                Debug.WriteLine($"Media Foundation 초기화 실패: 0x{hr:X}");
            }

            _fpsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _fpsTimer.Tick += UpdateFpsDisplay;

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 비디오 표시를 위한 윈도우 핸들 준비
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                _hwndVideo = ((HwndSource)source).Handle;
            }
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            await StopAsync();
            MFShutdown();
        }

        public async void Load(string ip, string stream)
        {
            string rtspUrl;

            if (ip.StartsWith("rtsp://"))
            {
                rtspUrl = ip; // 전체 URL이 전달된 경우
            }
            else
            {
                rtspUrl = $"rtsp://admin:admin@{ip}:554/{stream}";
            }

            rtspUrl = "rtsp://210.99.70.120:1935/live/cctv001.stream";

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
                await Task.Run(() => StartMediaFoundationStream(rtspUrl));
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

        private void StartMediaFoundationStream(string rtspUrl)
        {
            try
            {
                int hr;

                // 1. Media Session 생성
                hr = MFCreateMediaSession(IntPtr.Zero, out _mediaSession);
                if (hr != 0) throw new COMException($"Media Session 생성 실패: 0x{hr:X}", hr);

                // 2. Source Resolver로 미디어 소스 생성
                hr = MFCreateSourceResolver(out IntPtr sourceResolver);
                if (hr != 0) throw new COMException($"Source Resolver 생성 실패: 0x{hr:X}", hr);

                // 3. RTSP URL에서 미디어 소스 생성
                hr = CreateObjectFromURL(sourceResolver, rtspUrl, MF_RESOLUTION_MEDIASOURCE,
                    IntPtr.Zero, out MF_OBJECT_TYPE objectType, out _mediaSource);

                Marshal.Release(sourceResolver);

                if (hr != 0) throw new COMException($"RTSP 소스 생성 실패: 0x{hr:X}", hr);

                // 4. 토폴로지 생성
                CreateTopology();

                // 5. Media Session에 토폴로지 설정
                hr = SetTopology(_mediaSession, 0, _topology);
                if (hr != 0) throw new COMException($"토폴로지 설정 실패: 0x{hr:X}", hr);

                // 6. 재생 시작
                var propVar = new PROPVARIANT();
                hr = Start(_mediaSession, ref GUID_NULL, ref propVar);
                if (hr != 0) throw new COMException($"재생 시작 실패: 0x{hr:X}", hr);

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
                MonitorSessionEvents();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Media Foundation 스트림 시작 실패: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    LoadingText.Text = "연결 실패";
                });
                CleanupMediaFoundation();
            }
        }

        private void CreateTopology()
        {
            try
            {
                int hr;

                // 토폴로지 생성
                hr = MFCreateTopology(out _topology);
                if (hr != 0) throw new COMException($"토폴로지 생성 실패: 0x{hr:X}", hr);

                // 프레젠테이션 디스크립터 가져오기
                hr = CreatePresentationDescriptor(_mediaSource, out IntPtr pd);
                if (hr != 0) throw new COMException($"Presentation Descriptor 생성 실패: 0x{hr:X}", hr);

                try
                {
                    // 스트림 개수 가져오기
                    hr = GetStreamDescriptorCount(pd, out uint streamCount);
                    if (hr != 0) return;

                    // 각 스트림에 대해 토폴로지 노드 생성
                    for (uint i = 0; i < streamCount; i++)
                    {
                        hr = GetStreamDescriptorByIndex(pd, i, out bool selected, out IntPtr sd);
                        if (hr != 0 || !selected) continue;

                        try
                        {
                            // 소스 노드 생성
                            hr = MFCreateTopologyNode(MF_TOPOLOGY_SOURCESTREAM_NODE, out IntPtr sourceNode);
                            if (hr != 0) continue;

                            try
                            {
                                // 소스 노드 속성 설정
                                SetUnknown(sourceNode, ref MF_TOPONODE_SOURCE, _mediaSource);
                                SetUnknown(sourceNode, ref MF_TOPONODE_PRESENTATION_DESCRIPTOR, pd);
                                SetUnknown(sourceNode, ref MF_TOPONODE_STREAM_DESCRIPTOR, sd);

                                // 출력 노드 생성 (렌더러)
                                hr = MFCreateTopologyNode(MF_TOPOLOGY_OUTPUT_NODE, out IntPtr outputNode);
                                if (hr != 0) continue;

                                try
                                {
                                    // 비디오 렌더러 생성
                                    CreateVideoRenderer(outputNode);

                                    // 토폴로지에 노드 추가
                                    AddNode(_topology, sourceNode);
                                    AddNode(_topology, outputNode);

                                    // 노드 연결
                                    ConnectOutput(sourceNode, 0, outputNode, 0);
                                }
                                finally
                                {
                                    Marshal.Release(outputNode);
                                }
                            }
                            finally
                            {
                                Marshal.Release(sourceNode);
                            }
                        }
                        finally
                        {
                            Marshal.Release(sd);
                        }
                    }
                }
                finally
                {
                    Marshal.Release(pd);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"토폴로지 생성 오류: {ex.Message}");
            }
        }

        private void CreateVideoRenderer(IntPtr outputNode)
        {
            try
            {
                // Enhanced Video Renderer (EVR) 생성
                var hr = MFCreateVideoRendererActivate(_hwndVideo, out IntPtr activate);
                if (hr == 0)
                {
                    SetObject(outputNode, ref MF_TOPONODE_STREAMDESCRIPTOR, activate);
                    Marshal.Release(activate);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"비디오 렌더러 생성 오류: {ex.Message}");
            }
        }

        private void MonitorSessionEvents()
        {
            try
            {
                while (_isStreaming && _mediaSession != IntPtr.Zero)
                {
                    var hr = GetEvent(_mediaSession, MF_EVENT_FLAG_NO_WAIT, out IntPtr mediaEvent);

                    if (hr == 0 && mediaEvent != IntPtr.Zero)
                    {
                        _frameCount++;

                        // 이벤트 타입 확인
                        GetType(mediaEvent, out MediaEventType eventType);

                        if (eventType == MediaEventType.MESessionEnded ||
                            eventType == MediaEventType.MEError)
                        {
                            Debug.WriteLine($"세션 이벤트: {eventType}");
                            break;
                        }

                        Marshal.Release(mediaEvent);
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

            await Task.Delay(100);
            CleanupMediaFoundation();

            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                _fpsTimer.Stop();
            });
        }

        private void CleanupMediaFoundation()
        {
            try
            {
                if (_mediaSession != IntPtr.Zero)
                {
                    Stop(_mediaSession);
                    Close(_mediaSession);
                    Marshal.Release(_mediaSession);
                    _mediaSession = IntPtr.Zero;
                }

                if (_topology != IntPtr.Zero)
                {
                    Marshal.Release(_topology);
                    _topology = IntPtr.Zero;
                }

                if (_mediaSource != IntPtr.Zero)
                {
                    Shutdown(_mediaSource);
                    Marshal.Release(_mediaSource);
                    _mediaSource = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Media Foundation 정리 오류: {ex.Message}");
            }
        }

        // 테스트용 메서드
        public void LoadTestStream()
        {
            Load("rtsp://210.99.70.120:1935/live/cctv001.stream", "");
        }

        #region Media Foundation COM Interop

        // 상수들
        private const uint MF_VERSION = 0x20070;
        private const uint MFSTARTUP_LITE = 1;
        private const uint MF_RESOLUTION_MEDIASOURCE = 0x00000001;
        private const uint MF_EVENT_FLAG_NO_WAIT = 0x00000001;
        private const uint MF_TOPOLOGY_SOURCESTREAM_NODE = 0;
        private const uint MF_TOPOLOGY_OUTPUT_NODE = 2;

        // GUID들
        private static Guid GUID_NULL = Guid.Empty;
        private static Guid MF_TOPONODE_SOURCE = new("835c58ed-e075-4bc7-bcba-4de000df9ae6");
        private static Guid MF_TOPONODE_PRESENTATION_DESCRIPTOR = new("835c58ee-e075-4bc7-bcba-4de000df9ae6");
        private static Guid MF_TOPONODE_STREAM_DESCRIPTOR = new("835c58ef-e075-4bc7-bcba-4de000df9ae6");
        private static Guid MF_TOPONODE_STREAMDESCRIPTOR = new("835c58ef-e075-4bc7-bcba-4de000df9ae6");

        private enum MF_OBJECT_TYPE
        {
            MF_OBJECT_MEDIASOURCE = 0,
            MF_OBJECT_BYTESTREAM = 1,
            MF_OBJECT_INVALID = 2
        }

        private enum MediaEventType
        {
            MEUnknown = 0,
            MEError = 1,
            MESessionEnded = 110
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPVARIANT
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr data;
        }

        // Media Foundation API들
        [DllImport("mfplat.dll")]
        private static extern int MFStartup(uint version, uint flags);

        [DllImport("mfplat.dll")]
        private static extern int MFShutdown();

        [DllImport("mf.dll")]
        private static extern int MFCreateMediaSession(IntPtr config, out IntPtr session);

        [DllImport("mfplat.dll")]
        private static extern int MFCreateSourceResolver(out IntPtr resolver);

        [DllImport("mfplat.dll")]
        private static extern int MFCreateTopology(out IntPtr topology);

        [DllImport("mfplat.dll")]
        private static extern int MFCreateTopologyNode(uint nodeType, out IntPtr node);

        [DllImport("evr.dll")]
        private static extern int MFCreateVideoRendererActivate(IntPtr hwndVideo, out IntPtr activate);

        // COM 인터페이스 메서드들 (간접 호출)
        private static int CreateObjectFromURL(IntPtr sourceResolver, string url, uint flags,
            IntPtr props, out MF_OBJECT_TYPE objectType, out IntPtr source)
        {
            // 실제 구현에서는 IMFSourceResolver::CreateObjectFromURL 호출
            objectType = MF_OBJECT_TYPE.MF_OBJECT_INVALID;
            source = IntPtr.Zero;
            return unchecked((int)0x80004001); // E_NOTIMPL - 실제로는 COM 인터페이스 호출 필요
        }

        private static int CreatePresentationDescriptor(IntPtr mediaSource, out IntPtr pd)
        {
            pd = IntPtr.Zero;
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int GetStreamDescriptorCount(IntPtr pd, out uint count)
        {
            count = 0;
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int GetStreamDescriptorByIndex(IntPtr pd, uint index, out bool selected, out IntPtr sd)
        {
            selected = false;
            sd = IntPtr.Zero;
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int SetUnknown(IntPtr node, ref Guid key, IntPtr value)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int SetObject(IntPtr node, ref Guid key, IntPtr value)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int AddNode(IntPtr topology, IntPtr node)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int ConnectOutput(IntPtr sourceNode, uint sourceOutput, IntPtr sinkNode, uint sinkInput)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int SetTopology(IntPtr session, uint flags, IntPtr topology)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int Start(IntPtr session, ref Guid format, ref PROPVARIANT startPos)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int Stop(IntPtr session)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int Close(IntPtr session)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int Shutdown(IntPtr mediaSource)
        {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int GetEvent(IntPtr session, uint flags, out IntPtr mediaEvent)
        {
            mediaEvent = IntPtr.Zero;
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private static int GetType(IntPtr mediaEvent, out MediaEventType eventType)
        {
            eventType = MediaEventType.MEUnknown;
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        #endregion
    }
}