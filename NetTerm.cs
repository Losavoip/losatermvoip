using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ─── Logo applicazione: telefono + terminale, disegnato a runtime ─────────
    public static class AppIcon
    {
        static Icon _shared;
        public static Icon Shared { get { if (_shared == null) _shared = Create(); return _shared; } }

        static GraphicsPath Rounded(Rectangle r, int rad)
        {
            var p = new GraphicsPath(); int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        static Icon Create()
        {
            var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                var rect = new Rectangle(3, 3, 58, 58);
                using (var bg = new SolidBrush(Color.FromArgb(18, 24, 32)))
                using (var path = Rounded(rect, 13)) g.FillPath(bg, path);
                using (var pen = new Pen(Color.FromArgb(45, 62, 80), 2))
                using (var path = Rounded(rect, 13)) g.DrawPath(pen, path);

                // prompt terminale ">_"
                using (var pen = new Pen(Color.FromArgb(61, 220, 151), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLines(pen, new[] { new PointF(15, 21), new PointF(27, 33), new PointF(15, 45) });
                    g.DrawLine(pen, 33, 45, 47, 45);
                }
                // cornetta telefono (in alto a destra), inclinata
                var st = g.Save();
                g.TranslateTransform(45, 17);
                g.RotateTransform(40);
                using (var hb = new SolidBrush(Color.FromArgb(39, 194, 255)))
                using (var pen = new Pen(Color.FromArgb(39, 194, 255), 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, -8, 0, 8, 0);
                    g.FillEllipse(hb, -13, -5, 9, 9);
                    g.FillEllipse(hb, 4, -5, 9, 9);
                }
                g.Restore(st);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ─── Link e finestra "Informazioni" ──────────────────────────────────────
    public static class AppLinks
    {
        public const string Website = "https://losavoip.github.io";
        public const string Donate  = "https://paypal.me/DanieleLosapio";
        public const string GitHub  = "https://github.com/losavoip";
        public static void Open(string url) { try { Process.Start(url); } catch { } }
    }

    public class AboutDialog : Form
    {
        public AboutDialog()
        {
            Text = "Informazioni — LosaTerm Voip Terminal";
            Size = new Size(470, 410);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(22, 26, 34);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);
            try { Icon = AppIcon.Shared; } catch { }
            Build();
        }

        void Build()
        {
            var pic = new PictureBox {
                Image = AppIcon.Shared.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 18), Size = new Size(58, 58), BackColor = Color.Transparent };
            Controls.Add(pic);

            Controls.Add(new Label { Text = "LosaTerm  Voip Terminal", Location = new Point(92, 20),
                AutoSize = true, Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 230, 200) });
            Controls.Add(new Label { Text = "v1.1 · Stabile", Location = new Point(94, 52),
                AutoSize = true, ForeColor = Color.FromArgb(39, 194, 255) });

            Controls.Add(new Label {
                Text = L.T("about.bio"),
                Location = new Point(22, 90), Size = new Size(420, 168), ForeColor = Color.Gainsboro });

            int by = 268;
            var btnSite = Mk(L.T("help.website"), 22, by, 135, Color.FromArgb(40, 80, 140));
            btnSite.Click += (s, e) => AppLinks.Open(AppLinks.Website);
            var btnDon = Mk(L.T("help.donate"), 165, by, 165, Color.FromArgb(150, 50, 70));
            btnDon.Click += (s, e) => AppLinks.Open(AppLinks.Donate);
            var btnClose = Mk(L.T("about.close"), 338, by, 100, Color.FromArgb(60, 60, 75));
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnSite); Controls.Add(btnDon); Controls.Add(btnClose);

            Controls.Add(new Label { Text = "© 2026 Daniele Losapio", Location = new Point(22, 322),
                AutoSize = true, ForeColor = Color.Gray });
        }

        Button Mk(string t, int x, int y, int w, Color c)
        {
            var b = new Button { Text = t, Location = new Point(x, y), Width = w, Height = 32,
                FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }

    // ─── Win32 helpers per embedding finestra ────────────────────────────────

    public static class Win32
    {
        [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")] public static extern int    SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] public static extern int    GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] public static extern bool   SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] public static extern bool   ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")] public static extern bool   IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern uint   GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const int GWL_STYLE     = -16;
        public const int WS_CAPTION    = 0x00C00000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_BORDER     = 0x00800000;
        public const int WS_SYSMENU    = 0x00080000;
        public const uint SWP_NOZORDER   = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const int SW_SHOW = 5;

        public static void StripChrome(IntPtr hwnd)
        {
            int style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER | WS_SYSMENU);
            SetWindowLong(hwnd, GWL_STYLE, style);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | 0x0001 | 0x0002); // SWP_NOMOVE|SWP_NOSIZE
        }

        public static void EmbedAndResize(IntPtr hwnd, IntPtr parent, int w, int h)
        {
            SetParent(hwnd, parent);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, w, h, SWP_NOZORDER);
            ShowWindow(hwnd, SW_SHOW);
        }
    }

    // ─── Crypto (DPAPI) ───────────────────────────────────────────────────────

    public static class Crypto
    {
        public static string Encrypt(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser)); }
            catch { return s; }
        }
        public static string Decrypt(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser)); }
            catch { return s; }
        }
    }

    // ─── Model ───────────────────────────────────────────────────────────────

    [DataContract]
    public class Connection
    {
        [DataMember] public string Name         { get; set; }
        [DataMember] public string Host         { get; set; }
        [DataMember] public int    Port         { get; set; }
        [DataMember] public string Username     { get; set; }
        [DataMember] public string SshPasswordE { get; set; }
        [DataMember] public string IdentityFile { get; set; }
        [DataMember] public string VpnType      { get; set; }
        [DataMember] public string VpnSite      { get; set; }
        [DataMember] public string VpnUsername  { get; set; }
        [DataMember] public string VpnPasswordE { get; set; }
        [DataMember] public string Protocol     { get; set; }
        [DataMember] public string FtpPasswordE { get; set; }
        [DataMember] public int    FtpPort      { get; set; }
        [DataMember] public string Browser      { get; set; }  // Default | Chrome | Firefox | Edge
        [DataMember] public string WebPath      { get; set; }  // path opzionale es. /cucm-uds/

        public Connection() { Port = 22; FtpPort = 21; Protocol = "SSH"; VpnType = "Nessuna"; Browser = "Default"; }

        public string SshPassword { get { return Crypto.Decrypt(SshPasswordE); } set { SshPasswordE = Crypto.Encrypt(value); } }
        public string VpnPassword { get { return Crypto.Decrypt(VpnPasswordE); } set { VpnPasswordE = Crypto.Encrypt(value); } }
        public string FtpPassword { get { return Crypto.Decrypt(FtpPasswordE); } set { FtpPasswordE = Crypto.Encrypt(value); } }
        public override string ToString() { return Name; }
    }

    // ─── Connection Store ────────────────────────────────────────────────────

    public static class ConnectionStore
    {
        static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LosaTermVoip", "connections.json");
        public static string FileLoc { get { return FilePath; } }

        public static List<Connection> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<Connection>();
                var ser = new DataContractJsonSerializer(typeof(List<Connection>));
                using (var fs = File.OpenRead(FilePath))
                    return (List<Connection>)ser.ReadObject(fs);
            }
            catch { return new List<Connection>(); }
        }

        public static void Save(List<Connection> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var ser = new DataContractJsonSerializer(typeof(List<Connection>));
            using (var fs = File.Create(FilePath)) ser.WriteObject(fs, list);
        }
    }

    // ─── VPN Manager ─────────────────────────────────────────────────────────

    public static class VpnManager
    {
        static readonly string TracExe  = @"C:\Program Files (x86)\CheckPoint\Endpoint Connect\trac.exe";
        static readonly string TrGUIExe = @"C:\Program Files (x86)\CheckPoint\Endpoint Connect\TrGUI.exe";

        public static bool CheckpointAvailable { get { return File.Exists(TracExe); } }

        public static string CheckpointStatus()
        {
            if (!CheckpointAvailable) return "N/D";
            try
            {
                string o = RunTrac("info");
                if (o.IndexOf("Connected",    StringComparison.OrdinalIgnoreCase) >= 0) return "Connessa";
                if (o.IndexOf("Disconnected", StringComparison.OrdinalIgnoreCase) >= 0) return "Disconnessa";
                return "--";
            }
            catch { return "Errore"; }
        }

        public static string GetStatus()
        {
            return CheckpointAvailable ? "Checkpoint: " + CheckpointStatus() : "VPN: --";
        }

        public static void CheckpointConnectGui(string siteName, Action<string> log)
        {
            if (!CheckpointAvailable) { log("trac.exe non trovato."); return; }
            if (Process.GetProcessesByName("TrGUI").Length == 0) { log("Avvio TrGUI..."); Process.Start(TrGUIExe); Thread.Sleep(2500); }
            log("Connessione Checkpoint: " + siteName);
            var psi = new ProcessStartInfo(TracExe, "connectgui -s \"" + siteName + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi)) p.WaitForExit(5000);
        }

        public static void CheckpointDisconnect(Action<string> log)
        {
            if (!CheckpointAvailable) { log("trac.exe non trovato."); return; }
            log(RunTrac("disconnect"));
        }

        public static string RunTrac(string args)
        {
            var psi = new ProcessStartInfo(TracExe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit(10000); return o; }
        }

        static string FindFortiClient()
        {
            foreach (var p in new[] { @"C:\Program Files\Fortinet\FortiClient\FortiClient.exe", @"C:\Program Files (x86)\Fortinet\FortiClient\FortiClient.exe" })
                if (File.Exists(p)) return p;
            return null;
        }

        public static void FortinetOpen(Action<string> log)
        {
            string fc = FindFortiClient();
            if (fc == null) { log("FortiClient non trovato."); return; }
            log("Apertura FortiClient..."); Process.Start(fc);
        }

        public static void WindowsVpnConnect(string name, string user, string pass, Action<string> log)
        {
            log("rasdial: " + name);
            string args = "\"" + name + "\"" + (string.IsNullOrEmpty(user) ? "" : " " + user) + (string.IsNullOrEmpty(pass) ? "" : " " + pass);
            var psi = new ProcessStartInfo("rasdial", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi)) { log(p.StandardOutput.ReadToEnd()); p.WaitForExit(30000); }
        }

        public static void WindowsVpnDisconnect(string name, Action<string> log)
        {
            var psi = new ProcessStartInfo("rasdial", "\"" + name + "\" /disconnect") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi)) { log(p.StandardOutput.ReadToEnd()); p.WaitForExit(10000); }
        }

        public static bool CanReach(string host, int port, int ms = 3000)
        {
            try { using (var tc = new TcpClient()) { var ar = tc.BeginConnect(host, port, null, null); return ar.AsyncWaitHandle.WaitOne(ms) && tc.Connected; } }
            catch { return false; }
        }
    }

    // ─── Terminal / PuTTY Launcher ────────────────────────────────────────────

    public static class TerminalLauncher
    {
        public static string FindPutty()
        {
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // Priorità: un PuTTY installato dall'utente (riceve i suoi aggiornamenti di sicurezza)
            // vince sul putty.exe incluso nello zip, che resta come fallback "funziona sempre".
            foreach (var p in new[] {
                @"C:\Program Files\PuTTY\putty.exe",
                @"C:\Program Files (x86)\PuTTY\putty.exe",
                Path.Combine(exeDir, "putty.exe"),
                @"C:\tools\putty.exe"
            }) if (File.Exists(p)) return p;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try { string f = Path.Combine(dir.Trim(), "putty.exe"); if (File.Exists(f)) return f; } catch { }
            }
            return null;
        }

        public static string FindHelper(string exe)
        {
            string putty = FindPutty();
            if (putty == null) return null;
            string sibling = Path.Combine(Path.GetDirectoryName(putty), exe);
            return File.Exists(sibling) ? sibling : null;
        }

        // Cerca TranslatorX.exe nei percorsi tipici per il drag&drop
        public static string FindTranslatorX()
        {
            string pf   = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string lad  = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            foreach (var p in new[] {
                Path.Combine(pf,   "TranslatorX\\TranslatorX.exe"),
                Path.Combine(pf86, "TranslatorX\\TranslatorX.exe"),
                Path.Combine(pf,   "TranslatorX\\translatorx.exe"),
                Path.Combine(pf86, "TranslatorX\\translatorx.exe"),
                Path.Combine(lad,  "TranslatorX\\TranslatorX.exe"),
                Path.Combine(lad,  "Programs\\TranslatorX\\TranslatorX.exe"),
                Path.Combine(desk, "TranslatorX.exe"),
                Path.Combine(desk, "TranslatorX\\TranslatorX.exe")
            }) if (File.Exists(p)) return p;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try { string c = Path.Combine(dir.Trim(), "TranslatorX.exe"); if (File.Exists(c)) return c; } catch { }
            }
            return null;
        }

        public static string BuildPuttyArgs(Connection c, bool usePw = true)
        {
            var sb = new StringBuilder("-ssh -P " + c.Port);
            if (usePw && !string.IsNullOrEmpty(c.SshPassword))  sb.Append(" -pw \"" + c.SshPassword + "\"");
            if (!string.IsNullOrEmpty(c.IdentityFile)) sb.Append(" -i \"" + c.IdentityFile + "\"");
            sb.Append(" " + c.Username + "@" + c.Host);
            return sb.ToString();
        }

        // Lancia PuTTY come finestra standalone (fallback se non si embeda)
        public static void LaunchSshStandalone(Connection c)
        {
            string putty = FindPutty();
            if (putty != null) { Process.Start(putty, BuildPuttyArgs(c)); return; }
            // fallback ssh.exe
            var sb = new StringBuilder();
            if (c.Port != 22) sb.Append("-p " + c.Port + " ");
            if (!string.IsNullOrEmpty(c.IdentityFile)) sb.Append("-i \"" + c.IdentityFile + "\" ");
            sb.Append(c.Username + "@" + c.Host);
            string wt = GetWt();
            var psi = new ProcessStartInfo { UseShellExecute = true };
            if (wt != null) { psi.FileName = wt;       psi.Arguments = "-- ssh " + sb; }
            else            { psi.FileName = "cmd.exe"; psi.Arguments = "/k ssh " + sb; }
            Process.Start(psi);
        }

        public static void LaunchSftp(Connection c)
        {
            string psftp = FindHelper("psftp.exe");
            var sb = new StringBuilder();
            if (psftp != null) { sb.Append("-P " + c.Port); if (!string.IsNullOrEmpty(c.SshPassword)) sb.Append(" -pw \"" + c.SshPassword + "\""); if (!string.IsNullOrEmpty(c.IdentityFile)) sb.Append(" -i \"" + c.IdentityFile + "\""); sb.Append(" " + c.Username + "@" + c.Host); }
            else               { if (c.Port != 22) sb.Append("-P " + c.Port + " "); if (!string.IsNullOrEmpty(c.IdentityFile)) sb.Append("-i \"" + c.IdentityFile + "\" "); sb.Append(c.Username + "@" + c.Host); }
            string exe = psftp ?? "sftp", wt = GetWt();
            var psi = new ProcessStartInfo { UseShellExecute = true };
            if (wt != null) { psi.FileName = wt;       psi.Arguments = "-- \"" + exe + "\" " + sb; }
            else            { psi.FileName = "cmd.exe"; psi.Arguments = "/k \"" + exe + "\" " + sb; }
            Process.Start(psi);
        }

        static string GetWt()
        {
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
            return File.Exists(p) ? p : null;
        }
    }

    // ─── SSH Tab (PuTTY embedded + Analyzer) ─────────────────────────────────
    // NON è un UserControl: popola direttamente il TabPage passato nel costruttore
    // così il docking WinForms si applica direttamente ai controlli del TabPage
    // e PuTTY (HWND nativo) non può coprire la toolbar.

    public class SshTab
    {
        Process puttyProc;
        Panel   puttyPanel;
        SplitContainer splitAnalyzer;   // splitter orizzontale PuTTY | Analyzer
        IntPtr  puttyHwnd = IntPtr.Zero;
        LogAnalyzerPanel analyzerPanel;
        bool analyzerVisible = false;
        string logFilePath;
        TabPage ownerPage;

        public string SessionInfo { get; private set; }
        public string LogFilePath  { get { return logFilePath; } }
        public event EventHandler SessionClosed;

        // SshTab NON ha più toolbar interna.
        // La toolbar sta nella MainForm, FUORI dal TabControl,
        // così PuTTY (finestra Win32 nativa) non può mai coprirla.
        // Costruttore SSH (da Connection)
        public SshTab(Connection c, string puttyExe, TabPage page)
        {
            SessionInfo = c.Username + "@" + c.Host + ":" + c.Port;
            Init(c.Host, TerminalLauncher.BuildPuttyArgs(c), puttyExe, page);
        }

        // Costruttore generico (es. seriale): args PuTTY già pronti
        public SshTab(string sessionLabel, string logTag, string puttyArgs, string puttyExe, TabPage page)
        {
            SessionInfo = sessionLabel;
            Init(logTag, puttyArgs, puttyExe, page);
        }

        void Init(string logTag, string puttyArgs, string puttyExe, TabPage page)
        {
            ownerPage = page;

            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LosaTermVoip", "logs");
            Directory.CreateDirectory(logDir);
            logFilePath = Path.Combine(logDir,
                "session_" + logTag + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");

            // SplitContainer orizzontale: sopra PuTTY, sotto Analyzer (ridimensionabile)
            splitAnalyzer = new SplitContainer {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel2MinSize = 80,
                Panel1MinSize = 80,
                FixedPanel  = FixedPanel.Panel2,   // Panel2 (analyzer) mantiene la dimensione
                BackColor   = Color.FromArgb(20, 20, 30)
            };

            puttyPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            splitAnalyzer.Panel1.Controls.Add(puttyPanel);

            analyzerPanel = new LogAnalyzerPanel(logFilePath) { Dock = DockStyle.Fill };
            splitAnalyzer.Panel2.Controls.Add(analyzerPanel);
            splitAnalyzer.Panel2Collapsed = true;   // nascosto finché l'utente non clicca Analizzatore

            page.Controls.Add(splitAnalyzer);

            // Avvia PuTTY
            string args = puttyArgs + " -sessionlog \"" + logFilePath + "\"";
            puttyProc = new Process {
                StartInfo = new ProcessStartInfo(puttyExe, args) { UseShellExecute = false },
                EnableRaisingEvents = true
            };
            puttyProc.Exited += (s, e) => {
                puttyHwnd = IntPtr.Zero;
                if (page.IsHandleCreated)
                    page.BeginInvoke((Action)(() => {
                        if (SessionClosed != null) SessionClosed(this, EventArgs.Empty);
                    }));
            };
            puttyProc.Start();

            ThreadPool.QueueUserWorkItem(_ => {
                for (int i = 0; i < 100; i++) {
                    Thread.Sleep(150);
                    try {
                        puttyProc.Refresh();
                        IntPtr hwnd = puttyProc.MainWindowHandle;
                        if (hwnd != IntPtr.Zero) {
                            puttyHwnd = hwnd;
                            if (page.IsHandleCreated)
                                page.BeginInvoke((Action)(() => EmbedPutty()));
                            return;
                        }
                    } catch { }
                }
            });
        }

        public void ToggleAnalyzer()
        {
            analyzerVisible = !analyzerVisible;
            splitAnalyzer.Panel2Collapsed = !analyzerVisible;
            if (analyzerVisible && splitAnalyzer.Height > 0)
            {
                // Prima apertura: imposta altezza analizzatore a 260px
                int targetPanel2 = 260;
                int total = splitAnalyzer.Height;
                if (total > targetPanel2 + splitAnalyzer.Panel1MinSize)
                    splitAnalyzer.SplitterDistance = total - targetPanel2 - splitAnalyzer.SplitterWidth;
            }
            ResizePutty();
        }

        void EmbedPutty()
        {
            if (puttyHwnd == IntPtr.Zero || !puttyPanel.IsHandleCreated) return;
            Win32.StripChrome(puttyHwnd);
            int w = puttyPanel.ClientSize.Width;
            int h = puttyPanel.ClientSize.Height;
            if (w > 0 && h > 0)
                Win32.EmbedAndResize(puttyHwnd, puttyPanel.Handle, w, h);
            puttyPanel.Resize += (s, e) => ResizePutty();
        }

        void ResizePutty()
        {
            if (puttyHwnd != IntPtr.Zero && puttyPanel.Width > 0 && puttyPanel.Height > 0)
                Win32.SetWindowPos(puttyHwnd, IntPtr.Zero, 0, 0,
                    puttyPanel.ClientSize.Width, puttyPanel.ClientSize.Height, Win32.SWP_NOZORDER);
        }

        public void OpenLog()
        {
            if (File.Exists(logFilePath)) Process.Start("notepad.exe", "\"" + logFilePath + "\"");
            else MessageBox.Show("Log non ancora disponibile.", "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void CloseSession()
        {
            try { if (puttyProc != null && !puttyProc.HasExited) puttyProc.Kill(); } catch { }
        }

        // Stacca PuTTY dall'embed: torna finestra top-level con cornice/titolo, vivo.
        public IntPtr DetachWindow()
        {
            if (puttyHwnd == IntPtr.Zero) return IntPtr.Zero;
            Win32.SetParent(puttyHwnd, IntPtr.Zero);
            int style = Win32.GetWindowLong(puttyHwnd, Win32.GWL_STYLE);
            style |= Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_SYSMENU;
            Win32.SetWindowLong(puttyHwnd, Win32.GWL_STYLE, style);
            Win32.SetWindowPos(puttyHwnd, IntPtr.Zero, 220, 140, 900, 560, Win32.SWP_NOZORDER);
            Win32.ShowWindow(puttyHwnd, Win32.SW_SHOW);
            IntPtr h = puttyHwnd;
            puttyHwnd = IntPtr.Zero;   // non più embedded: il tab può chiudersi senza toccare PuTTY
            return h;
        }
    }

    // ─── UI helpers ───────────────────────────────────────────────────────────

    public static class UI
    {
        public static string InputBox(string prompt, string title = "", string def = "")
        {
            var f = new Form { Text = title, Size = new Size(360, 130), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
            var lbl = new Label { Text = prompt, Location = new Point(10,12), Width = 330 };
            var txt = new TextBox { Text = def, Location = new Point(10,35), Width = 330 };
            var ok  = new Button { Text = "OK",      Location = new Point(170,65), Width = 80, DialogResult = DialogResult.OK };
            var can = new Button { Text = "Annulla", Location = new Point(260,65), Width = 80, DialogResult = DialogResult.Cancel };
            f.Controls.AddRange(new Control[] { lbl, txt, ok, can });
            f.AcceptButton = ok; f.CancelButton = can;
            return f.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }

        public static Button Btn(string text, int w = 90) { return new Button { Text = text, Width = w, Height = 24, Margin = new Padding(2) }; }
        public static Label  Lbl(string text) { return new Label { Text = text, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left | AnchorStyles.Right }; }
    }

    // ─── Edit Connection Form ─────────────────────────────────────────────────

    public class EditConnectionForm : Form
    {
        public Connection Result { get; private set; }
        TextBox txtName, txtHost, txtPort, txtUser, txtSshPass, txtIdentity;
        TextBox txtVpnSite, txtVpnUser, txtVpnPass, txtFtpPass, txtFtpPort;
        TextBox txtWebPath;
        ComboBox cmbProtocol, cmbVpnType, cmbBrowser;

        public EditConnectionForm(Connection existing = null)
        {
            Text = existing == null ? L.T("ec.title_new") : L.T("ec.title_edit") + existing.Name;
            Size = new Size(460, 520); FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent; MaximizeBox = MinimizeBox = false;

            var tabs = new TabControl { Dock = DockStyle.Fill };

            // SSH Tab
            var tSSH = new TabPage(L.T("ec.tab_ssh"));
            var t1 = MakeTable(tSSH, 9);
            int r = 0;
            txtName = AddRow(t1, r++, L.T("ec.name"));
            t1.Controls.Add(UI.Lbl(L.T("conn.protocol")+":"), 0, r);
            cmbProtocol = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbProtocol.Items.AddRange(new object[] { "SSH", "SFTP", "SCP", "FTP", "HTTP", "HTTPS" });
            cmbProtocol.SelectedIndex = 0;
            t1.Controls.Add(cmbProtocol, 1, r++);
            txtHost    = AddRow(t1, r++, L.T("conn.host")+":");
            txtPort    = AddRow(t1, r++, L.T("ec.port_ssh"));
            txtUser    = AddRow(t1, r++, L.T("conn.username")+":");
            txtSshPass = AddRow(t1, r++, L.T("ec.pass_ssh"), true);
            txtIdentity = AddRow(t1, r++, L.T("ec.identity"));
            t1.Controls.Add(new Label(), 0, r);
            var btnKey = new Button { Text = L.T("ec.browse_key"), Dock = DockStyle.Fill };
            btnKey.Click += (s, e) => { using (var d = new OpenFileDialog()) if (d.ShowDialog() == DialogResult.OK) txtIdentity.Text = d.FileName; };
            t1.Controls.Add(btnKey, 1, r++);

            // VPN Tab
            var tVPN = new TabPage("VPN");
            var t2 = MakeTable(tVPN, 6);
            int rv = 0;
            t2.Controls.Add(UI.Lbl(L.T("conn.vpntype")+":"), 0, rv);
            cmbVpnType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbVpnType.Items.AddRange(new object[] { "Nessuna", "Checkpoint", "Fortinet", "Windows VPN", "Manuale" });
            cmbVpnType.SelectedIndex = 0;
            t2.Controls.Add(cmbVpnType, 1, rv++);
            txtVpnSite = AddRowTo(t2, rv++, L.T("ec.vpn_site"));
            txtVpnUser = AddRowTo(t2, rv++, L.T("ec.vpn_user"));
            txtVpnPass = AddRowTo(t2, rv++, L.T("ec.vpn_pass"), true);
            var note = new Label { Text = L.T("ec.vpn_note"), Dock = DockStyle.Fill, ForeColor = Color.DimGray, Padding = new Padding(4) };
            t2.SetColumnSpan(note, 2); t2.Controls.Add(note, 0, rv);

            // FTP Tab
            var tFTP = new TabPage("FTP");
            var t3 = MakeTable(tFTP, 3);
            txtFtpPass = AddRowTo(t3, 0, L.T("ec.pass_ftp"), true);
            txtFtpPort = AddRowTo(t3, 1, L.T("ftp.port"));
            var n2 = new Label { Text = L.T("ec.ftp_note"), ForeColor = Color.DimGray, Dock = DockStyle.Fill, Padding = new Padding(4) };
            t3.SetColumnSpan(n2, 2); t3.Controls.Add(n2, 0, 2);

            // Web Tab (HTTP / HTTPS)
            var tWeb = new TabPage("Web");
            var t4 = MakeTable(tWeb, 4);
            t4.Controls.Add(UI.Lbl(L.T("conn.browser")+":"), 0, 0);
            cmbBrowser = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBrowser.Items.AddRange(new object[] { "Default", "Chrome", "Firefox", "Edge" });
            cmbBrowser.SelectedIndex = 0;
            t4.Controls.Add(cmbBrowser, 1, 0);
            txtWebPath = AddRowTo(t4, 1, L.T("ec.web_path"));
            var nWeb = new Label {
                Text = L.T("ec.web_note"),
                ForeColor = Color.DimGray, Dock = DockStyle.Fill, Padding = new Padding(4)
            };
            t4.SetColumnSpan(nWeb, 2); t4.Controls.Add(nWeb, 0, 2);

            tabs.TabPages.AddRange(new TabPage[] { tSSH, tVPN, tFTP, tWeb });

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
            var btnOk = UI.Btn("OK"); var btnCan = UI.Btn(L.T("conn.cancel"));
            btnOk.Click += (s, e) => Save(); btnCan.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnPanel.Controls.AddRange(new Control[] { btnCan, btnOk });
            Controls.Add(tabs); Controls.Add(btnPanel);
            AcceptButton = btnOk; CancelButton = btnCan;

            if (existing != null) Fill(existing);
            else { txtPort.Text = "22"; txtFtpPort.Text = "21"; }
        }

        TableLayoutPanel MakeTable(TabPage page, int rows)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 2, RowCount = rows };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.Controls.Add(t);
            return t;
        }

        TextBox AddRow(TableLayoutPanel t, int row, string label, bool pwd = false)
        {
            t.Controls.Add(UI.Lbl(label), 0, row);
            var tb = new TextBox { Dock = DockStyle.Fill }; if (pwd) tb.PasswordChar = '●';
            t.Controls.Add(tb, 1, row); return tb;
        }

        TextBox AddRowTo(TableLayoutPanel t, int row, string label, bool pwd = false) { return AddRow(t, row, label, pwd); }

        void Fill(Connection c)
        {
            txtName.Text     = c.Name    ?? "";
            txtHost.Text     = c.Host    ?? "";
            txtPort.Text     = c.Port.ToString();
            txtUser.Text     = c.Username    ?? "";
            txtSshPass.Text  = c.SshPassword ?? "";
            txtIdentity.Text = c.IdentityFile ?? "";
            txtVpnSite.Text  = c.VpnSite     ?? "";
            txtVpnUser.Text  = c.VpnUsername  ?? "";
            txtVpnPass.Text  = c.VpnPassword  ?? "";
            txtFtpPass.Text  = c.FtpPassword  ?? "";
            txtFtpPort.Text  = c.FtpPort.ToString();

            int idx;
            string proto   = c.Protocol ?? "SSH";
            string vpnType = c.VpnType  ?? "Nessuna";
            string browser = c.Browser  ?? "Default";
            idx = cmbProtocol.Items.IndexOf(proto);   cmbProtocol.SelectedIndex = idx >= 0 ? idx : 0;
            idx = cmbVpnType.Items.IndexOf(vpnType);  cmbVpnType.SelectedIndex  = idx >= 0 ? idx : 0;
            idx = cmbBrowser.Items.IndexOf(browser);  cmbBrowser.SelectedIndex  = idx >= 0 ? idx : 0;
            txtWebPath.Text = c.WebPath ?? "";
        }

        void Save()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtHost.Text))
            { MessageBox.Show(L.T("ec.validate_req"), L.T("ec.validate_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            int p; if (!int.TryParse(txtPort.Text, out p)) p = 22;
            int fp; if (!int.TryParse(txtFtpPort.Text, out fp)) fp = 21;
            Result = new Connection
            {
                Name = txtName.Text.Trim(), Host = txtHost.Text.Trim(), Port = p,
                Username = txtUser.Text.Trim(), IdentityFile = txtIdentity.Text.Trim(),
                VpnType = cmbVpnType.SelectedItem != null ? cmbVpnType.SelectedItem.ToString() : "Nessuna",
                VpnSite = txtVpnSite.Text.Trim(), VpnUsername = txtVpnUser.Text.Trim(),
                Protocol = cmbProtocol.SelectedItem != null ? cmbProtocol.SelectedItem.ToString() : "SSH",
                Browser  = cmbBrowser.SelectedItem  != null ? cmbBrowser.SelectedItem.ToString()  : "Default",
                WebPath  = txtWebPath.Text.Trim(),
                FtpPort  = fp
            };
            Result.SshPassword = txtSshPass.Text;
            Result.VpnPassword = txtVpnPass.Text;
            Result.FtpPassword = txtFtpPass.Text;
            DialogResult = DialogResult.OK; Close();
        }
    }

    // ─── VPN Connect Form ─────────────────────────────────────────────────────

    public class VpnConnectForm : Form
    {
        Connection conn; RichTextBox rtb; ProgressBar pbar; Button btnAction;
        public bool Success { get; private set; }

        public VpnConnectForm(Connection c)
        {
            conn = c; Text = "VPN — " + (c.VpnSite ?? c.VpnType);
            Size = new Size(500, 260); FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen; MaximizeBox = MinimizeBox = false;
            pbar = new ProgressBar { Dock = DockStyle.Top, Height = 14, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
            rtb  = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(20,20,20), ForeColor = Color.LightGreen, Font = new Font("Consolas", 9) };
            btnAction = new Button { Text = L.T("about.close"), Dock = DockStyle.Bottom, Height = 30, Enabled = false };
            btnAction.Click += (s, e) => Close();
            Controls.Add(rtb); Controls.Add(pbar); Controls.Add(btnAction);
            Shown += (s, e) => Start();
        }

        void Log(string msg) { if (rtb.InvokeRequired) BeginInvoke((Action)(() => Log(msg))); else { rtb.AppendText(msg + "\n"); rtb.ScrollToCaret(); } }

        void Done(bool ok)
        {
            Success = ok; pbar.MarqueeAnimationSpeed = 0;
            if (ok)
            {
                Log("\n✔ Connessa! Apertura sessione...");
                Thread.Sleep(800);
                BeginInvoke((Action)(() => { DialogResult = DialogResult.OK; Close(); }));
            }
            else { Log("\nNon riuscita. Verifica il client VPN."); btnAction.Enabled = true; }
        }

        void Start()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string vt = conn.VpnType ?? "Nessuna";
                if (vt == "Checkpoint")
                {
                    VpnManager.CheckpointConnectGui(conn.VpnSite, Log);
                    for (int i = 0; i < 30; i++) { Thread.Sleep(1000); Log("Attesa... " + (i+1) + "s"); if (VpnManager.CanReach(conn.Host, conn.Port, 1500)) { BeginInvoke((Action)(() => Done(true))); return; } }
                    BeginInvoke((Action)(() => Done(false)));
                }
                else if (vt == "Fortinet")
                {
                    VpnManager.FortinetOpen(Log); Log("Connetti in FortiClient, attendo...");
                    for (int i = 0; i < 60; i++) { Thread.Sleep(1000); Log("Attesa... " + (i+1) + "s"); if (VpnManager.CanReach(conn.Host, conn.Port, 1500)) { BeginInvoke((Action)(() => Done(true))); return; } }
                    BeginInvoke((Action)(() => Done(false)));
                }
                else if (vt == "Windows VPN")
                {
                    VpnManager.WindowsVpnConnect(conn.VpnSite, conn.VpnUsername, conn.VpnPassword, Log);
                    Thread.Sleep(2000);
                    BeginInvoke((Action)(() => Done(VpnManager.CanReach(conn.Host, conn.Port, 3000))));
                }
                else if (vt == "Manuale")
                {
                    Log("Connetti manualmente la VPN, poi premi Continua.");
                    BeginInvoke((Action)(() => {
                        pbar.MarqueeAnimationSpeed = 0;
                        btnAction.Text = L.T("vpn.continue"); btnAction.Enabled = true;
                        btnAction.Click += (s2, e2) => { Success = true; DialogResult = DialogResult.OK; Close(); };
                    }));
                }
                else BeginInvoke((Action)(() => Done(true)));
            });
        }
    }

    // ─── SCP Transfer Form ────────────────────────────────────────────────────

    public class ScpTransferForm : Form
    {
        Connection conn; RadioButton rdUp, rdDown; TextBox txtLocal, txtRemote;
        Button btnBrowse, btnGo; RichTextBox rtbLog; ProgressBar pbar; CheckBox chkRec;
        bool sawEofError;

        public ScpTransferForm(Connection c)
        {
            conn = c; Text = "SCP Transfer — " + c.Name; Size = new Size(620, 440);
            FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterScreen; MaximizeBox = false;
            var top = new Panel { Dock = DockStyle.Top, Height = 170, Padding = new Padding(10) };
            rdUp   = new RadioButton { Text = L.T("scp.upload"),  Location = new Point(10,8),  Checked = true, Width = 230 };
            rdDown = new RadioButton { Text = L.T("scp.download"), Location = new Point(250,8), Width = 230 };
            txtLocal  = new TextBox { Location = new Point(130,37), Width = 330 };
            btnBrowse = new Button  { Text = "...", Location = new Point(465,35), Width = 35, Height = 23 };
            btnBrowse.Click += (s,e) => { if (rdUp.Checked) { using (var d = new OpenFileDialog()) if (d.ShowDialog() == DialogResult.OK) txtLocal.Text = d.FileName; } else { using (var d = new SaveFileDialog()) if (d.ShowDialog() == DialogResult.OK) txtLocal.Text = d.FileName; } };
            // default: host: senza ~/ (su Cisco IOS ~/ non esiste → usa flash:/bootflash:)
            txtRemote = new TextBox { Location = new Point(130,69), Width = 370, Text = c.Username + "@" + c.Host + ":" };
            var lblHint = new Label { Text = L.T("scp.hint"), Location = new Point(130, 92), Width = 460, ForeColor = Color.DimGray, Font = new Font("Segoe UI", 7.5f) };
            chkRec = new CheckBox { Text = L.T("scp.recursive"), Location = new Point(130, 110), Width = 220, ForeColor = Color.DimGray };
            btnGo = new Button { Text = L.T("scp.start"), Location = new Point(360,108), Width = 160, Height = 26 };
            btnGo.Click += Transfer;
            pbar = new ProgressBar { Location = new Point(10,142), Width = 580, Height = 16, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 0 };
            top.Controls.AddRange(new Control[] { rdUp, rdDown, new Label { Text=L.T("scp.local_path"),  Location=new Point(10,40),  Width=115 }, txtLocal, btnBrowse, new Label { Text=L.T("scp.remote_path"), Location=new Point(10,72), Width=115 }, txtRemote, lblHint, chkRec, btnGo, pbar });
            rtbLog = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(20,20,20), ForeColor = Color.LightGreen, Font = new Font("Consolas", 9) };
            Controls.Add(rtbLog); Controls.Add(top);
        }

        void Transfer(object s, EventArgs e)
        {
            string local = txtLocal.Text.Trim(), remote = txtRemote.Text.Trim();
            if (string.IsNullOrEmpty(local) || string.IsNullOrEmpty(remote)) { MessageBox.Show("Inserisci entrambi i percorsi."); return; }
            // controllo: il percorso remoto deve includere un file, non solo host:
            if (remote.TrimEnd().EndsWith(":") || remote.TrimEnd().EndsWith(":~/"))
            {
                if (MessageBox.Show("Il percorso remoto non specifica un file.\n\nSu Cisco IOS devi indicare ad es. flash:config.txt\nSu Linux ad es. /home/user/file.\n\nContinuare comunque?",
                    "Percorso remoto", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }
            sawEofError = false;
            string pscp = TerminalLauncher.FindHelper("pscp.exe");
            bool usePscp = pscp != null;
            string rec = chkRec.Checked ? "-r " : "";
            string exe, args;
            if (usePscp) { var sb = new StringBuilder("-scp " + rec + "-P " + conn.Port + " "); if (!string.IsNullOrEmpty(conn.SshPassword)) sb.Append("-pw \"" + conn.SshPassword + "\" "); if (!string.IsNullOrEmpty(conn.IdentityFile)) sb.Append("-i \"" + conn.IdentityFile + "\" "); sb.Append(rdUp.Checked ? "\"" + local + "\" " + remote : remote + " \"" + local + "\""); exe = pscp; args = sb.ToString(); }
            else { var sb = new StringBuilder(); if (chkRec.Checked) sb.Append("-r "); if (conn.Port != 22) sb.Append("-P " + conn.Port + " "); if (!string.IsNullOrEmpty(conn.IdentityFile)) sb.Append("-i \"" + conn.IdentityFile + "\" "); sb.Append(rdUp.Checked ? "\"" + local + "\" " + remote : remote + " \"" + local + "\""); exe = "scp"; args = sb.ToString(); }
            pbar.MarqueeAnimationSpeed = 30; btnGo.Enabled = false;
            // non loggare la password in chiaro
            Log((usePscp ? "pscp" : "scp") + " " + (conn.SshPassword != null ? args.Replace(conn.SshPassword, "******") : args) + "\n");
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (ps,pe) => { if (pe.Data != null) BeginInvoke((Action)(() => Log(pe.Data))); };
            proc.ErrorDataReceived  += (ps,pe) => { if (pe.Data != null) BeginInvoke((Action)(() => Log(pe.Data))); };
            proc.Exited += (ps,pe) => BeginInvoke((Action)(() => {
                pbar.MarqueeAnimationSpeed = 0; btnGo.Enabled = true;
                Log(proc.ExitCode == 0 ? "\n✔ Completato." : "\n✘ Errore (exit " + proc.ExitCode + ").");
                if (sawEofError)
                    Log("\n⚠ \"unexpected end-of-file\": il device ha chiuso la connessione.\n" +
                        "  Cause tipiche su Cisco IOS:\n" +
                        "   • SCP server non abilitato → in conf:  ip scp server enable\n" +
                        "   • Il path non esiste (usa flash: / bootflash:, non ~/)\n" +
                        "   • L'utente non ha privilegio 15\n" +
                        "  Su SBC/AudioCodes verifica che SFTP/SCP sia abilitato.");
            }));
            proc.Start(); proc.BeginOutputReadLine(); proc.BeginErrorReadLine();
        }
        void Log(string msg)
        {
            if (msg != null && msg.IndexOf("unexpected end-of-file", StringComparison.OrdinalIgnoreCase) >= 0) sawEofError = true;
            rtbLog.AppendText(msg + "\n"); rtbLog.ScrollToCaret();
        }
    }

    // ─── FTP Browser ──────────────────────────────────────────────────────────

    public class FtpBrowserForm : Form
    {
        Connection conn; string currentPath = "/"; ListView lvFiles; TextBox txtPath; Label lblStatus;

        public FtpBrowserForm(Connection c)
        {
            conn = c; Text = "FTP — " + c.Host; Size = new Size(720, 520); StartPosition = FormStartPosition.CenterScreen;
            var tp = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(2) };
            var bUp=UI.Btn(L.T("fb.up"),55); var bRef=UI.Btn("↺",32); txtPath=new TextBox{Width=300,Height=22}; var bGo=UI.Btn(L.T("fb.go"),42);
            var bDl=UI.Btn("⬇ Download",110); var bUl=UI.Btn("⬆ Upload",90); var bMk=UI.Btn(L.T("fb.newfolder"),100); var bDel=UI.Btn(L.T("btn.delete"),90);
            bUp.Click+=(s,e)=>GoUp(); bRef.Click+=(s,e)=>LoadDir(); bGo.Click+=(s,e)=>{currentPath=txtPath.Text;LoadDir();};
            bDl.Click+=(s,e)=>Download(); bUl.Click+=(s,e)=>Upload(); bMk.Click+=(s,e)=>MakeDir(); bDel.Click+=(s,e)=>Delete();
            tp.Controls.AddRange(new Control[]{bUp,txtPath,bGo,bRef,bDl,bUl,bMk,bDel});
            lvFiles=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true};
            lvFiles.Columns.Add(L.T("conn.name"),300); lvFiles.Columns.Add(L.T("fb.col_size"),100); lvFiles.Columns.Add(L.T("fb.col_date"),160); lvFiles.Columns.Add(L.T("fb.col_type"),60);
            lvFiles.DoubleClick+=OnDbl;
            lblStatus=new Label{Dock=DockStyle.Bottom,Text=L.T("fb.ready"),Height=22,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(4,0,0,0)};
            Controls.Add(lvFiles); Controls.Add(tp); Controls.Add(lblStatus);
            Shown+=(s,e)=>LoadDir();
        }

        void LoadDir()
        {
            lblStatus.Text="Caricamento: "+currentPath; txtPath.Text=currentPath; lvFiles.Items.Clear();
            ThreadPool.QueueUserWorkItem(_=>{
                try {
                    var req=MkReq(currentPath);
                    req.Method=WebRequestMethods.Ftp.ListDirectoryDetails;
                    using(var resp=(FtpWebResponse)req.GetResponse())
                    using(var sr=new StreamReader(resp.GetResponseStream())) {
                        string listing = sr.ReadToEnd();
                        if (!IsDisposed && IsHandleCreated)
                            BeginInvoke((Action<string>)(Populate), listing);
                    }
                } catch(Exception ex) {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)(()=>{ if(!IsDisposed) lblStatus.Text="Errore: "+ex.Message; }));
                }
            });
        }

        void Populate(string listing)
        {
            lvFiles.Items.Clear();
            foreach(var line in listing.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))
            {
                var parts=line.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries); if(parts.Length<9) continue;
                bool isDir=line[0]=='d'; string name=string.Join(" ",parts,8,parts.Length-8); if(name=="."||name=="..") continue;
                var item=new ListViewItem(new[]{name,isDir?"<DIR>":parts[4],parts[5]+" "+parts[6]+" "+parts[7],isDir?L.T("fb.type_folder"):"File"});
                item.Tag=isDir; item.ForeColor=isDir?Color.DarkBlue:Color.Black; lvFiles.Items.Add(item);
            }
            lblStatus.Text=currentPath+"  ("+lvFiles.Items.Count+" elementi)";
        }

        void OnDbl(object s,EventArgs e){ if(lvFiles.SelectedItems.Count==0)return; var item=lvFiles.SelectedItems[0]; if((bool)item.Tag){currentPath=currentPath.TrimEnd('/')+"/"+item.Text;LoadDir();}else Download(); }
        void GoUp(){ if(currentPath=="/")return; int idx=currentPath.TrimEnd('/').LastIndexOf('/'); currentPath=idx<=0?"/":currentPath.Substring(0,idx); LoadDir(); }

        void Download()
        {
            if(lvFiles.SelectedItems.Count==0){MessageBox.Show("Seleziona un file.");return;} var item=lvFiles.SelectedItems[0]; if((bool)item.Tag){MessageBox.Show("Seleziona un file.");return;}
            using(var dlg=new SaveFileDialog{FileName=item.Text}) if(dlg.ShowDialog()==DialogResult.OK){ string rp=currentPath.TrimEnd('/')+"/"+item.Text,lp=dlg.FileName; lblStatus.Text="Download: "+item.Text; ThreadPool.QueueUserWorkItem(_=>{ try{var req=MkReq(rp);req.Method=WebRequestMethods.Ftp.DownloadFile;using(var resp=(FtpWebResponse)req.GetResponse())using(var rs=resp.GetResponseStream())using(var fs=File.Create(lp))rs.CopyTo(fs);BeginInvoke((Action)(()=>lblStatus.Text="Download OK: "+item.Text));}catch(Exception ex){BeginInvoke((Action)(()=>lblStatus.Text="Errore: "+ex.Message));}}); }
        }

        void Upload()
        {
            using(var dlg=new OpenFileDialog()) if(dlg.ShowDialog()==DialogResult.OK){ string rp=currentPath.TrimEnd('/')+"/"+Path.GetFileName(dlg.FileName),lp=dlg.FileName; lblStatus.Text="Upload: "+Path.GetFileName(lp); ThreadPool.QueueUserWorkItem(_=>{ try{var req=MkReq(rp);req.Method=WebRequestMethods.Ftp.UploadFile;using(var fs=File.OpenRead(lp))using(var rs=req.GetRequestStream())fs.CopyTo(rs);BeginInvoke((Action)(()=>{lblStatus.Text="Upload OK.";LoadDir();}));}catch(Exception ex){BeginInvoke((Action)(()=>lblStatus.Text="Errore: "+ex.Message));}}); }
        }

        void MakeDir(){ string name=UI.InputBox("Nome nuova cartella:","Nuova Cartella"); if(string.IsNullOrWhiteSpace(name))return; try{var req=MkReq(currentPath.TrimEnd('/')+"/"+name);req.Method=WebRequestMethods.Ftp.MakeDirectory;req.GetResponse().Close();LoadDir();}catch(Exception ex){MessageBox.Show("Errore: "+ex.Message);} }
        void Delete(){ if(lvFiles.SelectedItems.Count==0)return; var item=lvFiles.SelectedItems[0]; if(MessageBox.Show("Eliminare \""+item.Text+"\"?","Conferma",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return; try{var req=MkReq(currentPath.TrimEnd('/')+"/"+item.Text);req.Method=(bool)item.Tag?WebRequestMethods.Ftp.RemoveDirectory:WebRequestMethods.Ftp.DeleteFile;req.GetResponse().Close();LoadDir();}catch(Exception ex){MessageBox.Show("Errore: "+ex.Message);} }

        FtpWebRequest MkReq(string path){ var req=(FtpWebRequest)WebRequest.Create("ftp://"+conn.Host+":"+conn.FtpPort+path); req.Credentials=new NetworkCredential(conn.Username,conn.FtpPassword??""); req.UsePassive=true;req.UseBinary=true;req.KeepAlive=false; return req; }
    }

    // ─── Claude AI Tab ────────────────────────────────────────────────────────
    // Apre chrome.exe --app=https://claude.ai in una finestra senza toolbar,
    // poi la incorpora nel tab con SetParent.
    // Claude Desktop rimane completamente separato e indipendente.

    public class ClaudeTab
    {
        Panel   claudePanel;
        IntPtr  claudeHwnd = IntPtr.Zero;
        bool    embedded   = false;
        Process browserProc;

        // Cerca Chrome o Edge (Edge è sempre presente su Win10/11)
        static string FindBrowser()
        {
            var candidates = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),         "Google\\Chrome\\Application\\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),      "Google\\Chrome\\Application\\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google\\Chrome\\Application\\chrome.exe"),
                // Edge (sempre presente su Win10/11)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),         "Microsoft\\Edge\\Application\\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),      "Microsoft\\Edge\\Application\\msedge.exe"),
            };
            foreach (var p in candidates) if (File.Exists(p)) return p;
            return null;
        }

        public ClaudeTab(TabPage page)
        {
            claudePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 20, 30) };
            var lbl = new Label {
                Text = "⏳ Apertura Claude AI...",
                Dock = DockStyle.Fill, ForeColor = Color.CornflowerBlue,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            claudePanel.Controls.Add(lbl);
            page.Controls.Add(claudePanel);

            string browser = FindBrowser();
            if (browser == null)
            {
                lbl.Text = "❌ Chrome o Edge non trovati.\nInstalla Chrome da google.com/chrome";
                return;
            }

            // --app= apre una finestra senza barre browser, sembra un'app nativa
            // --new-window forza una finestra nuova anche se il browser è già aperto
            // --disable-extensions riduce i popup di estensioni
            var psi = new ProcessStartInfo(browser,
                "--app=https://claude.ai --new-window --disable-extensions")
            {
                UseShellExecute = false
            };
            try { browserProc = Process.Start(psi); }
            catch (Exception ex)
            {
                lbl.Text = "❌ Errore avvio browser:\n" + ex.Message;
                return;
            }

            // Polling HWND: aspetta la finestra del browser con titolo che contiene "Claude"
            ThreadPool.QueueUserWorkItem(_ =>
            {
                for (int i = 0; i < 80; i++)   // max 40 secondi
                {
                    Thread.Sleep(500);
                    IntPtr hwnd = FindBrowserClaudeHwnd();
                    if (hwnd != IntPtr.Zero)
                    {
                        claudeHwnd = hwnd;
                        if (page.IsHandleCreated)
                            page.BeginInvoke((Action)(() => { lbl.Visible = false; Embed(); }));
                        return;
                    }
                }
                if (page.IsHandleCreated)
                    page.BeginInvoke((Action)(() =>
                        lbl.Text = "⚠ Timeout: la finestra Claude AI non è apparsa.\nRiprova."));
            });
        }

        // Cerca la finestra del browser che ha "Claude" nel titolo
        // e appartiene al processo che abbiamo appena lanciato
        IntPtr FindBrowserClaudeHwnd()
        {
            // Prima prova con il PID esatto del processo lanciato
            if (browserProc != null && !browserProc.HasExited)
            {
                try
                {
                    // Chrome/Edge aprono processi figli — cerca tra tutti i processi
                    // con lo stesso nome quello che ha una finestra visibile con titolo "Claude"
                    string procName = Path.GetFileNameWithoutExtension(browserProc.MainModule.FileName);
                    foreach (var p in Process.GetProcessesByName(procName))
                    {
                        try
                        {
                            IntPtr mw = p.MainWindowHandle;
                            if (mw != IntPtr.Zero && Win32.IsWindowVisible(mw))
                            {
                                string title = GetWindowTitle(mw);
                                if (title != null && title.IndexOf("Claude", StringComparison.OrdinalIgnoreCase) >= 0)
                                    return mw;
                            }
                        } catch { }
                    }
                } catch { }
            }
            // Fallback: FindWindow per titolo
            return FindWindowByTitle("Claude");
        }

        static string GetWindowTitle(IntPtr hwnd)
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        static IntPtr FindWindowByTitle(string titlePart)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hwnd, _) => {
                if (Win32.IsWindowVisible(hwnd))
                {
                    string t = GetWindowTitle(hwnd);
                    if (t != null && t.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0)
                    { found = hwnd; return false; }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        void Embed()
        {
            if (claudeHwnd == IntPtr.Zero || !claudePanel.IsHandleCreated) return;
            Win32.StripChrome(claudeHwnd);
            Win32.EmbedAndResize(claudeHwnd, claudePanel.Handle,
                claudePanel.ClientSize.Width, claudePanel.ClientSize.Height);
            claudePanel.Resize += (s, e) => {
                if (claudeHwnd != IntPtr.Zero)
                    Win32.SetWindowPos(claudeHwnd, IntPtr.Zero, 0, 0,
                        claudePanel.ClientSize.Width, claudePanel.ClientSize.Height, Win32.SWP_NOZORDER);
            };
            embedded = true;
        }

        // Chiusura tab: chiude la finestra browser che abbiamo aperto noi
        public void Detach()
        {
            if (claudeHwnd != IntPtr.Zero && embedded)
            {
                Win32.SetParent(claudeHwnd, IntPtr.Zero);
                int style = Win32.GetWindowLong(claudeHwnd, Win32.GWL_STYLE);
                style |= Win32.WS_CAPTION | Win32.WS_THICKFRAME | Win32.WS_SYSMENU;
                Win32.SetWindowLong(claudeHwnd, Win32.GWL_STYLE, style);
                Win32.ShowWindow(claudeHwnd, Win32.SW_SHOW);
            }
            // Chiudi il processo browser che abbiamo avviato noi
            try { if (browserProc != null && !browserProc.HasExited) browserProc.Kill(); } catch { }
        }

        // P/Invoke extra per EnumWindows e GetWindowText
        delegate bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsCallback lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder sb, int maxCount);
    }

    // ─── Main Form ────────────────────────────────────────────────────────────

    public class MainForm : Form
    {
        List<Connection> connections;
        ListView lvConn;
        TabControl tabMain;
        TabPage tabLog;
        RichTextBox rtbLog;
        ToolStripStatusLabel lblVpnStatus, lblPuttyStatus;
        System.Windows.Forms.Timer vpnTimer;

        // Pulsanti sessione SSH nel ToolStrip principale (immune a PuTTY HWND)
        ToolStripSeparator tssSsh;
        ToolStripLabel     tslSshInfo;
        ToolStripButton    tsbAnalyzer, tsbLog, tsbClose, tsbDetach;
        ToolStripLabel     tslIp;   // IP locale (+ VPN) sempre visibile in alto a destra
        ToolStripDropDownButton tsbSessions;   // selettore sessioni/tab aperti
        Dictionary<TabPage, SshTab> sshSessions = new Dictionary<TabPage, SshTab>();

        // Claude Desktop embedded
        TabPage   claudeTabPage;
        ClaudeTab claudeTab;

        // PCAP standalone tabs (possono essere più di uno)
        int pcapTabCounter = 0;

        // Layout persistente
        SplitContainer mainSplit;
        LayoutData layout;

        public MainForm()
        {
            Text = "LosaTerm   Voip Terminal"; Size = new Size(1100, 680);
            try { Icon = AppIcon.Shared; } catch { }
            MinimumSize = new Size(800, 480); StartPosition = FormStartPosition.CenterScreen; TopMost = true;
            connections = ConnectionStore.Load();
            layout = AppLayout.Load();
            BuildUI(); RefreshList(); UpdateStatus();
            ApplyLayout();
            vpnTimer = new System.Windows.Forms.Timer { Interval = 12000 };
            vpnTimer.Tick += (s, e) => UpdateStatus();
            vpnTimer.Start();
            Shown += (s, e) => { TopMost = false; BringToFront(); Activate(); };
            FormClosing += (s, e) => SaveLayout();

            // Drag & drop di PCAP/log sulla finestra
            AllowDrop = true;
            DragEnter += HandleDragEnter;
            DragDrop  += HandleDragDrop;
        }

        // Applica dimensioni/posizione/stato salvati all'avvio
        void ApplyLayout()
        {
            try
            {
                if (layout.W > 200 && layout.H > 150)
                {
                    StartPosition = FormStartPosition.Manual;
                    // verifica che la posizione sia su uno schermo visibile
                    var rect = new Rectangle(layout.X, layout.Y, layout.W, layout.H);
                    bool visible = false;
                    if (layout.X >= -50 && layout.Y >= -50)
                        foreach (var sc in Screen.AllScreens)
                            if (sc.WorkingArea.IntersectsWith(rect)) { visible = true; break; }
                    if (visible) { Location = new Point(layout.X, layout.Y); }
                    Size = new Size(layout.W, layout.H);
                }
                if (layout.State == (int)FormWindowState.Maximized)
                    WindowState = FormWindowState.Maximized;
            }
            catch { }
        }

        // Salva il layout corrente alla chiusura
        void SaveLayout()
        {
            try
            {
                layout.State = (int)WindowState;
                // se massimizzata, salva i bounds "normali" (RestoreBounds)
                var b = (WindowState == FormWindowState.Normal) ? Bounds : RestoreBounds;
                layout.X = b.X; layout.Y = b.Y; layout.W = b.Width; layout.H = b.Height;
                if (mainSplit != null && !mainSplit.IsDisposed)
                    layout.SplitMain = mainSplit.SplitterDistance;
                AppLayout.Save(layout);
            }
            catch { }
        }

        void BuildUI()
        {
            var menu = new MenuStrip();
            var mC = new ToolStripMenuItem(L.T("menu.connections"));
            mC.DropDownItems.Add(L.T("menu.new")+"...",    null, (s,e)=>AddConn());
            mC.DropDownItems.Add(L.T("menu.edit")+"...",   null, (s,e)=>EditConn());
            mC.DropDownItems.Add(L.T("menu.delete"),       null, (s,e)=>DelConn());
            mC.DropDownItems.Add(new ToolStripSeparator());
            mC.DropDownItems.Add(L.T("conn.ssh_embedded"), null, (s,e)=>Connect());
            mC.DropDownItems.Add(L.T("conn.ssh_window"),   null, (s,e)=>ConnectStandalone());
            mC.DropDownItems.Add("SFTP",                   null, (s,e)=>OpenSftp());
            mC.DropDownItems.Add(L.T("conn.scp_transfer"), null, (s,e)=>OpenScp());
            mC.DropDownItems.Add(L.T("conn.ftp_browser"),  null, (s,e)=>OpenFtp());
            mC.DropDownItems.Add(new ToolStripSeparator());
            mC.DropDownItems.Add(L.T("menu.open_config"),  null, (s,e)=>Process.Start("explorer.exe", "/select,\""+ConnectionStore.FileLoc+"\""));
            mC.DropDownItems.Add(L.T("menu.exit"),         null, (s,e)=>Close());

            var mT = new ToolStripMenuItem(L.T("menu.tools"));
            mT.DropDownItems.Add(L.T("tools.putty_path"),  null, (s,e)=>ShowPuttyPath());
            mT.DropDownItems.Add(L.T("tools.test_conn"),   null, (s,e)=>TestReach());

            var mLang = new ToolStripMenuItem("🌐 " + L.T("menu.language"));
            var langItems = new[] {
                new { Code="EN", Label="🇬🇧  English"  },
                new { Code="IT", Label="🇮🇹  Italiano" },
            };
            foreach (var li in langItems)
            {
                var code = li.Code;
                var item = new ToolStripMenuItem(li.Label);
                item.Checked  = (L.CurrentLang == code);
                item.Click   += (s, e) => {
                    L.Set(code);
                    MessageBox.Show(L.T("lang.restart"), "LosaTermVoip",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                mLang.DropDownItems.Add(item);
            }

            var mHelp = new ToolStripMenuItem("❓");
            mHelp.DropDownItems.Add(L.T("help.website"), null, (s,e)=>AppLinks.Open(AppLinks.Website));
            mHelp.DropDownItems.Add(L.T("help.donate"),  null, (s,e)=>AppLinks.Open(AppLinks.Donate));
            mHelp.DropDownItems.Add(new ToolStripSeparator());
            mHelp.DropDownItems.Add(L.T("help.about"),   null, (s,e)=>{ using (var d = new AboutDialog()) d.ShowDialog(this); });

            var mVoip = new ToolStripMenuItem("🧰 VoIP");
            mVoip.DropDownItems.Add("🩺 SIP Health Check (1-click)", null, (s, e) => OpenHealthCheck());
            mVoip.DropDownItems.Add(new ToolStripSeparator());
            mVoip.DropDownItems.Add("🔴 Cattura LIVE → pcap",  null, (s, e) => OpenLiveCapture());
            mVoip.DropDownItems.Add("🎧 RTP Player & DTMF",   null, (s, e) => OpenRtpPlayer());
            mVoip.DropDownItems.Add("📡 SIP OPTIONS Monitor", null, (s, e) => OpenOptionsMonitor());
            mVoip.DropDownItems.Add("🔑 SIP Registration Live", null, (s, e) => OpenSipRegister());
            mVoip.DropDownItems.Add("🚀 Generatore traffico (load)", null, (s, e) => OpenTrafficGen());
            mVoip.DropDownItems.Add("🌐 DNS VoIP Analyzer",   null, (s, e) => OpenDnsVoip());
            mVoip.DropDownItems.Add("🛰️ Tester STUN / NAT",   null, (s, e) => OpenStunTester());
            mVoip.DropDownItems.Add("🔥 Firewall port-check",  null, (s, e) => OpenFirewallCheck());
            mVoip.DropDownItems.Add("🧮 Calcolatori VoIP",    null, (s, e) => OpenVoipCalc());

            var mInfo = new ToolStripMenuItem("ℹ️ Info");
            mInfo.Click += (s, e) => ShowNetInfo();

            menu.Items.AddRange(new ToolStripItem[] { mC, mT, mLang, mVoip, mInfo, mHelp });
            MainMenuStrip = menu;

            var tb = new ToolStrip { Dock = DockStyle.Top };
            tb.Items.Add(TBtn(L.T("btn.new_conn"), (s,e)=>AddConn()));
            tb.Items.Add(TBtn(L.T("btn.edit"),     (s,e)=>EditConn()));
            tb.Items.Add(TBtn(L.T("btn.delete"),   (s,e)=>DelConn()));
            tb.Items.Add(new ToolStripSeparator());
            tb.Items.Add(TBtn("▶ SSH",      (s,e)=>Connect()));
            tb.Items.Add(TBtn("SSH ↗",      (s,e)=>ConnectStandalone()));
            tb.Items.Add(TBtn("SCP",        (s,e)=>OpenScp()));
            tb.Items.Add(TBtn("FTP",        (s,e)=>OpenFtp()));
            tb.Items.Add(new ToolStripSeparator());

            var btnPcap = new ToolStripButton(L.T("btn.pcap")) {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(0, 90, 200),   // blu (più leggibile del giallo)
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "Analizza un file PCAP/PCAPNG con tshark (Wireshark)"
            };
            btnPcap.Click += (s, e) => OpenPcapTab();

            var btnServer = new ToolStripButton(L.T("btn.server")) {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(150, 220, 255),
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "Avvia FTP Server o configura SFTP Server (OpenSSH)"
            };
            btnServer.Click += (s, e) => OpenServerManager();

            var btnTransX = new ToolStripButton(L.T("btn.simulator")) {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(255, 200, 100),
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "SIP Simulator, Cause Code Translator, SDP Analyzer (TranslatorX)"
            };
            btnTransX.Click += (s, e) => OpenTranslatorX();

            var btnCiscoDoc = new ToolStripButton(L.T("btn.doc")) {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(100, 200, 255),
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "Documentazione: raccolta link editabile (CUCM SRND, AudioCodes, ...)"
            };
            btnCiscoDoc.Click += (s, e) => OpenDocPanel();

            var btnHomer = new ToolStripButton(L.T("btn.syslog")) {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(180, 230, 130),
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "Server Syslog UDP (AudioCodes/Cisco): ricevi e analizza i log in tempo reale"
            };
            btnHomer.Click += (s, e) => OpenSyslog();

            var btnSbc = new ToolStripButton("🩺 SBC Health") {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(120, 230, 200),
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "Diagnostica trunk/SBC vendor-neutral: SIP OPTIONS, certificato TLS, DNS SRV/A"
            };
            btnSbc.Click += (s, e) => OpenSbcHealth();

            var btnNet = new ToolStripButton("🧰 Net") {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(150, 200, 150),
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "Net Tools: Ping, Traceroute, Port check, MTU, NTP"
            };
            btnNet.Click += (s, e) => OpenNetTools();

            var btnSerial = new ToolStripButton("🔌 Serial") {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(210, 180, 120),
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "Console seriale (COM) per gateway/SBC/router"
            };
            btnSerial.Click += (s, e) => OpenSerial();

            // ── Toolbar raggruppata per categoria ──
            tb.Items.Add(btnPcap); tb.Items.Add(btnSbc); tb.Items.Add(btnHomer); tb.Items.Add(btnNet); tb.Items.Add(btnSerial);
            tb.Items.Add(new ToolStripSeparator());
            tb.Items.Add(btnServer);
            tb.Items.Add(new ToolStripSeparator());
            tb.Items.Add(btnTransX); tb.Items.Add(btnCiscoDoc);

            // Indicatore IP locale (+ VPN) sempre visibile, ancorato a destra
            tslIp = new ToolStripLabel("") {
                Alignment   = ToolStripItemAlignment.Right,
                ForeColor   = Color.FromArgb(120, 230, 200),
                Font        = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText = "IP locale del PC (e IP VPN se una VPN è attiva)"
            };
            tb.Items.Add(tslIp);

            // ── Selettore sessioni/tab aperti (in alto a destra) ──
            tsbSessions = new ToolStripDropDownButton("🗂 Sessioni (1)") {
                Alignment    = ToolStripItemAlignment.Right,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor    = Color.FromArgb(180, 220, 255),
                Font         = new Font("Segoe UI", 9, FontStyle.Bold),
                ToolTipText  = "Tutte le sessioni/schede aperte — clicca per saltare a una",
                DropDownDirection = ToolStripDropDownDirection.Left   // pulsante a destra → apri verso sinistra
            };
            tsbSessions.DropDownOpening += (s, e) => {
                tsbSessions.DropDownItems.Clear();
                var closable = new List<TabPage>();
                foreach (TabPage tp in tabMain.TabPages)
                {
                    var page = tp;   // cattura per la closure
                    var it = new ToolStripMenuItem(page.Text.Trim());
                    it.Checked = (tabMain.SelectedTab == page);
                    it.Click += (s2, e2) => { tabMain.SelectedTab = page; };
                    tsbSessions.DropDownItems.Add(it);
                    if (page != tabLog) closable.Add(page);
                }
                if (closable.Count > 0)
                {
                    tsbSessions.DropDownItems.Add(new ToolStripSeparator());
                    // Voci di chiusura DIRETTE (niente sottomenu: su monitor a destra volava fuori schermo)
                    foreach (var tp in closable)
                    {
                        var page = tp;
                        var ci = new ToolStripMenuItem("✕ Chiudi: " + page.Text.Trim()) { ForeColor = Color.Firebrick };
                        ci.Click += (s2, e2) => CloseTab(page);
                        tsbSessions.DropDownItems.Add(ci);
                    }
                }
            };
            tb.Items.Add(tsbSessions);

            // Tutte le scritte della toolbar in nero (look pulito su barra chiara)
            foreach (ToolStripItem _it in tb.Items) _it.ForeColor = Color.Black;

            // ── Sezione SSH sessione attiva (nel ToolStrip, immune a PuTTY HWND) ──
            tssSsh = new ToolStripSeparator { Visible = false };
            tslSshInfo = new ToolStripLabel("") {
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Visible = false
            };
            tsbAnalyzer = new ToolStripButton("📊 Analizzatore") {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.Yellow, BackColor = Color.FromArgb(120, 80, 0),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Visible = false
            };
            tsbLog = new ToolStripButton("📄 Log") {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.LightGreen, BackColor = Color.FromArgb(20, 70, 20),
                Font = new Font("Segoe UI", 8),
                Visible = false
            };
            tsbDetach = new ToolStripButton("⧉ Stacca") {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.White, BackColor = Color.FromArgb(40, 70, 120),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ToolTipText = "Stacca questa sessione in una finestra separata (per vederne due affiancate)",
                Visible = false
            };
            tsbClose = new ToolStripButton("✕ Chiudi") {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.White, BackColor = Color.FromArgb(160, 30, 30),
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Visible = false
            };
            tsbAnalyzer.Click += (s, e) => { var st = tslSshInfo.Tag as SshTab; if (st != null) st.ToggleAnalyzer(); };
            tsbLog.Click      += (s, e) => { var st = tslSshInfo.Tag as SshTab; if (st != null) st.OpenLog(); };
            tsbDetach.Click   += (s, e) => DetachSession(tabMain.SelectedTab);
            tsbClose.Click    += (s, e) => { var st = tslSshInfo.Tag as SshTab; if (st != null) st.CloseSession(); };
            tb.Items.Add(tssSsh);
            tb.Items.Add(tslSshInfo);
            tb.Items.Add(tsbAnalyzer);
            tb.Items.Add(tsbLog);
            tb.Items.Add(tsbDetach);
            tb.Items.Add(tsbClose);

            // Ordine corretto Dock=Top: aggiungi ToolStrip PRIMA, MenuStrip DOPO
            // → MenuStrip viene processato per primo dal layout engine → va in cima
            Controls.Add(tb);
            Controls.Add(menu);

            var split = new SplitContainer { Dock = DockStyle.Fill };
            mainSplit = split;
            Shown += (s2, e2) =>
            {
                split.Panel1MinSize = 200; split.Panel2MinSize = 400;
                int sd = (layout != null && layout.SplitMain > 200) ? layout.SplitMain : 510;
                try { split.SplitterDistance = sd; } catch { }
            };
            // salva la posizione dello splitter quando l'utente la cambia
            split.SplitterMoved += (s2, e2) => { if (layout != null) layout.SplitMain = split.SplitterDistance; };

            // ── Lista connessioni ────────────────────────────────────────────
            var hdr = new Label { Text = "  " + L.T("menu.connections"), Dock = DockStyle.Top, Height = 24, BackColor = Color.FromArgb(45,85,140), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            lvConn = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false };
            // Trucco WinForms per aumentare l'altezza delle righe: ImageList finto 1×24px
            var rowHeightFix = new ImageList { ImageSize = new Size(1, 24) };
            rowHeightFix.Images.Add(new Bitmap(1, 24));
            lvConn.SmallImageList = rowHeightFix;
            lvConn.Columns.Add(L.T("conn.name"), 210); lvConn.Columns.Add(L.T("conn.host"), 130); lvConn.Columns.Add(L.T("col.port_short"), 40); lvConn.Columns.Add("VPN", 95);
            lvConn.DoubleClick += (s,e) => Connect();
            lvConn.KeyDown     += (s,e) => {
                if(e.Control && e.KeyCode==Keys.Up){ MoveConn(-1); e.Handled=true; return; }
                if(e.Control && e.KeyCode==Keys.Down){ MoveConn(1); e.Handled=true; return; }
                if(e.KeyCode==Keys.Delete)DelConn(); if(e.KeyCode==Keys.F2)EditConn(); if(e.KeyCode==Keys.Return)Connect();
            };
            var ctx = new ContextMenuStrip();
            ctx.Items.Add("▶ " + L.T("conn.ssh_embedded"), null, (s,e)=>Connect());
            ctx.Items.Add("▶ " + L.T("conn.ssh_window"),   null, (s,e)=>ConnectStandalone());
            ctx.Items.Add("SFTP",                          null, (s,e)=>OpenSftp());
            ctx.Items.Add(L.T("conn.scp_transfer"),        null, (s,e)=>OpenScp());
            ctx.Items.Add(L.T("conn.ftp_browser"),         null, (s,e)=>OpenFtp());
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add(L.T("menu.edit")+"...",          null, (s,e)=>EditConn());
            ctx.Items.Add(L.T("menu.delete"),              null, (s,e)=>DelConn());
            ctx.Items.Add(new ToolStripSeparator());
            ctx.Items.Add("▲ " + L.T("conn.move_up"),      null, (s,e)=>MoveConn(-1));
            ctx.Items.Add("▼ " + L.T("conn.move_down"),    null, (s,e)=>MoveConn(1));
            lvConn.ContextMenuStrip = ctx;
            split.Panel1.Controls.Add(hdr);    // Top prima
            split.Panel1.Controls.Add(lvConn); // Fill dopo

            // ── Area destra: solo tabMain (i pulsanti SSH sono nel ToolStrip sopra) ──
            tabMain = new TabControl { Dock = DockStyle.Fill };
            tabMain.SelectedIndexChanged += (s, e) => SyncSshBar();
            // Chiusura tab: tasto centrale = chiudi al volo; tasto destro = menu "Chiudi"
            tabMain.MouseUp += (s, e) => {
                if (e.Button != MouseButtons.Middle && e.Button != MouseButtons.Right) return;
                for (int i = 0; i < tabMain.TabPages.Count; i++)
                    if (tabMain.GetTabRect(i).Contains(e.Location))
                    {
                        var page = tabMain.TabPages[i];
                        if (page == tabLog) return;
                        if (e.Button == MouseButtons.Middle) CloseTab(page);
                        else {
                            var cm = new ContextMenuStrip();
                            if (sshSessions.ContainsKey(page))
                                cm.Items.Add("⧉ Stacca in finestra", null, (s2, e2) => DetachSession(page));
                            cm.Items.Add("✕ Chiudi questa sessione", null, (s2, e2) => CloseTab(page));
                            cm.Show(tabMain, e.Location);
                        }
                        return;
                    }
            };

            tabLog = new TabPage("  Log  ");
            rtbLog = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(20,20,20), ForeColor = Color.LightGreen, Font = new Font("Consolas", 9) };
            var btnClear = new Button { Text = "Pulisci", Dock = DockStyle.Bottom, Height = 26 };
            btnClear.Click += (s,e) => rtbLog.Clear();
            tabLog.Controls.Add(rtbLog); tabLog.Controls.Add(btnClear);
            tabMain.TabPages.Add(tabLog);

            split.Panel2.Controls.Add(tabMain);
            Controls.Add(split);

            var status = new StatusStrip();
            lblVpnStatus   = new ToolStripStatusLabel("VPN: --")     { BorderSides = ToolStripStatusLabelBorderSides.Right };
            lblPuttyStatus = new ToolStripStatusLabel("PuTTY: --")   { BorderSides = ToolStripStatusLabelBorderSides.Right };
            var hint = new ToolStripStatusLabel(L.T("status.hint"));
            // lblVpnStatus rimosso dalla barra su richiesta (resta come campo per UpdateStatus, non mostrato)
            status.Items.AddRange(new ToolStripItem[] { lblPuttyStatus, hint });
            Controls.Add(status);
        }

        ToolStripButton TBtn(string text, EventHandler click)
        {
            var b = new ToolStripButton(text) {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            b.Click += click; return b;
        }

        void RefreshList()
        {
            lvConn.Items.Clear();
            foreach (var c in connections)
            {
                // Nella lista mostra solo il tipo VPN (non il sito intero)
                string vpn = (c.VpnType == "Nessuna" || string.IsNullOrEmpty(c.VpnType)) ? "" : c.VpnType;
                var item = new ListViewItem(new[] { c.Name ?? "", c.Host ?? "", c.Protocol ?? "SSH", vpn });
                item.Tag = c;
                lvConn.Items.Add(item);
            }
            if (lvConn.Columns.Count >= 4)
            {
                lvConn.Columns[0].Width = 210; // Nome
                lvConn.Columns[1].Width = 130; // Host
                lvConn.Columns[2].Width = 42;  // P.
                lvConn.Columns[3].Width = 95;  // VPN
            }
        }

        Connection Sel() { if(lvConn.SelectedItems.Count==0){MessageBox.Show("Seleziona una connessione.","Attenzione",MessageBoxButtons.OK,MessageBoxIcon.Information);return null;} return (Connection)lvConn.SelectedItems[0].Tag; }
        void AddConn()  { using(var f=new EditConnectionForm()) if(f.ShowDialog()==DialogResult.OK){connections.Add(f.Result);ConnectionStore.Save(connections);RefreshList();Log("Aggiunta: "+f.Result.Name);} }
        void EditConn() { var c=Sel();if(c==null)return; using(var f=new EditConnectionForm(c)) if(f.ShowDialog()==DialogResult.OK){connections[connections.IndexOf(c)]=f.Result;ConnectionStore.Save(connections);RefreshList();Log("Modificata: "+f.Result.Name);} }
        void DelConn()  { var c=Sel();if(c==null)return; if(MessageBox.Show("Eliminare \""+c.Name+"\"?","Conferma",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){connections.Remove(c);ConnectionStore.Save(connections);RefreshList();Log("Eliminata: "+c.Name);} }

        // Riordina la connessione selezionata su (-1) o giù (+1) e salva l'ordine
        void MoveConn(int dir)
        {
            if (lvConn.SelectedItems.Count == 0) return;
            var c = lvConn.SelectedItems[0].Tag as Connection;
            if (c == null) return;
            int idx = connections.IndexOf(c);
            int ni = idx + dir;
            if (idx < 0 || ni < 0 || ni >= connections.Count) return;
            connections.RemoveAt(idx);
            connections.Insert(ni, c);
            ConnectionStore.Save(connections);
            RefreshList();
            foreach (ListViewItem it in lvConn.Items)
                if (ReferenceEquals(it.Tag, c)) { it.Selected = true; it.Focused = true; it.EnsureVisible(); break; }
            lvConn.Focus();
        }

        bool EnsureVpn(Connection c)
        {
            if (c.VpnType == "Nessuna" || string.IsNullOrEmpty(c.VpnType)) return true;
            if (VpnManager.CanReach(c.Host, c.Port, 1500)) { Log("Host raggiungibile, VPN skip."); return true; }
            Log("Avvio VPN (" + c.VpnType + ")...");
            using (var f = new VpnConnectForm(c)) { f.ShowDialog(); return f.Success; }
        }

        // HTTP / HTTPS → apre nel browser selezionato
        void OpenWebUrl(Connection c)
        {
            if (!EnsureVpn(c)) { Log("VPN non connessa. Annullato."); return; }
            int defaultPort = c.Protocol == "HTTPS" ? 443 : 80;
            int port = c.Port > 0 ? c.Port : defaultPort;
            string portPart = (port == defaultPort) ? "" : ":" + port;
            string path = (c.WebPath ?? "").TrimStart('/');
            string url = c.Protocol.ToLower() + "://" + c.Host + portPart + (path.Length > 0 ? "/" + path : "");
            Log("Apro " + url + " con " + (c.Browser ?? "Default"));

            string browser = c.Browser ?? "Default";
            if (browser == "Chrome")
            {
                string[] chromePaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),      "Google\\Chrome\\Application\\chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),   "Google\\Chrome\\Application\\chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google\\Chrome\\Application\\chrome.exe"),
                };
                foreach (var cp in chromePaths) if (File.Exists(cp)) { Process.Start(cp, "\"" + url + "\""); return; }
                // fallback
                Process.Start(url);
            }
            else if (browser == "Firefox")
            {
                string[] ffPaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Mozilla Firefox\\firefox.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Firefox\\firefox.exe"),
                };
                foreach (var fp2 in ffPaths) if (File.Exists(fp2)) { Process.Start(fp2, "\"" + url + "\""); return; }
                Process.Start(url);
            }
            else if (browser == "Edge")
            {
                Process.Start("msedge.exe", "\"" + url + "\"");
            }
            else // Default
            {
                Process.Start(url);
            }
        }

        // SSH embedded (PuTTY nel tab)
        void Connect()
        {
            var c = Sel(); if (c == null) return;

            // HTTP / HTTPS → browser, non SSH
            if (c.Protocol == "HTTP" || c.Protocol == "HTTPS") { OpenWebUrl(c); return; }

            string putty = TerminalLauncher.FindPutty();
            if (putty == null) { MessageBox.Show("putty.exe non trovato.\n\nCopia putty.exe nella stessa cartella di NetTerm.exe\noppure in C:\\Program Files\\PuTTY\\","PuTTY mancante",MessageBoxButtons.OK,MessageBoxIcon.Warning); return; }
            if (!EnsureVpn(c)) { Log("VPN non connessa. Annullato."); return; }

            Log("SSH embedded → " + c.Username + "@" + c.Host);
            var tabPage = new TabPage("  " + c.Name + "  ");
            tabMain.TabPages.Add(tabPage);
            tabMain.SelectedTab = tabPage;

            var sshTab = new SshTab(c, putty, tabPage);
            sshSessions[tabPage] = sshTab;

            sshTab.SessionClosed += (s, e) => {
                if (InvokeRequired) BeginInvoke((Action)(() => RemoveSshTab(tabPage, c.Name)));
                else RemoveSshTab(tabPage, c.Name);
            };
            SyncSshBar();
        }

        // ── Server Manager (FTP / SFTP) ───────────────────────────────────────
        ServerManagerForm  serverManagerForm;
        TranslatorXPanel   translatorXForm;
        DocLinksPanel      docForm;
        SyslogServerPanel  syslogForm;
        SbcHealthPanel     sbcHealthForm;
        NetToolsPanel      netToolsForm;
        RtpPlayerPanel     rtpForm;
        OptionsMonitorPanel optMonForm;
        DnsVoipPanel       dnsForm;
        HealthCheckPanel   healthForm;
        FirewallCheckPanel fwForm;
        LiveCapturePanel   liveCapForm;
        SipRegisterPanel   regForm;
        TrafficGenPanel    trafficForm;
        VoipCalcPanel      voipCalcForm;
        StunTesterPanel    stunForm;

        void OpenServerManager()
        {
            try {
                if (serverManagerForm == null || serverManagerForm.IsDisposed)
                    serverManagerForm = new ServerManagerForm();
                serverManagerForm.Show(this); serverManagerForm.BringToFront();
            } catch (Exception ex) {
                MessageBox.Show("Errore Server Manager:\n" + ex.ToString(), "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OpenTranslatorX()
        {
            if (translatorXForm == null || translatorXForm.IsDisposed)
                translatorXForm = new TranslatorXPanel();
            try { translatorXForm.Icon = AppIcon.Shared; } catch { }
            translatorXForm.Show(this); translatorXForm.BringToFront();
        }

        void OpenDocPanel()
        {
            try {
                if (docForm == null || docForm.IsDisposed)
                    docForm = new DocLinksPanel();
                docForm.Show(this); docForm.BringToFront();
            } catch (Exception ex) {
                MessageBox.Show("Errore Doc:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OpenSyslog()
        {
            try {
                if (syslogForm == null || syslogForm.IsDisposed)
                    syslogForm = new SyslogServerPanel();
                syslogForm.Show(this); syslogForm.BringToFront();
            } catch (Exception ex) {
                MessageBox.Show("Errore Syslog:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OpenSbcHealth()
        {
            try {
                if (sbcHealthForm == null || sbcHealthForm.IsDisposed)
                    sbcHealthForm = new SbcHealthPanel();
                try { sbcHealthForm.Icon = AppIcon.Shared; } catch { }
                sbcHealthForm.Show(this); sbcHealthForm.BringToFront();
            } catch (Exception ex) {
                MessageBox.Show("Errore SBC Health:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OpenNetTools()
        {
            try {
                if (netToolsForm == null || netToolsForm.IsDisposed)
                    netToolsForm = new NetToolsPanel();
                try { netToolsForm.Icon = AppIcon.Shared; } catch { }
                netToolsForm.Show(this); netToolsForm.BringToFront();
            } catch (Exception ex) {
                MessageBox.Show("Errore Net Tools:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OpenRtpPlayer()
        {
            try { if (rtpForm == null || rtpForm.IsDisposed) rtpForm = new RtpPlayerPanel();
                  try { rtpForm.Icon = AppIcon.Shared; } catch { } rtpForm.Show(this); rtpForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore RTP Player:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenOptionsMonitor()
        {
            try { if (optMonForm == null || optMonForm.IsDisposed) optMonForm = new OptionsMonitorPanel();
                  try { optMonForm.Icon = AppIcon.Shared; } catch { } optMonForm.Show(this); optMonForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore OPTIONS Monitor:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenVoipCalc()
        {
            try { if (voipCalcForm == null || voipCalcForm.IsDisposed) voipCalcForm = new VoipCalcPanel();
                  try { voipCalcForm.Icon = AppIcon.Shared; } catch { } voipCalcForm.Show(this); voipCalcForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore Calcolatori:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenTrafficGen()
        {
            try { if (trafficForm == null || trafficForm.IsDisposed) trafficForm = new TrafficGenPanel();
                  try { trafficForm.Icon = AppIcon.Shared; } catch { } trafficForm.Show(this); trafficForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore Generatore traffico:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenSipRegister()
        {
            try { if (regForm == null || regForm.IsDisposed) regForm = new SipRegisterPanel();
                  try { regForm.Icon = AppIcon.Shared; } catch { } regForm.Show(this); regForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore SIP Register:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenLiveCapture()
        {
            try { if (liveCapForm == null || liveCapForm.IsDisposed) { liveCapForm = new LiveCapturePanel(); liveCapForm.OnAnalyze = p => OpenPcapFile(p); }
                  try { liveCapForm.Icon = AppIcon.Shared; } catch { } liveCapForm.Show(this); liveCapForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore Cattura LIVE:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenHealthCheck()
        {
            try { if (healthForm == null || healthForm.IsDisposed) healthForm = new HealthCheckPanel();
                  try { healthForm.Icon = AppIcon.Shared; } catch { } healthForm.Show(this); healthForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore Health Check:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenDnsVoip()
        {
            try { if (dnsForm == null || dnsForm.IsDisposed) dnsForm = new DnsVoipPanel();
                  try { dnsForm.Icon = AppIcon.Shared; } catch { } dnsForm.Show(this); dnsForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore DNS Analyzer:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenFirewallCheck()
        {
            try { if (fwForm == null || fwForm.IsDisposed) fwForm = new FirewallCheckPanel();
                  try { fwForm.Icon = AppIcon.Shared; } catch { } fwForm.Show(this); fwForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore Firewall Check:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void OpenStunTester()
        {
            try { if (stunForm == null || stunForm.IsDisposed) stunForm = new StunTesterPanel();
                  try { stunForm.Icon = AppIcon.Shared; } catch { } stunForm.Show(this); stunForm.BringToFront(); }
            catch (Exception ex) { MessageBox.Show("Errore STUN Tester:\n" + ex.Message, "Debug", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void OpenSerial()
        {
            string putty = TerminalLauncher.FindPutty();
            if (putty == null) { MessageBox.Show("putty.exe non trovato.\n\nCopia putty.exe accanto a LosaTermVoip.exe oppure in C:\\Program Files\\PuTTY\\.", "PuTTY mancante", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            using (var d = new SerialConsolePanel())
            {
                try { d.Icon = AppIcon.Shared; } catch { }
                if (d.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(d.SelectedCom))
                {
                    if (d.Standalone)
                    {
                        try {
                            Process.Start(putty, "-serial " + d.SelectedCom + " -sercfg " + d.SelectedBaud + ",8,n,1,N");
                            Log("Seriale (finestra separata) → " + d.SelectedCom + " @ " + d.SelectedBaud);
                        } catch (Exception ex) { MessageBox.Show("Errore avvio PuTTY:\n" + ex.Message, "Console Seriale"); }
                    }
                    else OpenSerialTab(d.SelectedCom, d.SelectedBaud, putty);
                }
            }
        }

        // Apre una console seriale (PuTTY) EMBEDDATA in un tab, come l'SSH
        void OpenSerialTab(string com, string baud, string putty)
        {
            Log("Seriale embedded → " + com + " @ " + baud);
            var tabPage = new TabPage("  " + com + "  ");
            tabMain.TabPages.Add(tabPage);
            tabMain.SelectedTab = tabPage;

            string args = "-serial " + com + " -sercfg " + baud + ",8,n,1,N";
            var sshTab = new SshTab("Seriale " + com + " @ " + baud, "serial_" + com, args, putty, tabPage);
            sshSessions[tabPage] = sshTab;
            sshTab.SessionClosed += (s, e) => {
                if (InvokeRequired) BeginInvoke((Action)(() => RemoveSshTab(tabPage, com)));
                else RemoveSshTab(tabPage, com);
            };
            SyncSshBar();
        }

        void OpenPcapTab()
        {
            // Verifica tshark prima di aprire il dialog
            string tshark = PcapAnalyzer.FindTshark();
            if (tshark == null)
            {
                // Mostra dove abbiamo cercato per aiutare il debug
                string pf  = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string pf86= Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                MessageBox.Show(
                    "tshark.exe non trovato.\n\n" +
                    "Cercato in:\n" +
                    "  • " + pf  + "\\Wireshark\\tshark.exe\n" +
                    "  • " + pf86 + "\\Wireshark\\tshark.exe\n" +
                    "  • Registry HKLM\\SOFTWARE\\Wireshark\n" +
                    "  • PATH di sistema\n\n" +
                    "Wireshark è installato? Se sì, durante l'installazione\n" +
                    "assicurati di aver spuntato \"TShark\".\n\n" +
                    "Download: https://www.wireshark.org/download.html",
                    "tshark mancante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dlg = new OpenFileDialog {
                Title  = "Seleziona file PCAP da analizzare",
                Filter = "PCAP files|*.pcap;*.pcapng;*.cap|Tutti i file|*.*"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                OpenPcapFile(dlg.FileName);
            }
        }

        // Apre un file PCAP specifico in un nuovo tab analizzatore (usato anche da drag&drop)
        public void OpenPcapFile(string pcapFile)
        {
            string tshark = PcapAnalyzer.FindTshark();
            if (tshark == null) { MessageBox.Show("tshark.exe non trovato (installa Wireshark con TShark)."); return; }

            pcapTabCounter++;
            string shortName = Path.GetFileName(pcapFile);
            string tabTitle = "  📦 " + (shortName.Length > 18 ? shortName.Substring(0, 16) + "…" : shortName) + "  ";

            var tabPage = new TabPage(tabTitle) { ToolTipText = pcapFile };
            tabMain.TabPages.Add(tabPage);
            tabMain.SelectedTab = tabPage;

            string tempLog = Path.Combine(Path.GetTempPath(), "netterm_pcap_" + pcapTabCounter + ".log");
            File.WriteAllText(tempLog, "");

            var analyzer = new LogAnalyzerPanel(tempLog) { Dock = DockStyle.Fill };
            tabPage.Controls.Add(analyzer);
            analyzer.AnalyzePcap(tshark, pcapFile);

            tabPage.HandleDestroyed += (s, e) => { try { File.Delete(tempLog); } catch { } };
        }

        // ── Drag & Drop di file PCAP / log sulla finestra ──────────────────────
        void HandleDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        void HandleDragDrop(object sender, DragEventArgs e)
        {
            try
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;
                foreach (var f in files) ProcessDroppedFile(f);
            }
            catch (Exception ex) { Log("Errore drag&drop: " + ex.Message); }
        }

        void ProcessDroppedFile(string file)
        {
            if (!File.Exists(file)) return;
            string ext = Path.GetExtension(file).ToLowerInvariant();
            bool isPcap = (ext == ".pcap" || ext == ".pcapng" || ext == ".cap");

            using (var d = new DropActionDialog(Path.GetFileName(file), isPcap))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                switch (d.Choice)
                {
                    case DropChoice.AnalyzeHere:
                        if (isPcap) OpenPcapFile(file);
                        else OpenLogInSyslog(file);
                        break;
                    case DropChoice.TranslatorX:
                        OpenInTranslatorX(file);
                        break;
                    case DropChoice.Syslog:
                        OpenLogInSyslog(file);
                        break;
                }
            }
        }

        void OpenLogInSyslog(string file)
        {
            // apre il viewer syslog e ci carica il contenuto del file riga per riga
            try {
                if (syslogForm == null || syslogForm.IsDisposed) syslogForm = new SyslogServerPanel();
                syslogForm.Show(this); syslogForm.BringToFront();
                syslogForm.LoadFromFile(file);
            } catch (Exception ex) { Log("Errore apertura log: " + ex.Message); }
        }

        void OpenInTranslatorX(string file)
        {
            string tx = TerminalLauncher.FindTranslatorX();
            if (tx == null)
            {
                MessageBox.Show("TranslatorX.exe non trovato automaticamente.\n\n" +
                    "Cercato in Program Files, Desktop e PATH.\n" +
                    "Apri TranslatorX manualmente e carica il file:\n" + file,
                    "TranslatorX", MessageBoxButtons.OK, MessageBoxIcon.Information);
                try { Process.Start("explorer.exe", "/select,\"" + file + "\""); } catch { }
                return;
            }
            Log("TranslatorX ← " + Path.GetFileName(file) + " (apertura via File>Open)…");
            // L'automazione (Ctrl+O + incolla path + Invio) va eseguita su un thread STA
            var t = new Thread(() =>
            {
                string err;
                bool ok = TranslatorXLauncher.OpenLog(tx, file, out err);
                if (!ok && err != null)
                    BeginInvoke((Action)(() => MessageBox.Show(
                        "Non sono riuscito ad aprire automaticamente il log in TranslatorX:\n" + err +
                        "\n\nAprilo a mano con File > Open:\n" + file, "TranslatorX",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        void OpenClaudeTab()
        {
            // Se il tab Claude esiste già, portalo in primo piano
            if (claudeTabPage != null && tabMain.TabPages.Contains(claudeTabPage))
            {
                tabMain.SelectedTab = claudeTabPage;
                return;
            }

            claudeTabPage = new TabPage("  🤖 Claude AI  ");
            tabMain.TabPages.Add(claudeTabPage);
            tabMain.SelectedTab = claudeTabPage;

            claudeTab = new ClaudeTab(claudeTabPage);

            // Quando si chiude il tab, sgancia Claude (non lo killa)
            claudeTabPage.HandleDestroyed += (s, e) => {
                if (claudeTab != null) claudeTab.Detach();
                claudeTabPage = null; claudeTab = null;
            };
        }

        void RemoveSshTab(TabPage page, string name)
        {
            sshSessions.Remove(page);
            tabMain.TabPages.Remove(page);
            Log("Sessione chiusa: " + name);
            SyncSshBar();
        }

        // Stacca una sessione embedded in una finestra PuTTY separata (PuTTY resta vivo)
        void DetachSession(TabPage page)
        {
            SshTab st;
            if (page == null || page == tabLog || !sshSessions.TryGetValue(page, out st)) return;
            IntPtr h = st.DetachWindow();
            if (h == IntPtr.Zero)
            { MessageBox.Show("La sessione è ancora in avvio: riprova tra un istante.", "Stacca sessione", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            sshSessions.Remove(page);            // togli il tab MA non chiudere PuTTY
            tabMain.TabPages.Remove(page);
            Log("Sessione staccata in finestra separata.");
            SyncSshBar();
        }

        // Chiude un tab/sessione (il tab "Log" non si chiude mai)
        void CloseTab(TabPage page)
        {
            if (page == null || page == tabLog) return;
            SshTab st;
            if (sshSessions.TryGetValue(page, out st)) st.CloseSession();   // killa PuTTY → RemoveSshTab rimuove il tab
            else { tabMain.TabPages.Remove(page); SyncSshBar(); }
        }

        void UpdateSessionsButton()
        {
            if (tsbSessions == null || tabMain == null) return;
            tsbSessions.Text = "🗂 Sessioni (" + tabMain.TabPages.Count + ")";
        }

        void SyncSshBar()
        {
            UpdateSessionsButton();
            var tab = tabMain.SelectedTab;
            SshTab st = null;
            bool hasSession = tab != null && sshSessions.TryGetValue(tab, out st);

            if (hasSession)
            {
                tslSshInfo.Tag  = st;
                tslSshInfo.Text = "● " + st.SessionInfo;
                tssSsh.Visible      = true;
                tslSshInfo.Visible  = true;
                tsbAnalyzer.Visible = true;
                tsbLog.Visible      = true;
                tsbDetach.Visible   = true;
                tsbClose.Visible    = true;
            }
            else
            {
                tslSshInfo.Tag  = null;
                tslSshInfo.Text = "";
                tssSsh.Visible      = false;
                tslSshInfo.Visible  = false;
                tsbAnalyzer.Visible = false;
                tsbLog.Visible      = false;
                tsbDetach.Visible   = false;
                tsbClose.Visible    = false;
            }
        }

        // SSH finestra separata
        void ConnectStandalone()
        {
            var c = Sel(); if (c == null) return;
            if (!EnsureVpn(c)) { Log("VPN non connessa. Annullato."); return; }
            Log("SSH finestra → " + c.Username + "@" + c.Host);
            TerminalLauncher.LaunchSshStandalone(c);
        }

        void OpenSftp()
        {
            var c = Sel(); if (c == null) return;
            if (!EnsureVpn(c)) { Log("VPN non connessa."); return; }
            Log("SFTP → " + c.Host);
            Log("  (Se il terminale si chiude con \"unexpected end-of-file\": il device non ha");
            Log("   un server SFTP attivo — tipico su Cisco IOS. Usa SCP o abilita SFTP sul device.)");
            TerminalLauncher.LaunchSftp(c);
        }
        void OpenScp()  { var c=Sel();if(c==null)return; if(!EnsureVpn(c)){Log("VPN non connessa.");return;} new ScpTransferForm(c).Show(); }
        void OpenFtp()  { var c=Sel();if(c==null)return; if(!EnsureVpn(c)){Log("VPN non connessa.");return;} new FtpBrowserForm(c).Show(); }

        void ShowVpnInfo()  { if(!VpnManager.CheckpointAvailable){MessageBox.Show("Checkpoint non trovato.\nVerifica C:\\Program Files (x86)\\CheckPoint\\Endpoint Connect\\trac.exe","VPN");return;} MessageBox.Show(VpnManager.RunTrac("info"),"VPN Info",MessageBoxButtons.OK,MessageBoxIcon.Information); }
        void OpenVpnGui()   { string g=@"C:\Program Files (x86)\CheckPoint\Endpoint Connect\TrGUI.exe"; if(File.Exists(g))Process.Start(g); else MessageBox.Show("TrGUI.exe non trovato."); }
        void DisconnectWinVpn() { string name=UI.InputBox("Nome connessione Windows VPN:","Disconnetti VPN"); if(!string.IsNullOrEmpty(name))VpnManager.WindowsVpnDisconnect(name,Log); }

        void VpnConnectManual(string vpnType)
        {
            // Crea una connessione temporanea per il form VPN
            var tmp = new Connection { VpnType = vpnType, Name = vpnType };
            if (vpnType == "Checkpoint")
            {
                tmp.VpnSite = UI.InputBox("Nome sito Checkpoint (es: Wurth_VPN):", "Connetti Checkpoint");
                if (string.IsNullOrWhiteSpace(tmp.VpnSite)) return;
            }
            else if (vpnType == "Windows VPN")
            {
                tmp.VpnSite = UI.InputBox("Nome connessione Windows VPN:", "Connetti Windows VPN");
                if (string.IsNullOrWhiteSpace(tmp.VpnSite)) return;
                tmp.VpnUsername = UI.InputBox("Utente (lascia vuoto se non serve):", "Windows VPN", "");
            }
            Log("Avvio VPN " + vpnType + "...");
            using (var f = new VpnConnectForm(tmp)) f.ShowDialog();
            UpdateStatus();
        }
        void ShowPuttyPath() { string p=TerminalLauncher.FindPutty(); string exeDir=Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location); MessageBox.Show(p!=null?"PuTTY trovato:\n"+p+"\n\nAuto-login attivo.":"putty.exe non trovato.\n\nCopia putty.exe in:\n"+exeDir,"PuTTY",MessageBoxButtons.OK,p!=null?MessageBoxIcon.Information:MessageBoxIcon.Warning); }
        void TestReach()
        {
            var c = Sel(); if (c == null) return;
            tabMain.SelectedTab = tabLog;   // mostra il log così l'utente vede l'output
            Log("── Test connettività: " + c.Host + " (porta " + c.Port + ") ──");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                // 1) Ping ICMP
                string pingRes = "✘ no risposta";
                long rtt = -1;
                try
                {
                    using (var ping = new System.Net.NetworkInformation.Ping())
                    {
                        var reply = ping.Send(c.Host, 3000);
                        if (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                        { pingRes = "✔ " + reply.RoundtripTime + " ms"; rtt = reply.RoundtripTime; }
                        else if (reply != null) pingRes = "✘ " + reply.Status;
                    }
                }
                catch (Exception ex) { pingRes = "✘ " + ex.Message; }

                // 2) TCP sulla porta del servizio
                bool tcpOk = VpnManager.CanReach(c.Host, c.Port, 4000);

                BeginInvoke((Action)(() =>
                {
                    Log("  Ping ICMP : " + pingRes);
                    Log("  TCP :" + c.Port + "  : " + (tcpOk ? "✔ aperta" : "✘ chiusa/filtrata"));
                    string summary =
                        "Host: " + c.Host + "\n\n" +
                        "Ping ICMP: " + pingRes + "\n" +
                        "Porta TCP " + c.Port + ": " + (tcpOk ? "✔ raggiungibile" : "✘ non raggiungibile") +
                        (!tcpOk && rtt >= 0 ? "\n\nL'host risponde al ping ma la porta è chiusa/filtrata\n(servizio spento o firewall)." : "") +
                        (rtt < 0 && tcpOk ? "\n\nLa porta risponde ma il ping è bloccato (normale su molti firewall)." : "");
                    MessageBox.Show(summary, "Test connettività",
                        MessageBoxButtons.OK,
                        tcpOk ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }));
            });
        }

        void UpdateStatus()
        {
            string vpn = VpnManager.GetStatus();
            lblVpnStatus.Text = vpn;
            lblVpnStatus.ForeColor = vpn.Contains("Connessa") ? Color.Green : Color.DarkRed;
            string putty = TerminalLauncher.FindPutty();
            lblPuttyStatus.Text = putty != null ? "PuTTY: ✔" : L.T("status.putty_notfound");
            lblPuttyStatus.ForeColor = putty != null ? Color.Green : Color.DarkOrange;

            if (tslIp != null)
            {
                string ip  = GetPrimaryIp();
                string vip = GetVpnIp();
                tslIp.Text = "🖧 IP: " + ip + (vip != null ? "    🔒 VPN: " + vip : "") + "  ";
                tslIp.ForeColor = Color.Black;
            }
        }

        // ── Menu "Info": IP, subnet, gateway, DNS, MAC (+ VPN) ────────────────
        void ShowNetInfo()
        {
            var f = new Form {
                Text = L.B("ℹ️ Info Rete","ℹ️ Network Info"), Size = new Size(440, 300),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(24, 24, 32)
            };
            try { f.Icon = AppIcon.Shared; } catch { }
            var txt = new TextBox {
                Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 15, 22), ForeColor = Color.Gainsboro,
                Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None,
                Text = GetNetInfoText()
            };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.FromArgb(24, 24, 32), Padding = new Padding(6) };
            var btnCopy = new Button { Text = L.B("📋 Copia","📋 Copy"), Dock = DockStyle.Right, Width = 100, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 80, 140) };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += (s, e) => { try { Clipboard.SetText(txt.Text); } catch { } };
            var btnRefresh = new Button { Text = L.B("🔄 Aggiorna","🔄 Refresh"), Dock = DockStyle.Right, Width = 105, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 60, 80) };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => txt.Text = GetNetInfoText();
            bottom.Controls.Add(btnCopy); bottom.Controls.Add(btnRefresh);
            f.Controls.Add(txt); f.Controls.Add(bottom);
            f.ShowDialog(this);
        }

        string GetNetInfoText()
        {
            string primaryIp = GetPrimaryIp();
            string vip = GetVpnIp();
            string iface = "n/d", subnet = "n/d", gw = "n/d", dns = "n/d", mac = "n/d";
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    var p = ni.GetIPProperties();
                    foreach (var ua in p.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (ua.Address.ToString() != primaryIp) continue;
                        iface = ni.Name;
                        try { if (ua.IPv4Mask != null) subnet = ua.IPv4Mask.ToString(); } catch { }
                        var gws = new List<string>();
                        foreach (var g in p.GatewayAddresses)
                            if (g.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork && g.Address.ToString() != "0.0.0.0")
                                gws.Add(g.Address.ToString());
                        if (gws.Count > 0) gw = string.Join(", ", gws.ToArray());
                        var dl = new List<string>();
                        foreach (var d in p.DnsAddresses)
                            if (d.AddressFamily == AddressFamily.InterNetwork) dl.Add(d.ToString());
                        if (dl.Count > 0) dns = string.Join(", ", dl.ToArray());
                        try { mac = FormatMac(ni.GetPhysicalAddress()); } catch { }
                    }
                }
            }
            catch { }
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("  " + L.B("Interfaccia","Interface").PadRight(15) + ": " + iface);
            sb.AppendLine("  " + "IP".PadRight(15) + ": " + primaryIp);
            sb.AppendLine("  " + L.B("Subnet mask","Subnet mask").PadRight(15) + ": " + subnet);
            sb.AppendLine("  " + L.B("Default gateway","Default gateway").PadRight(15) + ": " + gw);
            sb.AppendLine("  " + "DNS".PadRight(15) + ": " + dns);
            sb.AppendLine("  " + "MAC".PadRight(15) + ": " + mac);
            if (vip != null)
                sb.AppendLine("  " + L.B("IP VPN","VPN IP").PadRight(15) + ": " + vip);
            return sb.ToString();
        }

        static string FormatMac(System.Net.NetworkInformation.PhysicalAddress pa)
        {
            if (pa == null) return "n/d";
            byte[] b = pa.GetAddressBytes();
            if (b == null || b.Length == 0) return "n/d";
            var parts = new string[b.Length];
            for (int i = 0; i < b.Length; i++) parts[i] = b[i].ToString("X2");
            return string.Join(":", parts);
        }

        // IP principale del PC (interfaccia usata per uscire verso la rete)
        static string GetPrimaryIp()
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                { s.Connect("8.8.8.8", 65530); return ((IPEndPoint)s.LocalEndPoint).Address.ToString(); }
            }
            catch { return "n/d"; }
        }

        // IP di una eventuale VPN attiva (adattatori Ppp/Tunnel o nomi noti)
        static string GetVpnIp()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    string d = (ni.Name + " " + ni.Description).ToLowerInvariant();
                    bool isVpn = ni.NetworkInterfaceType == NetworkInterfaceType.Ppp
                              || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel
                              || d.Contains("vpn") || d.Contains("checkpoint") || d.Contains("forti")
                              || d.Contains("anyconnect") || d.Contains("openvpn") || d.Contains("wireguard")
                              || d.Contains("tap") || d.Contains("tun") || d.Contains("pangp") || d.Contains("zscaler");
                    if (!isVpn) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork
                            && !ua.Address.ToString().StartsWith("169.254"))
                            return ua.Address.ToString();
                }
            }
            catch { }
            return null;
        }

        void Log(string msg)
        {
            if (rtbLog.InvokeRequired) { BeginInvoke((Action)(() => Log(msg))); return; }
            rtbLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\n");
            rtbLog.ScrollToCaret();
        }

        protected override void OnFormClosed(FormClosedEventArgs e) { vpnTimer.Stop(); base.OnFormClosed(e); }
    }

    // ─── Entry Point ──────────────────────────────────────────────────────────

    static class Program
    {
        // ── Migrazione automatica da NetTerm → LosaTermVoip ──────────────────
        static void MigrateFromNetTerm()
        {
            try
            {
                string appData   = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string newDir    = Path.Combine(appData, "LosaTermVoip");
                string newConn   = Path.Combine(newDir, "connections.json");

                // Se le connessioni LosaTermVoip esistono già → niente da fare
                if (File.Exists(newConn)) return;

                // Cerca connessioni salvate sotto il vecchio nome "NetTerm"
                string oldConn = Path.Combine(appData, "NetTerm", "connections.json");
                if (!File.Exists(oldConn)) return;

                // Crea la cartella destinazione e copia
                if (!Directory.Exists(newDir)) Directory.CreateDirectory(newDir);
                File.Copy(oldConn, newConn, overwrite: false);

                // Copia anche i log se presenti
                string oldLogs = Path.Combine(appData, "NetTerm", "logs");
                string newLogs = Path.Combine(newDir, "logs");
                if (Directory.Exists(oldLogs) && !Directory.Exists(newLogs))
                {
                    Directory.CreateDirectory(newLogs);
                    foreach (var f in Directory.GetFiles(oldLogs))
                        try { File.Copy(f, Path.Combine(newLogs, Path.GetFileName(f)), overwrite: false); } catch { }
                }
            }
            catch { /* migrazione silente — non blocca l'avvio */ }
        }

        [STAThread]
        static void Main()
        {
            // Cattura eccezioni non gestite nei thread di background — evita crash totale
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                try {
                    string msg = e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Errore sconosciuto";
                    File.AppendAllText(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "LosaTermVoip", "crash.log"),
                        "[" + DateTime.Now + "] " + msg + "\r\n\r\n");
                } catch { }
            };
            Application.ThreadException += (s, e) => {
                // Eccezioni UI thread: logga ma non crashare
                try {
                    File.AppendAllText(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "LosaTermVoip", "crash.log"),
                        "[" + DateTime.Now + "] ThreadException: " + e.Exception + "\r\n\r\n");
                } catch { }
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            L.Load();           // carica lingua salvata (default IT)
            MigrateFromNetTerm();
            Application.Run(new MainForm());
        }
    }
}
