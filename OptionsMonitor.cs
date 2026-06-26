using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  SIP OPTIONS Monitor — watchdog trunk: uptime, latenza, ultimo OK/errore.
    //  Bilingue IT/EN. SIP OPTIONS UDP nativo.
    // ════════════════════════════════════════════════════════════════════════
    public class OptionsMonitorPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);

        class Target
        {
            public string Host; public int Port;
            public long Sent, Ok;
            public string LastStatus = "—", LastErr = "";
            public long LastRtt = -1;
            public DateTime LastOk = DateTime.MinValue;
            public bool Busy;
        }

        readonly List<Target> targets = new List<Target>();
        ListView lv;
        TextBox txtHost, txtInterval;
        Button btnStart, btnStop;
        System.Windows.Forms.Timer timer;

        public OptionsMonitorPanel()
        {
            Text = "LosaTermVoip — SIP OPTIONS Monitor";
            Size = new Size(880, 520);
            MinimumSize = new Size(640, 360);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
            FormClosed += (s,e)=>{ if (timer!=null) timer.Stop(); };
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.FromArgb(34,34,46) };
            top.Controls.Add(new Label { Text=L.B("Trunk (host[:porta]):","Trunk (host[:port]):"), Location=new Point(12,14), AutoSize=true, ForeColor=Color.LightGray });
            txtHost = new TextBox { Location=new Point(150,11), Width=220, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle };
            txtHost.KeyDown += (s,e)=>{ if(e.KeyCode==Keys.Enter){ AddTarget(); e.Handled=e.SuppressKeyPress=true; } };
            top.Controls.Add(txtHost);
            var btnAdd = Btn(L.B("➕ Aggiungi","➕ Add"), 380, 9, 110, Color.FromArgb(40,80,140)); btnAdd.Click += (s,e)=>AddTarget(); top.Controls.Add(btnAdd);

            top.Controls.Add(new Label { Text=L.B("Intervallo (s):","Interval (s):"), Location=new Point(510,14), AutoSize=true, ForeColor=Color.LightGray });
            txtInterval = new TextBox { Location=new Point(600,11), Width=50, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text="10" };
            top.Controls.Add(txtInterval);

            btnStart = Btn(L.B("▶ Avvia","▶ Start"), 670, 9, 90, Color.FromArgb(30,110,30)); btnStart.Click += (s,e)=>StartMon(); top.Controls.Add(btnStart);
            btnStop  = Btn(L.B("■ Ferma","■ Stop"), 766, 9, 90, Color.FromArgb(120,30,30)); btnStop.Enabled=false; btnStop.Click += (s,e)=>StopMon(); top.Controls.Add(btnStop);

            top.Controls.Add(new Label { Text=L.B("Doppio click su una riga per rimuoverla. Un 200 OK = trunk attivo.","Double-click a row to remove it. A 200 OK = trunk up."),
                Location=new Point(12,44), AutoSize=true, ForeColor=Color.Gray });

            lv = new ListView { Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true, GridLines=true,
                BackColor=Color.FromArgb(18,18,30), ForeColor=Color.White };
            lv.Columns.Add("Trunk", 240); lv.Columns.Add(L.B("Stato","Status"), 130); lv.Columns.Add("RTT", 80);
            lv.Columns.Add("Uptime", 90); lv.Columns.Add("OK/Tot", 90); lv.Columns.Add(L.B("Ultimo OK","Last OK"), 120); lv.Columns.Add(L.B("Ultimo errore","Last error"), 110);
            lv.DoubleClick += (s,e)=>RemoveSelected();

            Controls.Add(lv);
            Controls.Add(top);
        }

        void AddTarget()
        {
            string h = (txtHost.Text ?? "").Trim();
            if (h.Length == 0) return;
            int port = 5060; string host = h;
            int idx = h.LastIndexOf(':');
            if (idx > 0) { host = h.Substring(0, idx); int.TryParse(h.Substring(idx+1), out port); }
            if (port <= 0) port = 5060;
            targets.Add(new Target { Host = host, Port = port });
            txtHost.Clear();
            RefreshRows();
        }

        void RemoveSelected()
        {
            if (lv.SelectedItems.Count == 0) return;
            int i = lv.SelectedItems[0].Index;
            if (i >= 0 && i < targets.Count) { targets.RemoveAt(i); RefreshRows(); }
        }

        void StartMon()
        {
            if (targets.Count == 0) { MessageBox.Show(L.B("Aggiungi almeno un trunk.","Add at least one trunk."), "Monitor"); return; }
            int sec; if (!int.TryParse(txtInterval.Text, out sec) || sec < 2) sec = 10;
            if (timer == null) { timer = new System.Windows.Forms.Timer(); timer.Tick += (s,e)=>Poll(); }
            timer.Interval = sec * 1000;
            timer.Start();
            btnStart.Enabled = false; btnStop.Enabled = true;
            Poll();
        }

        void StopMon()
        {
            if (timer != null) timer.Stop();
            btnStart.Enabled = true; btnStop.Enabled = false;
        }

        void Poll()
        {
            foreach (var t in targets)
            {
                if (t.Busy) continue;
                var tt = t; tt.Busy = true;
                ThreadPool.QueueUserWorkItem(_ => {
                    long rtt; string status; bool ok = SendOptions(tt.Host, tt.Port, out rtt, out status);
                    tt.Sent++; if (ok) { tt.Ok++; tt.LastOk = DateTime.Now; tt.LastRtt = rtt; tt.LastStatus = status; }
                    else { tt.LastStatus = "✗ " + status; tt.LastErr = status; tt.LastRtt = -1; }
                    tt.Busy = false;
                    if (lv.IsHandleCreated) lv.BeginInvoke((MethodInvoker)delegate { RefreshRows(); });
                });
            }
        }

        void RefreshRows()
        {
            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (var t in targets)
            {
                var it = new ListViewItem(t.Host + ":" + t.Port);
                it.SubItems.Add(t.LastStatus);
                it.SubItems.Add(t.LastRtt >= 0 ? t.LastRtt + " ms" : "—");
                double up = t.Sent > 0 ? 100.0 * t.Ok / t.Sent : 0;
                it.SubItems.Add(t.Sent > 0 ? up.ToString("0.#") + "%" : "—");
                it.SubItems.Add(t.Ok + "/" + t.Sent);
                it.SubItems.Add(t.LastOk == DateTime.MinValue ? "—" : t.LastOk.ToString("HH:mm:ss"));
                it.SubItems.Add(t.LastErr);
                bool good = t.LastStatus.StartsWith("200") || t.LastStatus.Contains("200 OK");
                it.ForeColor = t.Sent == 0 ? Color.Gainsboro : (good ? Color.LimeGreen : Color.OrangeRed);
                lv.Items.Add(it);
            }
            lv.EndUpdate();
        }

        static bool SendOptions(string host, int port, out long rttMs, out string status)
        {
            rttMs = -1; status = L.B("no risposta","no response");
            try
            {
                IPAddress ip = null;
                foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
                if (ip == null) { status = L.B("DNS fallito","DNS failed"); return false; }

                string local = "0.0.0.0";
                try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect(ip, port); local = ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { }

                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 2000;
                    int lport = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
                    string branch = "z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,12);
                    string tag = Guid.NewGuid().ToString("N").Substring(0,8);
                    string cid = Guid.NewGuid().ToString("N").Substring(0,16);
                    string msg =
                        "OPTIONS sip:" + host + " SIP/2.0\r\n" +
                        "Via: SIP/2.0/UDP " + local + ":" + lport + ";branch=" + branch + "\r\n" +
                        "Max-Forwards: 70\r\n" +
                        "From: <sip:losaterm@" + local + ">;tag=" + tag + "\r\n" +
                        "To: <sip:" + host + ">\r\n" +
                        "Call-ID: " + cid + "@" + local + "\r\n" +
                        "CSeq: 1 OPTIONS\r\n" +
                        "Contact: <sip:losaterm@" + local + ":" + lport + ">\r\n" +
                        "User-Agent: LosaTermVoip\r\n" +
                        "Accept: application/sdp\r\n" +
                        "Content-Length: 0\r\n\r\n";
                    byte[] data = Encoding.ASCII.GetBytes(msg);
                    var sw = Stopwatch.StartNew();
                    udp.Send(data, data.Length, new IPEndPoint(ip, port));
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref remote);
                    sw.Stop(); rttMs = sw.ElapsedMilliseconds;
                    string text = Encoding.ASCII.GetString(resp);
                    int nl = text.IndexOf("\r\n");
                    string firstLine = nl > 0 ? text.Substring(0, nl) : text;
                    if (firstLine.StartsWith("SIP/2.0"))
                    {
                        status = firstLine.Substring(8).Trim();
                        return true;
                    }
                    status = L.B("risposta non-SIP","non-SIP reply");
                    return false;
                }
            }
            catch (SocketException) { status = "timeout"; return false; }
            catch (Exception ex) { status = ex.Message; return false; }
        }

        static Button Btn(string t,int x,int y,int w,Color c){ var b=new Button{ Text=t, Location=new Point(x,y), Width=w, Height=28, FlatStyle=FlatStyle.Flat, BackColor=c, ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold)}; b.FlatAppearance.BorderSize=0; return b; }
    }
}
