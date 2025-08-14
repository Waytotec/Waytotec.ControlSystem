using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.Text;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Waytotec.ControlSystem.App.Views.Controls
{
    /// <summary>
    /// WebView2RtspViewer.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WebView2RtspViewer : UserControl
    {
        private WebView2? _webView;
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();
        private readonly DispatcherTimer _fpsTimer;
        private string _currentUrl = string.Empty;

        public WebView2RtspViewer()
        {
            InitializeComponent();

            _fpsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _fpsTimer.Tick += UpdateFpsDisplay;

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebView();
        }

        private async void OnUnloaded(object sender, RoutedEventArgs e)
        {
            await StopAsync();
        }

        private async Task InitializeWebView()
        {
            try
            {
                _webView = new WebView2();

                // WebView2를 그리드에 추가 (VideoImage 대신)
                MainGrid.Children.Remove(VideoImage);
                MainGrid.Children.Insert(0, _webView);

                await _webView.EnsureCoreWebView2Async();

                // 네비게이션 완료 이벤트로 프레임 카운팅
                _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                _webView.CoreWebView2.DOMContentLoaded += OnDOMContentLoaded;

                Debug.WriteLine("WebView2 초기화 완료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WebView2 초기화 실패: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    LoadingText.Text = "WebView2 초기화 실패";
                });
            }
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Debug.WriteLine("WebView2 네비게이션 완료");
                // 주기적으로 JavaScript 실행하여 비디오 상태 확인
                _ = MonitorVideoStatus();
            }
        }

        private void OnDOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            Debug.WriteLine("DOM 로드 완료");
        }

        private async Task MonitorVideoStatus()
        {
            try
            {
                while (_isStreaming && _webView?.CoreWebView2 != null)
                {
                    // JavaScript로 비디오 상태 확인
                    var result = await _webView.CoreWebView2.ExecuteScriptAsync(@"
                        (function() {
                            const video = document.getElementById('videoPlayer');
                            if (video) {
                                return {
                                    readyState: video.readyState,
                                    currentTime: video.currentTime,
                                    duration: video.duration,
                                    videoWidth: video.videoWidth,
                                    videoHeight: video.videoHeight
                                };
                            }
                            return null;
                        })();
                    ");

                    if (!string.IsNullOrEmpty(result) && result != "null")
                    {
                        _frameCount++;
                    }

                    await Task.Delay(1000); // 1초마다 체크
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"비디오 상태 모니터링 오류: {ex.Message}");
            }
        }

        public async void Load(string ip, string stream)
        {
            // string rtspUrl = $"rtsp://admin:admin@{ip}:554/{stream}";
            string rtspUrl = $"rtsp://210.99.70.120:1935/live/cctv001.stream";

            if (_isStreaming && rtspUrl == _currentUrl)
                return;

            await StopAsync();
            await StartStreamAsync(rtspUrl);
        }

        private async Task StartStreamAsync(string rtspUrl)
        {
            if (_webView?.CoreWebView2 == null)
            {
                await InitializeWebView();
                if (_webView?.CoreWebView2 == null)
                    return;
            }

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
                var htmlContent = GenerateHtmlPlayer(rtspUrl);
                _webView.CoreWebView2.NavigateToString(htmlContent);

                _isStreaming = true;

                Dispatcher.Invoke(() =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    _fpsTimer.Start();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"스트림 시작 실패: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    LoadingText.Text = $"연결 실패";
                });
            }
        }

        private string GenerateHtmlPlayer(string rtspUrl)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset='UTF-8'>");
            html.AppendLine("    <title>RTSP Stream</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body {");
            html.AppendLine("            margin: 0;");
            html.AppendLine("            padding: 0;");
            html.AppendLine("            background: #000;");
            html.AppendLine("            overflow: hidden;");
            html.AppendLine("        }");
            html.AppendLine("        #videoContainer {");
            html.AppendLine("            width: 100vw;");
            html.AppendLine("            height: 100vh;");
            html.AppendLine("            display: flex;");
            html.AppendLine("            justify-content: center;");
            html.AppendLine("            align-items: center;");
            html.AppendLine("        }");
            html.AppendLine("        video {");
            html.AppendLine("            width: 100%;");
            html.AppendLine("            height: 100%;");
            html.AppendLine("            object-fit: contain;");
            html.AppendLine("        }");
            html.AppendLine("        #errorMsg {");
            html.AppendLine("            color: white;");
            html.AppendLine("            text-align: center;");
            html.AppendLine("            font-family: Arial, sans-serif;");
            html.AppendLine("            display: none;");
            html.AppendLine("        }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("    <div id='videoContainer'>");
            html.AppendLine("        <video id='videoPlayer' autoplay muted>");
            html.AppendLine("            <source src='" + rtspUrl + "' type='application/x-rtsp'>");
            html.AppendLine("            Your browser does not support RTSP streaming.");
            html.AppendLine("        </video>");
            html.AppendLine("        <div id='errorMsg'>");
            html.AppendLine("            <h3>RTSP 스트림 연결 실패</h3>");
            html.AppendLine("            <p>브라우저에서 RTSP를 직접 지원하지 않습니다.</p>");
            html.AppendLine("            <p>WebRTC 또는 HLS 변환이 필요합니다.</p>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            html.AppendLine("    <script>");
            html.AppendLine("        const video = document.getElementById('videoPlayer');");
            html.AppendLine("        const errorMsg = document.getElementById('errorMsg');");
            html.AppendLine("        const videoContainer = document.getElementById('videoContainer');");
            html.AppendLine();
            html.AppendLine("        // 프레임 업데이트 감지 (콘솔 로그 제거)");
            html.AppendLine("        video.addEventListener('timeupdate', function() {");
            html.AppendLine("            // 프레임 업데이트 감지 (C#에서 주기적으로 체크함)");
            html.AppendLine("        });");
            html.AppendLine();
            html.AppendLine("        // 에러 처리");
            html.AppendLine("        video.addEventListener('error', function(e) {");
            html.AppendLine("            console.log('Video error:', e);");
            html.AppendLine("            video.style.display = 'none';");
            html.AppendLine("            errorMsg.style.display = 'block';");
            html.AppendLine("        });");
            html.AppendLine();
            html.AppendLine("        // 로드 완료");
            html.AppendLine("        video.addEventListener('loadeddata', function() {");
            html.AppendLine("            console.log('Video loaded successfully');");
            html.AppendLine("        });");
            html.AppendLine();
            html.AppendLine("        // 대체 방법: HTTP로 MJPEG 스트림 시도");
            html.AppendLine("        setTimeout(function() {");
            html.AppendLine("            if (video.readyState === 0) {");
            html.AppendLine("                console.log('RTSP 실패, HTTP MJPEG 시도');");
            html.AppendLine("                const img = document.createElement('img');");
            html.AppendLine("                img.style.width = '100%';");
            html.AppendLine("                img.style.height = '100%';");
            html.AppendLine("                img.style.objectFit = 'contain';");

            // IP 추출하여 HTTP MJPEG URL 생성
            var ip = ExtractIpFromRtspUrl(rtspUrl);
            html.AppendLine($"                img.src = 'http://{ip}/mjpeg';");

            html.AppendLine("                img.onerror = function() {");
            html.AppendLine($"                    img.src = 'http://{ip}:8080/video.mjpg';");
            html.AppendLine("                };");
            html.AppendLine("                video.style.display = 'none';");
            html.AppendLine("                videoContainer.appendChild(img);");
            html.AppendLine("            }");
            html.AppendLine("        }, 3000);");
            html.AppendLine("    </script>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string ExtractIpFromRtspUrl(string rtspUrl)
        {
            try
            {
                var uri = new Uri(rtspUrl);
                return uri.Host;
            }
            catch
            {
                return "192.168.1.100"; // 기본값
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

            if (_webView?.CoreWebView2 != null)
            {
                try
                {
                    _webView.CoreWebView2.NavigateToString("<html><body style='background:#000;'></body></html>");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WebView2 정리 오류: {ex.Message}");
                }
            }

            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            });
        }
    }
}
