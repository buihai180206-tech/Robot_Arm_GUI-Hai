using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;

namespace WindowsFormschoRobotcuaG8
{
    public partial class Form1 : Form
    {
        private class RobotPosition
        {
            public int J1; public int J2; public int J3; public int J4; public int J5; public int J6;
        }

        // --- 1. KHAI BÁO ĐẦY ĐỦ LINH KIỆN GIAO DIỆN THEO FILE GỐC ---
        private Panel pnlLeftControl;
        private TrackBar tkbJoint1, tkbJoint2, tkbJoint3, tkbJoint4, tkbJoint5, tkbJoint6;
        private Label lblJoint1, lblJoint2, lblJoint3, lblJoint4, lblJoint5, lblJoint6;
        private NumericUpDown numSpd1, numSpd2, numSpd3, numSpd4, numSpd5, numSpd6;
        private Label lblSpeedTitle;
        private Panel pnlVisionArea;
        private PictureBox picCamera;
        private TextBox txtDetectShape;
        private Label lblDetectShape;
        private Panel pnlSystemArea, pnlStatusLight;
        private TextBox txtTargetX, txtTargetY, txtTargetZ, txtStatus;
        private Button btnConnect, btnStart, btnStop, btnController, btnResetSystem, btnGrip;
        private ListBox lstSavedPositions;
        private Button btnSavePosition, btnClearPositions, btnRunPositions;
        private Label lblClock;

        // --- 2. CÁC ĐỐI TƯỢNG QUẢN LÝ LOGIC ---
        private HardwareManager _hardwareManager;
        private ESP32Streamer _esp32Streamer;
        private VisionProcessor _visionProcessor;
        private ClassifierManager _classifierManager;
        private PythonBridge _pythonBridge;

        // --- 3. BIẾN TRẠNG THÁI HỆ THỐNG ---
        private List<RobotPosition> savedPositions = new List<RobotPosition>();
        private bool isAutoRunning = false;
        private int currentAutoIndex = 0;
        private Timer tmtClock;
        private Timer tmtAutoStep;
        private const int AUTO_STEP_DELAY_MS = 1500;

        public Form1()
        {
            // Gọi hàm dựng giao diện chi tiết từng dòng đơn lẻ của bạn
            DungGiaoDienTuDong();

            _hardwareManager = new HardwareManager();
            _classifierManager = new ClassifierManager();

            // Khởi tạo 1 lần duy nhất chống lỗi trùng lặp định nghĩa
            _pythonBridge = new PythonBridge();
            _pythonBridge.OnPythonLog += UpdateLogSafe;

            _esp32Streamer = new ESP32Streamer(picCamera);
            _esp32Streamer.OnLogMessage += UpdateLogSafe;

            _visionProcessor = new VisionProcessor();
            _visionProcessor.OnLogMessage += UpdateLogSafe;
            _visionProcessor.OnProductDetected += VisionProcessor_OnProductDetected;

            _hardwareManager.OnDataReceived += HardwareManager_OnDataReceived;

            tmtClock = new Timer { Interval = 1000 };
            tmtClock.Tick += TmtClock_Tick;
            tmtClock.Start();

            tmtAutoStep = new Timer { Interval = AUTO_STEP_DELAY_MS };
            tmtAutoStep.Tick += TmtAutoStep_Tick;
        }

        private void DungGiaoDienTuDong()
        {
            this.Text = "HỆ THỐNG PHÂN LOẠI SẢN PHẨM SCADA - ROBOT G8";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- PANEL ĐIỀU KHIỂN CÁC TRỤC KHỚP CƠ KHÍ ---
            pnlLeftControl = new Panel { Location = new Point(10, 10), Size = new Size(320, 640), BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(pnlLeftControl);

            lblSpeedTitle = new Label { Text = "Tốc độ (%)", Location = new Point(220, 5), Size = new Size(70, 20), Font = new Font("Arial", 8, FontStyle.Bold) };
            pnlLeftControl.Controls.Add(lblSpeedTitle);

            // Khởi tạo chi tiết: TRỤC 1
            lblJoint1 = new Label { Text = "Trục 1 (Xoay Đế):", Location = new Point(10, 20), Size = new Size(120, 20) };
            tkbJoint1 = new TrackBar { Location = new Point(10, 40), Size = new Size(200, 45), Minimum = 0, Maximum = 180, Value = 180 };
            numSpd1 = new NumericUpDown { Location = new Point(220, 40), Size = new Size(60, 20), Minimum = 1, Maximum = 100, Value = 50 };
            pnlLeftControl.Controls.AddRange(new Control[] { lblJoint1, tkbJoint1, numSpd1 });

            // Khởi tạo chi tiết: TRỤC 2
            lblJoint2 = new Label { Text = "Trục 2 (Cánh Chính):", Location = new Point(10, 90), Size = new Size(120, 20) };
            tkbJoint2 = new TrackBar { Location = new Point(10, 110), Size = new Size(200, 45), Minimum = 0, Maximum = 180, Value = 120 };
            numSpd2 = new NumericUpDown { Location = new Point(220, 110), Size = new Size(60, 20), Minimum = 1, Maximum = 100, Value = 50 };
            pnlLeftControl.Controls.AddRange(new Control[] { lblJoint2, tkbJoint2, numSpd2 });

            // Khởi tạo chi tiết: TRỤC 3
            lblJoint3 = new Label { Text = "Trục 3 (Cánh Phụ):", Location = new Point(10, 160), Size = new Size(120, 20) };
            tkbJoint3 = new TrackBar { Location = new Point(10, 180), Size = new Size(200, 45), Minimum = 0, Maximum = 180, Value = 140 };
            numSpd3 = new NumericUpDown { Location = new Point(220, 180), Size = new Size(60, 20), Minimum = 1, Maximum = 100, Value = 50 };
            pnlLeftControl.Controls.AddRange(new Control[] { lblJoint3, tkbJoint3, numSpd3 });

            // Khởi tạo chi tiết: TRỤC 4
            lblJoint4 = new Label { Text = "Trục 4 (Xoay Cổ):", Location = new Point(10, 230), Size = new Size(120, 20) };
            tkbJoint4 = new TrackBar { Location = new Point(10, 250), Size = new Size(200, 45), Minimum = 0, Maximum = 180, Value = 90 };
            numSpd4 = new NumericUpDown { Location = new Point(220, 250), Size = new Size(60, 20), Minimum = 1, Maximum = 100, Value = 50 };
            pnlLeftControl.Controls.AddRange(new Control[] { lblJoint4, tkbJoint4, numSpd4 });

            // Khởi tạo chi tiết: TRỤC 5
            lblJoint5 = new Label { Text = "Trục 5 (Gập Cổ):", Location = new Point(10, 300), Size = new Size(120, 20) };
            tkbJoint5 = new TrackBar { Location = new Point(10, 320), Size = new Size(200, 45), Minimum = 0, Maximum = 180, Value = 75 };
            numSpd5 = new NumericUpDown { Location = new Point(220, 320), Size = new Size(60, 20), Minimum = 1, Maximum = 100, Value = 50 };
            pnlLeftControl.Controls.AddRange(new Control[] { lblJoint5, tkbJoint5, numSpd5 });

            // Khởi tạo chi tiết: TRỤC 6
            lblJoint6 = new Label { Text = "Trục 6 (Kẹp/Nhả):", Location = new Point(10, 370), Size = new Size(120, 20) };
            tkbJoint6 = new TrackBar { Location = new Point(10, 390), Size = new Size(200, 45), Minimum = 0, Maximum = 180, Value = 150 };
            numSpd6 = new NumericUpDown { Location = new Point(220, 390), Size = new Size(60, 20), Minimum = 1, Maximum = 100, Value = 100 };
            pnlLeftControl.Controls.AddRange(new Control[] { lblJoint6, tkbJoint6, numSpd6 });

            // Đăng ký sự kiện riêng lẻ tương ứng từng thanh cuộn
            tkbJoint1.Scroll += TkbJoint_Scroll;
            tkbJoint2.Scroll += TkbJoint_Scroll;
            tkbJoint3.Scroll += TkbJoint_Scroll;
            tkbJoint4.Scroll += TkbJoint_Scroll;
            tkbJoint5.Scroll += TkbJoint_Scroll;
            tkbJoint6.Scroll += TkbJoint_Scroll;

            // --- KHỐI LƯU ĐIỂM DẠY HỌC LỆNH ---
            btnSavePosition = new Button { Text = "Lưu Vị Trí", Location = new Point(10, 450), Size = new Size(95, 30), BackColor = Color.LightBlue };
            btnClearPositions = new Button { Text = "Xóa Hết Điểm", Location = new Point(110, 450), Size = new Size(95, 30) };
            btnRunPositions = new Button { Text = "Chạy Tự Động", Location = new Point(210, 450), Size = new Size(100, 30), BackColor = Color.LightGreen };
            //chú ý 1 dòng này
            lstSavedPositions = new ListBox { Location = new Point(10, 490), Size = new Size(300, 135) };

            btnSavePosition.Click += BtnSavePosition_Click;
            btnClearPositions.Click += BtnClearPositions_Click;
            btnRunPositions.Click += BtnRunPositions_Click;
           //chú ý dòng này
            pnlLeftControl.Controls.AddRange(new Control[] { btnSavePosition, btnClearPositions, btnRunPositions, lstSavedPositions });

            // --- PANEL PHẢI HIỂN THỊ CAMERA XỬ LÝ ẢNH AI ---
            pnlVisionArea = new Panel { Location = new Point(340, 10), Size = new Size(400, 420), BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(pnlVisionArea);

            picCamera = new PictureBox { Location = new Point(10, 10), Size = new Size(380, 320), BorderStyle = BorderStyle.Fixed3D, SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.Black };
            lblDetectShape = new Label { Text = "Kết Quả AI Vật Thể:", Location = new Point(10, 350), Size = new Size(120, 20) };
            txtDetectShape = new TextBox { Location = new Point(140, 347), Size = new Size(250, 25), ReadOnly = true, Font = new Font("Arial", 11, FontStyle.Bold), ForeColor = Color.Red };
            pnlVisionArea.Controls.AddRange(new Control[] { picCamera, lblDetectShape, txtDetectShape });

            // --- PANEL HỆ THỐNG VÀ ĐIỀU KHIỂN ---
            pnlSystemArea = new Panel { Location = new Point(750, 10), Size = new Size(320, 420), BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(pnlSystemArea);

            btnConnect = new Button { Text = "KẾT NỐI CỔNG COM", Location = new Point(10, 10), Size = new Size(180, 35), BackColor = Color.Orange, Font = new Font("Arial", 9, FontStyle.Bold) };
            pnlStatusLight = new Panel { Location = new Point(200, 17), Size = new Size(20, 20), BackColor = Color.Gray };
            btnConnect.Click += BtnConnect_Click;
            pnlSystemArea.Controls.AddRange(new Control[] { btnConnect, pnlStatusLight });

            btnStart = new Button { Text = "START (Chạy AI Thật)", Location = new Point(10, 60), Size = new Size(140, 35), BackColor = Color.LightGreen, Font = new Font("Arial", 8, FontStyle.Bold) };
            btnStop = new Button { Text = "STOP HỆ THỐNG", Location = new Point(160, 60), Size = new Size(140, 35), BackColor = Color.LightCoral, Font = new Font("Arial", 8, FontStyle.Bold) };
            btnController = new Button { Text = "CHẠY GIẢ LẬP TEST", Location = new Point(10, 105), Size = new Size(140, 35), BackColor = Color.Khaki };
            btnResetSystem = new Button { Text = "RESET SẢN LƯỢNG", Location = new Point(160, 105), Size = new Size(140, 35) };

            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
            btnController.Click += BtnController_Click;
            btnResetSystem.Click += BtnResetSystem_Click;
            pnlSystemArea.Controls.AddRange(new Control[] { btnStart, btnStop, btnController, btnResetSystem });

            // Nhập tọa độ thủ công X, Y, Z
            Label lblX = new Label { Text = "X (mm):", Location = new Point(10, 160), Size = new Size(50, 20) };
            txtTargetX = new TextBox { Location = new Point(60, 157), Size = new Size(60, 20), Text = "0" };
            Label lblY = new Label { Text = "Y (mm):", Location = new Point(130, 160), Size = new Size(50, 20) };
            txtTargetY = new TextBox { Location = new Point(180, 157), Size = new Size(60, 20), Text = "0" };
            Label lblZ = new Label { Text = "Z (mm):", Location = new Point(10, 195), Size = new Size(50, 20) };
            txtTargetZ = new TextBox { Location = new Point(60, 192), Size = new Size(60, 20), Text = "0" };
            btnGrip = new Button { Text = "GỬI TỌA ĐỘ", Location = new Point(130, 190), Size = new Size(110, 25), BackColor = Color.LightGray };

            btnGrip.Click += BtnGrip_Click;
            pnlSystemArea.Controls.AddRange(new Control[] { lblX, txtTargetX, lblY, txtTargetY, lblZ, txtTargetZ, btnGrip });

            // Đồng hồ LED số hệ thống
            lblClock = new Label { Location = new Point(10, 240), Size = new Size(300, 50), Font = new Font("Courier New", 12, FontStyle.Bold), ForeColor = Color.DarkBlue, TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.LightYellow };

            // ⭐ ĐÃ SỬA LỖI BIÊN DỊCH: Thêm .Controls để nạp nhãn thời gian vào Panel đúng chuẩn WinForms
            pnlSystemArea.Controls.Add(lblClock);

            // Khung hiển thị Log tiến trình giám sát toàn hệ thống
            txtStatus = new TextBox { Location = new Point(340, 440), Size = new Size(730, 210), Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9), BackColor = Color.Black, ForeColor = Color.Lime, ReadOnly = true };
            this.Controls.Add(txtStatus);
        }

        // ⭐ ĐÃ SỬA ĐỒNG BỘ: Tạo thêm hàm nạp chồng không tham số để khớp tương thích với các file Event Action cũ của bạn
        private void UpdateLogSafe()
        {
            if (txtStatus.InvokeRequired)
            {
                txtStatus.BeginInvoke((MethodInvoker)delegate { UpdateLogSafe(); });
            }
            else
            {
                txtStatus.AppendText($"[{DateTime.Now:HH:mm:ss}] Kích hoạt tiến trình..." + Environment.NewLine);
                txtStatus.SelectionStart = txtStatus.TextLength;
                txtStatus.ScrollToCaret();
            }
        }

        private void UpdateLogSafe(string message)
        {
            if (txtStatus.InvokeRequired)
            {
                txtStatus.BeginInvoke((MethodInvoker)delegate { UpdateLogSafe(message); });
            }
            else
            {
                txtStatus.AppendText(message + Environment.NewLine);
                txtStatus.SelectionStart = txtStatus.TextLength;
                txtStatus.ScrollToCaret();
            }
        }

        private void TkbJoint_Scroll(object sender, EventArgs e)
        {
            if (isAutoRunning) return;
            string packet = $"GOC:{tkbJoint1.Value},{tkbJoint2.Value},{tkbJoint3.Value},{tkbJoint4.Value},{tkbJoint5.Value},{tkbJoint6.Value}\n";
            _hardwareManager.SendData(packet);
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            _hardwareManager.IsSimulationMode = false;
            bool connected = _hardwareManager.Connect("COM3", 115200);
            if (connected)
            {
                pnlStatusLight.BackColor = Color.Lime;
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG]: Kết nối thành công phần cứng COM3.");
            
            }
            else
            {
                pnlStatusLight.BackColor = Color.Red;
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [LỖI]: Không mở được COM3. Chuyển sang mô phỏng.");
                _hardwareManager.IsSimulationMode = true;
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG]: Kích hoạt camera thực tế và xử lý AI.");

            // ⭐ ĐÃ ĐỒNG BỘ LUỒNG MỚI: Trỏ cổng kết nối lấy hình ảnh từ Server nội bộ phát ra từ Python
            _esp32Streamer.StartStream("http://127.0.0.1:5000/video_feed");
            _visionProcessor.StopVisionStream();

            // ⭐ ĐÃ SỬA LỖI KHÔNG KHỚP KIỂU: Gọi chạy ngầm file python đúng định dạng Task không lấy chuỗi trả về
            Task.Run(async () =>
            {
                await _pythonBridge.RunPythonScriptAsync("detect_shapes.py", "");
            });
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            isAutoRunning = false;
            tmtAutoStep.Stop();
            _esp32Streamer.StopStream();
            _visionProcessor.StopVisionStream();
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG]: YÊU CẦU DỪNG TOÀN BỘ HOẠT ĐỘNG KHẨN CẤP.");
        }

        private void BtnController_Click(object sender, EventArgs e)
        {
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG]: Chuyển sang chế độ giả lập test ngẫu nhiên không cam.");
            _esp32Streamer.StopStream();
            _visionProcessor.StartVisionStream();
        }

        private void BtnResetSystem_Click(object sender, EventArgs e)
        {
            _classifierManager.Report.ResetReport();
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG]: Đã reset báo cáo thống kê sản phẩm.");
        }

        private void BtnGrip_Click(object sender, EventArgs e)
        {
            string xyzPacket = $"XYZ:{txtTargetX.Text},{txtTargetY.Text},{txtTargetZ.Text},1\n";
            _hardwareManager.SendData(xyzPacket);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG]: Đã phát lệnh tọa độ thủ công: {xyzPacket.Trim()}");
        }

        private void VisionProcessor_OnProductDetected(VisionResult result)
        {
            if (txtDetectShape.InvokeRequired)
            {
                txtDetectShape.BeginInvoke((MethodInvoker)delegate { VisionProcessor_OnProductDetected(result); });
                return;
            }

            txtDetectShape.Text = result.Shape;
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [AI-CORE]: Nhận diện thành công -> KHỐI {result.Shape}");

            TrayCoordinate tray = _classifierManager.GetTrayCoordinate(result.Shape);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [BỘ NÃO]: Tọa độ khay đích: X={tray.X}, Y={tray.Y}, Z={tray.Z}");

            string targetPacket = $"XYZ:{tray.X},{tray.Y},{tray.Z},1\n";
            _hardwareManager.SendData(targetPacket);
        }

        private void HardwareManager_OnDataReceived(string packet)
        {
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [ARDUINO]: {packet}");
            if (packet.Contains("DONE"))
            {
                if (isAutoRunning)
                {
                    tmtAutoStep.Start();
                }
            }
        }

        private void BtnSavePosition_Click(object sender, EventArgs e)
        {
            RobotPosition pos = new RobotPosition
            {
                J1 = tkbJoint1.Value,
                J2 = tkbJoint2.Value,
                J3 = tkbJoint3.Value,
                J4 = tkbJoint4.Value,
                J5 = tkbJoint5.Value,
                J6 = tkbJoint6.Value
            };
            savedPositions.Add(pos);
            lstSavedPositions.Items.Add($"Điểm {savedPositions.Count}: [{pos.J1},{pos.J2},{pos.J3},{pos.J4},{pos.J5},{pos.J6}]");
        }

        private void BtnClearPositions_Click(object sender, EventArgs e)
        {
            savedPositions.Clear();
            lstSavedPositions.Items.Clear();
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [DẠY HỌC LỆNH]: Đã xóa sạch danh sách bộ nhớ điểm.");
        }

        private void BtnRunPositions_Click(object sender, EventArgs e)
        {
            if (savedPositions.Count == 0)
            {
                MessageBox.Show("Vui lòng lưu ít nhất một vị trí trước khi chạy chu trình!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            isAutoRunning = true;
            currentAutoIndex = 0;
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [AUTO-RUN]: Kích hoạt chu trình lặp điểm tự động.");
            RunNextSavedPosition();
        }

        private void TmtClock_Tick(object sender, EventArgs e)
        {
            if (lblClock == null) return;
            lblClock.Text = DateTime.Now.ToString("HH:mm:ss") + Environment.NewLine + DateTime.Now.ToString("dd/MM/yyyy");
        }

        private void TmtAutoStep_Tick(object sender, EventArgs e)
        {
            tmtAutoStep.Stop();
            if (isAutoRunning)
            {
                RunNextSavedPosition();
            }
        }

        private void RunNextSavedPosition()
        {
            if (currentAutoIndex >= savedPositions.Count)
            {
                isAutoRunning = false;
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [AUTO-RUN]: Hoàn thành chu trình lặp lại.");
                return;
            }
            RobotPosition pos = savedPositions[currentAutoIndex];

            tkbJoint1.Value = pos.J1; tkbJoint2.Value = pos.J2; tkbJoint3.Value = pos.J3;
            tkbJoint4.Value = pos.J4; tkbJoint5.Value = pos.J5; tkbJoint6.Value = pos.J6;

            string packet = $"GOC:{pos.J1},{pos.J2},{pos.J3},{pos.J4},{pos.J5},{pos.J6}\n";
            _hardwareManager.SendData(packet);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [AUTO RUN -> ĐIỂM {currentAutoIndex + 1}]: {packet.Trim()}");

            currentAutoIndex++;
        }
    }
}
/*
    Lưu ý: Đoạn code trên đã được chỉnh sửa và bổ sung thêm các phần tử giao diện, sự kiện, và logic quản lý trạng thái để hoàn thiện chức năng điều khiển robot, xử lý hình ảnh AI, và quản lý chu trình tự động lặp lại các vị trí đã lưu. Bạn có thể tham khảo và tích hợp vào dự án của mình, nhớ kiểm tra kỹ các tên biến và hàm để đảm bảo khớp với phần còn lại của hệ thống.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management; // Sử dụng hàm quét sâu Device Manager của Windows

namespace WindowsFormschoRobotcuaG8
{
    public partial class Form1 : Form
    {
        private class RobotPosition
        {
            public int J1;
            public int J2;
            public int J3;
            public int J4;
            public int J5;
            public int J6;
        }
        // --- 1. KHAI BÁO CÁC LINH KIỆN GIAO DIỆN ---
        private Panel pnlLeftControl;
        private TrackBar tkbJoint1, tkbJoint2, tkbJoint3, tkbJoint4, tkbJoint5, tkbJoint6;
        private Label lblJoint1, lblJoint2, lblJoint3, lblJoint4, lblJoint5, lblJoint6;
        //  ⭐  LINH KIỆN MỚI: Cấu hình tốc độ độc lập từng khớp theo ý thầy
        private NumericUpDown numSpd1, numSpd2, numSpd3, numSpd4, numSpd5, numSpd6;
        private Label lblSpeedTitle;
        //  ⭐  LINH KIỆN MỚI: Gripper speed control replaces old global speed

        private Panel pnlVisionArea;
        private PictureBox picCamera;
        private TextBox txtDetectShape;
        private Label lblDetectShape;
        private Panel pnlSystemArea, pnlStatusLight;
        private TextBox txtTargetX, txtTargetY, txtTargetZ, txtStatus;
        private Button btnConnect, btnStart, btnStop, btnController, btnResetSystem, btnGripToggle, btnClear;
        private ListBox lstLog;
        // Ô thả xuống chọn cổng COM trực quan
        private ComboBox cboComPorts;
        // Ô văn bản thống kê sản lượng chi tiết dưới cùng
        private TextBox txtStatistics;
        private Timer tmtSimulate;
        private Label lblClock;
        private Timer tmtClock;




        // ==================================================================================
        // --- 2. BIẾN QUẢN LÝ TỌA ĐỘ VÀ TRẠNG THÁI ---
        private float robotX = 0, robotY = 0, robotZ = 0;
        private enum RobotState { Idle, Connected, Running, Fault }
        private RobotState currentState = RobotState.Idle;

        // --- 3. KHAI BÁO CÁC BỘ NÃO QUẢN LÝ TÁCH BIỆT (OOP CHUẨN) ---
        private VisionProcessor _visionProcessor;
        private HardwareManager _hardwareManager;
        private ClassifierManager _classifierManager;

        // Thêm biến cờ cho trạng thái gắp
        private bool isGripped = false;
        private DateTime _lastSendTime = DateTime.MinValue;



        private List<RobotPosition> savedPositions = new List<RobotPosition>();
        private bool isAutoRunning = false;


        private int currentAutoIndex = 0;

        // FIX (vấn đề 1): Timer tạo độ trễ giữa các vị trí khi chạy tự động,
        // để mắt người có thể quan sát được robot di chuyển qua từng vị trí.
        private Timer tmtAutoStep;
        private const int AUTO_STEP_DELAY_MS = 2000; // 2 giây giữa mỗi vị trí

        // FIX (vấn đề 2): Cờ đánh dấu gói phản hồi "DONE" sắp tới là của lệnh
        // CHẠY TỌA ĐỘ thủ công (X..Y..Z..A), KHÔNG phải của chu trình auto-run,
        // để tránh statusCode=0 của lệnh thủ công vô tình kích RunNextSavedPosition().
        private bool isManualXYZCommandPending = false;
        private ESP32Streamer _esp32Streamer;
        private PythonBridge _pythonBridge;
        
        public Form1()
        {
            InitializeComponent();
            // 1. Phải khởi tạo các bộ não xử lý lõi TRƯỚC để chúng sẵn sàng có dữ liệu
            _visionProcessor = new VisionProcessor();
            _hardwareManager = new HardwareManager { IsSimulationMode = false };
            _classifierManager = new ClassifierManager();

            // Đấu nối PictureBox vào luồng camera mạng
            _esp32Streamer = new ESP32Streamer(picCamera);
            _esp32Streamer.OnLogMessage += UpdateLogSafe;

            // Khởi tạo cầu nối tiến trình Python
            _pythonBridge = new PythonBridge();
            _pythonBridge.OnPythonLog += UpdateLogSafe;

            // Đồng bộ đăng ký sự kiện cho khối quét ảnh (Nhớ sửa đúng tên chữ VisionProcesser của bạn)
            _visionProcesser.OnProductDetected += VisionProcessor_OnProductDetected;
            // 2. Khởi tạo cầu nối tiến trình Python xử lý AI/YOLO
            _pythonBridge = new PythonBridge();
            _pythonBridge.OnPythonLog += UpdateLogSafe;
            // 2. Sau đó mới gọi dựng giao diện để nạp dữ liệu từ các bộ não lên các TextBox
            DungGiaoDienTuDong();

            // ÉP HỆ THỐNG PHẢI KHÓA SẠCH NÚT NGAY TỪ ĐẦU (Giai đoạn khởi tạo Form)
            ResetToDefault();

            // ĐẤU NỐI SỰ KIỆN ĐA LUỒNG VÀ TRUYỀN THÔNG
            _visionProcessor.OnProductDetected += VisionProcessor_OnProductDetected;
            _hardwareManager.OnDataReceived += HardwareManager_OnDataReceived;

            tmtSimulate = new Timer();
            tmtSimulate.Interval = 100;
            tmtSimulate.Tick += tmtSimulate_Tick_1;

            // FIX (vấn đề 1): Timer độ trễ giữa các bước auto-run
            tmtAutoStep = new Timer();
            tmtAutoStep.Interval = AUTO_STEP_DELAY_MS;
            tmtAutoStep.Tick += TmtAutoStep_Tick;


        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ResetToDefault();
        }

        // --- 4. HÀM DỰNG GIAO DIỆN SCADA NÂNG CẤP MỚI ---
        private void DungGiaoDienTuDong()
        {
            // Window
            this.Size = new Size(1350, 740);
            this.Text = "HỆ THỐNG GIÁM SÁT & PHÂN LOẠI ROBOT";
            this.BackColor = Color.FromArgb(240, 244, 247);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Left control panel
            pnlLeftControl = new Panel { Size = new Size(400, 500), Location = new Point(15, 15), BackColor = Color.White, BorderStyle = BorderStyle.None };
            this.Controls.Add(pnlLeftControl);

            TrackBar[] trackBars = { tkbJoint1 = new TrackBar(), tkbJoint2 = new TrackBar(), tkbJoint3 = new TrackBar(), tkbJoint4 = new TrackBar(), tkbJoint5 = new TrackBar(), tkbJoint6 = new TrackBar() };
            NumericUpDown[] numSpeeds = { numSpd1 = new NumericUpDown(), numSpd2 = new NumericUpDown(), numSpd3 = new NumericUpDown(), numSpd4 = new NumericUpDown(), numSpd5 = new NumericUpDown(), numSpd6 = new NumericUpDown() };
            Label[] labels = { lblJoint1 = new Label(), lblJoint2 = new Label(), lblJoint3 = new Label(), lblJoint4 = new Label(), lblJoint5 = new Label(), lblJoint6 = new Label() };
            string[] jointNames = { "Trục 1 ", "Trục 2 ", "Trục 3 ", "Trục 4 ", "Trục 5 ", "Trục 6 " };

            for (int i = 0; i < 6; i++)
            {
                labels[i].Text = jointNames[i] + ": 0°";
                labels[i].Location = new Point(15, 10 + i * 68);
                labels[i].Size = new Size(150, 20);
                labels[i].Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                labels[i].ForeColor = Color.DarkSlateGray;
                pnlLeftControl.Controls.Add(labels[i]);

                // Cấu hình thanh gạt góc xoay động cơ
                if (i == 0)
                {
                    // Trục 1: giữ dải logic -180..180 cho người dùng trên UI.
                    // Khi gửi xuống Arduino, giá trị này sẽ được map sang 0..180
                    // ở phía firmware (mapJoint1ToServo). Không đổi gì ở đây.
                    trackBars[i].Minimum = -180;
                    trackBars[i].Maximum = 180;
                }
                else
                {
                    trackBars[i].Minimum = 0;
                    trackBars[i].Maximum = 180;
                }
                trackBars[i].Location = new Point(15, 30 + i * 68);
                trackBars[i].Size = new Size(250, 30);
                trackBars[i].Scroll += (s, e) => { UpdateAngleLabels(); SendRobotAngles(); };
                pnlLeftControl.Controls.Add(trackBars[i]);

                //  ⭐  TÍCH HỢP Ô NHẬP TỐC ĐỘ ĐỘC LẬP TỪNG KHỚP (10% -> 100%)
                numSpeeds[i].Minimum = 10;
                numSpeeds[i].Maximum = 100;
                numSpeeds[i].Value = 50; // Mặc định 50%
                numSpeeds[i].Location = new Point(280, 30 + i * 68);
                numSpeeds[i].Size = new Size(95, 23);
                numSpeeds[i].Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                numSpeeds[i].TextAlign = HorizontalAlignment.Center;
                numSpeeds[i].ValueChanged += (s, e) => SendIndependentSpeeds();
                pnlLeftControl.Controls.Add(numSpeeds[i]);

                // Gắn nhãn chữ nhỏ ghi chú "Tốc độ" ở trên đầu ô nhập của trục đầu tiên
                if (i == 0)
                {
                    lblSpeedTitle = new Label { Text = "Tốc độ (%)", Location = new Point(280, 10), Size = new Size(95, 18), Font = new Font("Segoe UI", 8F, FontStyle.Italic), ForeColor = Color.Gray };
                    pnlLeftControl.Controls.Add(lblSpeedTitle);
                }
            }

            Panel pnlControlButtons = new Panel
            {
                Name = "pnlControlButtons",
                Location = new Point(15, 405),
                Size = new Size(380, 90),
                BackColor = Color.White
            };

            pnlLeftControl.Controls.Add(pnlControlButtons);
            // Vision area (camera)
            pnlVisionArea = new Panel { Size = new Size(430, 500), Location = new Point(430, 15), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(pnlVisionArea);
            Label lblVisionTitle = new Label { Text = "CAMERA REAL-TIME THREAD FEED (2 FPS)", Location = new Point(15, 10), Size = new Size(400, 20), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.Navy };
            pnlVisionArea.Controls.Add(lblVisionTitle);
            picCamera = new PictureBox { Size = new Size(400, 350), Location = new Point(15, 35), BackColor = Color.Black, BorderStyle = BorderStyle.Fixed3D, SizeMode = PictureBoxSizeMode.StretchImage };
            pnlVisionArea.Controls.Add(picCamera);
            lblDetectShape = new Label { Text = "HÌNH DẠNG:", Location = new Point(15, 400), Size = new Size(90, 20), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            txtDetectShape = new TextBox { Location = new Point(110, 397), Size = new Size(280, 35), ReadOnly = true, BackColor = Color.GhostWhite, Font = new Font("Segoe UI", 10F, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            pnlVisionArea.Controls.Add(lblDetectShape); pnlVisionArea.Controls.Add(txtDetectShape);


            // Status light and log
            pnlStatusLight = new Panel { Size = new Size(40, 40), Location = new Point(875, 15), BackColor = Color.WhiteSmoke, BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(pnlStatusLight);

            // FIX: lstLog chưa từng được khởi tạo trước khi Add -> gây NullReferenceException khi load Form.
            // Khởi tạo và đặt vị trí/kích thước hợp lý bên cạnh đèn trạng thái.
            lstLog = new ListBox
            {
                Location = new Point(925, 15),
                Size = new Size(400, 500),
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen
            };
            this.Controls.Add(lstLog);

            // Target X/Y/Z and COM / control buttons
            Label lblX = new Label { Text = "Target X:", Location = new Point(150, 530), Size = new Size(60, 20) };
            txtTargetX = new TextBox { Location = new Point(150, 550), Size = new Size(50, 23), Text = "0" };
            Label lblY = new Label { Text = "Target Y:", Location = new Point(280, 530), Size = new Size(60, 20) };
            txtTargetY = new TextBox { Location = new Point(280, 550), Size = new Size(50, 23), Text = "0" };
            Label lblZ = new Label { Text = "Target Z:", Location = new Point(410, 530), Size = new Size(60, 20) };
            txtTargetZ = new TextBox { Location = new Point(410, 550), Size = new Size(50, 23), Text = "0" };
            this.Controls.AddRange(new Control[] { lblX, txtTargetX, lblY, txtTargetY, lblZ, txtTargetZ });

            Button btnSendXYZ = new Button
            {
                Text = "CHẠY TỌA ĐỘ",
                Location = new Point(480, 540),
                Size = new Size(110, 35),
                BackColor = Color.MediumPurple,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White
            };
            btnSendXYZ.Click += BtnSendXYZ_Click; // Gắn sự kiện click
            this.Controls.Add(btnSendXYZ);

            cboComPorts = new ComboBox
            {
                Location = new Point(0, 10),
                Size = new Size(110, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            pnlControlButtons.Controls.Add(cboComPorts);
            QuetCongComTuDong();

            btnConnect = new Button { Text = "Kết Nối COM", Location = new Point(120, 10), Size = new Size(110, 35), BackColor = Color.LightGray };
            btnStart = new Button { Text = "Chạy Tự Động", Location = new Point(0, 45), Size = new Size(110, 35), BackColor = Color.LightGray };
            btnController = new Button { Text = "Lưu vị trí", Location = new Point(240, 10), Size = new Size(100, 35), BackColor = Color.LightBlue };
            btnStop = new Button { Text = "DỪNG KHẨN", Location = new Point(120, 45), Size = new Size(110, 35), BackColor = Color.MistyRose, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            // Combined button: clears log + resets coordinates + sends RESET
            btnResetSystem = new Button { Text = "RESET HỆ THỐNG", Location = new Point(240, 45), Size = new Size(135, 35), BackColor = Color.NavajoWhite, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            btnConnect.Click += btnConnect_Click;
            btnStart.Click += btnStart_Click;
            btnController.Click += btnController_Click;
            btnStop.Click += btnStop_Click;
            btnResetSystem.Click += btnResetSystem_Click;

            pnlControlButtons.Controls.Add(btnConnect);
            pnlControlButtons.Controls.Add(btnController);
            pnlControlButtons.Controls.Add(btnStart);
            pnlControlButtons.Controls.Add(btnStop);
            pnlControlButtons.Controls.Add(btnResetSystem);


            tmtClock = new Timer();
            tmtClock.Interval = 1000;
            tmtClock.Tick += TmtClock_Tick;
            tmtClock.Start();

            // Status and Statistics
            // FIX: txtStatus và txtStatistics trước đây cùng Location (15,580) nên đè lên nhau.
            // Đặt txtStatus phía trên, txtStatistics phía dưới.
            txtStatus = new TextBox { Location = new Point(15, 580), Size = new Size(1310, 25), ReadOnly = true, BackColor = Color.White, Font = new Font("Segoe UI", 9.5F, FontStyle.Italic) };
            this.Controls.Add(txtStatus);

            txtStatistics = new TextBox
            {
                Location = new Point(15, 610),
                Size = new Size(1310, 200),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = Color.FromArgb(43, 43, 43),
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 13F, FontStyle.Bold)
            };
            this.Controls.Add(txtStatistics);
            HienThiBangThongKeTrong("");

            // Grip button
            btnGripToggle = new Button();
            btnGripToggle.Name = "btnGripToggle";
            btnGripToggle.Text = "GẮP VẬT";
            btnGripToggle.BackColor = Color.Orange;
            btnGripToggle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGripToggle.ForeColor = Color.Black;
            btnGripToggle.FlatStyle = FlatStyle.Flat;
            btnGripToggle.FlatAppearance.BorderSize = 1;
            btnGripToggle.Location = new Point(30, 520);
            btnGripToggle.Size = new Size(100, 42);
            btnGripToggle.Enabled = false;
            this.Controls.Add(btnGripToggle);
            btnGripToggle.BringToFront();
            btnGripToggle.Click += btnGripToggle_Click;

            // Clear button

            lblClock = new Label
            {
                Name = "lblClock",

                Location = new Point(1350, 680),

                Size = new Size(150, 80),

                Font = new Font("Consolas", 18F, FontStyle.Bold),

                ForeColor = Color.DarkBlue,

                BackColor = Color.White,

                BorderStyle = BorderStyle.FixedSingle,

                TextAlign = ContentAlignment.MiddleCenter
            };

            this.Controls.Add(lblClock);
            lblClock.BringToFront();
        }









        // ==================================================================================
        // --- 5. ĐÓN NHẬN ĐA LUỒNG & PHÂN TÍCH CHUỖI ---
        private void VisionProcessor_OnProductDetected(VisionResult result)
        {
            if (this.IsDisposed) return;

            // Ép luồng xử lý (Invoke) về luồng UI chính để cập nhật text an toàn
            this.BeginInvoke((MethodInvoker)delegate
            {
                // 1. Hiển thị hình dáng nhận diện được lên ô kết quả
                txtDetectShape.Text = result.Shape;

                // 2. Chuyển thông tin sang ClassifierManager để lấy tọa độ khay và cộng sản lượng
                TrayCoordinate tray = _classifierManager.GetTrayCoordinate(result.Shape);

                // 3. Đưa tọa độ khay mục tiêu lên các ô TextBox nhập Target
                txtTargetX.Text = tray.X.ToString("F1");
                txtTargetY.Text = tray.Y.ToString("F1");
                txtTargetZ.Text = tray.Z.ToString("F1");

                // 4. Cập nhật bảng thống kê sản lượng mới lên giao diện các TextBox hiển thị số lượng
                // (Bạn hãy thay thế tên txtCount... tương ứng với các ô đếm trên giao diện của bạn)
                txtCountRectangle.Text = _classifierManager.Report.SoLuongRectangle.ToString();
                txtCountSquare.Text = _classifierManager.Report.SoLuongSquare.ToString();
                txtCountPyramid.Text = _classifierManager.Report.SoLuongPyramid.ToString();
                txtCountCircle.Text = _classifierManager.Report.SoLuongCircle.ToString();
                txtTotalProducts.Text = _classifierManager.Report.TongSoSanPham.ToString();

                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Phân Loại]: Phát hiện vật {result.Shape} -> Điều hướng về khay X:{tray.X} Y:{tray.Y}");
            });
        }

        private void HardwareManager_OnDataReceived(string completePacket)
        {
            this.Invoke(new Action(() =>
            {
                if (currentState == RobotState.Fault) return;
                string[] tokens = completePacket.Split(',');
                if (tokens.Length >= 6)
                {
                    string statusCode = tokens[0].Trim();
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [RX - Mạch Báo Lên]: Gói sạch -> {completePacket} | Trạng thái: {statusCode}");


                    if (statusCode == "1")
                    {
                        txtStatus.Text = "Hệ thống: Cánh tay robot đang chuyển động ngoài đời thực (BUSY)...";
                        pnlStatusLight.BackColor = Color.Orange;
                    }
                    else if (statusCode == "0")
                    {
                        txtStatus.Text = "Hệ thống: Robot đã gắp xong sản phẩm thành công.";
                        pnlStatusLight.BackColor = Color.Cyan;

                        // FIX: Với lệnh "CHẠY TỌA ĐỘ" (X..Y..Z..A), gói DONE trả về sau khi
                        // executeIKAndPickup() đã gọi goHome() ở cuối chu trình -> val1..val6
                        // lúc này là VỊ TRÍ HOME, không phải tọa độ X,Y,Z vừa nhập.
                        // Do đó KHÔNG đồng bộ trackbar từ gói này để tránh "giá trị rác"
                        // nhảy về HOME ngay sau khi chạy.
                        if (!isManualXYZCommandPending)
                        {
                            // Lưu ý: Arduino trả val1 (servo1, dải vật lý 0..180) trong tokens[1],
                            // còn UI trục 1 (tkbJoint1) dùng dải logic -180..180.
                            // Hiện chỉ đồng bộ tkbJoint2 (val2, dải 0..180 khớp trực tiếp với UI).
                            // tkbJoint1 không tự động cập nhật ngược để tránh sai lệch dải giá trị
                            // (cần hàm map ngược 0..180 -> -180..180 nếu muốn đồng bộ đầy đủ).
                            if (float.TryParse(tokens[2], out float m2))
                            {
                                tkbJoint2.Value = (int)Math.Min(Math.Max(m2, 0), 180);
                                UpdateAngleLabels();
                            }
                        }

                        // FIX (vấn đề 2): Nếu gói DONE này là phản hồi của lệnh
                        // "CHẠY TỌA ĐỘ" thủ công (X..Y..Z..A gửi từ BtnSendXYZ_Click),
                        // thì KHÔNG được kích hoạt RunNextSavedPosition(), tránh
                        // robot di chuyển thêm 1 lần "rác" ngoài ý muốn.
                        if (isManualXYZCommandPending)
                        {
                            isManualXYZCommandPending = false;
                        }
                        else if (isAutoRunning)
                        {
                            // FIX (vấn đề 1): không gọi RunNextSavedPosition() ngay lập tức.
                            // Chờ AUTO_STEP_DELAY_MS (2 giây) để mắt người quan sát được
                            // robot đã di chuyển tới vị trí hiện tại trước khi sang vị trí kế.
                            tmtAutoStep.Stop();
                            tmtAutoStep.Start();
                        }
                    }
                    else if (statusCode == "2")
                    {
                        UpdateSystemState(RobotState.Fault);
                    }
                }
            }));
        }

        // --- 6. CÁC SỰ KIỆN ĐIỀU KHIỂN NÚT BẤM VÀ CẤU HÌNH TỐC ĐỘ ---
        private void UpdateAngleLabels()
        {
            if (lblJoint1 == null) return;
            lblJoint1.Text = $"Trục 1 :    {tkbJoint1.Value}°";
            lblJoint2.Text = $"Trục 2 :     {tkbJoint2.Value}°";
            lblJoint3.Text = $"Trục 3 :     {tkbJoint3.Value}°";
            lblJoint4.Text = $"Trục 4 :   {tkbJoint4.Value}°";
            lblJoint5.Text = $"Trục 5 :   {tkbJoint5.Value}°";
            lblJoint6.Text = $"Trục 6 :   {tkbJoint6.Value}°";
        }

        //  ⭐  HÀM MỚI: Phát gói lệnh cài đặt tốc độ độc lập cho từng khớp động cơ qua Serial
        // Gửi nguyên gói SET_SPEED,v1,v2,v3,v4,v5,v6 xuống Arduino.
        // Arduino đã có nhánh nhận lệnh này (xem file .ino: header == "SET_SPEED").
        // Phần áp dụng tốc độ cụ thể (nếu cần xử lý thêm) do bạn tự bổ sung sau.
        private void SendIndependentSpeeds()
        {
            if (_hardwareManager == null || !_hardwareManager.IsOpen() || currentState == RobotState.Fault) return;
            int v1 = (int)numSpd1.Value;
            int v2 = (int)numSpd2.Value;
            int v3 = (int)numSpd3.Value;
            int v4 = (int)numSpd4.Value;
            int v5 = (int)numSpd5.Value;
            int v6 = (int)numSpd6.Value;
            string speedPacket = $"SET_SPEED,{v1},{v2},{v3},{v4},{v5},{v6}\n";
            _hardwareManager.SendData(speedPacket);
        }

        private void SendRobotAngles()
        {
            // THÊM ĐOẠN NÀY LÀM VAN KHÓA (THROTTLE)
            // Nếu chưa trôi qua đủ 100ms (0.1 giây) kể từ lần gửi cuối, thì bỏ qua không gửi tiếp
            if ((DateTime.Now - _lastSendTime).TotalMilliseconds < 100)
            {
                return;
            }

            // Cập nhật lại mốc thời gian vừa gửi
            _lastSendTime = DateTime.Now;

            // Lưu ý: tkbJoint1.Value nằm trong dải -180..180 (logic UI).
            // Giá trị này được gửi NGUYÊN BẢN xuống Arduino; việc map sang
            // dải vật lý 0..180 của servo1 do Arduino tự thực hiện
            // (hàm mapJoint1ToServo trong file .ino).
            string cmd = $"MOVE,{tkbJoint1.Value},{tkbJoint2.Value},{tkbJoint3.Value},{tkbJoint4.Value},{tkbJoint5.Value},{tkbJoint6.Value}\n";

            if (_hardwareManager != null && _hardwareManager.IsOpen())
            {
                _hardwareManager.SendData(cmd);
            }
        }

        private void UpdateSystemState(RobotState newState)
        {
            currentState = newState;

            btnResetSystem.Enabled = true;
            switch (currentState)
            {
                case RobotState.Idle:
                    btnConnect.Text = "Kết Nối COM";
                    btnConnect.BackColor = Color.LightGray;
                    btnConnect.Enabled = true;
                    cboComPorts.Enabled = true;
                    btnStart.Enabled = false;
                    btnController.Enabled = false;
                    btnStop.Enabled = false;
                    pnlStatusLight.BackColor = Color.WhiteSmoke;
                    txtStatus.Text = "Hệ thống: Sẵn sàng. Vui lòng kết nối cổng COM để bắt đầu vận hành.";
                    txtDetectShape.Text = "---";
                    SetTrackbarsEnable(false);
                    SetSpeedsControlsEnable(false);

                    if (btnGripToggle != null) btnGripToggle.Enabled = false; // lock grip button
                    break;
                case RobotState.Connected:
                    btnConnect.Text = "ĐÃ KẾT NỐI";
                    btnConnect.BackColor = Color.LightGreen;
                    btnConnect.Enabled = false;
                    cboComPorts.Enabled = false;
                    btnStart.Enabled = true;
                    btnController.Enabled = true;
                    btnStop.Enabled = true;
                    pnlStatusLight.BackColor = Color.LimeGreen;
                    txtStatus.Text = "Hệ thống: Đã kết nối cổng COM thành công.";
                    SetTrackbarsEnable(true);
                    SetSpeedsControlsEnable(true);

                    if (btnGripToggle != null) btnGripToggle.Enabled = true; // allow grip when connected (including simulation)
                    break;
                case RobotState.Running:
                    btnConnect.Enabled = false;
                    cboComPorts.Enabled = false;
                    btnStart.Enabled = false;
                    btnController.Enabled = true;
                    btnStop.Enabled = true;
                    pnlStatusLight.BackColor = Color.Cyan;
                    txtStatus.Text = "Hệ thống: Luồng Camera chạy ngầm đang quét và phân loại sản phẩm...";
                    txtDetectShape.Text = "Đang quét...";

                    if (btnGripToggle != null) btnGripToggle.Enabled = true; // allow grip while running
                    break;
                case RobotState.Fault:
                    btnConnect.Text = "Kết Nối COM";
                    btnConnect.BackColor = Color.LightGray;
                    btnConnect.Enabled = false;
                    cboComPorts.Enabled = false;
                    btnStart.Enabled = false;
                    btnController.Enabled = false;
                    btnStop.Enabled = false;
                    pnlStatusLight.BackColor = Color.Crimson;
                    txtStatus.Text = "Hệ thống: DỪNG KHẨN CẤP! Đã hủy toàn bộ luồng ngầm và ngắt kết nối phần cứng.";
                    SetTrackbarsEnable(false);
                    SetSpeedsControlsEnable(false);

                    if (btnGripToggle != null) btnGripToggle.Enabled = false; // lock grip on fault
                    break;
            }
        }

        private void SetTrackbarsEnable(bool enable)
        {
            if (tkbJoint1 == null) return;
            tkbJoint1.Enabled = tkbJoint2.Enabled = tkbJoint3.Enabled =
            tkbJoint4.Enabled = tkbJoint5.Enabled = tkbJoint6.Enabled = enable; ;
        }

        // Đóng/Mở khóa đồng bộ cụm cài đặt tốc độ nâng cấp mới
        private void SetSpeedsControlsEnable(bool enable)
        {

            numSpd1.Enabled = numSpd2.Enabled = numSpd3.Enabled =
            numSpd4.Enabled = numSpd5.Enabled = numSpd6.Enabled = enable;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (cboComPorts.SelectedItem == null || string.IsNullOrEmpty(cboComPorts.SelectedItem.ToString()))
            {
                DialogResult dialogResult = MessageBox.Show(
                    "Hệ thống không tìm thấy cổng COM của Arduino nào đang kết nối!\nBạn có muốn BẬT chế độ Mô Phỏng (Simulation Mode) để chạy thử giao diện không?",
                    "CẢNH BÁO PHẦN CỨNG",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );
                if (dialogResult == DialogResult.Yes)
                {
                    _hardwareManager.IsSimulationMode = true;
                    _hardwareManager.Connect();
                    UpdateSystemState(RobotState.Connected);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [MÔ PHỎNG]: Đã kích hoạt chế độ giả lập phần mềm.");

                }
                else
                {
                    UpdateSystemState(RobotState.Idle);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [LỖI]: Kết nối thất bại. Hãy kiểm tra lại cáp USB mạch Arduino.");
                }

                return;
            }

            string targetPort = cboComPorts.SelectedItem.ToString();
            _hardwareManager.IsSimulationMode = false;
            bool connected = _hardwareManager.Connect(targetPort, 115200);
            if (connected)
            {
                UpdateSystemState(RobotState.Connected);
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG THẬT]: Nhận diện và kết nối thành công với mạch tại cổng {targetPort}.");
                // Đã đồng bộ dải tốc độ mặc định xuống vi điều khiển ngay sau khi kết nối thông suốt
                SendIndependentSpeeds();

            }
            else
            {
                DialogResult dialogResult = MessageBox.Show(
                    $"Không thể truy cập cổng {targetPort}! Khả năng cao do phần mềm khác (như Arduino IDE) đang chiếm quyền.\n\nBạn có muốn bật chế độ Mô Phỏng tạm thời để kiểm tra giao diện không?",
                    "LỖI CHIẾM QUYỀN CỔNG COM",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error
                );
                if (dialogResult == DialogResult.Yes)
                {
                    _hardwareManager.IsSimulationMode = true;
                    _hardwareManager.Connect();
                    UpdateSystemState(RobotState.Connected);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [MÔ PHỎNG]: Kích hoạt giả lập do cổng thật {targetPort} bị chiếm quyền.");
                }
                else
                {
                    UpdateSystemState(RobotState.Idle);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [LỖI]: Thất bại khi mở {targetPort}. Hãy đóng Serial Monitor!");
                }
            }

        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (savedPositions.Count == 0)
            {
                MessageBox.Show(
                    "Chưa có vị trí nào được lưu.");

                return;
            }

            UpdateSystemState(RobotState.Running);

            currentAutoIndex = 0;

            isAutoRunning = true;

            RunNextSavedPosition();
        }

        private void btnController_Click(object sender, EventArgs e)
        {
            RobotPosition pos = new RobotPosition()
            {
                J1 = tkbJoint1.Value,
                J2 = tkbJoint2.Value,
                J3 = tkbJoint3.Value,
                J4 = tkbJoint4.Value,
                J5 = tkbJoint5.Value,
                J6 = tkbJoint6.Value
            };

            savedPositions.Add(pos);

            UpdateLogSafe(
                $"[{DateTime.Now:HH:mm:ss}] [LƯU VỊ TRÍ] P{savedPositions.Count} = " +
                $"{pos.J1},{pos.J2},{pos.J3},{pos.J4},{pos.J5}, {pos.J6}");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            tmtSimulate.Stop();
            tmtAutoStep.Stop();
            isAutoRunning = false;
            isManualXYZCommandPending = false;

            _visionProcessor.StopVisionStream();
            _hardwareManager.Disconnect();

            UpdateSystemState(RobotState.Fault);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [CẢNH BÁO]: KÍCH HOẠT DỪNG KHẨN CẤP HỆ THỐNG!");

            HienThiBangThongKeTrong("BÁO CÁO THỐNG KÊ CHI TIẾT SẢN PHẨM PHÂN LOẠI (HỆ THỐNG ĐÃ DỪNG KHẨN)");
        }

        private void btnReset_Click(object sender, EventArgs e) => ResetToDefault();

        private void btnClear_Click(object sender, EventArgs e)
        {
            HienThiBangThongKeTrong("");
        }

        private void ResetToDefault()
        {
            if (savedPositions != null)
            {
                savedPositions.Clear();
            }
            if (tmtSimulate != null) tmtSimulate.Stop();
            if (tmtAutoStep != null) tmtAutoStep.Stop();
            isAutoRunning = false;
            isManualXYZCommandPending = false;
            currentAutoIndex = 0;

            if (_visionProcessor != null) _visionProcessor.StopVisionStream();
            if (_hardwareManager != null) _hardwareManager.Disconnect();
            if (_classifierManager != null && _classifierManager.Report != null)
            {
                _classifierManager.Report.ResetReport();
            }
            if (tkbJoint1 != null && tkbJoint2 != null && tkbJoint3 != null && tkbJoint4 != null && tkbJoint5 != null)
            {
                tkbJoint1.Value = tkbJoint2.Value = tkbJoint3.Value = tkbJoint4.Value = tkbJoint5.Value = tkbJoint6.Value = 90; ;
                UpdateAngleLabels();
            }




            if (numSpd1 != null)
            {
                numSpd1.Value = numSpd2.Value = numSpd3.Value =
                numSpd4.Value = numSpd5.Value = numSpd6.Value = 50;
            }

            QuetCongComTuDong();
            UpdateSystemState(RobotState.Idle);
            HienThiBangThongKeTrong("HỆ THỐNG ĐÃ RESET - SẴN SÀNG QUÉT CHU TRÌNH MỚI");
            if (lstLog != null)
            {
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Hệ thống]: Khởi tạo lại thành công.");

            }
        }

        private void QuetCongComTuDong()
        {
            if (cboComPorts == null) return;
            cboComPorts.Items.Clear();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'"))
                {
                    var ports = searcher.Get();
                    foreach (var port in ports)
                    {
                        string caption = port["Caption"]?.ToString();
                        if (caption != null && (caption.Contains("Arduino") || caption.Contains("USB Serial") || caption.Contains("CH340") || caption.Contains("CP210")))
                        {
                            int startIndex = caption.IndexOf("(COM");
                            int endIndex = caption.IndexOf(")", startIndex);
                            if (startIndex != -1 && endIndex != -1)
                            {
                                string comName = caption.Substring(startIndex + 1, endIndex - startIndex - 1);
                                if (!cboComPorts.Items.Contains(comName))
                                {
                                    cboComPorts.Items.Add(comName);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                string[] basicPorts = System.IO.Ports.SerialPort.GetPortNames();
                foreach (string p in basicPorts)
                {
                    if (!cboComPorts.Items.Contains(p)) cboComPorts.Items.Add(p);
                }
            }

            if (cboComPorts.Items.Count > 0)
            {
                cboComPorts.SelectedIndex = 0;
            }
            else
            {
                cboComPorts.Text = "";
            }
        }

        private void HienThiBangThongKeTrong(string tieuDe)
        {
            if (_classifierManager == null || _classifierManager.Report == null || txtStatistics == null) return;

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("====================================================================================================");
            sb.AppendLine($"   {(string.IsNullOrEmpty(tieuDe) ? "HỆ THỐNG GIÁM SÁT SẢN LƯỢNG SẢN PHẨM THỜI GIAN THỰC - ROBOT G8" : tieuDe)}");
            sb.AppendLine("====================================================================================================");
            sb.AppendLine("   DANH MỤC HÌNH KHỐI                                    |   SỐ LƯỢNG");
            sb.AppendLine("----------------------------------------------------------------------------------------------------");
            sb.AppendLine($"   - Hình Khối Vuông                                     |   {_classifierManager.Report.SoLuongVuong}");
            sb.AppendLine($"   - Hình Khối Tròn                                      |   {_classifierManager.Report.SoLuongTron}");
            sb.AppendLine($"   - Hình Khối Tam Giác                                  |   {_classifierManager.Report.SoLuongTamGiac}");
            sb.AppendLine("----------------------------------------------------------------------------------------------------");
            sb.AppendLine($"   >>> TỔNG SÓ LƯỢNG SẢN PHẨM ĐÃ XỬ LÝ TRONG CHU KỲ: {_classifierManager.Report.TongSoSanPham} vật thể.");
            sb.AppendLine("====================================================================================================");

            txtStatistics.Text = sb.ToString();
        }

        private void tmtSimulate_Tick_1(object sender, EventArgs e)
        {
            if (currentState == RobotState.Fault) return;
            float targetX = 0, targetY = 0, targetZ = 0;
            if (float.TryParse(txtTargetX.Text, out targetX) && float.TryParse(txtTargetY.Text, out targetY) && float.TryParse(txtTargetZ.Text, out targetZ))
            {
                float dx = targetX - robotX, dy = targetY - robotY, dz = targetZ - robotZ;
                if (Math.Abs(dx) > 0.1f || Math.Abs(dy) > 0.1f || Math.Abs(dz) > 0.1f)
                {
                    robotX += dx * 0.1f; robotY += dy * 0.1f; robotZ += dz * 0.1f;
                    if (robotX >= -180 && robotX <= 180) tkbJoint1.Value = (int)robotX;
                    if (robotY >= 0 && robotY <= 180) tkbJoint2.Value = (int)robotY;
                    if (robotZ >= 0 && robotZ <= 180) tkbJoint3.Value = (int)robotZ;
                    UpdateAngleLabels();
                }
                else
                {
                    tmtSimulate.Stop();
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Hệ thống]: Trục ảo mô phỏng đã cán đích.");

                    _hardwareManager.SimulateIncomingHardwareData($"0,{targetX},{targetY},{targetZ},0,0,0\n");
                }
            }
        }

        // Thêm phương thức xử lý sự kiện trong lớp Form1 (bên ngoài DungGiaoDienTuDong)
        private void btnGripToggle_Click(object sender, EventArgs e)
        {
            // Extra safety: only allow when connected or running (simulation sets Connected)
            if (currentState != RobotState.Connected && currentState != RobotState.Running)
            {
                MessageBox.Show("Vui lòng kết nối cổng COM hoặc bật chế độ mô phỏng trước khi sử dụng nút GẮP/THẢ.", "Yêu cầu kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (!isGripped)
                {
                    string cmd = "GRIP\n";
                    if (_hardwareManager != null && _hardwareManager.IsOpen())
                    {
                        _hardwareManager.SendData(cmd);
                    }
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Grip Toggle]: Sent -> {cmd.Trim()}");
                    btnGripToggle.Text = "THẢ VẬT";
                    btnGripToggle.BackColor = Color.LightGreen;
                    isGripped = true;
                }
                else
                {
                    string cmd = "RELEASE\n";
                    if (_hardwareManager != null && _hardwareManager.IsOpen())
                    {
                        _hardwareManager.SendData(cmd);
                    }
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Grip Toggle]: Sent -> {cmd.Trim()}");
                    btnGripToggle.Text = "GẮP VẬT";
                    btnGripToggle.BackColor = Color.Orange;
                    isGripped = false;
                }


            }
            catch (Exception ex)
            {
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Grip Toggle - ERROR]: {ex.Message}");
            }
        }
        // Chỉ thêm hàm này vào, đừng đụng vào mấy cái nút
        // Đảm bảo tên hàm là UpdateLogSafe để không trùng với code cũ
        private void BtnSendXYZ_Click(object sender, EventArgs e)
        {
            // Chặn người dùng nếu chưa kết nối mạch Arduino
            if (currentState != RobotState.Connected && currentState != RobotState.Running)
            {
                MessageBox.Show("Vui lòng kết nối cổng COM trước khi ra lệnh!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Lấy dữ liệu và kiểm tra xem người dùng nhập có đúng là Số (Float) hay không
            if (float.TryParse(txtTargetX.Text, out float targetX) &&
                float.TryParse(txtTargetY.Text, out float targetY) &&
                float.TryParse(txtTargetZ.Text, out float targetZ))
            {
                // ĐÓNG GÓI CHUỖI VÀ GỬI XUỐNG ARDUINO ĐỂ TÍNH IK
                string ikPacket = $"X{targetX}Y{targetY}Z{targetZ}A\n";

                if (_hardwareManager.IsSimulationMode)
                {
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Mô Phỏng]: Gửi lệnh IK ảo -> {ikPacket.Trim()}");
                    // Kích hoạt mô phỏng thanh trượt ảo nếu muốn
                    tmtSimulate.Start();
                }
                else
                {
                    // FIX (vấn đề 2): Đánh dấu lệnh XYZ thủ công đang chờ phản hồi DONE,
                    // để HardwareManager_OnDataReceived không hiểu nhầm đây là tín hiệu
                    // tiếp tục chu trình auto-run.
                    isManualXYZCommandPending = true;

                    _hardwareManager.SendData(ikPacket);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Thủ Công]: Đã gửi lệnh IK xuống Arduino -> {ikPacket.Trim()}");

                    // Khóa nút tạm thời để chờ Arduino chạy xong (Chờ mạch báo DONE lên)
                    txtStatus.Text = "Hệ thống: Đang chạy thuật toán Động học ngược (IK) tới tọa độ chỉ định...";
                    pnlStatusLight.BackColor = Color.Orange;
                }
            }
            else
            {
                MessageBox.Show("Dữ liệu nhập vào không hợp lệ. Vui lòng chỉ nhập số thực!", "Lỗi nhập liệu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // FIX: Hàm này trước đây rỗng (chỉ có ";") nên log không bao giờ hiển thị.
        // Giờ ghi vào lstLog (đã được khởi tạo ở DungGiaoDienTuDong).
        public void UpdateLogSafe(string message)
        {
            if (lstLog == null) return;

            if (lstLog.InvokeRequired)
            {
                lstLog.Invoke(new Action(() => UpdateLogSafe(message)));
                return;
            }

            lstLog.Items.Add(message);
            lstLog.TopIndex = lstLog.Items.Count - 1; // tự cuộn xuống dòng mới nhất
        }

        private void btnResetSystem_Click(object sender, EventArgs e)
        {
            // Gọi hàm reset kho lưu trữ dữ liệu
            _classifierManager.Report.ResetReport();

            // Sửa sạch các ô hiển thị số lượng trên GUI về 0
            txtCountRectangle.Text = "0";
            txtCountSquare.Text = "0";
            txtCountPyramid.Text = "0";
            txtCountCircle.Text = "0";
            txtTotalProducts.Text = "0";

            txtDetectShape.Clear();
            txtTargetX.Text = "0"; txtTargetY.Text = "0"; txtTargetZ.Text = "0";

            UpdateLogSafe("--- Đã xóa trắng số liệu báo cáo và khôi phục hệ thống mặc định ---");
        }

        // Hàm tự động chạy khi Camera Stream quét được vật thể
        private void VisionProcessor_OnProductDetected(VisionResult result)
        {
            if (this.IsDisposed) return;

            this.BeginInvoke((MethodInvoker)delegate
            {
                txtDetectShape.Text = result.Shape;

                // Bộ não phân loại tính toán tọa độ khay và cộng sản lượng
                TrayCoordinate tray = _classifierManager.GetTrayCoordinate(result.Shape);

                // Đẩy số tọa độ khay lên các ô nhập Target của SCADA
                txtTargetX.Text = tray.X.ToString("F1");
                txtTargetY.Text = tray.Y.ToString("F1");
                txtTargetZ.Text = tray.Z.ToString("F1");

                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Phân Loại]: Phát hiện vật {result.Shape} -> Điều hướng về khay X:{tray.X} Y:{tray.Y}");
            });
        }

        // Hàm sự kiện cho nút bấm START CAMERA STREAM (ESP32-Cam)
        private void btnStartCamera_Click(object sender, EventArgs e)
        {
            string cameraUrl = "http://192.168.1.4:81/stream"; // Thay IP ESP32-Cam của bạn ở đây
            _esp32Streamer.StartStream(cameraUrl);
        }

        // Hàm sự kiện cho nút bấm STOP CAMERA STREAM
        private void btnStopCamera_Click(object sender, EventArgs e)
        {
            _esp32Streamer.StopStream();
        }

        // Hàm gọi file thuật toán Python xử lý ảnh thực tế bên ngoài khi click nút bấm
        private async void btnCallPythonAI_Click(object sender, EventArgs e)
        {
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [C# -> Python]: Đang gọi AI xử lý ảnh...");

            // Tìm file Python nằm trong thư mục Debug/PythonScripts/detect_shapes.py
            string scriptPath = System.IO.Path.Combine(Application.StartupPath, "PythonScripts", "detect_shapes.py");
            string arguments = "--source stream";

            string pythonOutput = await _pythonBridge.RunPythonScriptAsync(scriptPath, arguments);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Python -> Kết quả]: {pythonOutput}");

            // Nếu Python nhận diện chuẩn ra 1 trong 4 khối, kích hoạt gắp phân loại ngay lập tức
            string cleanedOutput = pythonOutput.Trim().ToUpper();
            if (cleanedOutput == "RECTANGLE" || cleanedOutput == "SQUARE" || cleanedOutput == "PYRAMID" || cleanedOutput == "CIRCLE")
            {
                VisionResult realResult = new VisionResult { Shape = cleanedOutput };
                VisionProcessor_OnProductDetected(realResult);
            }
        }

        private void TmtClock_Tick(object sender, EventArgs e)
        {
            if (lblClock == null) return;

            lblClock.Text =
                DateTime.Now.ToString("HH:mm:ss")
                + Environment.NewLine +
                DateTime.Now.ToString("dd/MM/yyyy");
        }
        // FIX (vấn đề 1): Sau khi Arduino báo DONE và đã chờ đủ AUTO_STEP_DELAY_MS,
        // mới gọi bước tiếp theo của chu trình auto-run.
        private void TmtAutoStep_Tick(object sender, EventArgs e)
        {
            tmtAutoStep.Stop();
            if (isAutoRunning)
            {
                RunNextSavedPosition();
            }
        }

        private void RunNextSavedPosition()
        {
            if (currentAutoIndex >= savedPositions.Count)
            {
                isAutoRunning = false;

                UpdateLogSafe(
                    $"[{DateTime.Now:HH:mm:ss}] [AUTO] Hoàn thành chương trình.");

                return;
            }

            RobotPosition pos =
                savedPositions[currentAutoIndex];

            // Cập nhật trackbar trên UI theo vị trí đang chạy, để người dùng
            // thấy được góc hiện tại của từng trục trong quá trình auto-run.
            tkbJoint1.Value = pos.J1;
            tkbJoint2.Value = pos.J2;
            tkbJoint3.Value = pos.J3;
            tkbJoint4.Value = pos.J4;
            tkbJoint5.Value = pos.J5;
            tkbJoint6.Value = pos.J6;
            UpdateAngleLabels();

            string packet =
                $"MOVE,{pos.J1},{pos.J2},{pos.J3},{pos.J4},{pos.J5},{pos.J6}\n";

            _hardwareManager.SendData(packet);

            UpdateLogSafe(
                $"[{DateTime.Now:HH:mm:ss}] [AUTO] Gửi P{currentAutoIndex + 1}");

            currentAutoIndex++;
        }
    }

}
*/