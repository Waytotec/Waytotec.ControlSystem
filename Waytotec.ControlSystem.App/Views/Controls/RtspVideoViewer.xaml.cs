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
    /// <summary>
    /// RtspVideoViewer.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class RtspVideoViewer : UserControl
    {
        private VideoCapture? _capture;
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();

        public RtspVideoViewer()
        {
            InitializeComponent();
        }


        public async void Load(string ip, string stream)
        {
            LoadingOverlay.Visibility = Visibility.Visible;

            _isStreaming = false;
            _frameCount = 0;
            _watch.Restart();

            await Task.Run(() =>
            {
                string rtspUrl = $"rtsp://admin:admin@{ip}:554/{stream}";
                // var capture = new VideoCapture(rtspUrl);
                var capture = new VideoCapture();

                // 타임아웃 설정 (가능한 경우)
                capture.Set(VideoCaptureProperties.XI_Timeout, 2000); // 2초 제한

                bool opened = capture.Open(rtspUrl);

                if (!opened || !capture.IsOpened())
                {
                    Dispatcher.Invoke(() =>
                    {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        OverlayFps.Text = "카메라 연결 실패";
                    });
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    _capture = capture;
                    _isStreaming = true;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                });

                using var mat = new Mat();
                while (_isStreaming)
                {
                    _capture.Read(mat);
                    if (mat.Empty()) continue;

                    _frameCount++;

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
                    catch (TaskCanceledException)
                    {
                        // 앱 종료 중이면 무시
                        Stop();
                    }
                    catch (InvalidOperationException)
                    {
                        // Dispatcher가 죽은 상태일 수 있음
                        Stop();
                    }
                }
            });
        }

        public void Stop()
        {
            _isStreaming = false;
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
        }
    }
}
