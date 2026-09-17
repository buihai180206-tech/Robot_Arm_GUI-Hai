using System;
using System.IO.Ports;

namespace WindowsFormschoRobotcuaG8
{
    public class HardwareManager
    {
        private SerialPort _serialPort;
        private string _rxBuffer = ""; // Bộ đệm dòng chống đứt gãy gói tin truyền thông
        // ⭐ ĐẤU NỐI TRUYỀN THÔNG THỜI GIAN THỰC: Sự kiện bắn dữ liệu nhận được lên lớp điều khiển
        public event Action<string> OnDataReceived;

        public HardwareManager()
        {
            _serialPort = new SerialPort();
            // Đăng ký ngắt nhận dữ liệu từ phần cứng (Chạy trên Thread ngầm của hệ điều hành)
            _serialPort.DataReceived += SerialPort_DataReceived;
        }

        public bool Connect(string portName = "COM3", int baudRate = 115200)
        {
            try
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();

                _serialPort.PortName = portName;
                _serialPort.BaudRate = baudRate;

                // Cấu hình chuẩn truyền thông công nghiệp chống treo luồng
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 500;

                _serialPort.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch { }
        }

        public bool IsOpen()
        {
    
            return _serialPort != null && _serialPort.IsOpen;
        }

        public bool SendData(string dataPacket)
        {

            if (_serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    _serialPort.Write(dataPacket);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                // Đọc hết tất cả ký tự đang nằm trong hàng đợi cổng COM
                string incomingData = _serialPort.ReadExisting();
                _rxBuffer += incomingData;

                // Xử lý bộ đệm dòng: Kiểm tra xem gói tin đã kết thúc bằng ký tự '\n' chưa
                while (_rxBuffer.Contains("\n"))
                {
                    int lineEndIndex = _rxBuffer.IndexOf('\n');
                    string completePacket = _rxBuffer.Substring(0, lineEndIndex).Trim();

                    // Xóa gói tin đã xử lý ra khỏi bộ đệm
                    _rxBuffer = _rxBuffer.Substring(lineEndIndex + 1);

                    // Bắn dữ liệu sạch lên cho các lớp tầng trên thông qua Event công nghệ
                    OnDataReceived?.Invoke(completePacket);
                }
            }
            catch { }
        }
    }
}