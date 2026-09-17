using System;
using System.Collections.Generic;
using System.Drawing;
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
        private Button btnConnect, btnStart, btnStop, btnResetSystem, btnGripToggle;
        private Button btnSavePosition, btnRunPositions;
        private ListBox lstLog;
        private ComboBox cboComPorts;
        private TextBox txtStatistics;
        private Timer tmtSimulate; // FIX (lỗi 2): sẽ được khởi tạo trong constructor
        private Label lblClock;

        private HardwareManager _hardwareManager;
        private ESP32Streamer _esp32Streamer;
        private VisionProcessor _visionProcessor;
        private ClassifierManager _classifierManager;
        private PythonBridge _pythonBridge;

        private float robotX = 0, robotY = 0, robotZ = 0;
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
        // FIX (lỗi 7): cờ riêng cho lệnh MOVE auto, tránh GRIP/RELEASE nhảy vào auto-run
        private bool isAutoMovePending = false;

        public Form1()
        {
            InitializeComponent();

            _hardwareManager = new HardwareManager { IsSimulationMode = false };
            _classifierManager = new ClassifierManager();

            _pythonBridge = new PythonBridge();
            _pythonBridge.OnPythonLog += UpdateLogSafe;

            // FIX (lỗi 1): picCamera còn null ở đây, gán sau khi DungGiaoDienTuDong() chạy xong
            _visionProcessor = new VisionProcessor();
            _visionProcessor.OnLogMessage += UpdateLogSafe;
            _visionProcessor.OnProductDetected += VisionProcessor_OnProductDetected;

            _hardwareManager.OnDataReceived += HardwareManager_OnDataReceived;

            DungGiaoDienTuDong();

            // FIX (lỗi 1): gán picCamera SAU khi DungGiaoDienTuDong đã tạo xong picCamera
            _esp32Streamer = new ESP32Streamer(picCamera);
            _esp32Streamer.OnLogMessage += UpdateLogSafe;

            // FIX (lỗi 2): khởi tạo tmtSimulate ở đây tránh NullReferenceException
            tmtSimulate = new Timer { Interval = 100 };
            tmtSimulate.Tick += tmtSimulate_Tick_1;

            tmtAutoStep = new Timer { Interval = AUTO_STEP_DELAY_MS };
            tmtAutoStep.Tick += TmtAutoStep_Tick;

            tmtClock = new Timer { Interval = 1000 };
            tmtClock.Tick += TmtClock_Tick;
            tmtClock.Start();

            ResetToDefault();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ResetToDefault();
        }

        private void DungGiaoDienTuDong()
        {
            this.Size = new Size(1350, 740);
            this.Text = "HỆ THỐNG GIÁM SÁT & PHÂN LOẠI ROBOT";
            this.BackColor = Color.FromArgb(240, 244, 247);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

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

                trackBars[i].Minimum = (i == 0) ? -180 : 0;
                trackBars[i].Maximum = 180;
                trackBars[i].Location = new Point(15, 30 + i * 68);
                trackBars[i].Size = new Size(250, 30);
                trackBars[i].Scroll += (s, e) => { UpdateAngleLabels(); SendRobotAngles(); };
                pnlLeftControl.Controls.Add(trackBars[i]);

                numSpeeds[i].Minimum = 10;
                numSpeeds[i].Maximum = 100;
                numSpeeds[i].Value = 50;
                numSpeeds[i].Location = new Point(280, 30 + i * 68);
                numSpeeds[i].Size = new Size(95, 23);
                numSpeeds[i].Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                numSpeeds[i].TextAlign = HorizontalAlignment.Center;
                numSpeeds[i].ValueChanged += (s, e) => SendIndependentSpeeds();
                pnlLeftControl.Controls.Add(numSpeeds[i]);

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

            pnlVisionArea = new Panel { Size = new Size(430, 500), Location = new Point(430, 15), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(pnlVisionArea);
            Label lblVisionTitle = new Label { Text = "CAMERA REAL-TIME THREAD FEED", Location = new Point(15, 10), Size = new Size(400, 20), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.Navy };
            pnlVisionArea.Controls.Add(lblVisionTitle);
            picCamera = new PictureBox { Size = new Size(400, 350), Location = new Point(15, 35), BackColor = Color.Black, BorderStyle = BorderStyle.Fixed3D, SizeMode = PictureBoxSizeMode.StretchImage };
            pnlVisionArea.Controls.Add(picCamera);
            lblDetectShape = new Label { Text = "HÌNH DẠNG:", Location = new Point(15, 400), Size = new Size(90, 20), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            txtDetectShape = new TextBox { Location = new Point(110, 397), Size = new Size(280, 35), ReadOnly = true, BackColor = Color.GhostWhite, Font = new Font("Segoe UI", 10F, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            pnlVisionArea.Controls.Add(lblDetectShape);
            pnlVisionArea.Controls.Add(txtDetectShape);

            pnlStatusLight = new Panel { Size = new Size(40, 40), Location = new Point(875, 15), BackColor = Color.WhiteSmoke, BorderStyle = BorderStyle.FixedSingle };
            this.Controls.Add(pnlStatusLight);

            lstLog = new ListBox
            {
                Location = new Point(925, 15),
                Size = new Size(400, 500),
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen
            };
            this.Controls.Add(lstLog);

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
            btnSendXYZ.Click += BtnSendXYZ_Click;
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

            btnConnect    = new Button { Text = "Kết Nối COM",    Location = new Point(120, 10), Size = new Size(110, 35), BackColor = Color.LightGray };
            btnSavePosition = new Button { Text = "Lưu vị trí",   Location = new Point(240, 10), Size = new Size(100, 35), BackColor = Color.LightBlue };
            btnRunPositions = new Button { Text = "Chạy Tự Động", Location = new Point(0,   45), Size = new Size(110, 35), BackColor = Color.LightGray };
            // FIX (lỗi 3): chỉ khai báo btnStop 1 lần duy nhất, gắn 1 handler duy nhất
            btnStop       = new Button { Text = "DỪNG KHẨN",      Location = new Point(120, 45), Size = new Size(110, 35), BackColor = Color.MistyRose, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnResetSystem = new Button { Text = "RESET HỆ THỐNG",Location = new Point(240, 45), Size = new Size(135, 35), BackColor = Color.NavajoWhite, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            btnConnect.Click     += btnConnect_Click;
            btnSavePosition.Click += btnSavePosition_Click;
            btnRunPositions.Click += btnRunPositions_Click;
            btnStop.Click        += btnStop_Click;       // FIX (lỗi 3): 1 handler duy nhất
            btnResetSystem.Click += btnResetSystem_Click;

            pnlControlButtons.Controls.AddRange(new Control[] {
                btnConnect, btnSavePosition, btnRunPositions, btnStop, btnResetSystem
            });

            // Nút START AI — vị trí riêng ngoài panel
            btnStart = new Button
            {
                Text = "START AI",
                Location = new Point(15, 520),
                Size = new Size(120, 35),
                BackColor = Color.LightGreen,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnStart.Click += BtnStart_Click;
            this.Controls.Add(btnStart);

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

            btnGripToggle = new Button
            {
                Name = "btnGripToggle",
                Text = "GẮP VẬT",
                BackColor = Color.Orange,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(145, 520),
                Size = new Size(100, 35),
                Enabled = false
            };
            btnGripToggle.FlatAppearance.BorderSize = 1;
            btnGripToggle.Click += btnGripToggle_Click;
            this.Controls.Add(btnGripToggle);

            lblClock = new Label
            {
                Name = "lblClock",
                Location = new Point(1180, 15),
                Size = new Size(150, 55),
                Font = new Font("Consolas", 14F, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(lblClock);
        }

        // ==================================================================================

        private void VisionProcessor_OnProductDetected(VisionResult result)
        {
            if (txtDetectShape.InvokeRequired)
            {
                txtDetectShape.BeginInvoke((MethodInvoker)delegate { VisionProcessor_OnProductDetected(result); });
                return;
            }

            txtDetectShape.Text = result.Shape;
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [AI-CORE]: Nhận diện -> KHỐI {result.Shape}");

            TrayCoordinate tray = _classifierManager.GetTrayCoordinate(result.Shape);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [BỘ NÃO]: Tọa độ khay: X={tray.X}, Y={tray.Y}, Z={tray.Z}");

            // FIX (lỗi 5): đúng định dạng Arduino nhận
            string ikPacket = $"X{tray.X}Y{tray.Y}Z{tray.Z}A\n";
            _hardwareManager.SendData(ikPacket);
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
                    pnlStatusLight.BackColor = Color.Orange;
                }
                else if (statusCode == "0")
                {
                    txtStatus.Text = "Robot đã hoàn thành lệnh.";
                    pnlStatusLight.BackColor = Color.Cyan;

                    if (!isManualXYZCommandPending && float.TryParse(tokens[2], out float m2))
                    {
                        tkbJoint2.Value = (int)Math.Min(Math.Max(m2, 0), 180);
                        UpdateAngleLabels();
                    }

                    if (isManualXYZCommandPending)
                    {
                        isManualXYZCommandPending = false;
                    }
                    // FIX (lỗi 7): chỉ kích auto-run khi đúng gói MOVE auto, không phải GRIP/RELEASE
                    else if (isAutoMovePending)
                    {
                        isAutoMovePending = false;
                        if (isAutoRunning)
                        {
                            tmtAutoStep.Stop();
                            tmtAutoStep.Start();
                        }
                    }
                    // GRIP / RELEASE / SET_SPEED trả "0" → rơi vào đây, không làm gì
                }
                else if (statusCode == "2")
                {
                    UpdateSystemState(RobotState.Fault);
                }
            }));
        }

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

        private void SendIndependentSpeeds()
        {
            if (_hardwareManager == null || !_hardwareManager.IsOpen() || currentState == RobotState.Fault) return;
            string speedPacket = $"SET_SPEED,{(int)numSpd1.Value},{(int)numSpd2.Value},{(int)numSpd3.Value},{(int)numSpd4.Value},{(int)numSpd5.Value},{(int)numSpd6.Value}\n";
            _hardwareManager.SendData(speedPacket);
        }

        private void SendRobotAngles()
        {
            if ((DateTime.Now - _lastSendTime).TotalMilliseconds < 200) return;
            _lastSendTime = DateTime.Now;
            string cmd = $"MOVE,{tkbJoint1.Value},{tkbJoint2.Value},{tkbJoint3.Value},{tkbJoint4.Value},{tkbJoint5.Value},{tkbJoint6.Value}\n";
            if (_hardwareManager != null && _hardwareManager.IsOpen())
                _hardwareManager.SendData(cmd);
        }

        private void UpdateSystemState(RobotState newState)
        {
            currentState = newState;
            btnResetSystem.Enabled = true;

            switch (currentState)
            {
                case RobotState.Idle:
                    btnConnect.Text = "Kết Nối COM"; btnConnect.BackColor = Color.LightGray; btnConnect.Enabled = true;
                    cboComPorts.Enabled = true;
                    btnSavePosition.Enabled = false; btnRunPositions.Enabled = false; btnStop.Enabled = false;
                    pnlStatusLight.BackColor = Color.WhiteSmoke;
                    txtStatus.Text = "Sẵn sàng. Vui lòng kết nối cổng COM.";
                    txtDetectShape.Text = "---";
                    SetTrackbarsEnable(false); SetSpeedsControlsEnable(false);
                    if (btnGripToggle != null) btnGripToggle.Enabled = false;
                    break;

                case RobotState.Connected:
                    btnConnect.Text = "ĐÃ KẾT NỐI"; btnConnect.BackColor = Color.LightGreen; btnConnect.Enabled = false;
                    cboComPorts.Enabled = false;
                    btnSavePosition.Enabled = true; btnRunPositions.Enabled = true; btnStop.Enabled = true;
                    pnlStatusLight.BackColor = Color.LimeGreen;
                    txtStatus.Text = "Đã kết nối cổng COM thành công.";
                    SetTrackbarsEnable(true); SetSpeedsControlsEnable(true);
                    if (btnGripToggle != null) btnGripToggle.Enabled = true;
                    break;

                case RobotState.Running:
                    btnConnect.Enabled = false; cboComPorts.Enabled = false;
                    btnSavePosition.Enabled = false; btnRunPositions.Enabled = false; btnStop.Enabled = true;
                    pnlStatusLight.BackColor = Color.Cyan;
                    txtStatus.Text = "Camera đang quét và phân loại sản phẩm...";
                    txtDetectShape.Text = "Đang quét...";
                    if (btnGripToggle != null) btnGripToggle.Enabled = true;
                    break;

                case RobotState.Fault:
                    btnConnect.Text = "Kết Nối COM"; btnConnect.BackColor = Color.LightGray; btnConnect.Enabled = false;
                    cboComPorts.Enabled = false;
                    btnSavePosition.Enabled = false; btnRunPositions.Enabled = false; btnStop.Enabled = false;
                    pnlStatusLight.BackColor = Color.Crimson;
                    txtStatus.Text = "DỪNG KHẨN CẤP! Đã ngắt kết nối phần cứng.";
                    SetTrackbarsEnable(false); SetSpeedsControlsEnable(false);
                    if (btnGripToggle != null) btnGripToggle.Enabled = false;
                    break;
            }
        }

        private void SetTrackbarsEnable(bool enable)
        {
            if (tkbJoint1 == null) return;
            tkbJoint1.Enabled = tkbJoint2.Enabled = tkbJoint3.Enabled =
            tkbJoint4.Enabled = tkbJoint5.Enabled = tkbJoint6.Enabled = enable;
        }

        private void SetSpeedsControlsEnable(bool enable)
        {
            numSpd1.Enabled = numSpd2.Enabled = numSpd3.Enabled =
            numSpd4.Enabled = numSpd5.Enabled = numSpd6.Enabled = enable;
        }

        // FIX (lỗi 4): đọc cổng từ cboComPorts thay vì hard-code "COM3"
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (cboComPorts.SelectedItem == null || string.IsNullOrEmpty(cboComPorts.SelectedItem.ToString()))
            {
                var choice = MessageBox.Show(
                    "Không tìm thấy cổng COM Arduino!\nBật chế độ Mô Phỏng không?",
                    "CẢNH BÁO", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (choice == DialogResult.Yes)
                {
                    _hardwareManager.IsSimulationMode = true;
                    _hardwareManager.Connect();
                    UpdateSystemState(RobotState.Connected);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [MÔ PHỎNG]: Kích hoạt giả lập.");
                }
                return;
            }

            string port = cboComPorts.SelectedItem.ToString();
            _hardwareManager.IsSimulationMode = false;
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
                    $"Không mở được {port}!\nBật chế độ Mô Phỏng không?",
                    "LỖI COM", MessageBoxButtons.YesNo, MessageBoxIcon.Error);

                if (choice == DialogResult.Yes)
                {
                    _hardwareManager.IsSimulationMode = true;
                    _hardwareManager.Connect();
                    UpdateSystemState(RobotState.Connected);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [MÔ PHỎNG]: Kích hoạt do {port} bị chiếm.");
                }
                else
                {
                    UpdateSystemState(RobotState.Idle);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [LỖI]: Thất bại. Hãy đóng Serial Monitor!");
                }
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [HỆ THỐNG]: Kích hoạt camera và AI.");
            _esp32Streamer.StartStream("http://127.0.0.1:5000/video_feed");
            _visionProcessor.StopVisionStream();

            Task.Run(async () =>
            {
                await _pythonBridge.RunPythonScriptAsync("detect_shapes.py", "");
            });

            UpdateSystemState(RobotState.Running);
        }

        // FIX (lỗi 3): chỉ còn 1 handler duy nhất cho btnStop
        private void btnStop_Click(object sender, EventArgs e)
        {
            tmtSimulate?.Stop();
            tmtAutoStep?.Stop();
            isAutoRunning = false;
            isManualXYZCommandPending = false;
            isAutoMovePending = false;

            _esp32Streamer?.StopStream();
            _visionProcessor?.StopVisionStream();
            _hardwareManager?.Disconnect();

            UpdateSystemState(RobotState.Fault);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [CẢNH BÁO]: DỪNG KHẨN CẤP!");
            HienThiBangThongKeTrong("BÁO CÁO THỐNG KÊ (HỆ THỐNG ĐÃ DỪNG)");
        }

        private void btnRunPositions_Click(object sender, EventArgs e)
        {
            if (savedPositions.Count == 0)
            {
                MessageBox.Show("Chưa có vị trí nào được lưu.");
                return;
            }
            UpdateSystemState(RobotState.Running);
            currentAutoIndex = 0;
            isAutoRunning = true;
            RunNextSavedPosition();
        }

        private void btnSavePosition_Click(object sender, EventArgs e)
        {
            RobotPosition pos = new RobotPosition
            {
                J1 = tkbJoint1.Value, J2 = tkbJoint2.Value, J3 = tkbJoint3.Value,
                J4 = tkbJoint4.Value, J5 = tkbJoint5.Value, J6 = tkbJoint6.Value
            };
            savedPositions.Add(pos);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [LƯU] P{savedPositions.Count} = {pos.J1},{pos.J2},{pos.J3},{pos.J4},{pos.J5},{pos.J6}");
        }

        // FIX (lỗi 6): gọi ResetToDefault() thay vì chỉ reset report
        private void btnResetSystem_Click(object sender, EventArgs e)
        {
            ResetToDefault();
        }

        private void btnGripToggle_Click(object sender, EventArgs e)
        {
            if (currentState != RobotState.Connected && currentState != RobotState.Running)
            {
                MessageBox.Show("Vui lòng kết nối trước.", "Yêu cầu kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cmd = isGripped ? "RELEASE\n" : "GRIP\n";
            _hardwareManager?.SendData(cmd);
            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [GRIP]: {cmd.Trim()}");

            isGripped = !isGripped;
            btnGripToggle.Text = isGripped ? "THẢ VẬT" : "GẮP VẬT";
            btnGripToggle.BackColor = isGripped ? Color.LightGreen : Color.Orange;
        }

        private void BtnSendXYZ_Click(object sender, EventArgs e)
        {
            if (currentState != RobotState.Connected && currentState != RobotState.Running)
            {
                MessageBox.Show("Vui lòng kết nối COM trước!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (float.TryParse(txtTargetX.Text, out float targetX) &&
                float.TryParse(txtTargetY.Text, out float targetY) &&
                float.TryParse(txtTargetZ.Text, out float targetZ))
            {
                string ikPacket = $"X{targetX}Y{targetY}Z{targetZ}A\n";

                if (_hardwareManager.IsSimulationMode)
                {
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [Mô Phỏng]: Gửi IK ảo -> {ikPacket.Trim()}");
                    tmtSimulate.Start();
                }
                else
                {
                    isManualXYZCommandPending = true;
                    _hardwareManager.SendData(ikPacket);
                    UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [IK]: Gửi -> {ikPacket.Trim()}");
                    txtStatus.Text = "Đang tính IK và di chuyển...";
                    pnlStatusLight.BackColor = Color.Orange;
                }
            }
            else
            {
                MessageBox.Show("Dữ liệu không hợp lệ, chỉ nhập số!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            lstLog.TopIndex = lstLog.Items.Count - 1;
        }

        private void TmtClock_Tick(object sender, EventArgs e)
        {
            if (lblClock == null) return;
            lblClock.Text = DateTime.Now.ToString("HH:mm:ss") + Environment.NewLine + DateTime.Now.ToString("dd/MM/yyyy");
        }

        private void TmtAutoStep_Tick(object sender, EventArgs e)
        {
            tmtAutoStep.Stop();
            if (isAutoRunning) RunNextSavedPosition();
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
            tkbJoint1.Value = pos.J1; tkbJoint2.Value = pos.J2; tkbJoint3.Value = pos.J3;
            tkbJoint4.Value = pos.J4; tkbJoint5.Value = pos.J5; tkbJoint6.Value = pos.J6;
            UpdateAngleLabels();

            string packet = $"MOVE,{pos.J1},{pos.J2},{pos.J3},{pos.J4},{pos.J5},{pos.J6}\n";
            _hardwareManager.SendData(packet);

            // FIX (lỗi 7): đánh dấu đây là lệnh MOVE auto để phân biệt với GRIP/RELEASE
            isAutoMovePending = true;

            UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [AUTO] Gửi P{currentAutoIndex + 1}");
            currentAutoIndex++;
        }

        private void tmtSimulate_Tick_1(object sender, EventArgs e)
        {
            if (currentState == RobotState.Fault) return;
            if (!float.TryParse(txtTargetX.Text, out float targetX)) return;
            if (!float.TryParse(txtTargetY.Text, out float targetY)) return;
            if (!float.TryParse(txtTargetZ.Text, out float targetZ)) return;

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
                UpdateLogSafe($"[{DateTime.Now:HH:mm:ss}] [SIM]: Đã đến đích.");
                _hardwareManager.SimulateIncomingHardwareData($"0,{targetX},{targetY},{targetZ},0,0,0\n");
            }
        }

        private void ResetToDefault()
        {
            savedPositions?.Clear();
            tmtSimulate?.Stop();
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
                tkbJoint1.Value = tkbJoint2.Value = tkbJoint3.Value =
                tkbJoint4.Value = tkbJoint5.Value = tkbJoint6.Value = 90;
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
                    foreach (var port in searcher.Get())
                    {
                        string caption = port["Caption"]?.ToString();
                        if (caption != null && (caption.Contains("Arduino") || caption.Contains("USB Serial")
                            || caption.Contains("CH340") || caption.Contains("CP210")))
                        {
                            int s = caption.IndexOf("(COM"), en = caption.IndexOf(")", s);
                            if (s != -1 && en != -1)
                            {
                                string comName = caption.Substring(s + 1, en - s - 1);
                                if (!cboComPorts.Items.Contains(comName))
                                    cboComPorts.Items.Add(comName);
                            }
                        }
                    }
                }
            }
            catch
            {
                foreach (string p in System.IO.Ports.SerialPort.GetPortNames())
                    if (!cboComPorts.Items.Contains(p)) cboComPorts.Items.Add(p);
            }

            if (cboComPorts.Items.Count > 0) cboComPorts.SelectedIndex = 0;
            else cboComPorts.Text = "";
        }

        private void HienThiBangThongKeTrong(string tieuDe)
        {
            if (_classifierManager?.Report == null || txtStatistics == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("====================================================================================================");
            sb.AppendLine($"   {(string.IsNullOrEmpty(tieuDe) ? "HỆ THỐNG GIÁM SÁT SẢN LƯỢNG THỜI GIAN THỰC - ROBOT G8" : tieuDe)}");
            sb.AppendLine("====================================================================================================");
            sb.AppendLine("   DANH MỤC HÌNH KHỐI                                    |   SỐ LƯỢNG");
            sb.AppendLine("----------------------------------------------------------------------------------------------------");
            sb.AppendLine($"   - Hình Khối Hộp Vuông        |   {_classifierManager.Report.SoLuongSquare}");
            sb.AppendLine($"   - Hình Khối Hộp Chữ Nhật     |   {_classifierManager.Report.SoLuongRectangle}");
            sb.AppendLine($"   - Hình Khối Cầu               |   {_classifierManager.Report.SoLuongCircle}");
            sb.AppendLine($"   - Hình Khối Chóp              |   {_classifierManager.Report.SoLuongPyramid}");
            sb.AppendLine("----------------------------------------------------------------------------------------------------");
            sb.AppendLine($"   >>> TỔNG: {_classifierManager.Report.TongSoSanPham} vật thể.");
            sb.AppendLine("====================================================================================================");
            txtStatistics.Text = sb.ToString();
        }
    }
}