using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  SIP Registration Live — REGISTER con Digest MD5 + monitor refresh. Bilingue.
    // ════════════════════════════════════════════════════════════════════════
    public class SipRegisterPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtReg, txtUser, txtPass, txtExp;
        CheckBox chkKeep;
        Button btnReg, btnUnreg;
        RichTextBox rtb;
        System.Windows.Forms.Timer timer;

        string callId; int cseq; string host; int port; string user, pass; int expires;

        public SipRegisterPanel()
        {
            Text = "LosaTermVoip — SIP Registration Live";
            Size = new Size(780, 540);
            MinimumSize = new Size(620, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
            FormClosed += (s,e)=>{ if (timer!=null) timer.Stop(); };
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(34,34,46) };
            int x1=14, x2=120, y=12;
            top.Controls.Add(Lbl(L.B("Registrar (host[:porta]):","Registrar (host[:port]):"), x1, y));
            txtReg = T(x2+60, y, 230); txtReg.Text = "pbx.azienda.it"; top.Controls.Add(txtReg); y+=30;
            top.Controls.Add(Lbl(L.B("Utente:","User:"), x1, y));
            txtUser = T(x2, y, 170); top.Controls.Add(txtUser);
            top.Controls.Add(Lbl("Password:", x1+320, y));
            txtPass = T(x1+400, y, 150); txtPass.UseSystemPasswordChar = true; top.Controls.Add(txtPass); y+=30;
            top.Controls.Add(Lbl("Expires (s):", x1, y));
            txtExp = T(x2, y, 80); txtExp.Text="3600"; top.Controls.Add(txtExp);
            chkKeep = new CheckBox { Text=L.B("Mantieni registrato (refresh automatico)","Keep registered (auto-refresh)"), Location=new Point(x2+100,y+2), AutoSize=true, ForeColor=Color.LightGray };
            top.Controls.Add(chkKeep); y+=34;
            btnReg = Btn(L.B("▶ Registra","▶ Register"), x2, y, 120, Color.FromArgb(30,110,30)); btnReg.Click += (s,e)=>StartRegister(); top.Controls.Add(btnReg);
            btnUnreg = Btn(L.B("■ De-registra","■ Unregister"), x2+128, y, 120, Color.FromArgb(120,30,30)); btnUnreg.Enabled=false; btnUnreg.Click += (s,e)=>Unregister(); top.Controls.Add(btnUnreg);
            top.Controls.Add(new Label { Text="UDP · Digest MD5 (qop auth)", Location=new Point(x2+260,y+5), AutoSize=true, ForeColor=Color.Gray });

            rtb = new RichTextBox { Dock=DockStyle.Fill, ReadOnly=true, BackColor=Color.FromArgb(12,16,24),
                ForeColor=Color.Gainsboro, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };

            Controls.Add(rtb);
            Controls.Add(top);
        }

        void StartRegister()
        {
            host = (txtReg.Text ?? "").Trim(); port = 5060;
            int c = host.LastIndexOf(':'); if (c>0){ string p=host.Substring(c+1); host=host.Substring(0,c); int.TryParse(p, out port); }
            if (port<=0) port=5060;
            user = (txtUser.Text ?? "").Trim(); pass = txtPass.Text ?? "";
            if (!int.TryParse(txtExp.Text, out expires) || expires<0) expires=3600;
            if (host.Length==0 || user.Length==0) { MessageBox.Show(L.B("Compila registrar e utente.","Fill in registrar and user."), "Register"); return; }
            callId = Guid.NewGuid().ToString("N").Substring(0,20); cseq = 0;
            rtb.Clear();
            btnReg.Enabled=false; btnUnreg.Enabled=true;
            RegisterOnce(expires, true);
        }

        void Unregister()
        {
            if (timer!=null) timer.Stop();
            if (callId != null) RegisterOnce(0, false);
            btnReg.Enabled=true; btnUnreg.Enabled=false;
        }

        void RegisterOnce(int exp, bool scheduleRefresh)
        {
            ThreadPool.QueueUserWorkItem(_ => {
                int grantedExp = exp;
                try
                {
                    IPAddress ip = null;
                    foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily==AddressFamily.InterNetwork){ ip=a; break; }
                    if (ip==null) { Log(L.B("✗ DNS: impossibile risolvere ","✗ DNS: cannot resolve ") + host + "\r\n", Color.OrangeRed); Reenable(); return; }
                    string local="0.0.0.0";
                    try { using (var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,0)){ s.Connect(ip,port); local=((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch {}

                    using (var udp = new UdpClient(0))
                    {
                        udp.Client.ReceiveTimeout = 4000;
                        int lport = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
                        string uri = "sip:" + host;

                        cseq++;
                        string m1 = BuildRegister(local, lport, uri, exp, null);
                        Log("→ REGISTER (CSeq " + cseq + ", Expires " + exp + ")\r\n", Color.FromArgb(120,200,255));
                        string r1 = SendRecv(udp, new IPEndPoint(ip, port), m1);
                        int code1 = StatusCode(r1);
                        Log("← " + FirstLine(r1) + "\r\n", code1==200?Color.LimeGreen:Color.Orange);

                        if (code1==401 || code1==407)
                        {
                            string hdr = HeaderValue(r1, code1==407 ? "Proxy-Authenticate" : "WWW-Authenticate");
                            string realm = Param(hdr,"realm"), nonce = Param(hdr,"nonce"), qop = Param(hdr,"qop"), opaque = Param(hdr,"opaque");
                            Log("   challenge: realm=\"" + realm + "\"  nonce=\"" + nonce + "\"" + (qop!=null?"  qop="+qop:"") + "\r\n", Color.Gray);
                            string auth = BuildAuth(user, pass, realm, nonce, qop, opaque, uri);
                            cseq++;
                            string m2 = BuildRegister(local, lport, uri, exp, auth);
                            Log("→ REGISTER + Authorization (CSeq " + cseq + ")\r\n", Color.FromArgb(120,200,255));
                            string r2 = SendRecv(udp, new IPEndPoint(ip, port), m2);
                            int code2 = StatusCode(r2);
                            Log("← " + FirstLine(r2) + "\r\n", code2==200?Color.LimeGreen:Color.OrangeRed);
                            if (code2==200) { grantedExp = ExtractExpires(r2, exp); Success(grantedExp, scheduleRefresh); }
                            else if (code2==403) Log(L.B("   ❌ 403: credenziali errate o IP non autorizzato.\r\n","   ❌ 403: wrong credentials or unauthorized IP.\r\n"), Color.OrangeRed);
                            else if (code2==404) Log(L.B("   ❌ 404: AOR/utente inesistente sul registrar.\r\n","   ❌ 404: AOR/user not found on the registrar.\r\n"), Color.OrangeRed);
                        }
                        else if (code1==200) { grantedExp = ExtractExpires(r1, exp); Success(grantedExp, scheduleRefresh); }
                        else if (code1==0) Log(L.B("   ✗ Nessuna risposta (timeout / UDP filtrato).\r\n","   ✗ No response (timeout / UDP filtered).\r\n"), Color.OrangeRed);
                    }
                }
                catch (Exception ex) { Log("✗ " + ex.Message + "\r\n", Color.OrangeRed); }
                if (exp==0) { Log(L.B("\r\n── De-registrato ──\r\n","\r\n── Unregistered ──\r\n"), Color.Gray); }
                Reenable();
            });
        }

        void Success(int grantedExp, bool scheduleRefresh)
        {
            Log(L.B("   ✅ Registrato. Expires concesso: ","   ✅ Registered. Granted Expires: ") + grantedExp + " s.\r\n", Color.LimeGreen);
            if (scheduleRefresh && chkKeep != null && IsHandleCreated && grantedExp > 10)
            {
                int refresh = Math.Max(10, grantedExp/2);
                BeginInvoke((MethodInvoker)delegate {
                    if (!chkKeep.Checked) return;
                    Log(L.B("   ⏱ Prossimo refresh tra ","   ⏱ Next refresh in ") + refresh + L.B(" s (alle "," s (at ") + DateTime.Now.AddSeconds(refresh).ToString("HH:mm:ss") + ").\r\n", Color.Gray);
                    if (timer==null) { timer = new System.Windows.Forms.Timer(); timer.Tick += (s,e)=>{ timer.Stop(); RegisterOnce(expires, true); }; }
                    timer.Interval = refresh*1000; timer.Start();
                });
            }
        }

        string BuildRegister(string local, int lport, string uri, int exp, string auth)
        {
            string branch = "z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,12);
            var sb = new StringBuilder();
            sb.Append("REGISTER " + uri + " SIP/2.0\r\n");
            sb.Append("Via: SIP/2.0/UDP " + local + ":" + lport + ";branch=" + branch + ";rport\r\n");
            sb.Append("Max-Forwards: 70\r\n");
            sb.Append("From: <sip:" + user + "@" + host + ">;tag=" + callId.Substring(0,8) + "\r\n");
            sb.Append("To: <sip:" + user + "@" + host + ">\r\n");
            sb.Append("Call-ID: " + callId + "@" + local + "\r\n");
            sb.Append("CSeq: " + cseq + " REGISTER\r\n");
            sb.Append("Contact: <sip:" + user + "@" + local + ":" + lport + ">\r\n");
            if (auth != null) sb.Append("Authorization: " + auth + "\r\n");
            sb.Append("Expires: " + exp + "\r\n");
            sb.Append("User-Agent: LosaTermVoip\r\nContent-Length: 0\r\n\r\n");
            return sb.ToString();
        }

        static string BuildAuth(string user, string pass, string realm, string nonce, string qop, string opaque, string uri)
        {
            string ha1 = Md5Hex(user + ":" + realm + ":" + pass);
            string ha2 = Md5Hex("REGISTER:" + uri);
            string resp;
            var sb = new StringBuilder();
            sb.Append("Digest username=\"" + user + "\", realm=\"" + realm + "\", nonce=\"" + nonce + "\", uri=\"" + uri + "\"");
            if (qop != null && qop.ToLower().Contains("auth"))
            {
                string nc = "00000001"; string cnonce = Guid.NewGuid().ToString("N").Substring(0,16);
                resp = Md5Hex(ha1 + ":" + nonce + ":" + nc + ":" + cnonce + ":auth:" + ha2);
                sb.Append(", response=\"" + resp + "\", algorithm=MD5, qop=auth, nc=" + nc + ", cnonce=\"" + cnonce + "\"");
            }
            else
            {
                resp = Md5Hex(ha1 + ":" + nonce + ":" + ha2);
                sb.Append(", response=\"" + resp + "\", algorithm=MD5");
            }
            if (!string.IsNullOrEmpty(opaque)) sb.Append(", opaque=\"" + opaque + "\"");
            return sb.ToString();
        }

        static string SendRecv(UdpClient udp, IPEndPoint dest, string msg)
        {
            byte[] data = Encoding.ASCII.GetBytes(msg);
            udp.Send(data, data.Length, dest);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            for (int i = 0; i < 5; i++)
            {
                byte[] resp;
                try { resp = udp.Receive(ref remote); }
                catch (SocketException) { return ""; }
                string text = Encoding.ASCII.GetString(resp);
                var m = Regex.Match(text, @"^SIP/2\.0\s+(\d{3})");
                if (m.Success && int.Parse(m.Groups[1].Value) >= 200) return text;
            }
            return "";
        }

        static string FirstLine(string s){ if(string.IsNullOrEmpty(s)) return L.B("(vuoto)","(empty)"); int n=s.IndexOf("\r\n"); return n>0?s.Substring(0,n):s; }
        static int StatusCode(string s){ var m=Regex.Match(s ?? "", @"^SIP/2\.0\s+(\d{3})"); return m.Success?int.Parse(m.Groups[1].Value):0; }
        static string HeaderValue(string s, string name){ var m=Regex.Match(s ?? "", @"(?im)^" + Regex.Escape(name) + @"\s*:\s*(.+)$"); return m.Success?m.Groups[1].Value.Trim():""; }
        static string Param(string hdr, string key){ var m=Regex.Match(hdr ?? "", key + @"\s*=\s*""?([^"",]+)""?", RegexOptions.IgnoreCase); return m.Success?m.Groups[1].Value.Trim():null; }
        static int ExtractExpires(string resp, int fallback)
        {
            var m = Regex.Match(resp ?? "", @"(?im)^Expires\s*:\s*(\d+)"); if (m.Success) return int.Parse(m.Groups[1].Value);
            var c = Regex.Match(resp ?? "", @"expires\s*=\s*(\d+)", RegexOptions.IgnoreCase); if (c.Success) return int.Parse(c.Groups[1].Value);
            return fallback;
        }

        static string Md5Hex(string s)
        {
            using (var md5 = MD5.Create())
            {
                byte[] h = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(); foreach (var b in h) sb.Append(b.ToString("x2")); return sb.ToString();
            }
        }

        void Log(string text, Color c)
        {
            if (!rtb.IsHandleCreated) return;
            rtb.BeginInvoke((MethodInvoker)delegate {
                rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0; rtb.SelectionColor = c;
                rtb.AppendText(text); rtb.ScrollToCaret();
            });
        }
        void Reenable() { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { if (!btnUnreg.Enabled) btnReg.Enabled = true; }); }

        static Label Lbl(string t,int x,int y){ return new Label{ Text=t, Location=new Point(x,y+2), AutoSize=true, ForeColor=Color.LightGray }; }
        TextBox T(int x,int y,int w){ return new TextBox{ Location=new Point(x,y), Width=w, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle }; }
        Button Btn(string t,int x,int y,int w,Color c){ var b=new Button{ Text=t, Location=new Point(x,y), Width=w, Height=28, FlatStyle=FlatStyle.Flat, BackColor=c, ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold)}; b.FlatAppearance.BorderSize=0; return b; }
    }
}
