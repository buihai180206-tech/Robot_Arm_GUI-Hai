using System;
using System.IO;
using System.Net;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormschoRobotcuaG8
{
    public class ESP32Streamer
    {
        private PictureBox _pictureBox;
        private CancellationTokenSource _cts;
        private bool _isStreaming = false;

        public event Action<string> OnLogMessage;

        public ESP32Streamer(PictureBox pictureBox)
        {
            _pictureBox = pictureBox;
        }

        public void StartStream(string streamUrl)
        {
            if (_isStreaming) return;
            _isStreaming = true;
            _cts = new CancellationTokenSource();
            OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Stream]: Kết nối mạng luồng ảnh AI ({streamUrl})...");
            
            Task.Run(async () => await StreamLogicAsync(streamUrl, _cts.Token));
        }

        public void StopStream()
        {
            if (!_isStreaming) return;
            _cts?.Cancel();
            _isStreaming = false;
            OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Stream]: Đã đóng kết nối nhận luồng ảnh.");
        }

        private async Task StreamLogicAsync(string url, CancellationToken token)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 5000;
                
                using (WebResponse response = await request.GetResponseAsync())
                using (Stream stream = response.GetResponseStream())
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Stream]: Nhận luồng ảnh AI thành công!");
                    
                    while (!token.IsCancellationRequested)
                    {
                        byte[] jpegBuffer = FindJpegFrame(reader, token);
                        if (jpegBuffer == null) continue;

                        using (MemoryStream ms = new MemoryStream(jpegBuffer))
                        {
                            Image img = Image.FromStream(ms);
                            
                            if (_pictureBox != null && !_pictureBox.IsDisposed)
                            {
                                _pictureBox.BeginInvoke((MethodInvoker)delegate
                                {
                                    // Hủy ảnh cũ ngay lập tức nhằm giải phóng bộ nhớ RAM để chống lag hình
                                    _pictureBox.Image?.Dispose(); 
                                    _pictureBox.Image = (Image)img.Clone();
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] [Stream - ERROR]: {ex.Message}");
                _isStreaming = false;
            }
        }

        private byte[] FindJpegFrame(BinaryReader reader, CancellationToken token)
        {
            MemoryStream ms = new MemoryStream();
            bool startFound = false;
            byte prevByte = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    byte currentByte = reader.ReadByte();

                    if (!startFound)
                    {
                        if (prevByte == 0xFF && currentByte == 0xD8) // Ký tự bắt đầu chuỗi JPEG
                        {
                            startFound = true;
                            ms.WriteByte(0xFF);
                            ms.WriteByte(0xD8);
                        }
                        prevByte = currentByte;
                    }
                    else
                    {
                        ms.WriteByte(currentByte);
                        if (prevByte == 0xFF && currentByte == 0xD9) // Ký tự kết thúc chuỗi JPEG
                        {
                            return ms.ToArray();
                        }
                        prevByte = currentByte;
                    }
                }
            }
            catch
            {
                return null;
            }
            return null;
        }
    }
}