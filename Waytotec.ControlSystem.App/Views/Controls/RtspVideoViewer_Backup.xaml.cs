using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Waytotec.ControlSystem.App.Views.Controls
{
    public partial class RtspVideoViewer_Backup : UserControl
    {
        private VideoCapture? _capture;
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();
        private readonly object _lock = new();

        public RtspVideoViewer_Backup()
        {
            InitializeComponent();
        }

        public async void Load(string ip, string stream)
        {
            Stop(); // 기존 스트리밍 중지
            LoadingOverlay.Visibility = Visibility.Visible;
            _frameCount = 0;
            _watch.Restart();

            string rtspUrl = $"rtsp://admin:admin@{ip}:554/{stream}";
            var capture = await TryConnectRtspAsync(rtspUrl, timeoutMillis: 5000);

            if (capture == null)
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    OverlayFps.Text = "카메라 연결 실패";

                    // NoCamera.png 리소스 이미지 표시
                    VideoImage.Source = new BitmapImage(new Uri("pack://application:,,,/Waytotec.ControlSystem.App;component/Resources/NoCamera.png"));

                });
                await Task.Delay(5000); // 내부 socket/RTSP 해제를 기다림
                return;
            }

            Dispatcher.Invoke(() =>
            {
                lock (_lock)
                {
                    _capture = capture;
                    _isStreaming = true;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            });

            await Task.Delay(500);

            await Task.Run(() =>
            {
                using var mat = new Mat();

                while (true)
                {
                    lock (_lock)
                    {
                        if (!_isStreaming || _capture == null || _capture.IsDisposed)
                            break;

                        try
                        {
                            if (!_capture.Read(mat))
                                continue;
                        }
                        catch (AccessViolationException ex)
                        {
                            Debug.WriteLine($"[ERROR] AccessViolation in Read: {ex.Message}");
                            break;
                        }
                    }

                    if (mat.Empty()) continue;

                    using Bitmap bitmap = BitmapConverter.ToBitmap(mat);
                    using MemoryStream ms = new();
                    bitmap.Save(ms, ImageFormat.Bmp);
                    ms.Position = 0;

                    BitmapImage bmpImage = new();
                    bmpImage.BeginInit();
                    bmpImage.StreamSource = ms;
                    bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                    bmpImage.EndInit();
                    bmpImage.Freeze();

                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (!_isStreaming || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                            {
                                Stop();
                                return;
                            }

                            VideoImage.Source = bmpImage;
                            double elapsed = _watch.Elapsed.TotalSeconds;
                            OverlayFps.Text = $"FPS: {_frameCount / Math.Max(elapsed, 1):F1}";
                            OverlayResolution.Text = $"해상도: {mat.Width}x{mat.Height}";
                            OverlayTime.Text = $"경과시간: {elapsed:F1}초";
                        });
                    }
                    catch (TaskCanceledException) { Stop(); }
                    catch (InvalidOperationException) { Stop(); }

                    _frameCount++;
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
