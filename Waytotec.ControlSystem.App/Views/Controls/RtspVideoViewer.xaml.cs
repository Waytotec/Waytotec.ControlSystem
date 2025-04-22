using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Waytotec.ControlSystem.App.Views.Controls
{
    public partial class RtspVideoViewer : UserControl
    {
        private VideoCapture? _capture;
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();
        private readonly object _lock = new();

        public RtspVideoViewer()
        {
            InitializeComponent();
        }

        public async void Load(string ip, string stream)
        {
            Stop(); // 기존 스트리밍 중지

            LoadingOverlay.Visibility = Visibility.Visible;
            _frameCount = 0;
            _watch.Restart();


            await Task.Run(() =>
            {
                try
                {
                    string rtspUrl = $"rtsp://admin:admin@{ip}:554/{stream}";

                    // ✅ 항상 새 VideoCapture 생성
                    using var capture = new VideoCapture(rtspUrl);
                    if (!capture.IsOpened())
                    {
                        Dispatcher.Invoke(() =>
                        {
                            OverlayFps.Text = "카메라 연결 실패";
                            LoadingOverlay.Visibility = Visibility.Collapsed;
                        });
                        return;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        _isStreaming = true;
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    });

                    using var mat = new Mat();
                    while (_isStreaming)
                    {
                        capture.Read(mat);
                        if (mat.Empty()) continue;

                        _frameCount++;
                        using var bitmap = BitmapConverter.ToBitmap(mat);
                        using var ms = new MemoryStream();
                        bitmap.Save(ms, ImageFormat.Bmp);
                        ms.Position = 0;

                        var bmpImage = new BitmapImage();
                        bmpImage.BeginInit();
                        bmpImage.StreamSource = ms;
                        bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                        bmpImage.EndInit();
                        bmpImage.Freeze();

                        Dispatcher.Invoke(() =>
                        {
                            VideoImage.Source = bmpImage;
                            double elapsed = _watch.Elapsed.TotalSeconds;
                            OverlayFps.Text = $"FPS: {_frameCount / Math.Max(elapsed, 1):F1}";
                            OverlayResolution.Text = $"해상도: {mat.Width}x{mat.Height}";
                            OverlayTime.Text = $"경과시간: {elapsed:F1}초";
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => OverlayFps.Text = $"오류: {ex.Message}");
                }
            });

        }

        private async Task<VideoCapture?> TryConnectRtspAsync(string url, int timeoutMillis = 5000)
        {
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var task = Task.Run(() =>
            {
                var cap = new VideoCapture(url);
                if (cap.IsOpened())
                    return cap;

                cap.Dispose();
                return null;
            }, token);

            var completed = await Task.WhenAny(task, Task.Delay(timeoutMillis, token));
            if (completed == task)
            {
                return await task;
            }
            else
            {
                cts.Cancel();
                return null;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isStreaming = false;
                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
            }
        }
    }
}
