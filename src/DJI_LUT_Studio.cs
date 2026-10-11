using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

namespace DJILutStudio
{
    public class MainForm : Form
    {
        // Dark Theme Colors
        private static readonly Color BgDark = Color.FromArgb(14, 17, 23);
        private static readonly Color BgCard = Color.FromArgb(22, 27, 34);
        private static readonly Color BgInput = Color.FromArgb(18, 22, 31);
        private static readonly Color BorderColor = Color.FromArgb(48, 54, 61);
        private static readonly Color TextMain = Color.FromArgb(240, 246, 252);
        private static readonly Color TextMuted = Color.FromArgb(139, 148, 158);
        private static readonly Color AccentCyan = Color.FromArgb(0, 210, 255);
        private static readonly Color AccentGreen = Color.FromArgb(0, 242, 169);
        private static readonly Color AccentGold = Color.FromArgb(255, 170, 0);

        // Controls
        private TextBox txtInputPath;
        private Button btnBrowseFile;
        private Button btnBrowseFolder;
        private ComboBox cmbDevice;
        private ComboBox cmbLut;
        private ComboBox cmbLook;
        private ComboBox cmbCodec;
        private ComboBox cmbScale;
        private TrackBar tbCq;
        private Label lblCqVal;
        private CheckBox chkSharpen;
        private CheckBox chkPreview;
        private Button btnStartGrade;
        private Button btnOpenFolder;
        private Button btnWatcher;
        private Button btnGenPreview;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblSpeedEta;
        private RichTextBox txtLog;
        private PictureBox picPreview;
        private Label lblPreviewPlaceholder;

        // Process tracking
        private Process activeProcess = null;
        private Thread workerThread = null;
        private string wslDistro = "Ubuntu-26.04";
        private string appDir = @"/home/gal756/LUT-adder";

        // Device and LUT database
        private Dictionary<string, List<LutEntry>> deviceDatabase;

        public class LutEntry
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public bool IsRecommended { get; set; }
            public LutEntry(string id, string name, bool rec = false)
            {
                Id = id;
                DisplayName = name;
                IsRecommended = rec;
            }
            public override string ToString() { return DisplayName + (IsRecommended ? " ★ [Recommended]" : ""); }
        }

        public MainForm(string initialPath = null)
        {
            InitializeComponent();
            InitDatabase();
            if (!string.IsNullOrEmpty(initialPath))
            {
                txtInputPath.Text = initialPath.Trim('"');
                AutoSelectDeviceForPath(initialPath);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "DJI LUT Studio - Open Source Video Color Grading Suite";
            this.Size = new Size(1000, 780);
            this.MinimumSize = new Size(950, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = BgDark;
            this.ForeColor = TextMain;
            this.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;

            // Top Header Panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = BgCard,
                Padding = new Padding(20, 10, 20, 10)
            };

            Label lblTitle = new Label
            {
                Text = "🎬 DJI LUT STUDIO",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = AccentCyan,
                AutoSize = true,
                Location = new Point(16, 16)
            };

            Label lblBadge = new Label
            {
                Text = "v2.0.0 Windows",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = AccentCyan,
                BackColor = Color.FromArgb(31, 58, 75),
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true,
                Location = new Point(220, 20)
            };

            Label lblGpu = new Label
            {
                Text = "● NVIDIA RTX 3070 NVENC (10-bit Active)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = AccentGreen,
                BackColor = Color.FromArgb(20, 45, 35),
                Padding = new Padding(10, 4, 10, 4),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(680, 18)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblBadge);
            pnlHeader.Controls.Add(lblGpu);
            this.Controls.Add(pnlHeader);

            // Main Content Layout
            Panel pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(20, 15, 20, 15)
            };
            this.Controls.Add(pnlMain);

            int curY = 10;

            // === 1. Source Video Section ===
            GroupBox grpSource = CreateSectionBox("1. SOURCE VIDEO OR FOLDER (DRAG & DROP)", 940, 80, curY);
            pnlMain.Controls.Add(grpSource);

            txtInputPath = new TextBox
            {
                Location = new Point(16, 32),
                Size = new Size(680, 28),
                BackColor = BgInput,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Text = @"D:\dji",
                AllowDrop = true
            };
            txtInputPath.DragEnter += MainForm_DragEnter;
            txtInputPath.DragDrop += MainForm_DragDrop;

            btnBrowseFile = CreateButton("Browse Clip...", new Point(710, 30), new Size(105, 30), BgCard, TextMain);
            btnBrowseFile.Click += (s, e) => {
                OpenFileDialog dlg = new OpenFileDialog { Filter = "Video Files (*.mp4;*.mov;*.mkv)|*.mp4;*.mov;*.mkv|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == DialogResult.OK) {
                    txtInputPath.Text = dlg.FileName;
                    AutoSelectDeviceForPath(dlg.FileName);
                }
            };

            btnBrowseFolder = CreateButton("Browse Folder...", new Point(825, 30), new Size(105, 30), BgCard, TextMain);
            btnBrowseFolder.Click += (s, e) => {
                FolderBrowserDialog dlg = new FolderBrowserDialog();
                if (dlg.ShowDialog() == DialogResult.OK) {
                    txtInputPath.Text = dlg.SelectedPath;
                }
            };

            grpSource.Controls.Add(txtInputPath);
            grpSource.Controls.Add(btnBrowseFile);
            grpSource.Controls.Add(btnBrowseFolder);
            curY += 95;

            // === 2. Camera Model & 3D LUT Section ===
            GroupBox grpLut = CreateSectionBox("2. DJI CAMERA / DRONE & 3D LUT CONVERSION PROFILE", 940, 90, curY);
            pnlMain.Controls.Add(grpLut);

            Label lblDevTitle = new Label { Text = "DJI Camera / Drone Model:", Location = new Point(16, 26), AutoSize = true, ForeColor = TextMuted };
            cmbDevice = new ComboBox { Location = new Point(16, 48), Size = new Size(380, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = BgInput, ForeColor = TextMain };
            cmbDevice.SelectedIndexChanged += (s, e) => PopulateLutsForSelectedDevice();

            Label lblLutTitle = new Label { Text = "3D Conversion LUT (D-Log / D-Log M to Rec.709):", Location = new Point(420, 26), AutoSize = true, ForeColor = TextMuted };
            cmbLut = new ComboBox { Location = new Point(420, 48), Size = new Size(400, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = BgInput, ForeColor = TextMain };

            Button btnCustomLut = CreateButton("Custom...", new Point(830, 47), new Size(95, 29), BgCard, TextMain);
            btnCustomLut.Click += (s, e) => {
                OpenFileDialog dlg = new OpenFileDialog { Filter = "3D LUT Files (*.cube)|*.cube|All Files (*.*)|*.*" };
                if (dlg.ShowDialog() == DialogResult.OK) {
                    cmbLut.Items.Add(new LutEntry(dlg.FileName, "Custom: " + Path.GetFileName(dlg.FileName), true));
                    cmbLut.SelectedIndex = cmbLut.Items.Count - 1;
                }
            };

            grpLut.Controls.Add(lblDevTitle);
            grpLut.Controls.Add(cmbDevice);
            grpLut.Controls.Add(lblLutTitle);
            grpLut.Controls.Add(cmbLut);
            grpLut.Controls.Add(btnCustomLut);
            curY += 105;

            // === 3. Look Preset & Encoder Settings ===
            GroupBox grpSettings = CreateSectionBox("3. LOOK STYLE & HARDWARE ENCODING OPTIONS", 940, 140, curY);
            pnlMain.Controls.Add(grpSettings);

            // Row 1
            Label lblLook = new Label { Text = "Post-LUT Look Style:", Location = new Point(16, 26), AutoSize = true, ForeColor = TextMuted };
            cmbLook = new ComboBox { Location = new Point(16, 48), Size = new Size(280, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = BgInput, ForeColor = TextMain };
            cmbLook.Items.AddRange(new object[] {
                "Vibrant Pop (Recommended: Punchy Sky, Greens & Sharpness)",
                "Natural Rec.709 (100% DJI Baseline Color Reference)",
                "Cinematic Film (Filmic 35mm S-Curve, Rich Shadows)",
                "Golden Hour (Warm Amber Sunlight & Sunset Glow)",
                "Teal & Orange (Blockbuster Complementary Contrast)"
            });
            cmbLook.SelectedIndex = 0;

            Label lblCodec = new Label { Text = "Encoder Codec:", Location = new Point(320, 26), AutoSize = true, ForeColor = TextMuted };
            cmbCodec = new ComboBox { Location = new Point(320, 48), Size = new Size(280, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = BgInput, ForeColor = TextMain };
            cmbCodec.Items.AddRange(new object[] {
                "HEVC / H.265 (10-Bit RTX NVENC Hardware)",
                "H.264 (8/10-Bit RTX NVENC Hardware)",
                "Apple ProRes 422 HQ (Broadcast Mastering)"
            });
            cmbCodec.SelectedIndex = 0;

            Label lblScale = new Label { Text = "Output Resolution:", Location = new Point(620, 26), AutoSize = true, ForeColor = TextMuted };
            cmbScale = new ComboBox { Location = new Point(620, 48), Size = new Size(300, 28), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = BgInput, ForeColor = TextMain };
            cmbScale.Items.AddRange(new object[] {
                "Keep Source Resolution (Up to 4K 60fps)",
                "Scale to 4K UHD (3840x2160)",
                "Scale to 2.7K (2704x1520)",
                "Scale to 1080p Proxy (Quick Discord / WhatsApp share)"
            });
            cmbScale.SelectedIndex = 0;

            // Row 2
            Label lblCq = new Label { Text = "Quality (CQ Factor):", Location = new Point(16, 88), AutoSize = true, ForeColor = TextMuted };
            tbCq = new TrackBar { Location = new Point(16, 105), Size = new Size(220, 28), Minimum = 15, Maximum = 28, Value = 19, TickStyle = TickStyle.None };
            lblCqVal = new Label { Text = "19 (High Quality)", Location = new Point(245, 108), AutoSize = true, ForeColor = AccentCyan, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            tbCq.ValueChanged += (s, e) => { lblCqVal.Text = tbCq.Value + (tbCq.Value <= 19 ? " (High Quality)" : " (Standard)"); };

            chkSharpen = new CheckBox { Text = "Apply Subtle Foliage Sharpening", Location = new Point(370, 105), AutoSize = true, Checked = true, ForeColor = TextMain };
            chkPreview = new CheckBox { Text = "Generate Comparison JPEG", Location = new Point(620, 105), AutoSize = true, Checked = true, ForeColor = TextMain };

            grpSettings.Controls.Add(lblLook);
            grpSettings.Controls.Add(cmbLook);
            grpSettings.Controls.Add(lblCodec);
            grpSettings.Controls.Add(cmbCodec);
            grpSettings.Controls.Add(lblScale);
            grpSettings.Controls.Add(cmbScale);
            grpSettings.Controls.Add(lblCq);
            grpSettings.Controls.Add(tbCq);
            grpSettings.Controls.Add(lblCqVal);
            grpSettings.Controls.Add(chkSharpen);
            grpSettings.Controls.Add(chkPreview);
            curY += 155;

            // === 4. Side-by-Side Preview Box ===
            GroupBox grpPreview = CreateSectionBox("4. BEFORE / AFTER COLOR GRADE COMPARISON", 940, 210, curY);
            pnlMain.Controls.Add(grpPreview);

            btnGenPreview = CreateButton("Generate Snapshot Preview", new Point(16, 26), new Size(200, 28), BgCard, AccentCyan);
            btnGenPreview.Click += (s, e) => RunPreviewGeneration();

            Panel pnlImgContainer = new Panel
            {
                Location = new Point(16, 60),
                Size = new Size(905, 140),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };

            picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                Visible = false
            };

            lblPreviewPlaceholder = new Label
            {
                Text = "Click 'Generate Snapshot Preview' or grade a video to display the split comparison (Original D-Log vs Graded Rec.709).",
                ForeColor = TextMuted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            pnlImgContainer.Controls.Add(picPreview);
            pnlImgContainer.Controls.Add(lblPreviewPlaceholder);
            grpPreview.Controls.Add(btnGenPreview);
            grpPreview.Controls.Add(pnlImgContainer);
            curY += 225;

            // === 5. Live Logs ===
            GroupBox grpLogs = CreateSectionBox("5. LIVE PIPELINE LOGS", 940, 120, curY);
            pnlMain.Controls.Add(grpLogs);

            txtLog = new RichTextBox
            {
                Location = new Point(16, 25),
                Size = new Size(905, 85),
                BackColor = Color.FromArgb(6, 8, 11),
                ForeColor = AccentGreen,
                Font = new Font("Consolas", 9f),
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };
            txtLog.AppendText("DJI LUT Studio v2.0.0 Native Windows Engine Ready.\nSelect a file or folder above and click 'Apply LUT & Start Grading'.\n");
            grpLogs.Controls.Add(txtLog);
            curY += 135;

            // === Bottom Action Bar ===
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 85,
                BackColor = BgCard,
                Padding = new Padding(20, 10, 20, 10)
            };

            progressBar = new ProgressBar
            {
                Location = new Point(20, 14),
                Size = new Size(540, 16),
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            lblStatus = new Label
            {
                Text = "Ready to grade.",
                Location = new Point(20, 36),
                AutoSize = true,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 9f)
            };

            lblSpeedEta = new Label
            {
                Text = "",
                Location = new Point(20, 56),
                AutoSize = true,
                ForeColor = AccentCyan,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };

            btnWatcher = CreateButton("⏱ Watcher Mode", new Point(575, 20), new Size(125, 42), BgCard, TextMain);
            btnWatcher.Click += (s, e) => ToggleWatcher();

            btnOpenFolder = CreateButton("📁 Open Folder", new Point(710, 20), new Size(115, 42), BgCard, TextMain);
            btnOpenFolder.Click += (s, e) => OpenGradedFolder();

            btnStartGrade = new Button
            {
                Text = "🚀 APPLY LUT & GRADE",
                Location = new Point(835, 20),
                Size = new Size(135, 42),
                BackColor = AccentCyan,
                ForeColor = Color.FromArgb(12, 15, 20),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnStartGrade.FlatAppearance.BorderSize = 0;
            btnStartGrade.Click += (s, e) => StartGradingProcess();

            pnlBottom.Controls.Add(progressBar);
            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(lblSpeedEta);
            pnlBottom.Controls.Add(btnWatcher);
            pnlBottom.Controls.Add(btnOpenFolder);
            pnlBottom.Controls.Add(btnStartGrade);
            this.Controls.Add(pnlBottom);
        }

        private GroupBox CreateSectionBox(string title, int width, int height, int y)
        {
            return new GroupBox
            {
                Text = title,
                Location = new Point(20, y),
                Size = new Size(width, height),
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                BackColor = BgCard
            };
        }

        private Button CreateButton(string text, Point loc, Size sz, Color bg, Color fg)
        {
            Button btn = new Button
            {
                Text = text,
                Location = loc,
                Size = sz,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = BorderColor;
            return btn;
        }

        private void InitDatabase()
        {
            deviceDatabase = new Dictionary<string, List<LutEntry>>();

            deviceDatabase["DJI O4 Pro / Air Unit"] = new List<LutEntry> {
                new LutEntry("o4_dlogm_rec709", "Official O4 D-Log M to Rec.709 V1", true)
            };
            deviceDatabase["DJI Avata 2"] = new List<LutEntry> {
                new LutEntry("avata2_dlogm_rec709", "Official Avata 2 D-Log M to Rec.709 V1", true)
            };
            deviceDatabase["DJI Avata 360"] = new List<LutEntry> {
                new LutEntry("avata360_dlogm_rec709", "Official Avata 360 D-Log M to Rec.709", true)
            };
            deviceDatabase["DJI Mavic 3 / Pro / Classic"] = new List<LutEntry> {
                new LutEntry("m3_dlog_rec709_std", "Official Mavic 3 D-Log to Rec.709 Standard", true),
                new LutEntry("m3_dlog_rec709_vivid", "Official Mavic 3 D-Log to Rec.709 (Vivid)"),
                new LutEntry("m3_dlogm_rec709", "Official Mavic 3 D-Log M to Rec.709")
            };
            deviceDatabase["DJI Mavic 4 Pro"] = new List<LutEntry> {
                new LutEntry("m4p_dlog_std", "Official Mavic 4 Pro D-Log to Rec.709 Standard", true),
                new LutEntry("m4p_dlog_vivid", "Official Mavic 4 Pro D-Log to Rec.709 (Vivid)"),
                new LutEntry("m4p_dlogm", "Official Mavic 4 Pro D-Log M to Rec.709")
            };
            deviceDatabase["DJI Air 3 / Air 3S"] = new List<LutEntry> {
                new LutEntry("air3_dlogm", "Official Air 3 D-Log M to Rec.709 V1", true),
                new LutEntry("air3s_dlogm", "Official Air 3S D-Log M to Rec.709 V1")
            };
            deviceDatabase["DJI Mini 4 Pro / Mini 5 Pro"] = new List<LutEntry> {
                new LutEntry("mini4_dlogm", "Official Mini 4 Pro D-Log M to Rec.709 V1", true),
                new LutEntry("mini5_dlogm", "Official Mini 5 Pro D-Log M to Rec.709")
            };
            deviceDatabase["DJI Osmo Pocket 3"] = new List<LutEntry> {
                new LutEntry("op3_dlogm", "Official Pocket 3 D-Log M to Rec.709 V1", true),
                new LutEntry("op3_spring", "Master Look: Spring Pro (Fresh Greens & Soft Skin)"),
                new LutEntry("op3_summer", "Master Look: Summer Pro (Deep Cyan & Sun-Kissed)"),
                new LutEntry("op3_autumn", "Master Look: Autumn Pro (Warm Terracotta & Amber)"),
                new LutEntry("op3_winter", "Master Look: Winter Pro (Cool High-Contrast Clean)")
            };
            deviceDatabase["DJI Osmo Pocket 4 / 4 Pro"] = new List<LutEntry> {
                new LutEntry("op4_dlog_std", "Official Pocket 4 D-Log to Rec.709 Standard", true),
                new LutEntry("op4_dlog_vivid", "Official Pocket 4 D-Log to Rec.709 (Vivid)"),
                new LutEntry("op4p_dlog2_std", "Official Pocket 4P D-Log2 to Rec.709"),
                new LutEntry("op4p_dlog2_vivid", "Official Pocket 4P D-Log2 to Rec.709 (Vivid)")
            };
            deviceDatabase["DJI Osmo Action 4"] = new List<LutEntry> {
                new LutEntry("oa4_dlogm", "Official Action 4 D-Log M to Rec.709 V1", true),
                new LutEntry("oa4_ice", "Action Look: Ice Pro (Snow & Glacial Sports)"),
                new LutEntry("oa4_nature", "Action Look: Nature Pro (Warm Sunlight & Grass)"),
                new LutEntry("oa4_forest", "Action Look: Forest Pro (Deep Moss & Mountain Biking)")
            };
            deviceDatabase["DJI Osmo Action 5 Pro"] = new List<LutEntry> {
                new LutEntry("oa5p_dlogm", "Official Action 5 Pro D-Log M to Rec.709 V1", true),
                new LutEntry("oa5p_mei", "Master Look: Mei / Plum (Romantic Magenta Shadows)"),
                new LutEntry("oa5p_ju", "Master Look: Ju / Chrysanthemum (Golden Radiance)"),
                new LutEntry("oa5p_zhu", "Master Look: Zhu / Bamboo (Vibrant Outdoor Contrast)"),
                new LutEntry("oa5p_lan", "Master Look: Lan / Orchid (Cool Oceanic Teal)")
            };
            deviceDatabase["DJI Osmo Action 6"] = new List<LutEntry> {
                new LutEntry("oa6_dlogm", "Official Action 6 D-Log M to Rec.709", true)
            };
            deviceDatabase["DJI Osmo 360 / Nano / Lito"] = new List<LutEntry> {
                new LutEntry("osmo_360_dlogm", "Official Osmo 360 Panoramic D-Log M", true),
                new LutEntry("osmo_nano_dlogm", "Official Osmo Nano D-Log M"),
                new LutEntry("lito_x1_dlogm", "Official Lito X1 D-Log M")
            };
            deviceDatabase["DJI Zenmuse X7 / Ronin 4D / Inspire 3"] = new List<LutEntry> {
                new LutEntry("x7_dlog_rec709", "Official Cinema D-Log to Rec.709", true),
                new LutEntry("x7_linear_dlog", "Official Linear RAW to D-Log")
            };
            deviceDatabase["DJI Phantom 4 / Phantom 4 Pro"] = new List<LutEntry> {
                new LutEntry("p4_dlog_rec709", "Official Phantom 4 D-Log to Rec.709", true),
                new LutEntry("p4_dlog_srgb", "Official Phantom 4 D-Log to sRGB Improv")
            };
            deviceDatabase["DJI Generic / Universal D-Log M"] = new List<LutEntry> {
                new LutEntry("generic_dlogm_rec709", "Universal DJI D-Log M to Rec.709", true)
            };

            foreach (var key in deviceDatabase.Keys)
            {
                cmbDevice.Items.Add(key);
            }
            cmbDevice.SelectedIndex = 0;
        }

        private void PopulateLutsForSelectedDevice()
        {
            cmbLut.Items.Clear();
            string sel = cmbDevice.SelectedItem as string;
            if (sel != null && deviceDatabase.ContainsKey(sel))
            {
                foreach (var lut in deviceDatabase[sel])
                {
                    cmbLut.Items.Add(lut);
                }
            }
            if (cmbLut.Items.Count > 0)
            {
                cmbLut.SelectedIndex = 0;
            }
        }

        private void AutoSelectDeviceForPath(string path)
        {
            string upper = Path.GetFileName(path).ToUpperInvariant();
            if (upper.Contains("AVATA")) cmbDevice.SelectedItem = "DJI Avata 2";
            else if (upper.Contains("MAVIC3") || upper.Contains("M3")) cmbDevice.SelectedItem = "DJI Mavic 3 / Pro / Classic";
            else if (upper.Contains("MAVIC4") || upper.Contains("M4")) cmbDevice.SelectedItem = "DJI Mavic 4 Pro";
            else if (upper.Contains("POCKET3") || upper.Contains("OP3")) cmbDevice.SelectedItem = "DJI Osmo Pocket 3";
            else if (upper.Contains("POCKET4") || upper.Contains("OP4")) cmbDevice.SelectedItem = "DJI Osmo Pocket 4 / 4 Pro";
            else if (upper.Contains("ACTION4") || upper.Contains("OA4")) cmbDevice.SelectedItem = "DJI Osmo Action 4";
            else if (upper.Contains("ACTION5") || upper.Contains("OA5")) cmbDevice.SelectedItem = "DJI Osmo Action 5 Pro";
            else if (upper.Contains("AIR3")) cmbDevice.SelectedItem = "DJI Air 3 / Air 3S";
            else if (upper.Contains("MINI4") || upper.Contains("MINI5")) cmbDevice.SelectedItem = "DJI Mini 4 Pro / Mini 5 Pro";
            else cmbDevice.SelectedItem = "DJI O4 Pro / Air Unit";
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                txtInputPath.Text = files[0];
                AutoSelectDeviceForPath(files[0]);
            }
        }

        private void StartGradingProcess()
        {
            if (activeProcess != null && !activeProcess.HasExited)
            {
                MessageBox.Show("A grading job is currently running.", "DJI LUT Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string inputPath = txtInputPath.Text.Trim('"');
            if (string.IsNullOrEmpty(inputPath) || (!File.Exists(inputPath) && !Directory.Exists(inputPath)))
            {
                MessageBox.Show("Please enter a valid file or folder path.", "DJI LUT Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LutEntry lutEntry = cmbLut.SelectedItem as LutEntry;
            string lutId = lutEntry != null ? lutEntry.Id : "o4_dlogm_rec709";

            string[] lookMap = { "vibrant", "natural", "cinematic", "golden", "teal_orange" };
            string look = lookMap[Math.Max(0, cmbLook.SelectedIndex)];

            string[] codecMap = { "hevc", "h264", "prores" };
            string codec = codecMap[Math.Max(0, cmbCodec.SelectedIndex)];

            string[] scaleMap = { "keep", "4k", "2.7k", "1080p" };
            string scale = scaleMap[Math.Max(0, cmbScale.SelectedIndex)];

            int cq = tbCq.Value;
            string sharpenFlag = chkSharpen.Checked ? "--sharpen" : "";
            string previewFlag = chkPreview.Checked ? "-p" : "";

            btnStartGrade.Enabled = false;
            btnStartGrade.Text = "GRADING...";
            progressBar.Value = 0;
            lblStatus.Text = "Grading with NVIDIA RTX NVENC in progress...";
            lblSpeedEta.Text = "";

            txtLog.AppendText("\n========================================\n");
            txtLog.AppendText(string.Format("[START] Video: {0}\n", Path.GetFileName(inputPath)));
            txtLog.AppendText(string.Format("[LUT] {0} ({1})\n", lutEntry != null ? lutEntry.DisplayName : lutId, lutId));
            txtLog.AppendText(string.Format("[LOOK] {0} | [CODEC] {1} | [CQ] {2}\n", look.ToUpperInvariant(), codec.ToUpperInvariant(), cq));

            workerThread = new Thread(() =>
            {
                try
                {
                    string args = string.Format("-d {0} -e /usr/bin/python3 {1}/dji_lut_studio/cli.py \"{2}\" -l {3} --look {4} -c {5} --cq {6} --scale {7} {8} {9} --overwrite",
                        wslDistro, appDir, inputPath, lutId, look, codec, cq, scale, sharpenFlag, previewFlag);

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "wsl.exe",
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    activeProcess = new Process { StartInfo = psi };
                    activeProcess.OutputDataReceived += (s, e) => ParseProcessOutput(e.Data);
                    activeProcess.ErrorDataReceived += (s, e) => ParseProcessOutput(e.Data);

                    activeProcess.Start();
                    activeProcess.BeginOutputReadLine();
                    activeProcess.BeginErrorReadLine();
                    activeProcess.WaitForExit();

                    bool success = (activeProcess.ExitCode == 0);
                    this.Invoke(new Action(() =>
                    {
                        btnStartGrade.Enabled = true;
                        btnStartGrade.Text = "🚀 APPLY LUT & GRADE";
                        if (success)
                        {
                            progressBar.Value = 100;
                            lblStatus.Text = "Grading Completed! Output placed in 'graded' folder.";
                            lblSpeedEta.Text = "✔ Render Finished";
                            txtLog.AppendText("[SUCCESS] Grading complete!\n");
                            LoadPreviewImageIfAvailable(inputPath);
                        }
                        else
                        {
                            lblStatus.Text = "Grading exited with errors. Check logs.";
                            txtLog.AppendText("[ERROR] Process exited with code " + activeProcess.ExitCode + "\n");
                        }
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        btnStartGrade.Enabled = true;
                        btnStartGrade.Text = "🚀 APPLY LUT & GRADE";
                        lblStatus.Text = "Error: " + ex.Message;
                        txtLog.AppendText("[EXCEPTION] " + ex.Message + "\n");
                    }));
                }
            });
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        private void ParseProcessOutput(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            this.Invoke(new Action(() =>
            {
                txtLog.AppendText(line + "\n");
                txtLog.SelectionStart = txtLog.Text.Length;
                txtLog.ScrollToCaret();

                // Regex for percentage progress: [█████░░] 45.2% | 55.0 fps (3.2x) | ETA: 00:04
                Match mPct = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
                if (mPct.Success)
                {
                    double pct = double.Parse(mPct.Groups[1].Value);
                    progressBar.Value = Math.Min(100, Math.Max(0, (int)pct));
                    lblStatus.Text = string.Format("Encoding: {0:F1}%", pct);
                }

                Match mSpd = Regex.Match(line, @"\|\s*([^\n\r]+?)\s*\|");
                if (mSpd.Success)
                {
                    lblSpeedEta.Text = mSpd.Groups[1].Value.Trim();
                }
            }));
        }

        private void RunPreviewGeneration()
        {
            string inputPath = txtInputPath.Text.Trim('"');
            if (!File.Exists(inputPath))
            {
                MessageBox.Show("Please select a single video file to preview.", "DJI LUT Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LutEntry lutEntry = cmbLut.SelectedItem as LutEntry;
            string lutId = lutEntry != null ? lutEntry.Id : "o4_dlogm_rec709";
            string[] lookMap = { "vibrant", "natural", "cinematic", "golden", "teal_orange" };
            string look = lookMap[Math.Max(0, cmbLook.SelectedIndex)];

            btnGenPreview.Enabled = false;
            btnGenPreview.Text = "Generating Preview...";

            ThreadPool.QueueUserWorkItem((state) =>
            {
                try
                {
                    string outDir = Path.Combine(Path.GetDirectoryName(inputPath), "graded");
                    string outPrev = Path.Combine(outDir, Path.GetFileNameWithoutExtension(inputPath) + "_preview.jpg");

                    string script = string.Format(
                        "from dji_lut_studio.manifest import manifest; " +
                        "from dji_lut_studio.engine import generate_preview_snapshot; " +
                        "from pathlib import Path; " +
                        "p_lut = manifest.resolve_lut_file('{0}'); " +
                        "generate_preview_snapshot(Path('{1}'), p_lut, Path('{2}'), look_style='{3}', sharpen=True)",
                        lutId, inputPath.Replace("\\", "/"), outPrev.Replace("\\", "/"), look
                    );

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "wsl.exe",
                        Arguments = string.Format("-d {0} -e /usr/bin/python3 -c \"{1}\"", wslDistro, script),
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process p = Process.Start(psi);
                    p.WaitForExit();

                    this.Invoke(new Action(() =>
                    {
                        btnGenPreview.Enabled = true;
                        btnGenPreview.Text = "Generate Snapshot Preview";
                        if (File.Exists(outPrev))
                        {
                            LoadPreviewImageDirect(outPrev);
                        }
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        btnGenPreview.Enabled = true;
                        btnGenPreview.Text = "Generate Snapshot Preview";
                        txtLog.AppendText("[PREVIEW ERROR] " + ex.Message + "\n");
                    }));
                }
            });
        }

        private void LoadPreviewImageIfAvailable(string inputPath)
        {
            try
            {
                string dir = Directory.Exists(inputPath) ? inputPath : Path.GetDirectoryName(inputPath);
                string gradedDir = Path.Combine(dir, "graded");
                if (Directory.Exists(gradedDir))
                {
                    string[] jpegs = Directory.GetFiles(gradedDir, "*_preview.jpg");
                    if (jpegs.Length > 0)
                    {
                        LoadPreviewImageDirect(jpegs[jpegs.Length - 1]);
                    }
                }
            }
            catch { }
        }

        private void LoadPreviewImageDirect(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    picPreview.Image = Image.FromStream(fs);
                }
                picPreview.Visible = true;
                lblPreviewPlaceholder.Visible = false;
            }
            catch { }
        }

        private void OpenGradedFolder()
        {
            string inPath = txtInputPath.Text.Trim('"');
            string targetDir = "";
            if (File.Exists(inPath)) targetDir = Path.Combine(Path.GetDirectoryName(inPath), "graded");
            else if (Directory.Exists(inPath)) targetDir = Path.Combine(inPath, "graded");

            if (!string.IsNullOrEmpty(targetDir))
            {
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                Process.Start("explorer.exe", targetDir);
            }
            else
            {
                MessageBox.Show("Please choose a valid input file or folder first.", "DJI LUT Studio");
            }
        }

        private void ToggleWatcher()
        {
            string inPath = txtInputPath.Text.Trim('"');
            if (string.IsNullOrEmpty(inPath) || !Directory.Exists(inPath))
            {
                MessageBox.Show("Please select a folder to watch (e.g. D:\\dji\\incoming).", "DJI LUT Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string outDir = Path.Combine(inPath, "graded");
            string args = string.Format("-d {0} -e /usr/bin/python3 {1}/dji_lut_studio/cli.py -w \"{2}\" -o \"{3}\" --look vibrant -p",
                wslDistro, appDir, inPath, outDir);

            Process.Start(new ProcessStartInfo("cmd.exe", "/c start \"DJI LUT Studio - Folder Watcher\" wsl.exe " + args));
        }

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string initialPath = (args != null && args.Length > 0) ? args[0] : null;
            Application.Run(new MainForm(initialPath));
        }
    }
}
