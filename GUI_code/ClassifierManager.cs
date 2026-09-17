using System;

namespace WindowsFormschoRobotcuaG8
{
    public class TrayCoordinate
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public char ShapeCode { get; set; } // ⭐ THÊM: ký tự gửi xuống Arduino
    }

    public class ClassifierManager
    {
        public ProductionReport Report { get; private set; }

        public ClassifierManager()
        {
            Report = new ProductionReport();
        }

        // Chỉ lấy tọa độ + mã hình, KHÔNG tăng đếm ở đây nữa
        // Việc tăng đếm chuyển sang Form1 khi Arduino báo hoàn thành (statusCode = "0")
        public TrayCoordinate GetTrayCoordinate(string shape)
        {
            var tray = new TrayCoordinate { X = 0, Y = 0, Z = 0, ShapeCode = '?' };
            if (string.IsNullOrEmpty(shape)) return tray;

            switch (shape.Trim().ToUpper())
            {
                case "RECTANGLE":
                    tray.X = 40f; tray.Y = 115f; tray.Z = 80f;
                    tray.ShapeCode = 'R';
                    break;
                case "SQUARE":
                    tray.X = 90f; tray.Y = 130f; tray.Z = 60f;
                    tray.ShapeCode = 'S';
                    break;
                case "PYRAMID":
                    tray.X = 150f; tray.Y = 90f; tray.Z = 110f;
                    tray.ShapeCode = 'P';
                    break;
                case "CIRCLE":
                    tray.X = 110f; tray.Y = 110f; tray.Z = 70f;
                    tray.ShapeCode = 'C';
                    break;
            }
            return tray;
        }

        // ⭐ THÊM: gọi hàm này từ Form1 khi Arduino báo "0" (hoàn thành 1 chu trình gắp)
        public void TangSoLuong(string shape)
        {
            switch (shape.Trim().ToUpper())
            {
                case "RECTANGLE": Report.SoLuongRectangle++; break;
                case "SQUARE": Report.SoLuongSquare++; break;
                case "PYRAMID": Report.SoLuongPyramid++; break;
                case "CIRCLE": Report.SoLuongCircle++; break;
            }
        }
    }
}