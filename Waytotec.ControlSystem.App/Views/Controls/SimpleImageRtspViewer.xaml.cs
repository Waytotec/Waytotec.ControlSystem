using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Waytotec.ControlSystem.App.Views.Controls
{
    /// <summary>
    /// SimpleImageRtspViewer.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SimpleImageRtspViewer : UserControl
    {
        private static readonly HttpClient _httpClient = new();
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();
        private readonly DispatcherTimer _fpsTimer;
        private string _currentIp = string.Empty;
        private CancellationTokenSource? _streamCts;
        private Task? _streamTask;

        public SimpleImageRtspViewer()
        {
            InitializeComponent();

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
        }

        public async void Load(string ip, string stream)
        {
            if (_isStreaming && ip == _currentIp)
                return;

            await StopAsync();
            await StartStreamAsync(ip);
        }

        private async Task StartStreamAsync(string ip)
        {
            ip = "210.99.70.120:1935";
            _currentIp = ip;
            _frameCount = 0;
            _watch.Restart();

            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "연결 중...";
            });

            _streamCts = new CancellationTokenSource();
            var token = _streamCts.Token;

            _streamTask = Task.Run(async () =>
            {
                // 여러 URL 패턴 시도
                var urlPatterns = new[]
                {
                    $"http://{ip}/mjpeg",                    // 일반적인 MJPEG 스트림
                    $"http://{ip}:8080/video.mjpg",          // 대안 포트
                    $"http://{ip}/video.cgi?resolution=320x240", // CGI 스타일
                    $"http://{ip}/snapshot.jpg",             // 스냅샷 방식
                    $"http://{ip}/img/video.asf",            // ASF 스트림
                    $"http://{ip}/cgi-bin/mjpg/video.cgi"    // CGI-BIN 방식
                };

                foreach (var url in urlPatterns)
                {
                    if (token.IsCancellationRequested)
                        return;

                    try
                    {
                        Debug.WriteLine($"시도 중: {url}");

                        if (await TryStreamUrl(url, token))
                        {
                            Debug.WriteLine($"성공: {url}");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"실패 {url}: {ex.Message}");
                    }
                }

                // 모든 URL 실패 시 폴백
                await TrySnapshotMode(ip, token);
            }, token);
        }

        private async Task<bool> TryStreamUrl(string url, CancellationToken token)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);

                if (!response.IsSuccessStatusCode)
                    return false;

                var contentType = response.Content.Headers.ContentType?.MediaType;
                Debug.WriteLine($"Content-Type: {contentType}");

                if (contentType?.Contains("multipart") == true)
                {
                    // MJPEG 스트림 처리
                    await ProcessMjpegStream(response, token);
                    return true;
                }
                else if (contentType?.Contains("image") == true)
                {
                    // 단일 이미지 처리
                    await ProcessSingleImage(response, token);
                    return true;
                }

                return false;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

        private async Task ProcessMjpegStream(HttpResponseMessage response, CancellationToken token)
        {
            _isStreaming = true;

            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                _fpsTimer.Start();
            });

            using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[1024 * 1024]; // 1MB 버퍼

            while (_isStreaming && !token.IsCancellationRequested)
            {
                try
                {
                    // MJPEG 바운더리 찾기 및 이미지 추출
                    var imageData = await ExtractImageFromMjpegStream(stream, buffer, token);

                    if (imageData != null && imageData.Length > 0)
                    {
                        await DisplayImage(imageData);
                        _frameCount++;
                    }

                    await Task.Delay(33, token); // ~30fps 제한
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MJPEG 스트림 처리 오류: {ex.Message}");
                    break;
                }
            }
        }

        private async Task ProcessSingleImage(HttpResponseMessage response, CancellationToken token)
        {
            var imageData = await response.Content.ReadAsByteArrayAsync();
            await DisplayImage(imageData);

            // 단일 이미지는 스냅샷 모드로 전환
            await TrySnapshotMode(_currentIp, token);
        }

        private async Task<byte[]?> ExtractImageFromMjpegStream(Stream stream, byte[] buffer, CancellationToken token)
        {
            try
            {
                // 간단한 JPEG 헤더 찾기 (0xFF, 0xD8)
                var jpegStart = new byte[] { 0xFF, 0xD8 };
                var jpegEnd = new byte[] { 0xFF, 0xD9 };

                using var imageStream = new MemoryStream();
                bool foundStart = false;
                int prevByte = -1;

                while (!token.IsCancellationRequested)
                {
                    var currentByte = stream.ReadByte();
                    if (currentByte == -1) break;

                    if (!foundStart)
                    {
                        if (prevByte == 0xFF && currentByte == 0xD8)
                        {
                            foundStart = true;
                            imageStream.WriteByte(0xFF);
                            imageStream.WriteByte(0xD8);
                        }
                    }
                    else
                    {
                        imageStream.WriteByte((byte)currentByte);

                        if (prevByte == 0xFF && currentByte == 0xD9)
                        {
                            // JPEG 완료
                            return imageStream.ToArray();
                        }
                    }

                    prevByte = currentByte;

                    // 이미지가 너무 크면 중단
                    if (imageStream.Length > 5 * 1024 * 1024) // 5MB 제한
                        break;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task TrySnapshotMode(string ip, CancellationToken token)
        {
            _isStreaming = true;

            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                LoadingText.Text = "스냅샷 모드";
                _fpsTimer.Start();
            });

            var snapshotUrls = new[]
            {
                $"http://{ip}/snapshot.jpg",
                $"http://{ip}/image.jpg",
                $"http://{ip}/cgi-bin/snapshot.cgi",
                $"http://{ip}/jpeg",
            };

            while (_isStreaming && !token.IsCancellationRequested)
            {
                foreach (var url in snapshotUrls)
                {
                    if (!_isStreaming || token.IsCancellationRequested)
                        break;

                    try
                    {
                        using var response = await _httpClient.GetAsync(url, token);
                        if (response.IsSuccessStatusCode)
                        {
                            var imageData = await response.Content.ReadAsByteArrayAsync();
                            await DisplayImage(imageData);
                            _frameCount++;
                            break;
                        }
                    }
                    catch
                    {
                        // 다음 URL 시도
                    }
                }

                await Task.Delay(1000, token); // 1초마다 스냅샷
            }
        }

        private async Task DisplayImage(byte[] imageData)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    using var ms = new MemoryStream(imageData);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    //bitmap.EndInit();
                    bitmap.Freeze();

                    VideoImage.Source = bitmap;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"이미지 표시 오류: {ex.Message}");
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
            _isStreaming = false;
            _fpsTimer.Stop();

            _streamCts?.Cancel();

            if (_streamTask != null)
            {
                try
                {
                    await _streamTask;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Debug.WriteLine($"스트림 정리 오류: {ex.Message}");
                }
            }

            _streamCts?.Dispose();
            _streamCts = null;
            _streamTask = null;

            Dispatcher.Invoke(() =>
            {
                VideoImage.Source = null;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            });
        }
    }
}
