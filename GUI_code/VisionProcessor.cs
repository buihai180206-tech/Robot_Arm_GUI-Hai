using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFormschoRobotcuaG8
{
    /// <summary>
    /// Poll endpoint /detect từ Python server (http://127.0.0.1:5001/detect) mỗi 500ms.
    /// Khi vật xuất hiện ổn định tại cùng vị trí trong STABLE_FRAMES frame liên tiếp
    /// thì mới bắn OnProductDetected — tránh gắp vật đang di chuyển hoặc detect nhấp nháy.
    /// </summary>
    public class VisionResult
    {
        public string Shape { get; set; }   // Tên hình: SQUARE / RECTANGLE / CIRCLE / PYRAMID
        public float X { get; set; }   // Tọa độ X thực tế (cm) của vật
        public float Y { get; set; }   // Tọa độ Y thực tế (cm) của vật
    }
    public class VisionProcessor
    {
        // ── Cấu hình ────────────────────────────────────────────────────────────────
        private const string DETECT_URL = "http://127.0.0.1:5000/detect";
        private const int POLL_MS = 2000;   // Chu kỳ poll (ms)
        private const int STABLE_FRAMES = 4;     // Số frame liên tiếp cùng vị trí → coi là ổn định
        private const float STABLE_RADIUS = 2.5f;  // Ngưỡng sai lệch tọa độ (cm) vẫn coi là cùng vị trí
        private const string NO_OBJECT_SHAPE = "---"; // Python trả về khi không thấy vật

        // ── Internal state ───────────────────────────────────────────────────────────
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        private CancellationTokenSource _cts;
        private bool _isStreaming = false;

        // Debounce: theo dõi vật đang được quan sát
        private string _pendingShape = "";
        private float _pendingX = 0f;
        private float _pendingY = 0f;
        private int _stableCount = 0;

        // ── Events ───────────────────────────────────────────────────────────────────
        public event Action<VisionResult> OnProductDetected;
        public event Action<string> OnLogMessage;

        // ── Public API ───────────────────────────────────────────────────────────────
        public void StartVisionStream()
        {
            if (_isStreaming) return;
            _isStreaming = true;
            _cts = new CancellationTokenSource();
            ResetDebounce();

            OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Vision] Bắt đầu poll Python /detect mỗi {POLL_MS}ms.");

            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await PollOnceAsync();
                        await Task.Delay(POLL_MS, _cts.Token);
                    }
                    catch (TaskCanceledException) { break; }
                    catch (Exception ex)
                    {
                        OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Vision - Lỗi]: {ex.Message}");
                        await Task.Delay(1000, _cts.Token); // Chờ thêm khi lỗi mạng
                    }
                }
            }, _cts.Token);
        }

        public void StopVisionStream()
        {
            if (!_isStreaming) return;
            _cts?.Cancel();
            _isStreaming = false;
            ResetDebounce();
            OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Vision] Đã dừng poll /detect.");
        }

        public bool IsStreaming => _isStreaming;

        // ── Core: poll một lần ──────────────────────────────────────────────────────
        private async Task PollOnceAsync()
        {
            string json;
            try
            {
                json = await _http.GetStringAsync(DETECT_URL);
            }
            catch
            {
                // Python server chưa sẵn sàng → reset đếm, thử lại sau
                ResetDebounce();
                return;
            }

            // Parse JSON: {"shape":"SQUARE","x":40.0,"y":115.0}
            JObject obj = JObject.Parse(json);
            string shape = (obj["shape"]?.ToString() ?? "").Trim().ToUpper();
            float x = obj["x"] != null ? (float)obj["x"] : 0f;
            float y = obj["y"] != null ? (float)obj["y"] : 0f;

            // Không có vật → reset
            if (string.IsNullOrEmpty(shape) || shape == NO_OBJECT_SHAPE)
            {
                ResetDebounce();
                return;
            }

            // Kiểm tra có cùng vật cùng vị trí không
            bool sameObject = shape == _pendingShape
                           && Distance(x, y, _pendingX, _pendingY) <= STABLE_RADIUS;

            if (sameObject)
            {
                _stableCount++;
            }
            else
            {
                // Vật mới hoặc đã dịch chuyển → bắt đầu đếm lại
                _pendingShape = shape;
                _pendingX = x;
                _pendingY = y;
                _stableCount = 1;
            }

            OnLogMessage?.Invoke(
                $"[{DateTime.Now:HH:mm:ss}] [Vision] Detect: {shape} " +
                $"X={x} Y={y} | Ổn định: {_stableCount}/{STABLE_FRAMES}");

            // Đủ frame ổn định → bắn sự kiện lên Form1
            if (_stableCount >= STABLE_FRAMES)
            {
                ResetDebounce(); // Reset ngay để không bắn lặp cùng vật

                OnProductDetected?.Invoke(new VisionResult
                {
                    Shape = shape,
                    X = x,
                    Y = y
                });

                OnLogMessage?.Invoke(
                    $"[{DateTime.Now:HH:mm:ss}] [Vision] ✓ VẬT ỔN ĐỊNH → Gửi lệnh gắp: " +
                    $"{shape} tại X={x} Y={y}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────
        private void ResetDebounce()
        {
            _pendingShape = "";
            _pendingX = 0f;
            _pendingY = 0f;
            _stableCount = 0;
        }

        private static float Distance(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}