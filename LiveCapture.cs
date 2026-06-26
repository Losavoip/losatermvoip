using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Cattura pacchetti LIVE — dumpcap (Wireshark) → .pcap, hand-off al Ladder.
    //  Bilingue IT/EN. Richiede Wireshark + Npcap.
    // ════════════════════════════════════════════════════════════════════════
    public class LiveCapturePanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);

        public Action<string> OnAnalyze;

        ComboBox cmbIface;
        TextBox txtFilter, txtOut;
        Button btnStart, btnStop, btnLadder, btnRefresh, btnBrowse;
        Label lblStatus;
        Process proc;
        System.Windows.Forms.Timer timer;
        DateTime startedAt;
        string lastFile;

        const string VoipFilter = "port 5060 or port 5061 or port 3478 or port 5349 or portrange 10000-20000";

        public LiveCapturePanel()
        {
            Text = "LosaTermVoip — Live Packet Capture";
            Size = new Size(720, 320);
            MinimumSize = new Size(600, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
            FormClosed += (s,e)=>{ StopCapture(); };
        }

        string Dumpcap()
        {
            string t = PcapAnalyzer.FindTshark();
            if (t == null) return null;
            string d = Path.Combine(Path.GetDirectoryName(t), "dumpcap.exe");
            return File.Exists(d) ? d : null;
        }

        void Build()
        {
            int x1=16, x2=130, y=18;

            Controls.Add(Lbl(L.B("Interfaccia:","Interface:"), x1, y));
            cmbIface = new ComboBox { Location=new Point(x2,y-2), Width=420, DropDownStyle=ComboBoxStyle.DropDownList, BackColor=CIn, ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
            Controls.Add(cmbIface);
            btnRefresh = Btn("🔄", x2+428, y-3, 40, Color.FromArgb(60,60,80)); btnRefresh.Click += (s,e)=>RefreshIfaces(); Controls.Add(btnRefresh);
            y += 40;

            Controls.Add(Lbl(L.B("Filtro (BPF):","Filter (BPF):"), x1, y));
            txtFilter = new TextBox { Location=new Point(x2,y-2), Width=420, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text=VoipFilter };
            Controls.Add(txtFilter);
            var bV = Btn("VoIP", x2+428, y-3, 50, Color.FromArgb(40,80,140)); bV.Click += (s,e)=>txtFilter.Text=VoipFilter; Controls.Add(bV);
            var bA = Btn(L.B("Tutto","All"), x2+482, y-3, 55, Color.FromArgb(60,60,80)); bA.Click += (s,e)=>txtFilter.Text=""; Controls.Add(bA);
            y += 40;

            Controls.Add(Lbl(L.B("Salva in:","Save to:"), x1, y));
            txtOut = new TextBox { Location=new Point(x2,y-2), Width=420, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle,
                Text=Path.Combine(Path.GetTempPath(), "losaterm_capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pcapng") };
            Controls.Add(txtOut);
            btnBrowse = Btn("📂", x2+428, y-3, 40, Color.FromArgb(60,60,80)); btnBrowse.Click += (s,e)=>Browse(); Controls.Add(btnBrowse);
            y += 46;

            btnStart  = Btn(L.B("▶ Avvia cattura","▶ Start capture"), x2, y, 150, Color.FromArgb(30,110,30)); btnStart.Click += (s,e)=>StartCapture(); Controls.Add(btnStart);
            btnStop   = Btn(L.B("■ Ferma","■ Stop"), x2+158, y, 100, Color.FromArgb(120,30,30)); btnStop.Enabled=false; btnStop.Click += (s,e)=>StopCapture(); Controls.Add(btnStop);
            btnLadder = Btn(L.B("🪜 Apri nel Ladder","🪜 Open in Ladder"), x2+266, y, 170, Color.FromArgb(40,80,140)); btnLadder.Enabled=false; btnLadder.Click += (s,e)=>OpenLadder(); Controls.Add(btnLadder);
            y += 46;

            lblStatus = new Label { Location=new Point(x1,y), Size=new Size(660,40), ForeColor=Color.Gainsboro,
                Font=new Font("Consolas",9.5f), Text=L.B("Pronto. Scegli interfaccia + filtro e premi Avvia.","Ready. Pick interface + filter and press Start.") };
            Controls.Add(lblStatus);

            RefreshIfaces();
        }

        void RefreshIfaces()
        {
            cmbIface.Items.Clear();
            string dc = Dumpcap();
            if (dc == null) { lblStatus.Text = L.B("✗ dumpcap.exe non trovato. Installa Wireshark (con Npcap).","✗ dumpcap.exe not found. Install Wireshark (with Npcap)."); lblStatus.ForeColor = Color.OrangeRed; return; }
            try
            {
                string outp = Run(dc, "-D");
                var ipMap = IfaceIpMap();
                foreach (var line in outp.Replace("\r","").Split('\n'))
                {
                    string ln = line.Trim(); if (ln.Length == 0) continue;
                    int dot = ln.IndexOf('.');
                    if (dot <= 0) continue;
                    string num = ln.Substring(0, dot).Trim();
                    string rest = ln.Substring(dot+1).Trim();
                    int op = rest.LastIndexOf('('); int cp = rest.LastIndexOf(')');
                    string friendly = (op>=0 && cp>op) ? rest.Substring(op+1, cp-op-1) : rest;
                    string ipStr = "";
                    var gm = Regex.Match(rest, @"\{[0-9A-Fa-f\-]+\}");
                    if (gm.Success && ipMap.ContainsKey(gm.Value)) ipStr = "  (" + ipMap[gm.Value] + ")";
                    cmbIface.Items.Add(new IfaceItem { Num = num, Label = num + ": " + friendly + ipStr });
                }
                if (cmbIface.Items.Count > 0) { cmbIface.SelectedIndex = 0; lblStatus.Text = cmbIface.Items.Count + L.B(" interfacce trovate."," interfaces found."); lblStatus.ForeColor = Color.Gainsboro; }
                else { lblStatus.Text = L.B("Nessuna interfaccia: serve Npcap installato (privilegi).","No interfaces: Npcap must be installed (privileges)."); lblStatus.ForeColor = Color.Orange; }
            }
            catch (Exception ex) { lblStatus.Text = "✗ " + ex.Message; lblStatus.ForeColor = Color.OrangeRed; }
        }

        class IfaceItem { public string Num, Label; public override string ToString(){ return Label; } }

        static Dictionary<string,string> IfaceIpMap()
        {
            var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string ipv4 = null;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork) { ipv4 = ua.Address.ToString(); break; }
                    if (ipv4 != null && !map.ContainsKey(ni.Id)) map[ni.Id] = ipv4;
                }
            }
            catch { }
            return map;
        }

        void Browse()
        {
            using (var d = new SaveFileDialog { Filter="pcapng (*.pcapng)|*.pcapng|pcap (*.pcap)|*.pcap", FileName=Path.GetFileName(txtOut.Text) })
                if (d.ShowDialog(this) == DialogResult.OK) txtOut.Text = d.FileName;
        }

        void StartCapture()
        {
            string dc = Dumpcap(); if (dc == null) { MessageBox.Show(L.B("dumpcap non trovato.","dumpcap not found."), "Capture"); return; }
            var ifc = cmbIface.SelectedItem as IfaceItem;
            if (ifc == null) { MessageBox.Show(L.B("Seleziona un'interfaccia.","Select an interface."), "Capture"); return; }
            lastFile = txtOut.Text.Trim();
            if (lastFile.Length == 0) { MessageBox.Show(L.B("Indica un file di output.","Specify an output file."), "Capture"); return; }

            var args = new StringBuilder();
            args.Append("-i " + ifc.Num);
            string f = txtFilter.Text.Trim();
            if (f.Length > 0) args.Append(" -f \"" + f.Replace("\"","") + "\"");
            args.Append(" -w \"" + lastFile + "\"");
            try
            {
                proc = new Process { StartInfo = new ProcessStartInfo(dc, args.ToString()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } };
                proc.EnableRaisingEvents = true;
                proc.Exited += (s,e)=>{ if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { OnCaptureEnded(); }); };
                proc.Start();
                proc.BeginErrorReadLine();
                proc.ErrorDataReceived += (s,e)=>{};
                startedAt = DateTime.Now;
                btnStart.Enabled = false; btnStop.Enabled = true; btnLadder.Enabled = false;
                cmbIface.Enabled = txtFilter.Enabled = txtOut.Enabled = btnBrowse.Enabled = btnRefresh.Enabled = false;
                if (timer == null) { timer = new System.Windows.Forms.Timer { Interval = 1000 }; timer.Tick += (s,e)=>Tick(); }
                timer.Start(); Tick();
            }
            catch (Exception ex) { MessageBox.Show(L.B("Errore avvio dumpcap:\n","dumpcap start error:\n") + ex.Message, "Capture", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void Tick()
        {
            var ts = DateTime.Now - startedAt;
            long size = 0; try { if (File.Exists(lastFile)) size = new FileInfo(lastFile).Length; } catch {}
            lblStatus.Text = L.B("🔴 Catturando… ","🔴 Capturing… ") + ts.ToString(@"mm\:ss") + L.B("   file: ","   file: ") + (size/1024) + " KB";
            lblStatus.ForeColor = Color.OrangeRed;
        }

        void StopCapture()
        {
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch {}
        }

        void OnCaptureEnded()
        {
            if (timer != null) timer.Stop();
            btnStart.Enabled = true; btnStop.Enabled = false;
            cmbIface.Enabled = txtFilter.Enabled = txtOut.Enabled = btnBrowse.Enabled = btnRefresh.Enabled = true;
            long size = 0; try { if (File.Exists(lastFile)) size = new FileInfo(lastFile).Length; } catch {}
            if (size > 0)
            {
                btnLadder.Enabled = true;
                lblStatus.Text = L.B("✅ Cattura salvata: ","✅ Capture saved: ") + lastFile + "  (" + (size/1024) + " KB)";
                lblStatus.ForeColor = Color.LimeGreen;
            }
            else { lblStatus.Text = L.B("Cattura terminata, file vuoto (nessun pacchetto / permessi?).","Capture ended, empty file (no packets / permissions?)."); lblStatus.ForeColor = Color.Orange; }
            proc = null;
        }

        void OpenLadder()
        {
            if (lastFile == null || !File.Exists(lastFile)) return;
            if (OnAnalyze != null) OnAnalyze(lastFile);
            else { try { Process.Start("explorer.exe", "/select,\"" + lastFile + "\""); } catch {} }
        }

        static string Run(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute=false, RedirectStandardOutput=true, CreateNoWindow=true, StandardOutputEncoding=Encoding.UTF8 };
            using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return o; }
        }

        static Label Lbl(string t,int x,int y){ return new Label{ Text=t, Location=new Point(x,y+2), AutoSize=true, ForeColor=Color.LightGray }; }
        Button Btn(string t,int x,int y,int w,Color c){ var b=new Button{ Text=t, Location=new Point(x,y), Width=w, Height=28, FlatStyle=FlatStyle.Flat, BackColor=c, ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold)}; b.FlatAppearance.BorderSize=0; return b; }
    }
}
