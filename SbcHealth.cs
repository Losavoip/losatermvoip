using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  SBC HEALTH — diagnostica vendor-neutral per trunk/SBC (Teams Direct
    //  Routing, AudioCodes, Ribbon, Cisco CUBE, Alcatel, Asterisk...).
    //  Tre strumenti nativi .NET (nessuna dipendenza esterna):
    //    1) SIP OPTIONS  — "il trunk risponde?" (UDP/TCP/TLS)
    //    2) TLS / Cert   — CN/SAN, scadenza, versione TLS (causa #1 di down DR)
    //    3) DNS SRV/A    — _sip._tcp, _sips._tls (via dnsapi.dll di Windows)
    // ════════════════════════════════════════════════════════════════════════
    public class SbcHealthPanel : Form
    {
        // Palette coerente col resto dell'app
        static readonly Color CBg    = Color.FromArgb(24, 24, 32);
        static readonly Color CBar   = Color.FromArgb(34, 34, 46);
        static readonly Color CInput = Color.FromArgb(45, 45, 60);
        static readonly Color COut   = Color.FromArgb(16, 16, 22);

        static readonly Random Rnd = new Random();

        public SbcHealthPanel()
        {
            Text = "LosaTermVoip — SBC Health (SIP OPTIONS / TLS / DNS)";
            Size = new Size(880, 620);
            MinimumSize = new Size(640, 460);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildOptionsTab());
            tabs.TabPages.Add(BuildTlsTab());
            tabs.TabPages.Add(BuildDnsTab());
            Controls.Add(tabs);
        }

        // ───────────────────────── helper UI comuni ─────────────────────────

        static Label Lbl(string t, int x, int y)
        {
            return new Label { Text = t, Location = new Point(x, y), AutoSize = true, ForeColor = Color.LightGray };
        }

        static TextBox Txt(int x, int y, int w, string val)
        {
            return new TextBox { Location = new Point(x, y), Width = w, Text = val, BackColor = CInput, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        }

        static Button Btn(string t, int x, int y, int w, Color c)
        {
            return new Button { Text = t, Location = new Point(x, y), Width = w, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        }

        static TextBox OutBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false,
                ScrollBars = ScrollBars.Both, BackColor = COut, ForeColor = Color.Gainsboro,
                Font = new Font("Consolas", 9), BorderStyle = BorderStyle.None
            };
        }

        // Scrive nel box di output dal thread di lavoro in sicurezza
        void Show(TextBox box, string text)
        {
            if (box.InvokeRequired) { box.BeginInvoke((MethodInvoker)delegate { box.Text = text; }); }
            else { box.Text = text; }
        }

        // ════════════════════════ TAB 1 — SIP OPTIONS ════════════════════════

        TabPage BuildOptionsTab()
        {
            var tab = new TabPage("  SIP OPTIONS  ") { BackColor = CBg, ForeColor = Color.White };
            var top = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = CBar };

            top.Controls.Add(Lbl("Host / IP:", 10, 14));
            var txtHost = Txt(90, 11, 220, "");
            top.Controls.Add(txtHost);

            top.Controls.Add(Lbl(L.T("sbc.port"), 330, 14));
            var txtPort = Txt(382, 11, 60, "5060");
            top.Controls.Add(txtPort);

            top.Controls.Add(Lbl(L.T("sbc.transport"), 462, 14));
            var cboTrans = new ComboBox { Location = new Point(534, 11), Width = 80, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CInput, ForeColor = Color.White };
            cboTrans.Items.AddRange(new object[] { "UDP", "TCP", "TLS" });
            cboTrans.SelectedIndex = 0;
            cboTrans.SelectedIndexChanged += (s, e) => {
                if ((string)cboTrans.SelectedItem == "TLS" && txtPort.Text == "5060") txtPort.Text = "5061";
                if ((string)cboTrans.SelectedItem != "TLS" && txtPort.Text == "5061") txtPort.Text = "5060";
            };
            top.Controls.Add(cboTrans);

            top.Controls.Add(Lbl(L.T("sbc.timeout"), 10, 52));
            var txtTo = Txt(108, 49, 60, "3000");
            top.Controls.Add(txtTo);

            var btnGo = Btn(L.T("sbc.send_options"), 200, 49, 140, Color.FromArgb(30, 110, 30));
            top.Controls.Add(btnGo);

            var lblStat = new Label { Text = "", Location = new Point(352, 53), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            top.Controls.Add(lblStat);

            var info = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft,
                Text = L.T("sbc.opt_hint") };
            var outBox = OutBox();

            btnGo.Click += (s, e) => {
                string host = txtHost.Text.Trim();
                if (host.Length == 0) { MessageBox.Show("Inserisci host o IP.", "SIP OPTIONS"); return; }
                int port, to;
                if (!int.TryParse(txtPort.Text.Trim(), out port)) port = 5060;
                if (!int.TryParse(txtTo.Text.Trim(), out to)) to = 3000;
                string trans = (string)cboTrans.SelectedItem;
                btnGo.Enabled = false;
                lblStat.Text = "…"; lblStat.ForeColor = Color.Khaki;
                Show(outBox, "Invio OPTIONS " + trans + " a " + host + ":" + port + " …");
                var th = new Thread(delegate () { RunOptions(host, port, trans, to, outBox, lblStat, btnGo); });
                th.IsBackground = true; th.Start();
            };

            tab.Controls.Add(outBox);
            tab.Controls.Add(info);
            tab.Controls.Add(top);
            return tab;
        }

        void RunOptions(string host, int port, string trans, int timeout, TextBox outBox, Label lblStat, Button btnGo)
        {
            string result;
            string statTxt = "";
            Color statCol = Color.Gray;
            try
            {
                string resp;
                long ms;
                if (trans == "UDP") resp = OptionsUdp(host, port, timeout, out ms);
                else                resp = OptionsStream(host, port, trans == "TLS", timeout, out ms);

                int code = ParseStatus(resp);
                if (code > 0)
                {
                    statTxt = "● " + code; statCol = (code >= 200 && code < 300) ? Color.LightGreen : Color.Orange;
                    result = "✓ Risposta in " + ms + " ms (status " + code + ")\r\n" +
                             "──────────────────────────────────────────\r\n" + resp;
                }
                else
                {
                    statTxt = "● ?"; statCol = Color.Orange;
                    result = "Risposta ricevuta in " + ms + " ms (status non riconosciuto)\r\n" +
                             "──────────────────────────────────────────\r\n" + resp;
                }
            }
            catch (Exception ex)
            {
                statTxt = "● timeout / errore"; statCol = Color.OrangeRed;
                result = "✗ Nessuna risposta.\r\n\r\n" + ex.Message +
                         "\r\n\r\nPossibili cause: trunk giù, porta/trasporto errati, firewall, " +
                         "oppure il SBC non risponde a OPTIONS da IP non whitelistati.";
            }
            Show(outBox, result);
            if (lblStat.InvokeRequired) lblStat.BeginInvoke((MethodInvoker)delegate { lblStat.Text = statTxt; lblStat.ForeColor = statCol; });
            if (btnGo.InvokeRequired) btnGo.BeginInvoke((MethodInvoker)delegate { btnGo.Enabled = true; });
        }

        // Costruisce una richiesta SIP OPTIONS valida
        static string BuildOptions(string host, int port, string localIp, int localPort, string transport)
        {
            string branch = "z9hG4bK" + RandHex(16);
            string tag    = RandHex(10);
            string callId = RandHex(16) + "@" + localIp;
            var sb = new StringBuilder();
            sb.Append("OPTIONS sip:" + host + ":" + port + " SIP/2.0\r\n");
            sb.Append("Via: SIP/2.0/" + transport + " " + localIp + ":" + localPort + ";branch=" + branch + ";rport\r\n");
            sb.Append("Max-Forwards: 70\r\n");
            sb.Append("From: \"LosaTermVoip\" <sip:losaterm@" + localIp + ">;tag=" + tag + "\r\n");
            sb.Append("To: <sip:" + host + ">\r\n");
            sb.Append("Call-ID: " + callId + "\r\n");
            sb.Append("CSeq: 1 OPTIONS\r\n");
            sb.Append("Contact: <sip:losaterm@" + localIp + ":" + localPort + ">\r\n");
            sb.Append("User-Agent: LosaTermVoip-SBCHealth\r\n");
            sb.Append("Accept: application/sdp\r\n");
            sb.Append("Content-Length: 0\r\n\r\n");
            return sb.ToString();
        }

        static string OptionsUdp(string host, int port, int timeout, out long ms)
        {
            using (var u = new UdpClient())
            {
                u.Client.ReceiveTimeout = timeout;
                u.Connect(host, port);
                var local = (IPEndPoint)u.Client.LocalEndPoint;
                string req = BuildOptions(host, port, local.Address.ToString(), local.Port, "UDP");
                byte[] data = Encoding.ASCII.GetBytes(req);
                var sw = Stopwatch.StartNew();
                u.Send(data, data.Length);
                var rep = new IPEndPoint(IPAddress.Any, 0);
                byte[] resp = u.Receive(ref rep);
                sw.Stop(); ms = sw.ElapsedMilliseconds;
                return Encoding.ASCII.GetString(resp);
            }
        }

        static string OptionsStream(string host, int port, bool tls, int timeout, out long ms)
        {
            using (var c = new TcpClient())
            {
                var ar = c.BeginConnect(host, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeout)) throw new Exception("Connessione TCP scaduta (" + timeout + " ms)");
                c.EndConnect(ar);

                Stream stream = c.GetStream();
                if (tls)
                {
                    var ssl = new SslStream(stream, false, delegate { return true; });
                    ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                    stream = ssl;
                }
                var local = (IPEndPoint)c.Client.LocalEndPoint;
                string req = BuildOptions(host, port, local.Address.ToString(), local.Port, tls ? "TLS" : "TCP");
                byte[] data = Encoding.ASCII.GetBytes(req);
                var sw = Stopwatch.StartNew();
                stream.Write(data, 0, data.Length);
                stream.Flush();
                string resp = ReadResponse(stream, timeout);
                sw.Stop(); ms = sw.ElapsedMilliseconds;
                if (resp.Length == 0) throw new Exception("Connesso ma nessun dato ricevuto entro il timeout.");
                return resp;
            }
        }

        static string ReadResponse(Stream s, int timeout)
        {
            s.ReadTimeout = timeout;
            var sb = new StringBuilder();
            byte[] buf = new byte[4096];
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeout)
            {
                int n;
                try { n = s.Read(buf, 0, buf.Length); }
                catch (IOException) { break; }
                if (n <= 0) break;
                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
                if (sb.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0) break;
            }
            return sb.ToString();
        }

        static int ParseStatus(string resp)
        {
            if (string.IsNullOrEmpty(resp)) return 0;
            string first = resp.Split('\n')[0];
            if (!first.StartsWith("SIP/2.0", StringComparison.Ordinal)) return 0;
            string[] parts = first.Split(' ');
            int code;
            if (parts.Length >= 2 && int.TryParse(parts[1], out code)) return code;
            return 0;
        }

        // ═══════════════════════ TAB 2 — TLS / CERT ═════════════════════════

        TabPage BuildTlsTab()
        {
            var tab = new TabPage("  " + L.T("sbc.tab_tls") + "  ") { BackColor = CBg, ForeColor = Color.White };
            var top = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = CBar };

            top.Controls.Add(Lbl("Host:", 10, 18));
            var txtHost = Txt(56, 15, 240, "");
            top.Controls.Add(txtHost);

            top.Controls.Add(Lbl(L.T("sbc.port"), 312, 18));
            var txtPort = Txt(360, 15, 60, "5061");
            top.Controls.Add(txtPort);

            var btnGo = Btn(L.T("sbc.check"), 440, 14, 120, Color.FromArgb(40, 80, 140));
            top.Controls.Add(btnGo);

            var info = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft,
                Text = L.T("sbc.tls_hint") };
            var outBox = OutBox();

            btnGo.Click += (s, e) => {
                string host = txtHost.Text.Trim();
                if (host.Length == 0) { MessageBox.Show("Inserisci host.", "TLS"); return; }
                int port;
                if (!int.TryParse(txtPort.Text.Trim(), out port)) port = 5061;
                btnGo.Enabled = false;
                Show(outBox, "Connessione TLS a " + host + ":" + port + " …");
                var th = new Thread(delegate () { RunTls(host, port, outBox, btnGo); });
                th.IsBackground = true; th.Start();
            };

            tab.Controls.Add(outBox);
            tab.Controls.Add(info);
            tab.Controls.Add(top);
            return tab;
        }

        void RunTls(string host, int port, TextBox outBox, Button btnGo)
        {
            string result;
            try
            {
                X509Certificate2 cert = null;
                string chainInfo = "";
                using (var c = new TcpClient())
                {
                    var ar = c.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(5000)) throw new Exception("Connessione scaduta.");
                    c.EndConnect(ar);

                    RemoteCertificateValidationCallback cb = delegate (object snd, X509Certificate crt, X509Chain chn, System.Net.Security.SslPolicyErrors err)
                    {
                        if (crt != null) cert = new X509Certificate2(crt);
                        chainInfo = (err == System.Net.Security.SslPolicyErrors.None) ? "catena valida" : err.ToString();
                        return true; // accetta per poter ispezionare anche cert scaduti/non fidati
                    };
                    var ssl = new SslStream(c.GetStream(), false, cb);
                    ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);

                    if (cert == null) throw new Exception("Handshake riuscito ma nessun certificato presentato.");

                    string san = "(nessuno)";
                    foreach (X509Extension ext in cert.Extensions)
                        if (ext.Oid != null && ext.Oid.Value == "2.5.29.17")
                            san = ext.Format(false);

                    double days = (cert.NotAfter - DateTime.Now).TotalDays;
                    string expFlag = days < 0 ? "  ⛔ SCADUTO!" : (days < 30 ? "  ⚠️ scade tra " + (int)days + " giorni" : "  ✓");

                    var sb = new StringBuilder();
                    sb.Append("TLS negoziato : " + ssl.SslProtocol + "\r\n");
                    sb.Append("Cifratura     : " + ssl.CipherAlgorithm + " " + ssl.CipherStrength + " bit\r\n");
                    sb.Append("Validazione   : " + chainInfo + "\r\n");
                    sb.Append("──────────────────────────────────────────\r\n");
                    sb.Append("Soggetto (CN) : " + cert.GetNameInfo(X509NameType.SimpleName, false) + "\r\n");
                    sb.Append("SAN           : " + san.Replace("\r\n", ", ").TrimEnd(',', ' ') + "\r\n");
                    sb.Append("Emittente     : " + cert.Issuer + "\r\n");
                    sb.Append("Valido da     : " + cert.NotBefore + "\r\n");
                    sb.Append("Valido fino   : " + cert.NotAfter + expFlag + "\r\n");
                    sb.Append("Serial        : " + cert.SerialNumber + "\r\n");
                    sb.Append("Thumbprint    : " + cert.Thumbprint + "\r\n");
                    result = sb.ToString();
                }
            }
            catch (Exception ex)
            {
                result = "✗ Errore TLS:\r\n\r\n" + ex.Message +
                         "\r\n\r\nVerifica host/porta (di solito 5061), che il SBC esponga TLS e che il firewall sia aperto.";
            }
            Show(outBox, result);
            if (btnGo.InvokeRequired) btnGo.BeginInvoke((MethodInvoker)delegate { btnGo.Enabled = true; });
        }

        // ═══════════════════════ TAB 3 — DNS SRV / A ════════════════════════

        TabPage BuildDnsTab()
        {
            var tab = new TabPage("  DNS SRV / A  ") { BackColor = CBg, ForeColor = Color.White };
            var top = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = CBar };

            top.Controls.Add(Lbl(L.T("sbc.domain"), 10, 14));
            var txtDom = Txt(74, 11, 250, "");
            top.Controls.Add(txtDom);

            top.Controls.Add(Lbl(L.T("sbc.srv_service"), 10, 52));
            var cboSrv = new ComboBox { Location = new Point(104, 49), Width = 160, DropDownStyle = ComboBoxStyle.DropDown, BackColor = CInput, ForeColor = Color.White };
            cboSrv.Items.AddRange(new object[] {
                "_sip._udp", "_sip._tcp", "_sips._tcp", "_sips._tls",
                "_collab-edge._tls",        // Cisco MRA / Expressway edge
                "_cisco-uds._tcp",          // Cisco UDS (interno)
                "_sipfederationtls._tcp",   // federazione SIP (Teams/Lync)
                "_xmpp-server._tcp", "_xmpp-client._tcp",
                "_h323ls._udp", "_h323cs._tcp",
                "(solo record A)" });
            cboSrv.SelectedIndex = 1;
            top.Controls.Add(cboSrv);

            var btnGo = Btn(L.T("sbc.resolve"), 280, 49, 110, Color.FromArgb(90, 60, 140));
            top.Controls.Add(btnGo);

            var info = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft,
                Text = L.T("sbc.dns_hint") };
            var outBox = OutBox();

            btnGo.Click += (s, e) => {
                string dom = txtDom.Text.Trim().TrimStart('.');
                if (dom.Length == 0) { MessageBox.Show("Inserisci un dominio.", "DNS"); return; }
                string svc = (string)cboSrv.SelectedItem;
                btnGo.Enabled = false;
                Show(outBox, "Risoluzione in corso …");
                var th = new Thread(delegate () { RunDns(svc, dom, outBox, btnGo); });
                th.IsBackground = true; th.Start();
            };

            tab.Controls.Add(outBox);
            tab.Controls.Add(info);
            tab.Controls.Add(top);
            return tab;
        }

        void RunDns(string svc, string dom, TextBox outBox, Button btnGo)
        {
            var sb = new StringBuilder();
            var aTargets = new List<string>();

            if (svc != null && svc.StartsWith("_", StringComparison.Ordinal))
            {
                string srvName = svc + "." + dom;
                sb.Append("SRV  " + srvName + "\r\n──────────────────────────────────────────\r\n");
                try
                {
                    List<SrvRec> recs = SrvLookup(srvName);
                    if (recs.Count == 0) sb.Append("  (nessun record SRV)\r\n");
                    foreach (SrvRec r in recs)
                    {
                        sb.Append("  prio " + r.Priority + "  peso " + r.Weight + "  porta " + r.Port + "  →  " + r.Target + "\r\n");
                        aTargets.Add(r.Target);
                    }
                }
                catch (Exception ex) { sb.Append("  Errore: " + ex.Message + "\r\n"); }
                sb.Append("\r\n");
            }
            else
            {
                aTargets.Add(dom);
            }

            // Record A per i target trovati (o per il dominio stesso)
            foreach (string t in aTargets)
            {
                sb.Append("A    " + t + "\r\n");
                try
                {
                    IPAddress[] ips = Dns.GetHostAddresses(t);
                    foreach (IPAddress ip in ips)
                        if (ip.AddressFamily == AddressFamily.InterNetwork || ip.AddressFamily == AddressFamily.InterNetworkV6)
                            sb.Append("       " + ip + "\r\n");
                }
                catch (Exception ex) { sb.Append("       Errore: " + ex.Message + "\r\n"); }
                sb.Append("\r\n");
            }

            Show(outBox, sb.ToString());
            if (btnGo.InvokeRequired) btnGo.BeginInvoke((MethodInvoker)delegate { btnGo.Enabled = true; });
        }

        struct SrvRec { public ushort Priority, Weight, Port; public string Target; }

        // ── Interop dnsapi.dll (resolver nativo di Windows, niente nslookup) ──

        const ushort DNS_TYPE_SRV = 33;
        const int DnsFreeRecordList = 1;

        [DllImport("dnsapi.dll", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int DnsQuery_W(string name, ushort type, uint options, IntPtr extra, ref IntPtr results, IntPtr reserved);

        [DllImport("dnsapi.dll")]
        static extern void DnsRecordListFree(IntPtr recordList, int freeType);

        [StructLayout(LayoutKind.Sequential)]
        struct DnsRecordHeader
        {
            public IntPtr pNext;
            public IntPtr pName;
            public ushort wType;
            public ushort wDataLength;
            public uint flags;
            public uint dwTtl;
            public uint dwReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DnsSrvData
        {
            public IntPtr pNext;
            public IntPtr pName;
            public ushort wType;
            public ushort wDataLength;
            public uint flags;
            public uint dwTtl;
            public uint dwReserved;
            [MarshalAs(UnmanagedType.LPWStr)] public string pNameTarget;
            public ushort wPriority;
            public ushort wWeight;
            public ushort wPort;
            public ushort Pad;
        }

        static List<SrvRec> SrvLookup(string name)
        {
            var list = new List<SrvRec>();
            IntPtr results = IntPtr.Zero;
            int ret = DnsQuery_W(name, DNS_TYPE_SRV, 0, IntPtr.Zero, ref results, IntPtr.Zero);
            if (ret != 0)
            {
                if (ret == 9501) return list;                       // DNS_INFO_NO_RECORDS
                if (ret == 9003) throw new Exception("dominio inesistente (NXDOMAIN)");
                throw new Win32Exception(ret);
            }
            try
            {
                IntPtr ptr = results;
                while (ptr != IntPtr.Zero)
                {
                    var hdr = (DnsRecordHeader)Marshal.PtrToStructure(ptr, typeof(DnsRecordHeader));
                    if (hdr.wType == DNS_TYPE_SRV)
                    {
                        var srv = (DnsSrvData)Marshal.PtrToStructure(ptr, typeof(DnsSrvData));
                        var r = new SrvRec();
                        r.Priority = srv.wPriority; r.Weight = srv.wWeight; r.Port = srv.wPort; r.Target = srv.pNameTarget;
                        list.Add(r);
                    }
                    ptr = hdr.pNext;
                }
            }
            finally { DnsRecordListFree(results, DnsFreeRecordList); }
            return list;
        }

        // ───────────────────────── util ─────────────────────────

        static string RandHex(int chars)
        {
            const string h = "0123456789abcdef";
            var sb = new StringBuilder(chars);
            for (int i = 0; i < chars; i++) sb.Append(h[Rnd.Next(16)]);
            return sb.ToString();
        }
    }
}
