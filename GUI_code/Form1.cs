using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace WindowsFormschoRobotcuaG8
{
    public partial class Form1 : Form
    {
        private class RobotPosition
        {
            public int J1; public int J2; public int J3; public int J4; public int J5; public int J6; public int J7;
        }

        private Panel pnlLeftControl;
        private TrackBar tkbJoint1, tkbJoint2, tkbJoint3, tkbJoint4, tkbJoint5, tkbJoint6, tkbJoint7;
        private Label lblJoint1, lblJoint2, lblJoint3, lblJoint4, lblJoint5, lblJoint6, lblJoint7;
        private NumericUpDown numSpd1, numSpd2, numSpd3, numSpd4, numSpd5, numSpd6,numSpd7;
        private Label lblSpeedTitle;


        private Panel pnlVisionArea;
        private PictureBox picCamera;
        private TextBox txtDetectShape;
        private Label lblDetectShape;
        private TextBox txtTargetX, txtTargetY, txtTargetZ, txtStatus;
        //có thêm btnGripToggle, btnClear;
        private Button btnConnect, btnStart, btnStop1, btnStop2, btnResetSystem, btnGripToggle;
        private Button btnSavePosition, btnRunPositions;


        //có thể thừa
        private ListBox lstLog;
        private ComboBox cboComPorts;
        private TextBox txtStatistics;
      



        private HardwareManager _hardwareManager;
        private ESP32Streamer _esp32Streamer;
        private VisionProcessor _visionProcessor;
        private ClassifierManager _classifierManager;
        private PythonBridge _pythonBridge;

        private enum RobotState { Idle, Connected, Running, Fault }
        private RobotState currentState = RobotState.Idle;
        private bool isGripped = false;
        private DateTime _lastSendTime = DateTime.MinValue;




        private List<RobotPosition> savedPositions = new List<RobotPosition>();
        private bool isAutoRunning = false;
        private int currentAutoIndex = 0;
        private Timer tmtAutoStep;
        private Timer tmtClock;
        private const int AUTO_STEP_DELAY_MS = 2000;

        private bool isManualXYZCommandPending = false;
        private bool isAutoMovePending = false;

        private Queue<VisionResult> _shapeQueue = new Queue<VisionResult>();
        private bool _isRobotBusy = false;
        private string _currentPickingShape = "";
        public Form1()
        {
            InitializeComponent();// khởi tạo các bộ não xử lý lõi để sẵn sàng có dữ liệu

            _hardwareManager = new HardwareManager ();
            _classifierManager = new ClassifierManager();
            _pythonBridge = new PythonBridge();
            _pythonBridge.OnPythonLog += UpdateLogSafe;
            _visionProcessor = new VisionProcessor();
            _visionProcessor.OnLogMessage += UpdateLogSafe;
            _visionProcessor.OnProductDetected += VisionProcessor_OnProductDetected;
            _hardwareManager.OnDataReceived += HardwareManager_OnDataReceived;
            
            DungGiaoDienTuDong();
            _esp32Streamer = new ESP32Streamer(picCamera);
            _esp32Streamer.OnLogMessage += UpdateLogSafe;

            
            tmtClock = new Timer { Interval = 1000 };
            tmtClock.Start();

            //Timer độ trễ giữa các bước auto-run
            tmtAutoStep = new Timer();
            tmtAutoStep.Interval = AUTO_STEP_DELAY_MS;
            tmtAutoStep.Tick += TmtAutoStep_Tick;
            ResetToDefault();
        }
        private void DungGiaoDienTuDong()
        {

            this.Size = new Size(1350, 740);
            this.Text = "HỆ THỐNG GIÁM SÁT & PHÂN LOẠI ROBOT";
            this.BackColor = Color.FromArgb(240, 244, 247);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Left control panel
            pnlLeftControl = new Panel { Size = new Size(400, 500), Location = new Point(50, 15), BackColor = Color.White, BorderStyle = BorderStyle.None };
            this.Controls.Add(pnlLeftControl);

            TrackBar[] trackBars = { tkbJoint1 = new TrackBar(), tkbJoint2 = new TrackBar(), tkbJoint3 = new TrackBar(), tkbJoint4 = new TrackBar(), tkbJoint5 = new TrackBar(), tkbJoint6 = new TrackBar()};
            NumericUpDown[] numSpeeds = { numSpd1 = new NumericUpDown(), numSpd2 = new NumericUpDown(), numSpd3 = new NumericUpDown(), numSpd4 = new NumericUpDown(), numSpd5 = new NumericUpDown(), numSpd6 = new NumericUpDown() };
            Label[] labels = { lblJoint1 = new Label(), lblJoint2 = new Label(), lblJoint3 = new Label(), lblJoint4 = new Label(), lblJoint5 = new Label(), lblJoint6 = new Label() };
            string[] jointNames = { "Trục 1 ", "Trục 2 ", "Trục 3 ", "Trục 4 ", "Trục 5 ", "Trục 6 "};

            for (int i = 0; i < 6; i++)
            {
                labels[i].Text = jointNames[i] + ": 0°";
                labels[i].Location = new Point(15, 10 + i * 68);
                labels[i].Size = new Size(150, 20);
                labels[i].Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                labels[i].ForeColor = Color.DarkSlateGray;
                pnlLeftControl.Controls.Add(labels[i]);
                trackBars[i].Minimum = 0;
                trackBars[i].Maximum = 180;
                trackBars[i].Location = new Point(15, 30 + i * 68);
                trackBars[i].Size = new Size(250, 30);
                trackBars[i].Scroll += (s, e) => { UpdateAngleLabels(); SendRobotAngles(); };
                pnlLeftControl.Controls.Add(trackBars[i]);

                // ô nhập tốc độ 10%->100%
                numSpeeds[i].Minimum = 10;
                numSpeeds[i].Maximum = 100;
                numSpeeds[i].Value = 50; // Mặc định 50%
                numSpeeds[i].Location = new Point(280, 30 + i * 68);
                numSpeeds[i].Size = new Size(95, 23);
                numSpeeds[i].Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                numSpeeds[i].TextAlign = HorizontalAlignment.Center;
                numSpeeds[i].ValueChanged += (s, e) => SendIndependentSpeeds();
                pnlLeftControl.Controls.Add(numSpeeds[i]);
                
                // chữ nhỏ ghi chú "Tốc độ" ở trên đầu ô nhập của trục đầu tiên
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

            btnConnect = new Button { 
                Text = "Kết Nối COM", 
                Location = new Point(120, 10), 
                Size = new Size(110, 35), 
                BackColor = Color.LightGray 
            };
            btnRunPositions = new Button { Text = "Chạy Tự Động", Location = new Point(0, 45), Size = new Size(110, 35), BackColor = Color.LightGray };
            btnSavePosition = new Button { Text = "Lưu vị trí", Location = new Point(240, 10), Size = new Size(110, 35), BackColor = Color.LightBlue };
            btnStop1 = new Button { Text = "DỪNG KHẨN", Location = new Point(120, 45), Size = new Size(110, 35), BackColor = Color.MistyRose, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            // Combined button: clears log + resets coordinates + sends RESET
            btnResetSystem = new Button { Text = "RESET", Location = new Point(240, 45), Size = new Size(110, 35), BackColor = Color.NavajoWhite, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            btnConnect.Click += btnConnect_Click;

            btnRunPositions.Click += btnRunPositions_Click;
            btnSavePosition.Click += btnSavePosition_Click;
            btnStop1.Click += btnStop1_Click;
            btnResetSystem.Click += btnResetSystem_Click;

            pnlControlButtons.Controls.Add(btnConnect);
            pnlControlButtons.Controls.Add(btnSavePosition);
            pnlControlButtons.Controls.Add(btnRunPositions);
            pnlControlButtons.Controls.Add(btnStop1);
            pnlControlButtons.Controls.Add(btnResetSystem);

            // Grip button
            btnGripToggle = new Button();
            btnGripToggle.Name = "btnGripToggle";
            btnGripToggle.Text = "GẮP VẬT";
            btnGripToggle.BackColor = Color.Orange;
            btnGripToggle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGripToggle.ForeColor = Color.Black;
            btnGripToggle.FlatStyle = FlatStyle.Flat;
            btnGripToggle.FlatAppearance.BorderSize = 1;
            btnGripToggle.Location = new Point(50, 530);
            btnGripToggle.Size = new Size(100, 42);
            btnGripToggle.Enabled = false;
            this.Controls.Add(btnGripToggle);
            btnGripToggle.BringToFront();
            btnGripToggle.Click += btnGripToggle_Click;

            Label lblX = new Label { Text = "Target X:", Location = new Point(170, 530), Size = new Size(60, 20) };
            txtTargetX = new TextBox { Location = new Point(170, 550), Size = new Size(50, 23), Text = "0" };
            Label lblY = new Label { Text = "Target Y:", Location = new Point(300, 530), Size = new Size(60, 20) };
            txtTargetY = new TextBox { Location = new Point(300, 550), Size = new Size(50, 23), Text = "0" };
            Label lblZ = new Label { Text = "Target Z:", Location = new Point(430, 530), Size = new Size(60, 20) };
            txtTargetZ = new TextBox { Location = new Point(430, 550), Size = new Size(50, 23), Text = "0" };
            this.Controls.AddRange(new Control[] { lblX, txtTargetX, lblY, txtTargetY, lblZ, txtTargetZ });

            Button btnSendXYZ = new Button
            {
                Text = "CHẠY TỌA ĐỘ",
                Location = new Point(500, 540),
                Size = new Size(110, 35),
                BackColor = Color.MediumPurple,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White
            };
            btnSendXYZ.Click += BtnSendXYZ_Click; // Gắn sự kiện click
            this.Controls.Add(btnSendXYZ);

            // Vision area (camera)
            
            pnlVisionArea = new Panel { Size = new Size(430, 500), Location = new Point(500, 15), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(pnlVisionArea);

            Label lblVisionTitle = new Label { Text = "CAMERA REAL-TIME THREAD FEED", Location = new Point(15, 10), Size = new Size(400, 20), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.Navy };
            pnlVisionArea.Controls.Add(lblVisionTitle);

            picCamera = new PictureBox { Size = new Size(400, 350), Location = new Point(15, 35), BackColor = Color.Black, BorderStyle = BorderStyle.Fixed3D, SizeMode = PictureBoxSizeMode.StretchImage };
            pnlVisionArea.Controls.Add(picCamera);

            lblDetectShape = new Label { Text = "HÌNH DẠNG:", Location = new Point(15, 400), Size = new Size(90, 20), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            txtDetectShape = new TextBox { Location = new Point(110, 397), Size = new Size(280, 35), ReadOnly = true, BackColor = Color.GhostWhite, Font = new Font("Segoe UI", 10F, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            pnlVisionArea.Controls.Add(lblDetectShape); pnlVisionArea.Controls.Add(txtDetectShape);

             btnStart = new Button
            {
                Text = "START (Chạy AI Thật)",
                Location = new Point(110, 450),
                Size = new Size(135, 35),
                BackColor = Color.LightGreen,
                Font = new Font("Arial", 8, FontStyle.Bold)
            };

            btnStop2 = new Button
            {
                Text = "STOP HỆ THỐNG",
                Location = new Point(255, 450), // Đặt sát cạnh nút Start
                Size = new Size(135, 35),
                BackColor = Color.LightCoral,
                Font = new Font("Arial", 8, FontStyle.Bold)
            };
            btnStart.Click += BtnStart_Click;
            btnStop2.Click += BtnStop2_Click;
            pnlVisionArea.Controls.Add(btnStart);
            pnlVisionArea.Controls.Add(btnStop2);

            Panel pnlInfo = new Panel
            {
                Location = new Point(980, 15),
                Size = new Size(400, 270),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(pnlInfo);
            pnlInfo.BringToFront();

            PictureBox picLogo = new PictureBox
            {
                Location = new Point(10, 10),
                Size = new Size(90, 120),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };
            string logoPath = @"D:\du lieu o C\HUST\WindowsFormschoRobotcuaG8 - CopyforPython - Copy\bin\Debug\logo.png";
            if (File.Exists(logoPath))
            {
                var stream = new System.IO.MemoryStream(File.ReadAllBytes(logoPath));
                picLogo.Image = Image.FromStream(stream);
            }
            pnlInfo.Controls.Add(picLogo);
            Label lblTruong = new Label
            {
                Text = "ĐẠI HỌC BÁCH KHOA HÀ NỘI",
                Location = new Point(110, 10),
                Size = new Size(270, 40),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.DarkRed,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlInfo.Controls.Add(lblTruong);

            Label lblNhom = new Label
            {
                Text = "NHÓM 8",
                Location = new Point(10, 125),
                Size = new Size(365, 28),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.DarkRed,
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlInfo.Controls.Add(lblNhom);

            // Thành viên
            Label lblThanhVien = new Label
            {
                Text = "Thành viên:",
                Location = new Point(10, 170),
                Size = new Size(365, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.DarkSlateGray
            };
            pnlInfo.Controls.Add(lblThanhVien);

            Label lblSV1 = new Label
            {
                Text = "Bùi Văn Hải  —  MSSV: 202417384",
                Location = new Point(20, 195),
                Size = new Size(355, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.Black
            };
            pnlInfo.Controls.Add(lblSV1);
            Label lblSV2 = new Label
            {
                Text = "Phạm Quốc Việt  —  MSSV: 202417611",
                Location = new Point(20, 215),
                Size = new Size(355, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.Black
            };
            pnlInfo.Controls.Add(lblSV2);
            Label lblSV3 = new Label
            {
                Text = "Đào Đức Bảo  —  MSSV: 20216046",
                Location = new Point(20, 235),
                Size = new Size(355, 22),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.Black
            };
            pnlInfo.Controls.Add(lblSV3);


            // Khởi tạo log và đặt vị trí/kích thước hợp lý bên cạnh đèn trạng thái.
            lstLog = new ListBox
            {
                Location = new Point(980, 300),
                Size = new Size(400, 215),
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen
            };
            this.Controls.Add(lstLog);
            txtStatus = new TextBox { Location = new Point(15, 580), Size = new Size(1310, 25), ReadOnly = true, BackColor = Color.White, Font = new Font("Segoe UI", 9.5F, FontStyle.Italic) };
            this.Controls.Add(txtStatus);

            txtStatistics = new TextBox
            {
                Location = new Point(15, 610),
                Size = new Size(1370, 200),
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
            tmtClock = new Timer();
            tmtClock.Interval = 1000;
            tmtClock.Start();
        }

        /// //////////////
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
        private void SendRobotAngles()
        {

            // Nếu chưa trôi qua đủ 100ms (0.1 giây) kể từ lần gửi cuối, thì bỏ qua không gửi tiếp
            if ((DateTime.Now - _lastSendTime).TotalMilliseconds < 200)
            {
                return;
            }
            _lastSendTime = DateTime.Now;
            string cmd = $"MOVE,{tkbJoint1.Value},{tkbJoint2.Value},{tkbJoint3.Value},{tkbJoint4.Value},{tkbJoint7.Value},{tkbJoint6.Value}\n";

            if (_hardwareManager != null && _hardwareManager.IsOpen())
            {
                _hardwareManager.SendData(cmd);
            }
        }
        private void SendIndependentSpeeds()
        {
            if (_hardwareManager == null || !_hardwareManager.IsOpen() || currentState == RobotState.Fault) return;
            int v1 = (int)numSpd1.Value;
            int v2 = (int)numSpd2.Value;
            int v3 = (int)numSpd3.Value;
            int v4 = (int)numSpd4.Value;
            int v5 = (int)numSpd5.Value;
            int v6 = (int)numSpd6.Value;
            int v7 = (int)numSpd6.Value;
            string speedPacket = $"SET_SPEED,{v1},{v2},{v3},{v4},{v5},{v7}\n";
            _hardwareManager.SendData(speedPacket);
        }
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (cboComPorts.SelectedItem == null || string.IsNullOrEmpty(cboComPorts.SelectedItem.ToString()))
            {
                var choice = MessageBox.Show(
                    "Không tìm thấy cổng COM Arduino!",
                    "CẢNH BÁO", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            }
            string port = cboComPorts.SelectedItem.ToString();
            bool connected = _hardwareManager.Connect(port, 115200);

            if (connected)
            {
                UpdateSystemState(RobotState.Connected);
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG]: Kết nối thành công tại {port}.");
                SendIndependentSpeeds();
            }
            else
            {
                var choice = MessageBox.Show(
                    $"Không mở được {port}!",
                    "LỖI COM", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
            }
        }
        private void btnSavePosition_Click(object sender, EventArgs e)
        {
            RobotPosition pos = new RobotPosition()
            {
                J1 = tkbJoint1.Value,
                J2 = tkbJoint2.Value,
                J3 = tkbJoint3.Value,
                J4 = tkbJoint4.Value,
                J5 = tkbJoint5.Value,
                J6 = tkbJoint6.Value,
                J7 = tkbJoint7.Value

            };

            savedPositions.Add(pos);

            UpdateLogSafe(
                $"[{DateTime.Now:HH:mm:ss}] [LƯU VỊ TRÍ] P{savedPositions.Count} = " +
                $"{pos.J1},{pos.J2},{pos.J3},{pos.J4},{pos.J5}, {pos.J6}");
        }
        private void btnRunPositions_Click(object sender, EventArgs e)
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
        private void btnStop1_Click(object sender, EventArgs e)
        {
            tmtAutoStep.Stop();
            isAutoRunning = false;
            isManualXYZCommandPending = false;

            _visionProcessor.StopVisionStream();
            _hardwareManager.Disconnect();

            UpdateSystemState(RobotState.Fault);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [CẢNH BÁO]: KÍCH HOẠT DỪNG KHẨN CẤP HỆ THỐNG!");

            HienThiBangThongKeTrong("BÁO CÁO THỐNG KÊ CHI TIẾT SẢN PHẨM PHÂN LOẠI (HỆ THỐNG ĐÃ DỪNG KHẨN)");
        }
        private void btnResetSystem_Click(object sender, EventArgs e)
        {
            lstLog.Items.Clear();
            ResetToDefault();
        }
        private void btnGripToggle_Click(object sender, EventArgs e)
        {
            if (currentState != RobotState.Connected && currentState != RobotState.Running)
            {
                MessageBox.Show("Vui lòng kết nối cổng COM", "Yêu cầu kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                //string ikPacket = $"X{targetX}Y{targetY}Z{targetZ}A\n";
                string ikPacket = $"PICK,{targetX},{targetY},{targetZ},?\n";
                isManualXYZCommandPending = true;

                _hardwareManager.SendData(ikPacket);
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Thủ Công]: Đã gửi lệnh IK xuống Arduino -> {ikPacket.Trim()}");

                // Khóa nút tạm thời để chờ Arduino chạy xong (Chờ mạch báo DONE lên)
                txtStatus.Text = "Hệ thống: Đang chạy thuật toán Động học ngược (IK) tới tọa độ chỉ định...";

            }
            else
            {
                MessageBox.Show("Dữ liệu nhập vào không hợp lệ. Vui lòng chỉ nhập số thực!", "Lỗi nhập liệu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void VisionProcessor_OnProductDetected(VisionResult result)
        {
            if (txtDetectShape.InvokeRequired)
            {
                txtDetectShape.BeginInvoke((MethodInvoker)delegate {
                    VisionProcessor_OnProductDetected(result);
                });
                return;
            }

            // Lọc trùng theo tọa độ (±2.5cm), không lọc theo tên
            // → 2 hình vuông ở 2 vị trí khác nhau sẽ đều được gắp
            const float DUP_RADIUS = 2.5f;
            foreach (var item in _shapeQueue)
            {
                float dx = item.X - result.X;
                float dy = item.Y - result.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < DUP_RADIUS)
                {
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [QUEUE] Bỏ qua trùng vị trí: " +
                                  $"{result.Shape} tại X={result.X} Y={result.Y}");
                    return;
                }
            }

            _shapeQueue.Enqueue(result);
            txtDetectShape.Text = result.Shape;
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [QUEUE] Thêm: {result.Shape} " +
                          $"X={result.X} Y={result.Y} | Hàng đợi: {_shapeQueue.Count} vật");

            if (!_isRobotBusy)
                GapVatTiepTheo();
        }
        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (currentState != RobotState.Connected)
            {
                MessageBox.Show("Vui lòng kết nối cổng COM trước khi Start.",
                                "Chưa kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Khởi động MJPEG stream để hiển thị camera trên picCamera
            _esp32Streamer.StartStream("http://127.0.0.1:5000/video_feed");

            // Khởi động luồng poll /detect thật (thay thế giả lập)
            _visionProcessor.StartVisionStream();

            // Chạy Python script ngầm
            Task.Run(async () => {
                await _pythonBridge.RunPythonScriptAsync("detect_shapes.py", "");
            });

            UpdateSystemState(RobotState.Running);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG] Camera + AI thật đã khởi động.");
        }
        private void BtnStop2_Click(object sender, EventArgs e)
        {
            isAutoRunning = false;
            tmtAutoStep.Stop();
            _esp32Streamer.StopStream();
            _visionProcessor.StopVisionStream(); // Dừng poll /detect
            _shapeQueue.Clear();
            _isRobotBusy = false;
            _currentPickingShape = "";
            UpdateSystemState(RobotState.Connected); // Về Connected, không về Idle
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG] Đã dừng AI. Robot vẫn kết nối.");
        }
        private void GapVatTiepTheo()
        {
            if (_shapeQueue.Count == 0 || _isRobotBusy) return;

            VisionResult next = _shapeQueue.Dequeue();
            _currentPickingShape = next.Shape;
            _isRobotBusy = true;
            // Lấy ShapeCode để Arduino biết đưa về khay nào (S/R/C/P)
            TrayCoordinate tray = _classifierManager.GetTrayCoordinate(next.Shape);

            //string ikPacket = $"X{next.X}Y{next.Y}Z3A{tray.ShapeCode}\n";
            string ikPacket = $"PICK,{next.X},{next.Y},3,{tray.ShapeCode}\n";
            _hardwareManager.SendData(ikPacket);
            txtStatus.Text = $"Đang gắp: {next.Shape} tại X={next.X} Y={next.Y}...";
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [GẮP] {next.Shape} → " +
                          $"X={next.X} Y={next.Y} Z=0 ShapeCode={tray.ShapeCode}");
        }
        private void HardwareManager_OnDataReceived(string completePacket)
        {
            this.Invoke(new Action(() =>
            {
                if (currentState == RobotState.Fault) return;
                string[] tokens = completePacket.Split(',');
                if (tokens.Length < 6) return;

                string statusCode = tokens[0].Trim();
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [RX]: {completePacket} | Status: {statusCode}");

                if (statusCode == "1")
                {
                    txtStatus.Text = "Robot đang chuyển động (BUSY)...";
                }
                else if (statusCode == "0")
                {
                    // ⭐ Chỉ đếm khi robot vừa hoàn thành 1 chu trình gắp từ camera
                    if (_isRobotBusy && !string.IsNullOrEmpty(_currentPickingShape))
                    {
                        _classifierManager.TangSoLuong(_currentPickingShape);
                        UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HOÀN THÀNH]: Đã gắp xong {_currentPickingShape}.");
                        _currentPickingShape = "";
                        HienThiBangThongKeTrong(""); // refresh bảng số lượng
                    }

                    _isRobotBusy = false;
                    txtStatus.Text = "Robot đã hoàn thành lệnh.";

                    // Đồng bộ trackbar trục 2
                    if (!isManualXYZCommandPending && float.TryParse(tokens[2], out float m2))
                    {
                        tkbJoint2.Value = (int)Math.Min(Math.Max(m2, 0), 180);
                        UpdateAngleLabels();
                    }

                    if (isManualXYZCommandPending)
                    {
                        isManualXYZCommandPending = false;
                    }
                    else if (isAutoMovePending)
                    {
                        isAutoMovePending = false;
                        if (isAutoRunning) { tmtAutoStep.Stop(); tmtAutoStep.Start(); }
                    }

                    //Tự động gắp vật tiếp theo trong hàng đợi nếu còn
                    if (_shapeQueue.Count > 0)
                        GapVatTiepTheo();
                }
                else if (statusCode == "2")
                {
                    UpdateSystemState(RobotState.Fault);
                }
            }));
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
                    btnSavePosition.Enabled = false;
                    btnRunPositions.Enabled = false;
                    btnStop1.Enabled = false;
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
                    btnSavePosition.Enabled = true;
                    btnRunPositions.Enabled = true;
                    btnStop1.Enabled = true;
                    txtStatus.Text = "Hệ thống: Đã kết nối cổng COM thành công.";
                    SetTrackbarsEnable(true);
                    SetSpeedsControlsEnable(true);

                    if (btnGripToggle != null) btnGripToggle.Enabled = true; // allow grip when connected (including simulation)
                    break;
                case RobotState.Running:
                    btnConnect.Enabled = false;
                    cboComPorts.Enabled = false;
                    btnSavePosition.Enabled = false;
                    btnRunPositions.Enabled = true;
                    btnStop1.Enabled = true;
                    txtStatus.Text = "Hệ thống: Luồng Camera chạy ngầm đang quét và phân loại sản phẩm...";
                    txtDetectShape.Text = "Đang quét...";

                    if (btnGripToggle != null) btnGripToggle.Enabled = true; // allow grip while running
                    break;
                case RobotState.Fault:
                    btnConnect.Text = "Kết Nối COM";
                    btnConnect.BackColor = Color.LightGray;
                    btnConnect.Enabled = false;
                    cboComPorts.Enabled = false;
                    btnSavePosition.Enabled = false;
                    btnRunPositions.Enabled = false;
                    btnStop1.Enabled = false;
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
            tkbJoint4.Enabled = tkbJoint5.Enabled = tkbJoint6.Enabled =  enable;
        }
        private void SetSpeedsControlsEnable(bool enable)
        {

            numSpd1.Enabled = numSpd2.Enabled = numSpd3.Enabled =
            numSpd4.Enabled = numSpd5.Enabled = numSpd6.Enabled =  enable;
        }

        private void ResetToDefault()
        {
            // ⭐ THÊM: xóa hàng đợi và reset trạng thái robot
            _shapeQueue.Clear();
            _isRobotBusy = false;
            _currentPickingShape = "";

            savedPositions?.Clear();
            tmtAutoStep?.Stop();
            isAutoRunning = false;
            isManualXYZCommandPending = false;
            isAutoMovePending = false;
            currentAutoIndex = 0;

            _visionProcessor?.StopVisionStream();
            _esp32Streamer?.StopStream();
            _hardwareManager?.Disconnect();
            _classifierManager?.Report?.ResetReport();

            if (tkbJoint1 != null)
            {
                tkbJoint1.Value = 0;
                tkbJoint2.Value = 102;
                tkbJoint3.Value = 142;
                tkbJoint4.Value = 114;
                tkbJoint5.Value = 98;
                tkbJoint6.Value = 120;
             
                UpdateAngleLabels();
            }

            if (numSpd1 != null)
                numSpd1.Value = numSpd2.Value = numSpd3.Value =
                numSpd4.Value = numSpd5.Value = numSpd6.Value = 50;

            QuetCongComTuDong();
            UpdateSystemState(RobotState.Idle);
            HienThiBangThongKeTrong("HỆ THỐNG ĐÃ RESET - SẴN SÀNG QUÉT CHU TRÌNH MỚI");
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Hệ thống]: Khởi tạo lại thành công.");
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

            sb.AppendLine($"   {(string.IsNullOrEmpty(tieuDe) ? "HỆ THỐNG GIÁM SÁT SẢN LƯỢNG SẢN PHẨM THỜI GIAN THỰC - ROBOT G8" : tieuDe)}");
            sb.AppendLine("====================================================================================================");
            sb.AppendLine("   DANH MỤC HÌNH KHỐI                              |   SỐ LƯỢNG");
            sb.AppendLine("----------------------------------------------------------------------------------------------------");
            sb.AppendLine($"   - Hình Khối Hộp Vuông                          |   {_classifierManager.Report.SoLuongSquare}");
            sb.AppendLine($"   - Hình Khối Hộp Chữ Nhật                       |   {_classifierManager.Report.SoLuongRectangle}");
            sb.AppendLine($"   - Hình Khối Cầu                                |   {_classifierManager.Report.SoLuongCircle}");
            sb.AppendLine($"   - Hình Khối Chóp                               |   {_classifierManager.Report.SoLuongPyramid}");
            sb.AppendLine($"   >>> TỔNG SÓ LƯỢNG SẢN PHẨM ĐÃ XỬ LÝ TRONG CHU KỲ: {_classifierManager.Report.TongSoSanPham} vật thể.");
            sb.AppendLine("====================================================================================================");

            txtStatistics.Text = sb.ToString();
        }
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
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [AUTO] Hoàn thành chương trình.");
                UpdateSystemState(RobotState.Connected);
                return;
            }

            RobotPosition pos = savedPositions[currentAutoIndex];
            tkbJoint1.Value = pos.J1;
            tkbJoint2.Value = pos.J2;
            tkbJoint3.Value = pos.J3;
            tkbJoint4.Value = pos.J4;
            tkbJoint5.Value = pos.J5;
            tkbJoint6.Value = pos.J6;
            tkbJoint7.Value = pos.J7;
            UpdateAngleLabels();
            isAutoMovePending = true;

            string packet = $"MOVE,{pos.J1},{pos.J2},{pos.J3},{pos.J4},{pos.J5},{pos.J6},{pos.J7}\n";
            _hardwareManager.SendData(packet);

            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [AUTO] Gửi P{currentAutoIndex + 1}/{savedPositions.Count}");

            currentAutoIndex++;
        }

    }
}
