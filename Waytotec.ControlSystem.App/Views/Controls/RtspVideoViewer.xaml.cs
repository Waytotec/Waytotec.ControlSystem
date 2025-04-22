using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Waytotec.ControlSystem.App.Views.Controls
{
    public partial class RtspVideoViewer : UserControl
    {
        private VideoCapture? _capture;
        private bool _isStreaming = false;
        private int _frameCount = 0;
        private Stopwatch _watch = new();
        private readonly object _lock = new();
        private CancellationTokenSource? _streamCts;
        private Task? _currentStreamTask;
        private string _currentIp = string.Empty;
        private string _currentStream = string.Empty;

        public RtspVideoViewer()
        {
            InitializeComponent();
        }

        public async void Load(string ip, string stream)
        {
            // 이미 같은 카메라에 연결 중이면 무시
            if (_isStreaming && ip == _currentIp && stream == _currentStream)
                return;

            // UI 상태 업데이트 (로딩 표시)
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                OverlayFps.Text = "카메라 연결 중...";
            });

            // 기존 스트림 작업 취소
            await CancelExistingStreamAsync();

            // 새 스트림 작업 시작
            _currentIp = ip;
            _currentStream = stream;
            _frameCount = 0;
            _watch.Restart();

            _streamCts = new CancellationTokenSource();
            var token = _streamCts.Token;

            // 스트림 작업 시작 및 추적
            _currentStreamTask = StartStreamAsync(ip, stream, token);

            // 작업 완료 시 정리 (부모 스레드는 기다리지 않음)
            _ = _currentStreamTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.WriteLine($"스트림 태스크 오류: {t.Exception?.InnerException?.Message}");
                }
            }, TaskScheduler.Default);
        }

        private async Task StartStreamAsync(string ip, string stream, CancellationToken token)
        {
            string rtspUrl = $"rtsp://admin:admin@{ip}:554/{stream}";
            VideoCapture? newCapture = null;
            bool connectSuccess = false;

            try
            {
                // RTSP 연결 시도 - 짧은 타임아웃 (3초)
                newCapture = await TryConnectRtspAsync(rtspUrl, 3000, token);

                if (newCapture == null || token.IsCancellationRequested)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // 현재 IP가 여전히 우리가 시도 중인 IP인 경우만 UI 업데이트
                        if (ip == _currentIp && stream == _currentStream)
                        {
                            OverlayFps.Text = $"카메라 {ip} 연결 실패";
                            LoadingOverlay.Visibility = Visibility.Collapsed;
                        }
                    });
                    return;
                }

                // 연결 성공 시 상태 업데이트
                lock (_lock)
                {
                    if (!token.IsCancellationRequested)
                    {
                        // 이전 캡처 정리
                        ReleaseCapture();

                        // 새 캡처 설정
                        _capture = newCapture;
                        _isStreaming = true;
                        connectSuccess = true;
                    }
                    else
                    {
                        // 취소된 경우 리소스 정리
                        newCapture.Release();
                        newCapture.Dispose();
                        return;
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    if (ip == _currentIp && stream == _currentStream)
                    {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                });

                // 스트리밍 루프
                using var mat = new Mat();
                while (_isStreaming && !token.IsCancellationRequested)
                {
                    bool readSuccess = false;

                    lock (_lock)
                    {
                        if (_capture != null && _capture.IsOpened())
                        {
                            try
                            {
                                readSuccess = _capture.Read(mat);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"프레임 읽기 오류: {ex.Message}");
                                break;
                            }
                        }
                        else break;
                    }

                    if (!readSuccess || mat.Empty())
                    {
                        await Task.Delay(50, token);
                        continue;
                    }

                    _frameCount++;

                    try
                    {
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

                        if (!token.IsCancellationRequested)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                // 현재 IP가 우리가 처리 중인 IP인 경우만 UI 업데이트
                                if (ip == _currentIp && stream == _currentStream)
                                {
                                    VideoImage.Source = bmpImage;
                                    double elapsed = _watch.Elapsed.TotalSeconds;
                                    OverlayFps.Text = $"FPS: {_frameCount / Math.Max(elapsed, 1):F1}";
                                    OverlayResolution.Text = $"해상도: {mat.Width}x{mat.Height}";
                                    OverlayTime.Text = $"경과시간: {elapsed:F1}초";
                                }
                            }, DispatcherPriority.Background);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"비트맵 변환 오류: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상적인 취소 - 아무것도 하지 않음
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"스트림 처리 오류: {ex.Message}");

                // 현재 IP가 우리가 처리하던 IP인 경우만 UI 업데이트
                Dispatcher.Invoke(() =>
                {
                    if (ip == _currentIp && stream == _currentStream)
                    {
                        OverlayFps.Text = $"오류: {ex.Message}";
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                });
            }
            finally
            {
                // 이 IP에 대한 스트림이 아직 현재 IP인 경우만 정리
                if (ip == _currentIp && stream == _currentStream && !connectSuccess)
                {
                    lock (_lock)
                    {
                        _isStreaming = false;
                    }
                }

                // 새 캡처가 현재 캡처가 되지 않은 경우, 정리
                if (newCapture != null && newCapture != _capture)
                {
                    try
                    {
                        newCapture.Release();
                        newCapture.Dispose();
                    }
                    catch { }
                }
            }
        }

        // 기존 스트림 작업 취소
        private async Task CancelExistingStreamAsync()
        {
            try
            {
                // 기존 토큰 소스 취소
                if (_streamCts != null && !_streamCts.IsCancellationRequested)
                {
                    _streamCts.Cancel();
                }

                // 작업이 완료될 때까지 짧은 시간 대기 (최대 500ms)
                if (_currentStreamTask != null)
                {
                    await Task.WhenAny(_currentStreamTask, Task.Delay(500));
                }

                // 토큰 소스 정리
                if (_streamCts != null)
                {
                    _streamCts.Dispose();
                    _streamCts = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"스트림 취소 오류: {ex.Message}");
            }
        }

        // VideoCapture 객체 해제 (락 내부에서 호출해야 함)
        private void ReleaseCapture()
        {
            try
            {
                if (_capture != null)
                {
                    _capture.Release();
                    _capture.Dispose();
                    _capture = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"캡처 해제 오류: {ex.Message}");
            }
        }

        public void Stop()
        {
            StopAsync().ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            try
            {
                await CancelExistingStreamAsync();

                lock (_lock)
                {
                    _isStreaming = false;
                    ReleaseCapture();
                    _currentIp = string.Empty;
                    _currentStream = string.Empty;
                }

                // UI 상태 업데이트
                Dispatcher.Invoke(() =>
                {
                    VideoImage.Source = null;
                    OverlayFps.Text = string.Empty;
                    OverlayResolution.Text = string.Empty;
                    OverlayTime.Text = string.Empty;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                });

                // 메모리 정리
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"정지 처리 오류: {ex.Message}");
            }
        }

        private async Task<VideoCapture?> TryConnectRtspAsync(string url, int timeoutMillis, CancellationToken parentToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            linkedCts.CancelAfter(timeoutMillis);
            var token = linkedCts.Token;

            try
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        // 연결 시도 로그
                        Debug.WriteLine($"RTSP 연결 시도: {url}");
                        var startTime = DateTime.Now;

                        var cap = new VideoCapture(url);
                        if (!cap.IsOpened())
                        {
                            Debug.WriteLine($"연결 실패: IsOpened() 반환 false");
                            cap.Release();
                            cap.Dispose();
                            return null;
                        }

                        // 테스트 프레임 읽기
                        using var testMat = new Mat();
                        bool readSuccess = cap.Read(testMat);

                        var elapsed = DateTime.Now - startTime;
                        Debug.WriteLine($"연결 완료: {readSuccess}, 소요 시간: {elapsed.TotalMilliseconds}ms");

                        if (!readSuccess || testMat.Empty())
                        {
                            cap.Release();
                            cap.Dispose();
                            return null;
                        }

                        return cap;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"연결 예외: {ex.Message}");
                        return null;
                    }
                }, token);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("연결 시도 타임아웃");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"연결 태스크 예외: {ex.Message}");
                return null;
            }
        }
    }
}
