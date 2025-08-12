using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Waytotec.ControlSystem.App.Views.Controls
{
    /// <summary>
    /// MediaFoundationRtspViewer.xaml에 대한 상호 작용 논리
    /// </summary>    
    public partial class MediaFoundationRtspViewer : UserControl
    {
        private IntPtr _mediaSession = IntPtr.Zero;
        private IntPtr _topology = IntPtr.Zero;
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();
        private readonly object _lock = new();
        private CancellationTokenSource? _streamCts;
        private readonly DispatcherTimer _fpsTimer;
        private string _currentUrl = string.Empty;

        public MediaFoundationRtspViewer()
        {
            InitializeComponent();

            // Media Foundation 초기화
            MFStartup();

            _fpsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _fpsTimer.Tick += UpdateFpsDisplay;

            // UserControl 이벤트 구독
            this.Unloaded += OnUnloaded;
        }

        public async void Load(string ip, string stream)
        {
            string rtspUrl = $"rtsp://admin:admin@{ip}:554/{stream}";

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

            _streamCts = new CancellationTokenSource();
            var token = _streamCts.Token;

            try
            {
                await Task.Run(() => StartMediaFoundationStream(rtspUrl, token), token);
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"스트림 오류: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    LoadingText.Text = "연결 실패";
                });
            }
        }

        private void StartMediaFoundationStream(string rtspUrl, CancellationToken token)
        {
            try
            {
                // Media Session 생성
                MFCreateMediaSession(IntPtr.Zero, out _mediaSession);

                // Source Resolver로 RTSP URL 해석
                MFCreateSourceResolver(out IntPtr sourceResolver);

                var hr = MFCreateSourceReaderFromURL(rtspUrl, IntPtr.Zero, out IntPtr sourceReader);
                if (hr != 0)
                {
                    throw new COMException($"RTSP 연결 실패: {rtspUrl}", hr);
                }

                // 비디오 스트림 설정
                ConfigureVideoStream(sourceReader);

                lock (_lock)
                {
                    _isStreaming = true;
                }

                Dispatcher.Invoke(() =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    _fpsTimer.Start();
                });

                // 프레임 읽기 루프
                ReadFrameLoop(sourceReader, token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Media Foundation 스트림 시작 실패: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    LoadingText.Text = $"연결 실패: {ex.Message}";
                });
            }
        }

        private void ConfigureVideoStream(IntPtr sourceReader)
        {
            // 미디어 타입 설정 (RGB32로 변환)
            MFCreateMediaType(out IntPtr mediaType);

            // 비디오 포맷 설정
            MFSetAttributeGUID(mediaType, MF_MT_MAJOR_TYPE, MFMediaType_Video);
            MFSetAttributeGUID(mediaType, MF_MT_SUBTYPE, MFVideoFormat_RGB32);

            // Source Reader에 미디어 타입 설정
            MFSourceReaderSetCurrentMediaType(sourceReader, 0, IntPtr.Zero, mediaType);

            // 비디오 정보 가져오기
            MFSourceReaderGetCurrentMediaType(sourceReader, 0, out IntPtr currentType);

            MFGetAttributeSize(currentType, MF_MT_FRAME_SIZE, out uint width, out uint height);
            Debug.WriteLine($"비디오 해상도: {width}x{height}");

            Marshal.Release(mediaType);
            Marshal.Release(currentType);
        }

        private void ReadFrameLoop(IntPtr sourceReader, CancellationToken token)
        {
            while (_isStreaming && !token.IsCancellationRequested)
            {
                try
                {
                    var hr = MFSourceReaderReadSample(sourceReader, 0, 0, out int streamIndex,
                        out int flags, out long timestamp, out IntPtr sample);

                    if (hr != 0 || sample == IntPtr.Zero)
                    {
                        Thread.Sleep(33); // ~30fps
                        continue;
                    }

                    ProcessVideoSample(sample);
                    Marshal.Release(sample);

                    _frameCount++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"프레임 읽기 오류: {ex.Message}");
                    break;
                }
            }

            if (sourceReader != IntPtr.Zero)
                Marshal.Release(sourceReader);
        }

        private void ProcessVideoSample(IntPtr sample)
        {
            try
            {
                // Media Buffer 가져오기
                MFSampleGetBufferByIndex(sample, 0, out IntPtr buffer);

                // 버퍼 데이터 접근
                MFMediaBufferLock(buffer, out IntPtr data, out int maxLength, out int currentLength);

                if (data != IntPtr.Zero && currentLength > 0)
                {
                    // RGB 데이터를 Bitmap으로 변환
                    var bitmap = CreateBitmapFromRgbData(data, currentLength);
                    if (bitmap != null)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (_isStreaming)
                                VideoImage.Source = bitmap;
                        });
                    }
                }

                MFMediaBufferUnlock(buffer);
                Marshal.Release(buffer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"비디오 샘플 처리 오류: {ex.Message}");
            }
        }

        private BitmapSource? CreateBitmapFromRgbData(IntPtr data, int length)
        {
            try
            {
                // 예상 해상도 (실제로는 미디어 타입에서 가져와야 함)
                int width = 640;
                int height = 480;
                int stride = width * 4; // RGB32 = 4 bytes per pixel

                if (length < stride * height)
                    return null;

                var bitmap = BitmapSource.Create(
                    width, height,
                    96, 96, // DPI
                    System.Windows.Media.PixelFormats.Bgr32,
                    null,
                    data,
                    length,
                    stride);

                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bitmap 생성 오류: {ex.Message}");
                return null;
            }
        }

        private void UpdateFpsDisplay(object? sender, EventArgs e)
        {
            if (!_isStreaming) return;

            var elapsed = _watch.Elapsed.TotalSeconds;
            var fps = _frameCount / Math.Max(elapsed, 1);

            OverlayFps.Text = $"FPS: {fps:F1}";
            OverlayResolution.Text = "해상도: 640x480";
            OverlayTime.Text = $"경과시간: {elapsed:F1}초";
        }

        public async Task StopAsync()
        {
            lock (_lock)
            {
                _isStreaming = false;
            }

            _streamCts?.Cancel();

            await Task.Delay(100); // 정리 시간 확보

            if (_mediaSession != IntPtr.Zero)
            {
                MFMediaSessionClose(_mediaSession);
                Marshal.Release(_mediaSession);
                _mediaSession = IntPtr.Zero;
            }

            if (_topology != IntPtr.Zero)
            {
                Marshal.Release(_topology);
                _topology = IntPtr.Zero;
            }

            _streamCts?.Dispose();
            _streamCts = null;

            Dispatcher.Invoke(() =>
            {
                VideoImage.Source = null;
                LoadingOverlay.Visibility = Visibility.Collapsed;
                _fpsTimer.Stop();
            });
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            await StopAsync();
            MFShutdown();
        }

        #region Media Foundation P/Invoke 선언

        [DllImport("mfplat.dll")]
        private static extern int MFStartup(uint version = 0x20070, uint flags = 0);

        [DllImport("mfplat.dll")]
        private static extern int MFShutdown();

        [DllImport("mf.dll")]
        private static extern int MFCreateMediaSession(IntPtr config, out IntPtr session);

        [DllImport("mf.dll")]
        private static extern int MFMediaSessionClose(IntPtr session);

        [DllImport("mfreadwrite.dll")]
        private static extern int MFCreateSourceReaderFromURL(
            [MarshalAs(UnmanagedType.LPWStr)] string url,
            IntPtr attributes,
            out IntPtr sourceReader);

        [DllImport("mfplat.dll")]
        private static extern int MFCreateSourceResolver(out IntPtr resolver);

        [DllImport("mfplat.dll")]
        private static extern int MFCreateMediaType(out IntPtr mediaType);

        [DllImport("mfreadwrite.dll")]
        private static extern int MFSourceReaderSetCurrentMediaType(
            IntPtr reader, int streamIndex, IntPtr reserved, IntPtr mediaType);

        [DllImport("mfreadwrite.dll")]
        private static extern int MFSourceReaderGetCurrentMediaType(
            IntPtr reader, int streamIndex, out IntPtr mediaType);

        [DllImport("mfreadwrite.dll")]
        private static extern int MFSourceReaderReadSample(
            IntPtr reader, int streamIndex, int flags,
            out int actualStreamIndex, out int streamFlags,
            out long timestamp, out IntPtr sample);

        [DllImport("mfplat.dll")]
        private static extern int MFSampleGetBufferByIndex(IntPtr sample, int index, out IntPtr buffer);

        [DllImport("mfplat.dll")]
        private static extern int MFMediaBufferLock(IntPtr buffer, out IntPtr data, out int maxLength, out int currentLength);

        [DllImport("mfplat.dll")]
        private static extern int MFMediaBufferUnlock(IntPtr buffer);

        [DllImport("mfplat.dll")]
        private static extern int MFSetAttributeGUID(IntPtr attributes, Guid key, Guid value);

        [DllImport("mfplat.dll")]
        private static extern int MFGetAttributeSize(IntPtr attributes, Guid key, out uint width, out uint height);

        // Media Foundation 상수들
        private static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private static readonly Guid MF_MT_FRAME_SIZE = new("b725dc7e-8efc-4963-bc0c-dd969ec5c3f7");
        private static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00aa00389b71");
        private static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00aa00389b71");

        #endregion
    }

}
