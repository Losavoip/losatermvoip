using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Firewall / Port Check VoIP — TCP connect + probe UDP reali. Bilingue.
    // ════════════════════════════════════════════════════════════════════════
    public class FirewallCheckPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtHost;
        ListView lv;
        Button btnGo;

        static readonly object[][] Ports = new[] {
            new object[]{ 5060, "UDP", "SIP (UDP)",         "options" },
            new object[]{ 5060, "TCP", "SIP (TCP)",         "tcp" },
            new object[]{ 5061, "TCP", "SIP/TLS (SIPS)",    "tcp" },
            new object[]{ 3478, "UDP", "STUN / TURN",       "stun" },
            new object[]{ 3478, "TCP", "TURN (TCP)",        "tcp" },
            new object[]{ 5349, "TCP", "TURNS (TLS)",       "tcp" },
            new object[]{  443, "TCP", "HTTPS (prov/Teams)","tcp" },
            new object[]{   80, "TCP", "HTTP (prov)",       "tcp" },
            new object[]{   69, "UDP", "TFTP (prov)",       "info" },
            new object[]{   22, "TCP", "SSH (mgmt)",        "tcp" },
        };

        public FirewallCheckPanel()
        {
            Text = "LosaTermVoip — VoIP Firewall / Port Check";
            Size = new Size(720, 520);
            MinimumSize = new Size(560, 380);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(34,34,46) };
            top.Controls.Add(new Label { Text="Host / SBC:", Location=new Point(12,18), AutoSize=true, ForeColor=Color.LightGray });
            txtHost = new TextBox { Location=new Point(90,15), Width=280, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle };
            txtHost.KeyDown += (s,e)=>{ if(e.KeyCode==Keys.Enter){ Run(); e.Handled=e.SuppressKeyPress=true; } };
            top.Controls.Add(txtHost);
            btnGo = new Button { Text=L.B("🔥 Test porte","🔥 Test ports"), Location=new Point(382,13), Width=130, Height=28, FlatStyle=FlatStyle.Flat,
                BackColor=Color.FromArgb(40,80,140), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btnGo.FlatAppearance.BorderSize=0; btnGo.Click += (s,e)=>Run(); top.Controls.Add(btnGo);
            var btnR = ReportHelper.MakeButton(520, 14);
            btnR.Click += (s,e)=>ReportHelper.ExportText(this, "Firewall port-check", LvToText());
            top.Controls.Add(btnR);

            lv = new ListView { Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true, GridLines=true,
                BackColor=Color.FromArgb(18,18,30), ForeColor=Color.White };
            lv.Columns.Add(L.B("Porta","Port"), 80); lv.Columns.Add("Proto", 70); lv.Columns.Add(L.B("Servizio","Service"), 200); lv.Columns.Add(L.B("Esito","Result"), 320);

            var foot = new Label { Dock=DockStyle.Bottom, Height=40, ForeColor=Color.Gray, TextAlign=ContentAlignment.MiddleLeft, Padding=new Padding(8,0,0,0),
                Text=L.B("  RTP/RTCP (UDP 10000-20000 tipico) non è testabile senza una chiamata reale: usa il RTP Player su un PCAP.","  RTP/RTCP (UDP 10000-20000 typical) can't be tested without a real call: use the RTP Player on a PCAP.") };

            Controls.Add(lv); Controls.Add(foot); Controls.Add(top);
        }

        string LvToText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Firewall port-check — " + (txtHost.Text ?? "").Trim());
            sb.AppendLine(new string('-', 62));
            sb.AppendLine(string.Format("{0,-8} {1,-6} {2,-24} {3}", "Port", "Proto", "Service", "Result"));
            foreach (ListViewItem it in lv.Items)
                sb.AppendLine(string.Format("{0,-8} {1,-6} {2,-24} {3}", it.Text,
                    it.SubItems.Count > 1 ? it.SubItems[1].Text : "",
                    it.SubItems.Count > 2 ? it.SubItems[2].Text : "",
                    it.SubItems.Count > 3 ? it.SubItems[3].Text : ""));
            return sb.ToString();
        }

        void Run()
        {
            string host = (txtHost.Text ?? "").Trim();
            if (host.Length == 0) return;
            lv.Items.Clear(); btnGo.Enabled = false;
            foreach (var p in Ports)
            {
                var it = new ListViewItem(p[0].ToString());
                it.SubItems.Add((string)p[1]); it.SubItems.Add((string)p[2]); it.SubItems.Add(L.B("⏳ test…","⏳ testing…"));
                lv.Items.Add(it);
            }
            ThreadPool.QueueUserWorkItem(_ => {
                for (int i = 0; i < Ports.Length; i++)
                {
                    int idx = i;
                    int port = (int)Ports[i][0]; string mode = (string)Ports[i][3];
                    string res; Color col;
                    Probe(host, port, mode, out res, out col);
                    if (lv.IsHandleCreated)
                        lv.BeginInvoke((MethodInvoker)delegate {
                            if (idx < lv.Items.Count) { lv.Items[idx].SubItems[3].Text = res; lv.Items[idx].ForeColor = col; }
                            ReportHelper.Set("Firewall port-check", LvToText());
                        });
                }
                if (btnGo.IsHandleCreated) btnGo.BeginInvoke((MethodInvoker)delegate { btnGo.Enabled = true; });
            });
        }

        static void Probe(string host, int port, string mode, out string res, out Color col)
        {
            if (mode == "tcp")
            {
                var c = new TcpClient();
                try
                {
                    var ar = c.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(2500)) { try{c.Close();}catch{} res = L.B("🚫 Filtrata (timeout — firewall droppa)","🚫 Filtered (timeout — firewall drops)"); col = Color.OrangeRed; return; }
                    c.EndConnect(ar); try{c.Close();}catch{}
                    res = L.B("✅ Aperta (TCP connesso)","✅ Open (TCP connected)"); col = Color.LimeGreen; return;
                }
                catch (SocketException se)
                {
                    if (se.SocketErrorCode == SocketError.ConnectionRefused) { res = L.B("⛔ Chiusa (RST — raggiungibile, nessun servizio)","⛔ Closed (RST — reachable, no service)"); col = Color.Orange; }
                    else { res = "🚫 " + se.SocketErrorCode; col = Color.OrangeRed; }
                    return;
                }
                catch (Exception ex) { res = "✗ " + ex.Message; col = Color.OrangeRed; return; }
            }
            if (mode == "stun")
            {
                bool ok = StunProbe(host, port);
                res = ok ? L.B("✅ Aperta (risposta STUN)","✅ Open (STUN reply)") : L.B("🚫 Nessuna risposta (filtrata o no STUN)","🚫 No response (filtered or no STUN)");
                col = ok ? Color.LimeGreen : Color.OrangeRed; return;
            }
            if (mode == "options")
            {
                bool ok = OptionsProbe(host, port);
                res = ok ? L.B("✅ Aperta (risposta SIP)","✅ Open (SIP reply)") : L.B("🚫 Nessuna risposta (filtrata o no SIP qui)","🚫 No response (filtered or no SIP here)");
                col = ok ? Color.LimeGreen : Color.OrangeRed; return;
            }
            res = L.B("ℹ️ UDP non testabile direttamente","ℹ️ UDP not directly testable"); col = Color.Gray;
        }

        static IPAddress Resolve(string host)
        {
            try { foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily == AddressFamily.InterNetwork) return a; } catch { }
            return null;
        }

        static bool StunProbe(string host, int port)
        {
            IPAddress ip = Resolve(host); if (ip == null) return false;
            try
            {
                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 2500;
                    byte[] req = new byte[20];
                    req[0]=0x00; req[1]=0x01; req[4]=0x21; req[5]=0x12; req[6]=0xA4; req[7]=0x42;
                    var rnd = new Random(); for (int i=8;i<20;i++) req[i]=(byte)rnd.Next(256);
                    udp.Send(req, 20, new IPEndPoint(ip, port));
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    udp.Receive(ref remote);
                    return true;
                }
            }
            catch { return false; }
        }

        static bool OptionsProbe(string host, int port)
        {
            IPAddress ip = Resolve(host); if (ip == null) return false;
            try
            {
                string local = "0.0.0.0";
                try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect(ip, port); local = ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { }
                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 2500;
                    int lp = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
                    string br = "z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,12);
                    string msg = "OPTIONS sip:" + host + " SIP/2.0\r\nVia: SIP/2.0/UDP " + local + ":" + lp + ";branch=" + br +
                        "\r\nMax-Forwards: 70\r\nFrom: <sip:losaterm@" + local + ">;tag=" + Guid.NewGuid().ToString("N").Substring(0,8) +
                        "\r\nTo: <sip:" + host + ">\r\nCall-ID: " + Guid.NewGuid().ToString("N").Substring(0,16) + "@" + local +
                        "\r\nCSeq: 1 OPTIONS\r\nContent-Length: 0\r\n\r\n";
                    byte[] data = Encoding.ASCII.GetBytes(msg);
                    udp.Send(data, data.Length, new IPEndPoint(ip, port));
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref remote);
                    return Encoding.ASCII.GetString(resp).StartsWith("SIP/2.0");
                }
            }
            catch { return false; }
        }
    }
}
