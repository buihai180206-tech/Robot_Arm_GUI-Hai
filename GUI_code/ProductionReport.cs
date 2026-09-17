using System;

namespace WindowsFormschoRobotcuaG8 { 

    public class ProductionReport
    {
        // Bộ đếm sản lượng độc lập cho 4 hình dáng mới theo yêu cầu
        public int SoLuongRectangle { get; set; } = 0;
        public int SoLuongSquare { get; set; } = 0;
        public int SoLuongPyramid { get; set; } = 0;
        public int SoLuongCircle { get; set; } = 0;

        // Tự động tính tổng sản lượng dựa trên 4 hình khối
        public int TongSoSanPham => SoLuongRectangle + SoLuongSquare + SoLuongPyramid + SoLuongCircle;

        /// <summary>
        /// Hàm xóa sạch bộ đếm sản lượng khi nhấn nút Reset hệ thống
        /// </summary>
        public void ResetReport()
        {
            SoLuongRectangle = 0;
            SoLuongSquare = 0;
            SoLuongPyramid = 0;
            SoLuongCircle = 0;
        }
    }
}