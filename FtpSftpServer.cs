using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  FTP SERVER — implementazione pura C#/.NET 4.8, nessun NuGet
    //  Supporta: USER, PASS, PWD, CWD, CDUP, LIST, PASV, RETR, STOR,
    //            DELE, MKD, RMD, RNFR, RNTO, SIZE, TYPE, SYST, FEAT, QUIT
    //  Solo modalità PASV (passive) — funziona anche con NAT/firewall
    // ═══════════════════════════════════════════════════════════════════════════

    public class FtpServer
    {
        // ── Configurazione ────────────────────────────────────────────────────
        public int    Port     { get; private set; }
        public string RootDir  { get; private set; }
        public string Username { get; private set; }
        public bool   AllowAnonymous { get; private set; }
        public bool   ReadOnly { get; private set; }
        public bool   IsRunning { get { return running; } }

        string password;
        TcpListener listener;
        volatile bool running;
        Thread listenThread;

        public event Action<string> LogLine;   // UI log callback
        public event Action<string> ClientConn; // nuova connessione

        // ── Start / Stop ──────────────────────────────────────────────────────
        public void Start(int port, string rootDir, string user, string pass,
                          bool allowAnon = false, bool readOnly = false)
        {
            if (running) return;
            Port     = port;
            RootDir  = rootDir;
            Username = user;
            password = pass;
            AllowAnonymous = allowAnon;
            ReadOnly = readOnly;

            if (!Directory.Exists(rootDir)) Directory.CreateDirectory(rootDir);

            listener     = new TcpListener(IPAddress.Any, port);
            listener.Start();
            running      = true;

            listenThread = new Thread(AcceptLoop) { IsBackground = true, Name = "FtpAccept" };
            listenThread.Start();

            Log("FTP server avviato su porta " + port + " — root: " + rootDir);
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            try { listener.Stop(); } catch { }
            Log("FTP server fermato.");
        }

        // ── Loop accettazione ─────────────────────────────────────────────────
        void AcceptLoop()
        {
            while (running)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    Log("Connessione da " + ((IPEndPoint)client.Client.RemoteEndPoint).Address);
                    if (ClientConn != null) ClientConn(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());
                    ThreadPool.QueueUserWorkItem(_ => HandleSession(client));
                }
                catch { if (running) Log("Errore accept."); }
            }
        }

        // ── Sessione FTP ──────────────────────────────────────────────────────
        void HandleSession(TcpClient ctrl)
        {
            ctrl.ReceiveTimeout = 120000;
            ctrl.SendTimeout    = 30000;
            var ns      = ctrl.GetStream();
            var reader  = new StreamReader(ns, Encoding.ASCII);
            var writer  = new StreamWriter(ns, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };

            bool authenticated = false;
            string cwd         = "/";
            string renameFrom  = null;
            string pendingUser = null;
            TcpListener pasvListener = null;
            int pasvPort = 0;

            Send(writer, "220 LosaTermVoip FTP Server 1.0");

            try
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int sp = line.IndexOf(' ');
                    string cmd  = (sp > 0 ? line.Substring(0, sp) : line).ToUpper().Trim();
                    string arg  = sp > 0 ? line.Substring(sp + 1).Trim() : "";

                    Log("[" + GetRemoteIp(ctrl) + "] " + cmd + (cmd == "PASS" ? " ****" : (arg.Length > 0 ? " " + arg : "")));

                    switch (cmd)
                    {
                        case "USER":
                            pendingUser = arg;
                            if (AllowAnonymous && (arg.ToLower() == "anonymous" || arg.ToLower() == "ftp"))
                            { authenticated = true; Send(writer, "230 Login anonimo OK."); }
                            else
                                Send(writer, "331 Password richiesta per " + arg);
                            break;

                        case "PASS":
                            if (authenticated) { Send(writer, "230 Già autenticato."); break; }
                            if (pendingUser == Username && arg == password)
                            { authenticated = true; Send(writer, "230 Login corretto."); }
                            else
                                Send(writer, "530 Login non corretto.");
                            break;

                        case "QUIT":
                            Send(writer, "221 Arrivederci.");
                            goto done;

                        case "SYST":
                            Send(writer, "215 WINDOWS_NT");
                            break;

                        case "FEAT":
                            writer.WriteLine("211-Features:");
                            writer.WriteLine(" PASV");
                            writer.WriteLine(" SIZE");
                            writer.WriteLine(" UTF8");
                            Send(writer, "211 End");
                            break;

                        case "TYPE":
                            Send(writer, "200 Type impostato a " + arg);
                            break;

                        case "PWD":
                        case "XPWD":
                            if (!CheckAuth(writer, authenticated)) break;
                            Send(writer, "257 \"" + cwd + "\" è la directory corrente.");
                            break;

                        case "CWD":
                        case "XCWD":
                            if (!CheckAuth(writer, authenticated)) break;
                            {
                                string newPath = ResolvePath(cwd, arg);
                                string physNew = PhysPath(RootDir, newPath);
                                if (Directory.Exists(physNew))
                                { cwd = newPath; Send(writer, "250 Directory cambiata in " + cwd); }
                                else
                                    Send(writer, "550 Directory non trovata: " + arg);
                            }
                            break;

                        case "CDUP":
                            if (!CheckAuth(writer, authenticated)) break;
                            {
                                string parent = cwd == "/" ? "/" : cwd.Substring(0, Math.Max(1, cwd.LastIndexOf('/')));
                                if (parent == "") parent = "/";
                                cwd = parent;
                                Send(writer, "200 Directory cambiata in " + cwd);
                            }
                            break;

                        case "PASV":
                            if (!CheckAuth(writer, authenticated)) break;
                            {
                                // Chiudi listener PASV precedente
                                if (pasvListener != null) { try { pasvListener.Stop(); } catch { } }
                                // Trova porta libera
                                pasvPort = FindFreePort();
                                pasvListener = new TcpListener(IPAddress.Any, pasvPort);
                                pasvListener.Start();
                                // Invia IP locale
                                string localIp = GetLocalIp().Replace('.', ',');
                                int p1 = pasvPort / 256, p2 = pasvPort % 256;
                                Send(writer, string.Format("227 Entering Passive Mode ({0},{1},{2}).", localIp, p1, p2));
                            }
                            break;

                        case "LIST":
                        case "NLST":
                            if (!CheckAuth(writer, authenticated)) break;
                            {
                                string listPath = arg.Length > 0 ? PhysPath(RootDir, ResolvePath(cwd, arg))
                                                                  : PhysPath(RootDir, cwd);
                                var data = OpenDataConn(writer, pasvListener);
                                if (data == null) break;
                                Send(writer, "150 Apertura connessione dati.");
                                try
                                {
                                    var sb = new StringBuilder();
                                    if (Directory.Exists(listPath))
                                    {
                                        foreach (var d in Directory.GetDirectories(listPath))
                                        {
                                            var di = new DirectoryInfo(d);
                                            sb.AppendFormat("drwxr-xr-x 1 user group {0,12} {1} {2}\r\n",
                                                0, di.LastWriteTime.ToString("MMM dd HH:mm"), di.Name);
                                        }
                                        foreach (var f in Directory.GetFiles(listPath))
                                        {
                                            var fi = new FileInfo(f);
                                            sb.AppendFormat("-rw-r--r-- 1 user group {0,12} {1} {2}\r\n",
                                                fi.Length, fi.LastWriteTime.ToString("MMM dd HH:mm"), fi.Name);
                                        }
                                    }
                                    byte[] buf = Encoding.UTF8.GetBytes(sb.ToString());
                                    data.Write(buf, 0, buf.Length);
                                    data.Close();
                                    Send(writer, "226 Transfer OK.");
                                }
                                catch (Exception ex) { data.Close(); Send(writer, "550 Errore: " + ex.Message); }
                            }
                            break;

                        case "RETR":
                            if (!CheckAuth(writer, authenticated)) break;
                            {
                                string filePath = PhysPath(RootDir, ResolvePath(cwd, arg));
                                if (!File.Exists(filePath)) { Send(writer, "550 File non trovato."); break; }
                                var data = OpenDataConn(writer, pasvListener);
                                if (data == null) break;
                                Send(writer, "150 Apertura connessione dati per " + arg);
                                try
                                {
                                    using (var fs = File.OpenRead(filePath))
                                        fs.CopyTo(data);
                                    data.Close();
                                    Send(writer, "226 Transfer completato.");
                                    Log("RETR " + arg + " (" + new FileInfo(filePath).Length + " byte)");
                                }
                                catch (Exception ex) { data.Close(); Send(writer, "550 Errore: " + ex.Message); }
                            }
                            break;

                        case "STOR":
                            if (!CheckAuth(writer, authenticated)) break;
                            if (ReadOnly) { Send(writer, "550 Server in sola lettura."); break; }
                            {
                                string filePath = PhysPath(RootDir, ResolvePath(cwd, arg));
                                var data = OpenDataConn(writer, pasvListener);
                                if (data == null) break;
                                Send(writer, "150 Pronto a ricevere " + arg);
                                try
                                {
                                    using (var fs = File.Create(filePath))
                                        data.CopyTo(fs);
                                    data.Close();
                                    Send(writer, "226 Upload completato.");
                                    Log("STOR " + arg + " (" + new FileInfo(filePath).Length + " byte)");
                                }
                                catch (Exception ex) { data.Close(); Send(writer, "550 Errore: " + ex.Message); }
                            }
                            break;

                        case "DELE":
                            if (!CheckAuth(writer, authenticated)) break;
                            if (ReadOnly) { Send(writer, "550 Server in sola lettura."); break; }
                            {
                                string fp = PhysPath(RootDir, ResolvePath(cwd, arg));
                                if (File.Exists(fp)) { File.Delete(fp); Send(writer, "250 File eliminato."); }
                                else Send(writer, "550 File non trovato.");
                            }
                            break;

                        case "MKD":
                        case "XMKD":
                            if (!CheckAuth(writer, authenticated)) break;
                            if (ReadOnly) { Send(writer, "550 Server in sola lettura."); break; }
                            {
                                string dp = PhysPath(RootDir, ResolvePath(cwd, arg));
                                Directory.CreateDirectory(dp);
                                Send(writer, "257 \"" + ResolvePath(cwd, arg) + "\" creata.");
                            }
                            break;

                        case "RMD":
                        case "XRMD":
                            if (!CheckAuth(writer, authenticated)) break;
                            if (ReadOnly) { Send(writer, "550 Server in sola lettura."); break; }
                            {
                                string dp = PhysPath(RootDir, ResolvePath(cwd, arg));
                                if (Directory.Exists(dp)) { Directory.Delete(dp, recursive: false); Send(writer, "250 Directory eliminata."); }
                                else Send(writer, "550 Directory non trovata.");
                            }
                            break;

                        case "RNFR":
                            if (!CheckAuth(writer, authenticated)) break;
                            if (ReadOnly) { Send(writer, "550 Server in sola lettura."); break; }
                            renameFrom = PhysPath(RootDir, ResolvePath(cwd, arg));
                            Send(writer, "350 File sorgente OK, inserire RNTO.");
                            break;

                        case "RNTO":
                            if (!CheckAuth(writer, authenticated)) break;
                            if (ReadOnly || renameFrom == null) { Send(writer, "503 RNFR prima."); break; }
                            {
                                string renameTo = PhysPath(RootDir, ResolvePath(cwd, arg));
                                if (File.Exists(renameFrom)) { File.Move(renameFrom, renameTo); Send(writer, "250 Rinominato."); }
                                else if (Directory.Exists(renameFrom)) { Directory.Move(renameFrom, renameTo); Send(writer, "250 Rinominato."); }
                                else Send(writer, "550 Sorgente non trovata.");
                                renameFrom = null;
                            }
                            break;

                        case "SIZE":
                            if (!CheckAuth(writer, authenticated)) break;
                            {
                                string fp = PhysPath(RootDir, ResolvePath(cwd, arg));
                                if (File.Exists(fp)) Send(writer, "213 " + new FileInfo(fp).Length);
                                else Send(writer, "550 File non trovato.");
                            }
                            break;

                        case "NOOP":
                            Send(writer, "200 OK");
                            break;

                        case "OPTS":
                            Send(writer, "200 OK");
                            break;

                        default:
                            Send(writer, "502 Comando non implementato: " + cmd);
                            break;
                    }
                }
            }
            catch { /* client disconnesso */ }
            finally
            {
                if (pasvListener != null) try { pasvListener.Stop(); } catch { }
                try { ctrl.Close(); } catch { }
                Log("Sessione chiusa da " + GetRemoteIp(ctrl));
            }
            done:;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static bool CheckAuth(StreamWriter w, bool auth)
        {
            if (!auth) { Send(w, "530 Non autenticato."); return false; }
            return true;
        }

        static void Send(StreamWriter w, string msg)
        {
            try { w.WriteLine(msg); } catch { }
        }

        static Stream OpenDataConn(StreamWriter writer, TcpListener pasvListener)
        {
            if (pasvListener == null) { Send(writer, "425 Usa prima PASV."); return null; }
            try
            {
                pasvListener.Server.ReceiveTimeout = 10000;
                var dc = pasvListener.AcceptTcpClient();
                pasvListener.Stop();
                return dc.GetStream();
            }
            catch { Send(writer, "425 Impossibile aprire connessione dati."); return null; }
        }

        // Risolve path FTP relativo → path FTP assoluto
        static string ResolvePath(string cwd, string arg)
        {
            if (string.IsNullOrEmpty(arg)) return cwd;
            if (arg.StartsWith("/")) return NormalizeFtpPath(arg);
            string combined = cwd.TrimEnd('/') + "/" + arg;
            return NormalizeFtpPath(combined);
        }

        static string NormalizeFtpPath(string path)
        {
            var parts = new List<string>();
            foreach (var p in path.Split('/'))
            {
                if (p == "" || p == ".") continue;
                if (p == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                else parts.Add(p);
            }
            return "/" + string.Join("/", parts.ToArray());
        }

        // Converte path FTP assoluto → path fisico Windows
        static string PhysPath(string root, string ftpPath)
        {
            string rel = ftpPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(root, rel));
            // Sicurezza: impedisce path traversal fuori dalla root
            if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                return root;
            return full;
        }

        static int FindFreePort()
        {
            var tmp = new TcpListener(IPAddress.Loopback, 0);
            tmp.Start();
            int p = ((IPEndPoint)tmp.LocalEndpoint).Port;
            tmp.Stop();
            return p;
        }

        static string GetLocalIp()
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    s.Connect("8.8.8.8", 65530);
                    return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
                }
            }
            catch { return "127.0.0.1"; }
        }

        static string GetRemoteIp(TcpClient c)
        {
            try { return ((IPEndPoint)c.Client.RemoteEndPoint).Address.ToString(); }
            catch { return "?"; }
        }

        void Log(string msg)
        {
            if (LogLine != null) LogLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SERVER MANAGER FORM  —  UI per FTP server + SFTP (OpenSSH Windows)
    // ═══════════════════════════════════════════════════════════════════════════

    public class ServerManagerForm : Form
    {
        FtpServer ftpServer = new FtpServer();
        TftpServer tftpServer = new TftpServer();

        // TFTP controls
        TextBox txtTftpPort, txtTftpRoot;
        Button btnTftpStart, btnTftpStop;
        ListBox lbTftpLog;
        Label lblTftpStatus;

        // DHCP (lab) controls
        DhcpServer dhcpServer = new DhcpServer();
        ComboBox cmbDhcpIf;
        TextBox txtDhcpStart, txtDhcpEnd, txtDhcpMask, txtDhcpGw, txtDhcpDns, txtDhcpTftp, txtDhcpLease;
        Button btnDhcpStart, btnDhcpStop;
        ListBox lbDhcpLog;
        Label lblDhcpStatus;

        // FTP controls
        NumericUpDown numFtpPort;
        TextBox txtFtpRoot, txtFtpUser, txtFtpPass;
        CheckBox chkFtpAnon, chkFtpReadOnly;
        Button btnFtpStart, btnFtpStop, btnFtpBrowse;
        ListBox lbFtpLog;
        Label lblFtpStatus;

        // SFTP/OpenSSH controls
        Label lblSshStatus;
        Button btnSshInstall, btnSshStart, btnSshStop, btnSshConfig, btnCreateUser;
        TextBox txtSshLog, txtNewUser, txtNewPass;

        // Tab
        TabControl tabs;

        public ServerManagerForm()
        {
            Text      = "LosaTermVoip — Server Manager (FTP / SFTP)";
            Size      = new Size(720, 770);
            MinimumSize = new Size(640, 600);
            BackColor = Color.FromArgb(24, 24, 32);
            ForeColor = Color.White;
            Font      = new Font("Segoe UI", 9);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUI();

            ftpServer.LogLine   += msg => SafeLog(lbFtpLog, msg);
            ftpServer.ClientConn += ip => SafeLog(lbFtpLog, ">>> Client connesso: " + ip);
            tftpServer.LogLine  += msg => SafeLog(lbTftpLog, msg);
            dhcpServer.LogLine  += msg => SafeLog(lbDhcpLog, msg);

            FormClosed += (s, e) => {
                if (ftpServer.IsRunning) ftpServer.Stop();
                if (tftpServer.IsRunning) tftpServer.Stop();
                if (dhcpServer.IsRunning) dhcpServer.Stop();
            };
        }

        void BuildUI()
        {
            tabs = new TabControl { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30,30,40) };

            tabs.TabPages.Add(BuildFtpTab());
            tabs.TabPages.Add(BuildSftpTab());
            tabs.TabPages.Add(BuildTftpTab());
            tabs.TabPages.Add(BuildDhcpTab());

            Controls.Add(tabs);
        }

        // ── Tab FTP ───────────────────────────────────────────────────────────
        TabPage BuildFtpTab()
        {
            var page = new TabPage(L.T("ftp.title")) { BackColor = Color.FromArgb(22,22,32), ForeColor = Color.White, Padding = new Padding(8) };

            // Config panel
            var cfg = new Panel { Dock = DockStyle.Top, Height = 210, BackColor = Color.FromArgb(30,30,45), Padding = new Padding(12) };
            page.Controls.Add(cfg);

            int y = 14;
            cfg.Controls.Add(DarkLabel(L.T("ftp.port"), 12, y));
            numFtpPort = new NumericUpDown { Location = new Point(120, y-2), Width=70, Minimum=1, Maximum=65535, Value=21,
                BackColor=Color.FromArgb(45,45,60), ForeColor=Color.White };
            cfg.Controls.Add(numFtpPort);

            chkFtpAnon = new CheckBox { Text=L.T("ftp.anon"), Location=new Point(200, y), ForeColor=Color.LightGray };
            cfg.Controls.Add(chkFtpAnon);
            chkFtpReadOnly = new CheckBox { Text=L.T("ftp.readonly"), Location=new Point(300, y), ForeColor=Color.LightGray };
            cfg.Controls.Add(chkFtpReadOnly);

            y += 34;
            cfg.Controls.Add(DarkLabel(L.T("ftp.root"), 12, y));
            txtFtpRoot = DarkTextBox(120, y-2, 350);
            txtFtpRoot.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "FtpRoot");
            cfg.Controls.Add(txtFtpRoot);
            btnFtpBrowse = DarkButton("📁", 478, y-2, 36);
            btnFtpBrowse.Click += (s,e) => {
                using (var d = new FolderBrowserDialog { SelectedPath = txtFtpRoot.Text })
                    if (d.ShowDialog() == DialogResult.OK) txtFtpRoot.Text = d.SelectedPath;
            };
            cfg.Controls.Add(btnFtpBrowse);

            y += 34;
            cfg.Controls.Add(DarkLabel(L.T("ftp.user"), 12, y));
            txtFtpUser = DarkTextBox(120, y-2, 160);
            txtFtpUser.Text = "ftpuser";
            cfg.Controls.Add(txtFtpUser);

            y += 34;
            cfg.Controls.Add(DarkLabel(L.T("ftp.pass"), 12, y));
            txtFtpPass = DarkTextBox(120, y-2, 160);
            txtFtpPass.PasswordChar = '●';
            cfg.Controls.Add(txtFtpPass);

            y += 40;
            btnFtpStart = DarkButton(L.T("ftp.start"), 12, y, 160, Color.FromArgb(30,100,30));
            btnFtpStart.Click += BtnFtpStart_Click;
            cfg.Controls.Add(btnFtpStart);

            btnFtpStop = DarkButton(L.T("ftp.stop"), 180, y, 90, Color.FromArgb(120,30,30));
            btnFtpStop.Enabled = false;
            btnFtpStop.Click += (s,e) => {
                ftpServer.Stop();
                btnFtpStart.Enabled = true; btnFtpStop.Enabled = false;
                lblFtpStatus.Text = L.T("ftp.stopped"); lblFtpStatus.ForeColor = Color.Gray;
            };
            cfg.Controls.Add(btnFtpStop);

            lblFtpStatus = new Label { Text=L.T("ftp.stopped"), Location=new Point(280, y+4), Width=300,
                ForeColor=Color.Gray, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            cfg.Controls.Add(lblFtpStatus);

            // Log
            var lblLog = new Label { Text=L.T("ftp.log"), Dock=DockStyle.Top, Height=22, ForeColor=Color.LightGray, Padding=new Padding(4,4,0,0) };
            page.Controls.Add(lblLog);

            lbFtpLog = new ListBox { Dock=DockStyle.Fill, BackColor=Color.FromArgb(12,12,20),
                ForeColor=Color.LimeGreen, Font=new Font("Consolas",8), BorderStyle=BorderStyle.None,
                HorizontalScrollbar=true };
            page.Controls.Add(lbFtpLog);

            // ordine docking
            page.Controls.SetChildIndex(cfg, 0);
            page.Controls.SetChildIndex(lblLog, 1);
            page.Controls.SetChildIndex(lbFtpLog, 2);

            return page;
        }

        void BtnFtpStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFtpRoot.Text))
            { MessageBox.Show("Inserire la cartella root FTP.", "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (!chkFtpAnon.Checked && string.IsNullOrWhiteSpace(txtFtpUser.Text))
            { MessageBox.Show("Inserire un nome utente FTP.", "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            try
            {
                ftpServer.Start((int)numFtpPort.Value, txtFtpRoot.Text, txtFtpUser.Text, txtFtpPass.Text,
                                chkFtpAnon.Checked, chkFtpReadOnly.Checked);
                btnFtpStart.Enabled = false; btnFtpStop.Enabled = true;
                lblFtpStatus.Text   = L.T("ftp.running") + (int)numFtpPort.Value;
                lblFtpStatus.ForeColor = Color.LimeGreen;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossibile avviare FTP server:\n" + ex.Message, "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Tab TFTP ──────────────────────────────────────────────────────────
        TabPage BuildTftpTab()
        {
            var page = new TabPage(L.T("tftp.title")) { BackColor = Color.FromArgb(22,22,32), ForeColor = Color.White, Padding = new Padding(8) };

            var cfg = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(30,30,45), Padding = new Padding(12) };
            page.Controls.Add(cfg);

            int y = 14;
            cfg.Controls.Add(DarkLabel(L.T("tftp.port"), 12, y));
            txtTftpPort = DarkTextBox(120, y-2, 70);
            txtTftpPort.Text = "69";
            cfg.Controls.Add(txtTftpPort);

            lblTftpStatus = new Label { Text = L.T("ftp.stopped"), Location = new Point(210, y), Width = 300,
                ForeColor = Color.Gray, Font = new Font("Segoe UI",9,FontStyle.Bold) };
            cfg.Controls.Add(lblTftpStatus);

            y += 34;
            cfg.Controls.Add(DarkLabel(L.T("ftp.root"), 12, y));
            txtTftpRoot = DarkTextBox(120, y-2, 350);
            txtTftpRoot.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TftpRoot");
            cfg.Controls.Add(txtTftpRoot);
            var btnBrowse = DarkButton("📁", 478, y-2, 36);
            btnBrowse.Click += (s,e) => { using (var d = new FolderBrowserDialog { SelectedPath = txtTftpRoot.Text }) if (d.ShowDialog() == DialogResult.OK) txtTftpRoot.Text = d.SelectedPath; };
            cfg.Controls.Add(btnBrowse);

            y += 40;
            btnTftpStart = DarkButton(L.T("sys.start"), 12, y, 150, Color.FromArgb(30,100,30));
            btnTftpStart.Click += BtnTftpStart_Click;
            cfg.Controls.Add(btnTftpStart);
            btnTftpStop = DarkButton(L.T("sys.stop"), 170, y, 90, Color.FromArgb(120,30,30));
            btnTftpStop.Enabled = false;
            btnTftpStop.Click += (s,e) => {
                tftpServer.Stop();
                btnTftpStart.Enabled = true; btnTftpStop.Enabled = false;
                lblTftpStatus.Text = L.T("ftp.stopped"); lblTftpStatus.ForeColor = Color.Gray;
            };
            cfg.Controls.Add(btnTftpStop);

            var hint = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft, Text = L.T("tftp.hint") };
            var lblLog = new Label { Text = L.T("ftp.log"), Dock = DockStyle.Top, Height = 22,
                ForeColor = Color.LightGray, Padding = new Padding(4,4,0,0) };
            lbTftpLog = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12,12,20),
                ForeColor = Color.LimeGreen, Font = new Font("Consolas",8), BorderStyle = BorderStyle.None, HorizontalScrollbar = true };

            page.Controls.Add(lbTftpLog);
            page.Controls.Add(hint);
            page.Controls.Add(lblLog);
            page.Controls.Add(cfg);
            page.Controls.SetChildIndex(cfg,    0);
            page.Controls.SetChildIndex(lblLog, 1);
            page.Controls.SetChildIndex(lbTftpLog, 2);
            return page;
        }

        void BtnTftpStart_Click(object sender, EventArgs e)
        {
            int port; if (!int.TryParse(txtTftpPort.Text.Trim(), out port)) port = 69;
            if (string.IsNullOrWhiteSpace(txtTftpRoot.Text))
            { MessageBox.Show("Inserire la cartella root TFTP.", "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                tftpServer.Start(port, txtTftpRoot.Text);
                btnTftpStart.Enabled = false; btnTftpStop.Enabled = true;
                lblTftpStatus.Text = L.T("ftp.running") + port;
                lblTftpStatus.ForeColor = Color.LimeGreen;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossibile avviare il TFTP su UDP/" + port + ":\n" + ex.Message +
                    "\n\nLe porte <1024 (come la 69) richiedono privilegi: avvia l'app come amministratore, oppure usa la 6969.",
                    "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Tab DHCP (LAB) ────────────────────────────────────────────────────
        TabPage BuildDhcpTab()
        {
            var page = new TabPage("🌐  DHCP (lab)") { BackColor = Color.FromArgb(22,22,32), ForeColor = Color.White, Padding = new Padding(8) };

            var cfg = new Panel { Dock = DockStyle.Top, Height = 252, BackColor = Color.FromArgb(30,30,45), Padding = new Padding(12) };
            page.Controls.Add(cfg);

            // Banner rosso fisso
            cfg.Controls.Add(new Label {
                Text = "  ⚠  DHCP LAB — solo reti di laboratorio isolate, MAI in produzione!",
                Location = new Point(8, 6), Width = 660, Height = 26,
                BackColor = Color.FromArgb(120,20,20), ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });

            int y = 42;
            cfg.Controls.Add(DLbl("Interfaccia:", 12, y+2));
            cmbDhcpIf = new ComboBox { Location = new Point(126, y-2), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45,55,80), ForeColor = Color.White };
            cfg.Controls.Add(cmbDhcpIf);
            lblDhcpStatus = new Label { Text = "● Fermo", Location = new Point(382, y+2), AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI",9,FontStyle.Bold) };
            cfg.Controls.Add(lblDhcpStatus);

            y += 34;
            cfg.Controls.Add(DLbl("Pool da:", 12, y+2));
            txtDhcpStart = DarkTextBox(126, y-2, 110); cfg.Controls.Add(txtDhcpStart);
            cfg.Controls.Add(DLbl("a:", 246, y+2));
            txtDhcpEnd = DarkTextBox(266, y-2, 110); cfg.Controls.Add(txtDhcpEnd);

            y += 34;
            cfg.Controls.Add(DLbl("Subnet:", 12, y+2));
            txtDhcpMask = DarkTextBox(126, y-2, 120); cfg.Controls.Add(txtDhcpMask);
            cfg.Controls.Add(DLbl("Gateway:", 300, y+2));
            txtDhcpGw = DarkTextBox(380, y-2, 120); cfg.Controls.Add(txtDhcpGw);

            y += 34;
            cfg.Controls.Add(DLbl("DNS:", 12, y+2));
            txtDhcpDns = DarkTextBox(126, y-2, 120); txtDhcpDns.Text = "8.8.8.8"; cfg.Controls.Add(txtDhcpDns);
            cfg.Controls.Add(DLbl("TFTP (opt.150):", 300, y+2));
            txtDhcpTftp = DarkTextBox(410, y-2, 100); cfg.Controls.Add(txtDhcpTftp);

            y += 34;
            cfg.Controls.Add(DLbl("Lease (s):", 12, y+2));
            txtDhcpLease = DarkTextBox(126, y-2, 70); txtDhcpLease.Text = "3600"; cfg.Controls.Add(txtDhcpLease);

            y += 36;
            btnDhcpStart = DarkButton("▶ Avvia DHCP (conferma)", 12, y, 200, Color.FromArgb(120,70,20));
            btnDhcpStart.Click += BtnDhcpStart_Click;
            cfg.Controls.Add(btnDhcpStart);
            btnDhcpStop = DarkButton("■ Ferma", 220, y, 90, Color.FromArgb(120,30,30));
            btnDhcpStop.Enabled = false;
            btnDhcpStop.Click += (s,e) => {
                dhcpServer.Stop();
                btnDhcpStart.Enabled = true; btnDhcpStop.Enabled = false;
                lblDhcpStatus.Text = "● Fermo"; lblDhcpStatus.ForeColor = Color.Gray;
            };
            cfg.Controls.Add(btnDhcpStop);

            // popola interfacce + auto-fill alla selezione
            cmbDhcpIf.SelectedIndexChanged += (s,e) => {
                if (cmbDhcpIf.SelectedItem == null) return;
                var parts = ((string)cmbDhcpIf.SelectedItem).Split(new[] { " / " }, StringSplitOptions.None);
                string ip = parts[0]; string m = parts.Length > 1 ? parts[1] : "255.255.255.0";
                txtDhcpMask.Text = m;
                var b = ip.Split('.');
                if (b.Length == 4) {
                    string pfx = b[0] + "." + b[1] + "." + b[2] + ".";
                    txtDhcpStart.Text = pfx + "100";
                    txtDhcpEnd.Text   = pfx + "110";
                    txtDhcpGw.Text    = pfx + "1";
                }
                txtDhcpTftp.Text = ip;   // di default il TFTP siamo noi
            };
            PopulateDhcpInterfaces();
            if (cmbDhcpIf.Items.Count > 0) cmbDhcpIf.SelectedIndex = 0;

            var lblLog = new Label { Text = L.T("ftp.log"), Dock = DockStyle.Top, Height = 22, ForeColor = Color.LightGray, Padding = new Padding(4,4,0,0) };
            lbDhcpLog = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12,12,20), ForeColor = Color.LimeGreen, Font = new Font("Consolas",8), BorderStyle = BorderStyle.None, HorizontalScrollbar = true };

            page.Controls.Add(lbDhcpLog);
            page.Controls.Add(lblLog);
            page.Controls.Add(cfg);
            page.Controls.SetChildIndex(cfg, 0);
            page.Controls.SetChildIndex(lblLog, 1);
            page.Controls.SetChildIndex(lbDhcpLog, 2);
            return page;
        }

        void PopulateDhcpInterfaces()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (ua.Address.ToString().StartsWith("169.254")) continue;
                        string m = (ua.IPv4Mask != null) ? ua.IPv4Mask.ToString() : "255.255.255.0";
                        cmbDhcpIf.Items.Add(ua.Address + " / " + m);
                    }
                }
            }
            catch { }
        }

        void BtnDhcpStart_Click(object sender, EventArgs e)
        {
            if (cmbDhcpIf.SelectedItem == null) { MessageBox.Show("Seleziona l'interfaccia di rete.", "DHCP"); return; }
            string localIp = ((string)cmbDhcpIf.SelectedItem).Split(new[] { " / " }, StringSplitOptions.None)[0];

            if (MessageBox.Show(
                "⚠ ATTENZIONE — stai per avviare un SERVER DHCP su " + localIp + ".\n\n" +
                "Un DHCP non autorizzato su una rete di PRODUZIONE può causare GRAVI disservizi " +
                "(IP errati, conflitti, down della LAN, telefoni che non registrano).\n\n" +
                "Usalo SOLO su una rete di laboratorio ISOLATA.\n\nVuoi davvero avviarlo?",
                "DHCP LAB — conferma pericolo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            int lease; if (!int.TryParse(txtDhcpLease.Text.Trim(), out lease) || lease < 60) lease = 3600;
            try
            {
                dhcpServer.Start(localIp, txtDhcpStart.Text.Trim(), txtDhcpEnd.Text.Trim(), txtDhcpMask.Text.Trim(),
                    txtDhcpGw.Text.Trim(), txtDhcpDns.Text.Trim(), txtDhcpTftp.Text.Trim(), lease);
                btnDhcpStart.Enabled = false; btnDhcpStop.Enabled = true;
                lblDhcpStatus.Text = "● ATTIVO (LAB)"; lblDhcpStatus.ForeColor = Color.OrangeRed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossibile avviare il DHCP (UDP/67):\n" + ex.Message +
                    "\n\nLa porta 67 richiede privilegi: avvia l'app come amministratore, e assicurati che nessun altro DHCP usi la 67.",
                    "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Tab SFTP (OpenSSH Windows) ────────────────────────────────────────
        TabPage BuildSftpTab()
        {
            var page = new TabPage(L.T("sftp.title")) { BackColor=Color.FromArgb(22,22,32), ForeColor=Color.White, Padding=new Padding(8) };

            var info = new Panel { Dock=DockStyle.Fill, BackColor=Color.FromArgb(30,30,45), Padding=new Padding(16), AutoScroll=true };

            int y = 12;
            AddLabel(info, L.T("sftp.intro1"), 12, y, Color.LightGray);
            y += 22;
            AddLabel(info, L.T("sftp.intro2"), 12, y, Color.Gray);
            y += 18;
            AddLabel(info, L.T("sftp.intro3"), 12, y, Color.Gray);

            y += 30;
            lblSshStatus = new Label { Text=L.T("sftp.checking"), Location=new Point(12, y), Width=480,
                Font=new Font("Segoe UI",9,FontStyle.Bold), ForeColor=Color.Yellow };
            info.Controls.Add(lblSshStatus);

            y += 28;
            btnSshInstall = DarkButton(L.T("sftp.install"), 12, y, 210, Color.FromArgb(20,70,120));
            btnSshInstall.Click += BtnSshInstall_Click;
            info.Controls.Add(btnSshInstall);

            y += 32;
            btnSshStart = DarkButton(L.T("sftp.start"), 12, y, 180, Color.FromArgb(30,100,30));
            btnSshStart.Click += BtnSshStart_Click;
            info.Controls.Add(btnSshStart);

            btnSshStop = DarkButton(L.T("sftp.stop"), 200, y, 180, Color.FromArgb(120,30,30));
            btnSshStop.Click += BtnSshStop_Click;
            info.Controls.Add(btnSshStop);

            y += 32;
            btnSshConfig = DarkButton(L.T("sftp.autostart"), 12, y, 260, Color.FromArgb(80,60,20));
            btnSshConfig.Click += BtnSshAutoStart_Click;
            info.Controls.Add(btnSshConfig);

            var btnCheck = DarkButton(L.T("sftp.refresh"), 280, y, 160);
            btnCheck.Click += (s,e) => RefreshSshStatus();
            info.Controls.Add(btnCheck);

            // ── Parametri di connessione (OpenSSH usa l'account Windows) ──────────
            y += 30;
            AddLabel(info, L.T("sftp.conn_params"), 12, y, Color.FromArgb(120, 180, 255));
            string winUser = Environment.UserDomainName + "\\" + Environment.UserName;
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            y += 22; AddLabel(info, "Host: " + GetLocalIp() + "      Porta: 22", 12, y, Color.Gainsboro);
            y += 18; AddLabel(info, "Utente: " + winUser + "   (il tuo account Windows — oppure creane uno dedicato sotto)", 12, y, Color.Gainsboro);
            y += 18; AddLabel(info, "Password: la password Windows di quell'account", 12, y, Color.Gainsboro);
            y += 18; AddLabel(info, "Cartella iniziale: " + homeDir, 12, y, Color.Gainsboro);

            // ── Condivisione di una cartella specifica ────────────────────────────
            y += 24;
            AddLabel(info, L.T("sftp.share_label"), 12, y, Color.Gray);
            y += 20;
            var txtShare = DarkTextBox(12, y, 420);
            info.Controls.Add(txtShare);
            var btnBrowse = DarkButton("📁", 440, y-1, 40);
            btnBrowse.Click += (s,e) => {
                using (var d = new FolderBrowserDialog())
                    if (d.ShowDialog() == DialogResult.OK) txtShare.Text = d.SelectedPath;
            };
            info.Controls.Add(btnBrowse);

            y += 34;
            var btnLink = DarkButton(L.T("sftp.create_link"), 12, y, 230, Color.FromArgb(40,90,120));
            btnLink.Click += (s,e) => CreateSftpShare(txtShare.Text, homeDir);
            info.Controls.Add(btnLink);
            var btnOpenHome = DarkButton(L.T("sftp.open_home"), 250, y, 130);
            btnOpenHome.Click += (s,e) => { try { Process.Start("explorer.exe", homeDir); } catch { } };
            info.Controls.Add(btnOpenHome);

            // ── Utente SFTP dedicato (crea un account Windows locale apposta) ──────
            y += 30;
            AddLabel(info, L.T("sftp.dedicated"), 12, y, Color.FromArgb(120, 180, 255));
            y += 20;
            AddLabel(info, L.T("sftp.dedicated_hint"), 12, y, Color.Gray);
            y += 24;
            info.Controls.Add(DarkLabel(L.T("sftp.username"), 12, y + 3, Color.Gainsboro));
            txtNewUser = DarkTextBox(120, y, 200);
            txtNewUser.Text = "sftp";
            info.Controls.Add(txtNewUser);
            y += 34;
            info.Controls.Add(DarkLabel(L.T("ftp.pass"), 12, y + 3, Color.Gainsboro));
            txtNewPass = DarkTextBox(120, y, 200);
            txtNewPass.PasswordChar = '●';
            info.Controls.Add(txtNewPass);
            var chkShowPass = new CheckBox { Text=L.T("sftp.show"), Location=new Point(330, y+2), AutoSize=true, ForeColor=Color.Gray };
            chkShowPass.CheckedChanged += (s,e) => txtNewPass.PasswordChar = chkShowPass.Checked ? '\0' : '●';
            info.Controls.Add(chkShowPass);
            var btnGenPw = DarkButton("🎲 Genera", 410, y - 1, 110, Color.FromArgb(55, 55, 85));
            btnGenPw.Click += (s, e) => {
                string pw = GenStrongPw();
                txtNewPass.Text = pw; txtNewPass.PasswordChar = '\0'; chkShowPass.Checked = true;
                try { Clipboard.SetText(pw); } catch { }
                AppendSshLog("🎲 Password generata e copiata negli appunti: " + pw);
                AppendSshLog("   Salvala: ti servirà sul CUCM (o nel client SFTP).");
            };
            info.Controls.Add(btnGenPw);
            info.Controls.Add(new Label {
                Text = L.T("sftp.pass_req"),
                Location = new Point(12, y + 26), Width = 600, Height = 34,
                ForeColor = Color.FromArgb(215, 175, 95), AutoSize = false });
            y += 62;
            btnCreateUser = DarkButton(L.T("sftp.create_user"), 12, y, 200, Color.FromArgb(30,100,30));
            btnCreateUser.Click += (s,e) => CreateSftpUser(txtNewUser.Text, txtNewPass.Text);
            info.Controls.Add(btnCreateUser);
            var btnRemoveUser = DarkButton("🗑 Rimuovi utente", 220, y, 160, Color.FromArgb(120,30,30));
            btnRemoveUser.Click += (s,e) => RemoveSftpUser(txtNewUser.Text);
            info.Controls.Add(btnRemoveUser);
            y += 30;
            AddLabel(info, "⚠ Crea un vero account Windows locale (standard, non admin). Richiede privilegi admin.", 12, y, Color.FromArgb(180,150,80));

            var lblLog2 = new Label { Text="📋 Output:", Dock=DockStyle.Bottom, Height=22, ForeColor=Color.LightGray, Padding=new Padding(4,4,0,0) };

            txtSshLog = new TextBox { Dock=DockStyle.Bottom, Height=78, Multiline=true, ScrollBars=ScrollBars.Vertical,
                BackColor=Color.FromArgb(12,12,20), ForeColor=Color.Cyan,
                Font=new Font("Consolas",8), ReadOnly=true, BorderStyle=BorderStyle.None };

            // Ordine: info riempie tutto lo spazio (scrollabile), log ancorato in basso.
            page.Controls.Add(info);       // Fill
            page.Controls.Add(lblLog2);    // Bottom
            page.Controls.Add(txtSshLog);  // Bottom
            page.Controls.SetChildIndex(txtSshLog, 0);  // più in basso
            page.Controls.SetChildIndex(lblLog2, 1);
            page.Controls.SetChildIndex(info, 2);       // riempie il resto

            // Check stato iniziale: SOLO dopo che la finestra ha un handle valido
            // (durante il costruttore BeginInvoke crasherebbe: handle non ancora creato).
            this.Shown += (s, e) => RefreshSshStatus();

            return page;
        }

        void RefreshSshStatus()
        {
            // mostra subito un placeholder
            lblSshStatus.Text = "⏳ Verifica OpenSSH…"; lblSshStatus.ForeColor = Color.Gray;
            // PowerShell (Get-WindowsCapability -Online è lento) → thread di background,
            // poi aggiorno la UI solo se la finestra ha già un handle valido.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string status = RunPsCommand("Get-Service -Name sshd -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Status");
                string installed = null;
                if (string.IsNullOrWhiteSpace(status))
                    installed = RunPsCommand("Get-WindowsCapability -Online -Name OpenSSH.Server* | Select-Object -ExpandProperty State");

                Action apply = () =>
                {
                    if (string.IsNullOrWhiteSpace(status))
                    {
                        if (installed != null && installed.Contains("Installed"))
                        {
                            lblSshStatus.Text      = "✔ OpenSSH installato, servizio non trovato";
                            lblSshStatus.ForeColor = Color.Yellow;
                            btnSshInstall.Enabled  = false;
                        }
                        else
                        {
                            lblSshStatus.Text      = "✗ OpenSSH Server non installato";
                            lblSshStatus.ForeColor = Color.OrangeRed;
                            btnSshInstall.Enabled  = true;
                        }
                        btnSshStart.Enabled = btnSshStop.Enabled = false;
                    }
                    else if (status.Trim().ToLower() == "running")
                    {
                        lblSshStatus.Text      = "▶ OpenSSH Server attivo (porta 22)";
                        lblSshStatus.ForeColor = Color.LimeGreen;
                        btnSshInstall.Enabled  = false;
                        btnSshStart.Enabled    = false;
                        btnSshStop.Enabled     = true;
                    }
                    else
                    {
                        lblSshStatus.Text      = "⏹ OpenSSH installato, servizio fermo (" + status.Trim() + ")";
                        lblSshStatus.ForeColor = Color.Gray;
                        btnSshInstall.Enabled  = false;
                        btnSshStart.Enabled    = true;
                        btnSshStop.Enabled     = false;
                    }
                };

                try { if (IsHandleCreated && !IsDisposed) BeginInvoke(apply); } catch { }
            });
        }

        void BtnSshInstall_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                "Installare OpenSSH Server (funzionalità opzionale di Windows)?\n\n" +
                "• Richiede privilegi di amministratore (apparirà il prompt UAC).\n" +
                "• Scarica da Windows Update: può richiedere ALCUNI MINUTI.\n" +
                "• Si aprirà una finestra PowerShell: NON chiuderla finché non finisce.\n\n" +
                "Se l'azienda blocca Windows Update, l'installazione potrebbe non riuscire:\n" +
                "in quel caso installa OpenSSH da Impostazioni › App › Funzionalità facoltative.",
                "LosaTermVoip — Installa OpenSSH",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            AppendSshLog("⏳ Installazione OpenSSH Server… (può richiedere alcuni minuti, non chiudere la finestra PowerShell)");
            btnSshInstall.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ => {
                // feedback testuale nella finestra elevata invece dello spinner che sembra bloccato
                string cmd =
                    "$ProgressPreference='SilentlyContinue'; " +
                    "Write-Host 'Installazione OpenSSH Server in corso...'; " +
                    "Write-Host 'Scarico da Windows Update, puo'' richiedere alcuni minuti. NON chiudere.'; " +
                    "Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 | Out-Null; " +
                    "Write-Host 'Installazione completata.'; Start-Sleep -Seconds 4";
                string res = RunPsCommandAdmin(cmd, 600000);   // fino a 10 minuti
                BeginInvoke((Action)(() => {
                    AppendSshLog(res.Length > 0 ? res : "Installazione completata.");
                    btnSshInstall.Enabled = true;
                    RefreshSshStatus();
                }));
            });
        }

        void BtnSshStart_Click(object sender, EventArgs e)
        {
            AppendSshLog("▶ Avvio servizio sshd...");
            string res = RunPsCommandAdmin("Start-Service sshd");
            AppendSshLog(res.Length > 0 ? res : "Servizio avviato.");
            RefreshSshStatus();
        }

        void BtnSshStop_Click(object sender, EventArgs e)
        {
            AppendSshLog("■ Stop servizio sshd...");
            string res = RunPsCommandAdmin("Stop-Service sshd");
            AppendSshLog(res.Length > 0 ? res : "Servizio fermato.");
            RefreshSshStatus();
        }

        void BtnSshAutoStart_Click(object sender, EventArgs e)
        {
            AppendSshLog("⚙ Imposto sshd su Automatic...");
            string res = RunPsCommandAdmin("Set-Service sshd -StartupType Automatic; Start-Service sshd");
            AppendSshLog(res.Length > 0 ? res : "Servizio impostato su Automatic e avviato.");
            RefreshSshStatus();
        }

        // Crea una scorciatoia (junction) nella home verso la cartella scelta:
        // i client SFTP atterrano nella home e trovano lì la cartella condivisa.
        // mklink /J non richiede privilegi di amministratore nella propria home.
        void CreateSftpShare(string target, string homeDir)
        {
            if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
            { AppendSshLog("✗ Seleziona prima una cartella valida da condividere."); return; }

            string name = Path.GetFileName(target.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(name)) name = "Condivisa";
            string link = Path.Combine(homeDir, "SFTP_" + name);

            if (Directory.Exists(link) || File.Exists(link))
            {
                AppendSshLog("ℹ La scorciatoia esiste già: " + link);
                try { Process.Start("explorer.exe", homeDir); } catch { }
                return;
            }
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
                var p = Process.Start(psi);
                string o = (p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd()).Trim();
                p.WaitForExit(10000);
                if (Directory.Exists(link))
                {
                    AppendSshLog("✔ Scorciatoia creata: " + link);
                    AppendSshLog("   I client SFTP la vedranno come cartella \"SFTP_" + name + "\" entrando nella home.");
                }
                else
                    AppendSshLog("✗ Impossibile creare la scorciatoia: " + o);
            }
            catch (Exception ex) { AppendSshLog("✗ Errore: " + ex.Message); }
        }

        // Crea (o aggiorna la password di) un account Windows locale dedicato all'SFTP.
        void CreateSftpUser(string user, string pass)
        {
            user = (user ?? "").Trim();
            pass = pass ?? "";
            if (user.Length == 0) { AppendSshLog("✗ Inserisci un username."); return; }
            if (user.IndexOfAny(new[] { ' ', '\\', '/', '"', '\'', '[', ']' }) >= 0)
            { AppendSshLog("✗ Username non valido (niente spazi o \\ / \" ' [ ])."); return; }
            if (pass.Length < 8)
            { AppendSshLog("✗ Password troppo corta: usa almeno 8 caratteri con maiuscole, minuscole e numeri (criteri di Windows)."); return; }
            if (!HasComplexity(pass))
            { AppendSshLog("✗ Password troppo debole per la policy del dominio: servono almeno 3 categorie tra MAIUSCOLE, minuscole, numeri e simboli. Premi 🎲 Genera per una conforme."); return; }

            AppendSshLog("⏳ Creo/aggiorno l'utente SFTP '" + user + "' (apparirà il prompt UAC)...");
            btnCreateUser.Enabled = false;
            string u = user.Replace("'", "''");
            string p = pass.Replace("'", "''");
            string cmd =
                "$ErrorActionPreference='Stop'; " +   // rende terminanti gli errori dei cmdlet → vengono catturati e mostrati
                "$sec = ConvertTo-SecureString '" + p + "' -AsPlainText -Force; " +
                "if (Get-LocalUser -Name '" + u + "' -ErrorAction SilentlyContinue) { " +
                    "Set-LocalUser -Name '" + u + "' -Password $sec; " +
                "} else { " +
                    "New-LocalUser -Name '" + u + "' -Password $sec -AccountNeverExpires -PasswordNeverExpires -Description 'LosaTermVoip SFTP' | Out-Null; " +
                "} " +
                "Add-LocalGroupMember -SID 'S-1-5-32-545' -Member '" + u + "' -ErrorAction SilentlyContinue";  // gruppo Users via SID (locale-proof)
            ThreadPool.QueueUserWorkItem(_ => {
                string res = RunPsCommandAdmin(cmd, 60000);
                BeginInvoke((Action)(() => {
                    if (res == "OK")
                    {
                        AppendSshLog("✔ Utente SFTP pronto: '" + user + "'.");
                        AppendSshLog("   ⓘ L'account è in Windows. La cartella C:\\Users\\" + user + " comparirà al PRIMO collegamento (è normale che ora non ci sia).");
                        AppendSshLog("   Collegati così:  sftp " + user + "@" + GetLocalIp());
                        AppendSshLog("   (assicurati che il servizio SSH sia avviato qui sopra)");
                    }
                    else AppendSshLog("✗ " + res);
                    btnCreateUser.Enabled = true;
                }));
            });
        }

        // Rimuove l'account Windows locale dedicato all'SFTP.
        void RemoveSftpUser(string user)
        {
            user = (user ?? "").Trim();
            if (user.Length == 0) { AppendSshLog("✗ Inserisci l'username da rimuovere."); return; }
            if (MessageBox.Show("Rimuovere l'account Windows locale '" + user + "'?\n\n" +
                "L'account e il suo profilo non saranno più utilizzabili per l'SFTP.",
                "LosaTermVoip — Rimuovi utente SFTP", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            AppendSshLog("⏳ Rimuovo l'utente '" + user + "'...");
            string u = user.Replace("'", "''");
            ThreadPool.QueueUserWorkItem(_ => {
                string res = RunPsCommandAdmin("Remove-LocalUser -Name '" + u + "'", 30000);
                BeginInvoke((Action)(() => AppendSshLog(res == "OK" ? "✔ Utente '" + user + "' rimosso." : "✗ " + res)));
            });
        }

        // ── PowerShell helpers ────────────────────────────────────────────────

        static string RunPsCommand(string cmd)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -Command \"" + cmd.Replace("\"","\\\"") + "\"")
                {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var p = Process.Start(psi);
                string o = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(15000);
                return o;
            }
            catch (Exception ex) { return "Errore: " + ex.Message; }
        }

        static string RunPsCommandAdmin(string cmd, int timeoutMs = 30000)
        {
            // Lancia PowerShell elevato tramite runas
            try
            {
                string tmpScript = Path.Combine(Path.GetTempPath(), "ltv_ssh_" + Guid.NewGuid().ToString("N") + ".ps1");
                string tmpOut    = tmpScript + ".out";
                // Avvolge il comando in try/catch: scrive 'OK' o 'ERR: <messaggio>' nel
                // file risultato, così l'app può mostrare l'errore reale (es. password
                // rifiutata dai criteri di Windows) invece di un generico "completato".
                string body =
                    "try {\r\n" + cmd + "\r\n" +
                    "[System.IO.File]::WriteAllText('" + tmpOut + "', 'OK')\r\n" +
                    "} catch {\r\n" +
                    "[System.IO.File]::WriteAllText('" + tmpOut + "', 'ERR: ' + $_.Exception.Message)\r\n" +
                    "}\r\n";
                File.WriteAllText(tmpScript, body);
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + tmpScript + "\"")
                {
                    UseShellExecute = true, Verb = "runas", CreateNoWindow = false
                };
                Process.Start(psi);
                // Con 'runas' il processo restituito da Start può uscire PRIMA che la
                // PowerShell elevata abbia finito di scrivere il file risultato: NON
                // fidarsi di WaitForExit → fare polling sul file .out finché compare.
                string result = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    if (File.Exists(tmpOut))
                    {
                        try { result = File.ReadAllText(tmpOut).Trim(); } catch { }
                        if (!string.IsNullOrEmpty(result)) break;
                    }
                    System.Threading.Thread.Sleep(250);
                }
                try { if (File.Exists(tmpOut)) File.Delete(tmpOut); } catch { }
                try { File.Delete(tmpScript); } catch { }
                if (!string.IsNullOrEmpty(result))
                    return result == "OK" ? "OK" : result;   // gli ERR: vengono mostrati
                return "Operazione non confermata (prompt UAC annullato o timeout). Riprova e approva l'UAC.";
            }
            catch (Exception ex) { return "Errore (serve Amministratore?): " + ex.Message; }
        }

        static string GetLocalIp()
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                { s.Connect("8.8.8.8", 65530); return ((IPEndPoint)s.LocalEndPoint).Address.ToString(); }
            }
            catch { return "127.0.0.1"; }
        }

        // Password robusta GARANTITA conforme: maiuscole+minuscole+numeri+simbolo,
        // 14 caratteri, senza caratteri ambigui (0/O/1/l/I).
        static string GenStrongPw()
        {
            const string U = "ABCDEFGHJKLMNPQRSTUVWXYZ", L = "abcdefghijkmnpqrstuvwxyz", D = "23456789", S = "!@#$%*-_+=";
            var rnd = new Random();
            char[] chars = new char[14];
            chars[0] = U[rnd.Next(U.Length)];
            chars[1] = L[rnd.Next(L.Length)];
            chars[2] = D[rnd.Next(D.Length)];
            chars[3] = S[rnd.Next(S.Length)];
            string all = U + L + D + S;
            for (int i = 4; i < chars.Length; i++) chars[i] = all[rnd.Next(all.Length)];
            for (int i = chars.Length - 1; i > 0; i--) { int j = rnd.Next(i + 1); char t = chars[i]; chars[i] = chars[j]; chars[j] = t; }
            return new string(chars);
        }

        // Almeno 3 categorie su 4 (criterio di complessità Windows/dominio).
        static bool HasComplexity(string p)
        {
            bool u = false, l = false, d = false, s = false;
            foreach (char c in p)
            {
                if (char.IsUpper(c)) u = true;
                else if (char.IsLower(c)) l = true;
                else if (char.IsDigit(c)) d = true;
                else s = true;
            }
            return ((u ? 1 : 0) + (l ? 1 : 0) + (d ? 1 : 0) + (s ? 1 : 0)) >= 3;
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        void SafeLog(ListBox lb, string msg)
        {
            if (lb.InvokeRequired) { lb.BeginInvoke((Action<ListBox,string>)SafeLog, lb, msg); return; }
            lb.Items.Add(msg);
            if (lb.Items.Count > 500) lb.Items.RemoveAt(0);
            lb.TopIndex = lb.Items.Count - 1;
        }

        void AppendSshLog(string msg)
        {
            if (txtSshLog.InvokeRequired) { txtSshLog.BeginInvoke((Action<string>)AppendSshLog, msg); return; }
            txtSshLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\r\n");
        }

        static Label DarkLabel(string text, int x, int y, Color? fg = null)
        {
            return new Label { Text=text, Location=new Point(x,y), Width=110,
                ForeColor=fg ?? Color.LightGray, AutoSize=false };
        }

        // Label a larghezza automatica (non si sovrappone ai campi accanto)
        static Label DLbl(string text, int x, int y)
        {
            return new Label { Text=text, Location=new Point(x,y), AutoSize=true, ForeColor=Color.LightGray };
        }

        static void AddLabel(Panel p, string text, int x, int y, Color fg)
        {
            p.Controls.Add(new Label { Text=text, Location=new Point(x,y), Width=560,
                ForeColor=fg, AutoSize=false });
        }

        static TextBox DarkTextBox(int x, int y, int w)
        {
            return new TextBox { Location=new Point(x,y), Width=w,
                BackColor=Color.FromArgb(45,45,60), ForeColor=Color.White,
                BorderStyle=BorderStyle.FixedSingle };
        }

        static Button DarkButton(string text, int x, int y, int w, Color? bg = null)
        {
            var b = new Button { Text=text, Location=new Point(x,y), Width=w, Height=26,
                FlatStyle=FlatStyle.Flat, ForeColor=Color.White,
                BackColor=bg ?? Color.FromArgb(50,50,70) };
            b.FlatAppearance.BorderColor = Color.FromArgb(80,80,100);
            return b;
        }
    }
}
