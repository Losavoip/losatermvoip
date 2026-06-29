using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  NET TOOLS — Ping, Traceroute, Port check, MTU, NTP. Nativo, bilingue IT/EN.
    // ════════════════════════════════════════════════════════════════════════
    public class NetToolsPanel : Form
    {
        static readonly Color CBg    = Color.FromArgb(24, 24, 32);
        static readonly Color CBar   = Color.FromArgb(34, 34, 46);
        static readonly Color CInput = Color.FromArgb(45, 45, 60);
        static readonly Color COut   = Color.FromArgb(16, 16, 22);

        public NetToolsPanel()
        {
            Text = "LosaTermVoip — Net Tools (Ping / Traceroute / Port / MTU / NTP)";
            Size = new Size(820, 600);
            MinimumSize = new Size(620, 440);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildPingTab());
            tabs.TabPages.Add(BuildTraceTab());
            tabs.TabPages.Add(BuildPortTab());
            tabs.TabPages.Add(BuildMtuTab());
            tabs.TabPages.Add(BuildNtpTab());
            Controls.Add(tabs);
        }

        static Label Lbl(string t, int x, int y) { return new Label { Text = t, Location = new Point(x, y), AutoSize = true, ForeColor = Color.LightGray }; }
        static TextBox Txt(int x, int y, int w, string v) { return new TextBox { Location = new Point(x, y), Width = w, Text = v, BackColor = CInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle }; }
        static Button Btn(string t, int x, int y, int w, Color c) { var b = new Button { Text = t, Location = new Point(x, y), Width = w, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) }; b.FlatAppearance.BorderSize = 0; return b; }
        static TextBox OutBox() { return new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, BackColor = COut, ForeColor = Color.Gainsboro, Font = new Font("Consolas", 9), BorderStyle = BorderStyle.None }; }

        void Append(TextBox box, string text)
        {
            if (box.InvokeRequired) box.BeginInvoke((MethodInvoker)delegate { box.AppendText(text + "\r\n"); });
            else box.AppendText(text + "\r\n");
        }
        void SetEnabled(Button b, bool en) { if (b.InvokeRequired) b.BeginInvoke((MethodInvoker)delegate { b.Enabled = en; }); else b.Enabled = en; }
        static Thread Bg(ThreadStart ts) { var t = new Thread(ts); t.IsBackground = true; t.Start(); return t; }
        static string NoHost() { return L.B("Inserisci host.","Enter a host."); }

        // ════════════════════════ PING ════════════════════════
        TabPage BuildPingTab()
        {
            var tab = new TabPage("  Ping  ") { BackColor = CBg, ForeColor = Color.White };
            var top = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = CBar };
            top.Controls.Add(Lbl("Host / IP:", 10, 16));
            var txtHost = Txt(80, 13, 240, "");
            top.Controls.Add(txtHost);
            top.Controls.Add(Lbl(L.B("N°:","Count:"), 336, 16));
            var txtN = Txt(380, 13, 50, "4");
            top.Controls.Add(txtN);
            var btn = Btn("▶ Ping", 444, 12, 100, Color.FromArgb(30, 110, 30));
            top.Controls.Add(btn);
            var outBox = OutBox();

            btn.Click += (s, e) => {
                string host = txtHost.Text.Trim(); if (host.Length == 0) { MessageBox.Show(NoHost()); return; }
                int n; if (!int.TryParse(txtN.Text.Trim(), out n) || n < 1) n = 4;
                outBox.Clear(); btn.Enabled = false;
                Bg(delegate { RunPing(host, n, outBox, btn); });
            };
            tab.Controls.Add(outBox); tab.Controls.Add(top);
            return tab;
        }

        void RunPing(string host, int n, TextBox outBox, Button btn)
        {
            try
            {
                var p = new Ping();
                byte[] buf = Encoding.ASCII.GetBytes(new string('a', 32));
                int sent = 0, recv = 0; long min = long.MaxValue, max = 0, sum = 0;
                Append(outBox, "PING " + host + "  (32 byte)");
                for (int i = 0; i < n; i++)
                {
                    sent++;
                    try
                    {
                        var r = p.Send(host, 3000, buf);
                        if (r.Status == IPStatus.Success)
                        {
                            recv++; sum += r.RoundtripTime;
                            if (r.RoundtripTime < min) min = r.RoundtripTime;
                            if (r.RoundtripTime > max) max = r.RoundtripTime;
                            Append(outBox, L.B("  risposta da ","  reply from ") + r.Address + " : " + L.B("tempo=","time=") + r.RoundtripTime + " ms  TTL=" + r.Options.Ttl);
                        }
                        else Append(outBox, "  " + r.Status);
                    }
                    catch (Exception ex) { Append(outBox, L.B("  errore: ","  error: ") + ex.Message); }
                    Thread.Sleep(500);
                }
                int loss = sent - recv;
                Append(outBox, "──────────────────────────────");
                Append(outBox, L.B("inviati=","sent=") + sent + L.B("  ricevuti=","  received=") + recv + L.B("  persi=","  lost=") + loss +
                    "  (" + (sent > 0 ? (100 * loss / sent) : 0) + L.B("% perdita)","% loss)"));
                if (recv > 0) Append(outBox, "rtt  min=" + min + "  avg=" + (sum / recv) + "  max=" + max + " ms");
            }
            catch (Exception ex) { Append(outBox, L.B("Errore: ","Error: ") + ex.Message); }
            SetEnabled(btn, true);
        }

        // ════════════════════════ TRACEROUTE ════════════════════════
        TabPage BuildTraceTab()
        {
            var tab = new TabPage("  Traceroute  ") { BackColor = CBg, ForeColor = Color.White };
            var top = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = CBar };
            top.Controls.Add(Lbl("Host / IP:", 10, 16));
            var txtHost = Txt(80, 13, 240, "");
            top.Controls.Add(txtHost);
            var btn = Btn(L.B("▶ Traccia","▶ Trace"), 336, 12, 110, Color.FromArgb(40, 80, 140));
            top.Controls.Add(btn);
            var outBox = OutBox();
            btn.Click += (s, e) => {
                string host = txtHost.Text.Trim(); if (host.Length == 0) { MessageBox.Show(NoHost()); return; }
                outBox.Clear(); btn.Enabled = false;
                Bg(delegate { RunTrace(host, outBox, btn); });
            };
            tab.Controls.Add(outBox); tab.Controls.Add(top);
            return tab;
        }

        void RunTrace(string host, TextBox outBox, Button btn)
        {
            try
            {
                Append(outBox, L.B("Traceroute verso ","Traceroute to ") + host + L.B("  (max 30 hop)","  (max 30 hops)"));
                var p = new Ping();
                byte[] buf = Encoding.ASCII.GetBytes(new string('a', 32));
                for (int ttl = 1; ttl <= 30; ttl++)
                {
                    var opt = new PingOptions(ttl, true);
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var r = p.Send(host, 3000, buf, opt);
                        sw.Stop();
                        string addr = (r.Address != null) ? r.Address.ToString() : "*";
                        if (r.Status == IPStatus.TtlExpired || r.Status == IPStatus.Success)
                            Append(outBox, ttl.ToString().PadLeft(2) + "  " + addr.PadRight(18) + "  " + sw.ElapsedMilliseconds + " ms");
                        else
                            Append(outBox, ttl.ToString().PadLeft(2) + "  " + addr.PadRight(18) + "  " + r.Status);
                        if (r.Status == IPStatus.Success) { Append(outBox, L.B("── destinazione raggiunta ──","── destination reached ──")); break; }
                    }
                    catch (Exception ex) { Append(outBox, ttl.ToString().PadLeft(2) + L.B("  errore: ","  error: ") + ex.Message); }
                }
            }
            catch (Exception ex) { Append(outBox, L.B("Errore: ","Error: ") + ex.Message); }
            SetEnabled(btn, true);
        }

        // ════════════════════════ PORT CHECK ════════════════════════
        TabPage BuildPortTab()
        {
            var tab = new TabPage("  Port check  ") { BackColor = CBg, ForeColor = Color.White };
            var top = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = CBar };
            top.Controls.Add(Lbl("Host / IP:", 10, 14));
            var txtHost = Txt(80, 11, 240, "");
            top.Controls.Add(txtHost);
            var btn = Btn(L.B("▶ Verifica","▶ Check"), 336, 10, 110, Color.FromArgb(90, 60, 140));
            top.Controls.Add(btn);
            top.Controls.Add(Lbl(L.B("Porte (separate da spazio/virgola):","Ports (space/comma separated):"), 10, 50));
            var txtPorts = Txt(270, 47, 250, "5060 5061 2000 8443");
            top.Controls.Add(txtPorts);
            var outBox = OutBox();
            btn.Click += (s, e) => {
                string host = txtHost.Text.Trim(); if (host.Length == 0) { MessageBox.Show(NoHost()); return; }
                string ports = txtPorts.Text;
                outBox.Clear(); btn.Enabled = false;
                Bg(delegate { RunPort(host, ports, outBox, btn); });
            };
            tab.Controls.Add(outBox); tab.Controls.Add(top);
            return tab;
        }

        void RunPort(string host, string portsStr, TextBox outBox, Button btn)
        {
            Append(outBox, L.B("Verifica porte TCP su ","TCP port check on ") + host);
            foreach (var tok in portsStr.Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int port;
                if (!int.TryParse(tok.Trim(), out port)) continue;
                bool open = false; string err = "";
                try
                {
                    using (var c = new TcpClient())
                    {
                        var ar = c.BeginConnect(host, port, null, null);
                        open = ar.AsyncWaitHandle.WaitOne(2000) && c.Connected;
                        if (open) c.EndConnect(ar);
                    }
                }
                catch (Exception ex) { err = ex.Message; }
                Append(outBox, L.B("  porta ","  port ") + port.ToString().PadRight(6) + (open ? L.B("✓ APERTA","✓ OPEN") : L.B("✗ chiusa/filtrata","✗ closed/filtered") + (err.Length > 0 ? " (" + err + ")" : "")));
            }
            Append(outBox, L.B("── fine ──","── end ──"));
            SetEnabled(btn, true);
        }

        // ════════════════════════ MTU ════════════════════════
        TabPage BuildMtuTab()
        {
            var tab = new TabPage("  MTU  ") { BackColor = CBg, ForeColor = Color.White };
            var top = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = CBar };
            top.Controls.Add(Lbl("Host / IP:", 10, 16));
            var txtHost = Txt(80, 13, 240, "");
            top.Controls.Add(txtHost);
            var btn = Btn(L.B("▶ Scopri MTU","▶ Discover MTU"), 336, 12, 140, Color.FromArgb(40, 110, 110));
            top.Controls.Add(btn);
            var info = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft,
                Text = L.B("  Ping con flag Don't-Fragment a dimensione crescente: trova la MTU massima del percorso (utile per audio monodirezionale/frammentazione).","  Don't-Fragment ping at increasing size: finds the path's max MTU (useful for one-way audio/fragmentation).") };
            var outBox = OutBox();
            btn.Click += (s, e) => {
                string host = txtHost.Text.Trim(); if (host.Length == 0) { MessageBox.Show(NoHost()); return; }
                outBox.Clear(); btn.Enabled = false;
                Bg(delegate { RunMtu(host, outBox, btn); });
            };
            tab.Controls.Add(outBox); tab.Controls.Add(info); tab.Controls.Add(top);
            return tab;
        }

        void RunMtu(string host, TextBox outBox, Button btn)
        {
            try
            {
                var p = new Ping();
                var opt = new PingOptions(64, true);
                Append(outBox, L.B("Scoperta MTU verso ","MTU discovery to ") + host + " (DF, binary search)…");
                int lo = 0, hi = 1472, best = 0;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    byte[] buf = new byte[mid];
                    bool ok = false;
                    try { var r = p.Send(host, 3000, buf, opt); ok = (r.Status == IPStatus.Success); }
                    catch { ok = false; }
                    if (ok) { best = mid; lo = mid + 1; }
                    else hi = mid - 1;
                }
                if (best > 0) Append(outBox, L.B("MTU del percorso ≈ ","Path MTU ≈ ") + (best + 28) + L.B(" byte  (payload max "," bytes  (max payload ") + best + ")");
                else Append(outBox, L.B("Nessuna risposta DF: l'host non risponde al ping o blocca ICMP.","No DF reply: the host doesn't answer ping or blocks ICMP."));
            }
            catch (Exception ex) { Append(outBox, L.B("Errore: ","Error: ") + ex.Message); }
            SetEnabled(btn, true);
        }

        // ════════════════════════ NTP ════════════════════════
        TabPage BuildNtpTab()
        {
            var tab = new TabPage("  NTP  ") { BackColor = CBg, ForeColor = Color.White };
            var top = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = CBar };
            top.Controls.Add(Lbl(L.B("Server NTP:","NTP server:"), 10, 16));
            var txtHost = Txt(90, 13, 240, "pool.ntp.org");
            top.Controls.Add(txtHost);
            var btn = Btn(L.B("▶ Interroga","▶ Query"), 346, 12, 120, Color.FromArgb(110, 80, 30));
            top.Controls.Add(btn);
            var info = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft,
                Text = L.B("  Un orologio sfasato rompe TLS/SRTP e disallinea i CDR. Confronta l'ora del server con quella del PC.","  A skewed clock breaks TLS/SRTP and misaligns CDRs. Compare the server time with the PC's.") };
            var outBox = OutBox();
            btn.Click += (s, e) => {
                string host = txtHost.Text.Trim(); if (host.Length == 0) { MessageBox.Show(L.B("Inserisci server NTP.","Enter an NTP server.")); return; }
                outBox.Clear(); btn.Enabled = false;
                Bg(delegate { RunNtp(host, outBox, btn); });
            };
            tab.Controls.Add(outBox); tab.Controls.Add(info); tab.Controls.Add(top);
            return tab;
        }

        void RunNtp(string host, TextBox outBox, Button btn)
        {
            try
            {
                var data = new byte[48];
                data[0] = 0x1B;
                using (var u = new UdpClient())
                {
                    u.Client.ReceiveTimeout = 3000;
                    u.Connect(host, 123);
                    var sw = Stopwatch.StartNew();
                    u.Send(data, data.Length);
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = u.Receive(ref ep);
                    sw.Stop();

                    int stratum = resp[1];
                    ulong intPart  = ((ulong)resp[40] << 24) | ((ulong)resp[41] << 16) | ((ulong)resp[42] << 8) | resp[43];
                    ulong fracPart = ((ulong)resp[44] << 24) | ((ulong)resp[45] << 16) | ((ulong)resp[46] << 8) | resp[47];
                    double ms = intPart * 1000.0 + (fracPart * 1000.0 / 4294967296.0);
                    var epoch1900 = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime serverUtc = epoch1900.AddMilliseconds(ms);
                    double offsetMs = (serverUtc - DateTime.UtcNow).TotalMilliseconds + sw.ElapsedMilliseconds / 2.0;

                    Append(outBox, L.B("Server NTP : ","NTP server : ") + host);
                    Append(outBox, "Stratum    : " + stratum);
                    Append(outBox, L.B("Ora server : ","Server time: ") + serverUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff") + L.B(" (locale)"," (local)"));
                    Append(outBox, L.B("Ora PC     : ","PC time    : ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    Append(outBox, "Offset     : " + offsetMs.ToString("F0") + " ms" +
                        (Math.Abs(offsetMs) > 1000 ? L.B("   ⚠ orologio sfasato!","   ⚠ clock skewed!") : "   ✓ ok"));
                }
            }
            catch (Exception ex) { Append(outBox, L.B("✗ Errore NTP: ","✗ NTP error: ") + ex.Message + L.B("\r\n(verifica raggiungibilità UDP/123)","\r\n(check UDP/123 reachability)")); }
            SetEnabled(btn, true);
        }
    }
}
