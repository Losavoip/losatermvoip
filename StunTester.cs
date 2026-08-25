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
    //  Tester STUN / NAT (RFC 5389) — IP pubblico riflesso + tipo NAT. Bilingue.
    // ════════════════════════════════════════════════════════════════════════
    public class StunTesterPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        ComboBox cmbServer;
        TextBox txtOut;
        Button btnTest, btnNat;

        public StunTesterPanel()
        {
            Text = "LosaTermVoip — STUN / NAT Tester";
            Size = new Size(640, 460);
            MinimumSize = new Size(520, 360);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(34,34,46) };
            top.Controls.Add(new Label { Text=L.B("Server STUN:","STUN server:"), Location=new Point(14,16), AutoSize=true, ForeColor=Color.LightGray });
            cmbServer = new ComboBox { Location=new Point(110,13), Width=320, DropDownStyle=ComboBoxStyle.DropDown,
                BackColor=CIn, ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
            cmbServer.Items.AddRange(new object[] {
                "stun.l.google.com:19302",
                "stun1.l.google.com:19302",
                "stun.cloudflare.com:3478",
                "stun.sipgate.net:3478"
            });
            cmbServer.SelectedIndex = 0;
            top.Controls.Add(cmbServer);
            btnTest = new Button { Text="🌐  Test", Location=new Point(444,11), Width=140, Height=28,
                FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(40,80,140), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btnTest.FlatAppearance.BorderSize=0; btnTest.Click += (s,e)=>RunTest();
            top.Controls.Add(btnTest);
            btnNat = new Button { Text=L.B("🔎 Tipo NAT","🔎 NAT type"), Location=new Point(444,44), Width=140, Height=24,
                FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(80,60,120), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btnNat.FlatAppearance.BorderSize=0; btnNat.Click += (s,e)=>ClassifyNat();
            top.Controls.Add(btnNat);
            top.Controls.Add(new Label { Text=L.B("IP pubblico riflesso + classificazione NAT (cone vs simmetrico).","Reflexive public IP + NAT classification (cone vs symmetric)."),
                Location=new Point(14,48), AutoSize=true, ForeColor=Color.Gray });

            var btnR = ReportHelper.MakeButton(594, 43);
            btnR.Click += (s,e)=>ReportHelper.ExportText(this, "STUN / NAT", txtOut.Text);
            top.Controls.Add(btnR);

            txtOut = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
                BackColor=Color.FromArgb(12,16,24), ForeColor=Color.LimeGreen, Font=new Font("Consolas",10), BorderStyle=BorderStyle.None };
            txtOut.TextChanged += (s,e)=>ReportHelper.Set("STUN / NAT", txtOut.Text);

            Controls.Add(txtOut);
            Controls.Add(top);
        }

        void RunTest()
        {
            string srv = (cmbServer.Text ?? "").Trim();
            txtOut.Text = L.B("Interrogo ","Querying ") + srv + " ...\r\n";
            btnTest.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ => {
                string res = DoStun(srv);
                if (txtOut.IsHandleCreated)
                    txtOut.BeginInvoke((MethodInvoker)delegate { txtOut.AppendText(res); btnTest.Enabled = true; });
            });
        }

        void ClassifyNat()
        {
            string srvA = (cmbServer.Text ?? "").Trim();
            string srvB = srvA.ToLower().Contains("cloudflare") ? "stun.l.google.com:19302" : "stun.cloudflare.com:3478";
            txtOut.Text = L.B("Classificazione NAT (confronto ","NAT classification (comparing ") + srvA + " vs " + srvB + ")...\r\n\r\n";
            btnNat.Enabled = false; btnTest.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ => {
                var sb = new StringBuilder();
                string la, lb, ea, eb;
                IPEndPoint ma = GetMapped(srvA, out la, out ea);
                IPEndPoint mb = GetMapped(srvB, out lb, out eb);
                if (ma == null) sb.AppendLine("✗ Server A (" + srvA + "): " + ea);
                if (mb == null) sb.AppendLine("✗ Server B (" + srvB + "): " + eb);
                if (ma != null && mb != null)
                {
                    sb.AppendLine("  " + L.B("IP locale       ","Local IP        ") + ": " + la);
                    sb.AppendLine("  " + L.B("Riflesso via A  ","Reflexive via A ") + ": " + ma.Address + ":" + ma.Port);
                    sb.AppendLine("  " + L.B("Riflesso via B  ","Reflexive via B ") + ": " + mb.Address + ":" + mb.Port);
                    sb.AppendLine("");
                    bool natted = la != ma.Address.ToString();
                    if (!natted)
                        sb.AppendLine(L.B("  → NESSUN NAT (IP pubblico diretto). RTP OK.","  → NO NAT (direct public IP). RTP OK."));
                    else if (ma.Port == mb.Port)
                    {
                        sb.AppendLine(L.B("  → NAT NON SIMMETRICO (full / restricted / port-restricted cone).","  → NON-SYMMETRIC NAT (full / restricted / port-restricted cone)."));
                        sb.AppendLine(L.B("    Porta pubblica uguale verso destinazioni diverse:","    Same public port toward different destinations:"));
                        sb.AppendLine(L.B("    l'RTP di norma funziona con STUN/keepalive. ✅","    RTP usually works with STUN/keepalive. ✅"));
                    }
                    else
                    {
                        sb.AppendLine(L.B("  → ⚠ NAT SIMMETRICO","  → ⚠ SYMMETRIC NAT"));
                        sb.AppendLine(L.B("    Porta pubblica DIVERSA per ogni destinazione (","    DIFFERENT public port per destination (") + ma.Port + " vs " + mb.Port + ").");
                        sb.AppendLine(L.B("    L'RTP fallisce / è monodirezionale senza TURN o SBC con NAT handling.","    RTP fails / is one-way without TURN or an SBC with NAT handling."));
                        sb.AppendLine(L.B("    Tipico dei firewall enterprise → usa l'SBC come media relay.","    Typical of enterprise firewalls → use the SBC as media relay."));
                    }
                }
                if (txtOut.IsHandleCreated)
                    txtOut.BeginInvoke((MethodInvoker)delegate { txtOut.AppendText(sb.ToString()); btnNat.Enabled = true; btnTest.Enabled = true; });
            });
        }

        static IPEndPoint GetMapped(string server, out string localPrimary, out string err)
        {
            localPrimary = "?"; err = null;
            string host; int port = 3478;
            int idx = server.LastIndexOf(':');
            if (idx > 0) { host = server.Substring(0, idx); int.TryParse(server.Substring(idx+1), out port); }
            else host = server;
            if (port <= 0) port = 3478;
            try
            {
                IPAddress ip = null;
                foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
                if (ip == null) { err = L.B("DNS fallito","DNS failed"); return null; }
                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 3000;
                    byte[] req = new byte[20];
                    req[0]=0x00; req[1]=0x01; req[4]=0x21; req[5]=0x12; req[6]=0xA4; req[7]=0x42;
                    var rnd = new Random(); for (int i=8;i<20;i++) req[i]=(byte)rnd.Next(256);
                    udp.Send(req, 20, new IPEndPoint(ip, port));
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp;
                    try { resp = udp.Receive(ref remote); } catch (SocketException) { err = "timeout"; return null; }
                    try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect("8.8.8.8", 65530); localPrimary = ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { }
                    IPEndPoint m = ParseXorMappedAddress(resp); if (m == null) m = ParseMappedAddress(resp);
                    if (m == null) { err = "no MAPPED-ADDRESS"; return null; }
                    return m;
                }
            }
            catch (Exception ex) { err = ex.Message; return null; }
        }

        string DoStun(string server)
        {
            string host; int port = 3478;
            int idx = server.LastIndexOf(':');
            if (idx > 0) { host = server.Substring(0, idx); int.TryParse(server.Substring(idx+1), out port); }
            else host = server;
            if (port <= 0) port = 3478;

            var sb = new StringBuilder();
            try
            {
                IPAddress[] addrs = Dns.GetHostAddresses(host);
                IPAddress ip = null;
                foreach (var a in addrs) if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
                if (ip == null) return L.B("✗ Impossibile risolvere ","✗ Cannot resolve ") + host + L.B(" in IPv4.\r\n"," to IPv4.\r\n");

                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 3000;
                    byte[] req = new byte[20];
                    req[0]=0x00; req[1]=0x01; req[2]=0x00; req[3]=0x00;
                    req[4]=0x21; req[5]=0x12; req[6]=0xA4; req[7]=0x42;
                    var rnd = new Random();
                    for (int i=8;i<20;i++) req[i]=(byte)rnd.Next(256);
                    udp.Send(req, req.Length, new IPEndPoint(ip, port));
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp;
                    try { resp = udp.Receive(ref remote); }
                    catch (SocketException) { return L.B("✗ Nessuna risposta (timeout 3s).\r\nIl server STUN potrebbe essere irraggiungibile o l'UDP "+port+" è bloccato dal firewall.\r\n","✗ No response (3s timeout).\r\nThe STUN server may be unreachable or UDP "+port+" is blocked by the firewall.\r\n"); }

                    IPEndPoint mapped = ParseXorMappedAddress(resp);
                    if (mapped == null) mapped = ParseMappedAddress(resp);
                    if (mapped == null) return L.B("✗ Risposta STUN ricevuta ma senza MAPPED-ADDRESS.\r\n","✗ STUN reply received but without MAPPED-ADDRESS.\r\n");

                    string localPrimary = "?";
                    try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect("8.8.8.8", 65530); localPrimary = ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { }

                    bool natted = localPrimary != mapped.Address.ToString();
                    sb.AppendLine(L.B("✔ Risposta STUN da ","✔ STUN reply from ") + host + ":" + port);
                    sb.AppendLine("");
                    sb.AppendLine("  " + L.B("IP locale PC      ","Local PC IP       ") + ": " + localPrimary);
                    sb.AppendLine("  " + L.B("IP:porta riflessi ","Reflexive IP:port ") + ": " + mapped.Address + ":" + mapped.Port + "   (server-reflexive)");
                    sb.AppendLine("");
                    sb.AppendLine(natted
                        ? L.B("  → Sei DIETRO NAT: il pubblico ("+mapped.Address+") è diverso dal locale.","  → You are BEHIND NAT: public ("+mapped.Address+") differs from local.")
                        : L.B("  → IP pubblico = IP locale: nessun NAT (indirizzo pubblico diretto).","  → Public IP = local IP: no NAT (direct public address)."));
                    sb.AppendLine("");
                    sb.AppendLine(L.B("  Nota: per Teams Direct Routing / WebRTC l'SBC/endpoint deve poter","  Note: for Teams Direct Routing / WebRTC the SBC/endpoint must be able to"));
                    sb.AppendLine(L.B("  raggiungere questo IP:porta pubblico. Se l'UDP è filtrato, il media fallisce.","  reach this public IP:port. If UDP is filtered, media fails."));
                }
            }
            catch (Exception ex) { return L.B("✗ Errore: ","✗ Error: ") + ex.Message + "\r\n"; }
            return sb.ToString();
        }

        static IPEndPoint ParseXorMappedAddress(byte[] resp)
        {
            int p = ReadAttr(resp, 0x0020);
            if (p < 0) return null;
            if (resp[p+1] != 0x01) return null;
            int xport = (resp[p+2]<<8 | resp[p+3]) ^ 0x2112;
            byte[] xa = new byte[4];
            xa[0]=(byte)(resp[p+4]^0x21); xa[1]=(byte)(resp[p+5]^0x12); xa[2]=(byte)(resp[p+6]^0xA4); xa[3]=(byte)(resp[p+7]^0x42);
            return new IPEndPoint(new IPAddress(xa), xport);
        }

        static IPEndPoint ParseMappedAddress(byte[] resp)
        {
            int p = ReadAttr(resp, 0x0001);
            if (p < 0) return null;
            if (resp[p+1] != 0x01) return null;
            int port = resp[p+2]<<8 | resp[p+3];
            byte[] a = new byte[]{ resp[p+4], resp[p+5], resp[p+6], resp[p+7] };
            return new IPEndPoint(new IPAddress(a), port);
        }

        static int ReadAttr(byte[] resp, int wantType)
        {
            if (resp == null || resp.Length < 20) return -1;
            int i = 20;
            while (i + 4 <= resp.Length)
            {
                int type = resp[i]<<8 | resp[i+1];
                int len  = resp[i+2]<<8 | resp[i+3];
                int val  = i + 4;
                if (type == wantType && val + len <= resp.Length) return val;
                i = val + len + ((4 - (len % 4)) % 4);
            }
            return -1;
        }
    }
}
