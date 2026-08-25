using System;
using System.Drawing;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  SIP Builder / Header tester — form: compili i campi, l'app assembla e
    //  invia (Via/Call-ID/branch/tag automatici). Auth Digest automatica sui
    //  401/407. UDP/TCP/TLS. Bilingue IT/EN.
    // ════════════════════════════════════════════════════════════════════════
    public class RawSipPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtTarget, txtFrom, txtTo, txtContact, txtCseq, txtExpires, txtExtra, txtBody, txtAuthUser, txtAuthPass, txtOut;
        ComboBox cmbTransport, cmbMethod;
        Button btnSend;

        public RawSipPanel()
        {
            Text = "LosaTermVoip — SIP Builder / Header tester";
            Size = new Size(920, 670);
            MinimumSize = new Size(700, 540);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(34,34,46) };
            top.Controls.Add(Lbl(L.B("Destinazione:","Target:"), 10, 12));
            txtTarget = Tx(96, 9, 210, "sbc.azienda.it:5060"); top.Controls.Add(txtTarget);
            cmbTransport = Cmb(314, 9, 80, new[]{ "UDP","TCP","TLS" }); top.Controls.Add(cmbTransport);
            btnSend = new Button { Text=L.B("▶ Invia","▶ Send"), Location=new Point(404,8), Width=120, Height=26, FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(30,110,30), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btnSend.FlatAppearance.BorderSize=0; btnSend.Click += (s,e)=>Send(); top.Controls.Add(btnSend);
            var btnR = ReportHelper.MakeButton(532, 8);
            btnR.Click += (s,e)=>ReportHelper.ExportText(this, "Raw SIP tester", txtOut.Text);
            top.Controls.Add(btnR);

            var form = new Panel { Dock = DockStyle.Top, Height = 252, BackColor = Color.FromArgb(28,28,40) };
            int y=12;
            form.Controls.Add(Lbl(L.B("Metodo:","Method:"), 12, y));
            cmbMethod = Cmb(90, y-2, 120, new[]{ "OPTIONS","INVITE","REGISTER","SUBSCRIBE","NOTIFY","MESSAGE","INFO","REFER","PUBLISH" }); form.Controls.Add(cmbMethod);
            form.Controls.Add(Lbl("CSeq:", 230, y)); txtCseq = Tx(278, y-2, 45, "1"); form.Controls.Add(txtCseq);
            form.Controls.Add(Lbl("Expires:", 335, y)); txtExpires = Tx(390, y-2, 45, ""); form.Controls.Add(txtExpires);
            form.Controls.Add(Lbl("Auth:", 452, y)); txtAuthUser = Tx(492, y-2, 95, ""); form.Controls.Add(txtAuthUser);
            txtAuthPass = Tx(590, y-2, 95, ""); txtAuthPass.UseSystemPasswordChar=true; form.Controls.Add(txtAuthPass);
            form.Controls.Add(new Label { Text=L.B("(user/pass per 401)","(user/pass for 401)"), Location=new Point(690,y+2), AutoSize=true, ForeColor=Color.Gray });
            y+=34;
            form.Controls.Add(Lbl("From:", 12, y)); txtFrom = Tx(90, y-2, 790, "sip:losaterm@$LOCALIP$"); txtFrom.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtFrom); y+=30;
            form.Controls.Add(Lbl("To:", 12, y)); txtTo = Tx(90, y-2, 790, "sip:$TARGET$"); txtTo.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtTo); y+=30;
            form.Controls.Add(Lbl("Contact:", 12, y)); txtContact = Tx(90, y-2, 790, "sip:losaterm@$LOCAL$"); txtContact.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtContact); y+=30;
            form.Controls.Add(Lbl(L.B("Header extra:","Extra headers:"), 12, y));
            txtExtra = new TextBox { Location=new Point(90,y-2), Width=790, Height=44, Multiline=true, ScrollBars=ScrollBars.Vertical, BackColor=CIn, ForeColor=Color.White, Font=new Font("Consolas",9), BorderStyle=BorderStyle.FixedSingle, Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            form.Controls.Add(txtExtra); y+=50;
            form.Controls.Add(Lbl(L.B("Body / SDP:","Body / SDP:"), 12, y));
            txtBody = new TextBox { Location=new Point(90,y-2), Width=790, Height=44, Multiline=true, ScrollBars=ScrollBars.Vertical, BackColor=CIn, ForeColor=Color.White, Font=new Font("Consolas",9), BorderStyle=BorderStyle.FixedSingle, Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            form.Controls.Add(txtBody);

            var hdrR = new Label { Text=L.B("  Messaggio inviato ↑  /  Risposta ↓","  Sent message ↑  /  Response ↓"), Dock=DockStyle.Top, Height=22, ForeColor=Color.LightGray, BackColor=Color.FromArgb(30,30,45), TextAlign=ContentAlignment.MiddleLeft };
            txtOut = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Both, WordWrap=false,
                BackColor=Color.FromArgb(12,16,24), ForeColor=Color.Gainsboro, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };
            txtOut.TextChanged += (s,e)=>ReportHelper.Set("Raw SIP tester", txtOut.Text);

            Controls.Add(txtOut);
            Controls.Add(hdrR);
            Controls.Add(form);
            Controls.Add(top);
        }

        void Send()
        {
            string tgt = (txtTarget.Text ?? "").Trim();
            string host = tgt; int port = 5060;
            int c = tgt.LastIndexOf(':'); if (c>0){ host=tgt.Substring(0,c); int.TryParse(tgt.Substring(c+1), out port); }
            string tr = cmbTransport.Text;
            if (port<=0) port = tr=="TLS"?5061:5060;
            string method = cmbMethod.Text;
            string from = txtFrom.Text ?? "", to = txtTo.Text ?? "", contact = txtContact.Text ?? "";
            string extra = txtExtra.Text ?? "", body = txtBody.Text ?? "";
            int cseqN; if (!int.TryParse(txtCseq.Text, out cseqN)) cseqN = 1;
            string expires = (txtExpires.Text ?? "").Trim();
            string authUser = (txtAuthUser.Text ?? "").Trim(), authPass = txtAuthPass.Text ?? "";
            btnSend.Enabled=false; txtOut.Text = L.B("Invio…","Sending…") + "\r\n";

            ThreadPool.QueueUserWorkItem(_ => {
                var sb = new StringBuilder();
                try
                {
                    IPAddress ip=null; foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily==AddressFamily.InterNetwork){ ip=a; break; }
                    if (ip==null) { ShowOut(L.B("✗ DNS: impossibile risolvere ","✗ DNS: cannot resolve ") + host); return; }
                    string localIp="0.0.0.0"; try { using (var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,0)){ s.Connect(ip,port); localIp=((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch {}

                    string callId = Guid.NewGuid().ToString("N").Substring(0,16) + "@" + localIp;
                    string fromTag = Guid.NewGuid().ToString("N").Substring(0,8);
                    string uri = "sip:" + host;

                    string sent1, resp1 = SendOnce(tr, ip, port, host, localIp, method, from, to, contact, cseqN, expires, extra, body, callId, fromTag, null, out sent1);
                    sb.Append(">>> " + sent1 + "\r\n<<< \r\n" + (resp1.Length>0?resp1:NoResp()));
                    if (resp1.Length>0) sb.Append("\r\n" + SipValidator.ReportRaw(resp1));

                    int code = StatusCode(resp1);
                    if ((code==401 || code==407) && authUser.Length>0)
                    {
                        string hv = HeaderValue(resp1, code==407 ? "Proxy-Authenticate" : "WWW-Authenticate");
                        string realm=Param(hv,"realm"), nonce=Param(hv,"nonce"), qop=Param(hv,"qop"), opaque=Param(hv,"opaque");
                        string auth = (code==407?"Proxy-Authorization: ":"Authorization: ") + Digest(authUser, authPass, realm, nonce, qop, opaque, method, uri);
                        string sent2, resp2 = SendOnce(tr, ip, port, host, localIp, method, from, to, contact, cseqN+1, expires, extra, body, callId, fromTag, auth, out sent2);
                        sb.Append("\r\n\r\n══ " + L.B("Ritento con Digest (CSeq "+(cseqN+1)+")","Retry with Digest (CSeq "+(cseqN+1)+")") + " ══\r\n\r\n");
                        sb.Append(">>> " + sent2 + "\r\n<<< \r\n" + (resp2.Length>0?resp2:NoResp()));
                        if (resp2.Length>0) sb.Append("\r\n" + SipValidator.ReportRaw(resp2));
                    }
                }
                catch (Exception ex) { sb.Append("✗ " + ex.Message); }
                ShowOut(sb.ToString());
            });
        }

        static string SendOnce(string tr, IPAddress ip, int port, string host, string localIp,
            string method, string from, string to, string contact, int cseqN, string expires, string extra, string body,
            string callId, string fromTag, string authHeader, out string sentMsg)
        {
            sentMsg = "";
            if (tr == "UDP")
            {
                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 4000;
                    int lport = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
                    sentMsg = BuildMsg(method, host, localIp, lport, tr, from, to, contact, cseqN, expires, extra, body, callId, fromTag, authHeader);
                    byte[] data = Encoding.ASCII.GetBytes(sentMsg);
                    udp.Send(data, data.Length, new IPEndPoint(ip, port));
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    try { return Encoding.ASCII.GetString(udp.Receive(ref remote)); } catch (SocketException) { return ""; }
                }
            }
            using (var tcp = new TcpClient())
            {
                var ar = tcp.BeginConnect(ip, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(4000)) return "";
                tcp.EndConnect(ar);
                int lport = ((IPEndPoint)tcp.Client.LocalEndPoint).Port;
                sentMsg = BuildMsg(method, host, localIp, lport, tr, from, to, contact, cseqN, expires, extra, body, callId, fromTag, authHeader);
                byte[] data = Encoding.ASCII.GetBytes(sentMsg);
                System.IO.Stream st = tcp.GetStream(); SslStream ssl=null;
                if (tr=="TLS") { ssl=new SslStream(st,false,(s,cc,ch,er)=>true); ssl.AuthenticateAsClient(host); st=ssl; }
                st.Write(data,0,data.Length); st.Flush();
                var buf=new byte[8192]; string resp="";
                try { st.ReadTimeout=4000; int n=st.Read(buf,0,buf.Length); if(n>0) resp=Encoding.ASCII.GetString(buf,0,n); } catch {}
                if (ssl!=null) ssl.Dispose();
                return resp;
            }
        }

        static string BuildMsg(string method, string host, string localIp, int lport, string tr,
                               string from, string to, string contact, int cseqN, string expires, string extra, string body,
                               string callId, string fromTag, string authHeader)
        {
            string local = localIp + ":" + lport;
            Func<string,string> ph = s => (s ?? "").Replace("$TARGET$", host).Replace("$LOCALIP$", localIp).Replace("$LOCAL$", local);
            from = ph(from); to = ph(to); contact = ph(contact); body = body ?? "";

            var sb = new StringBuilder();
            sb.Append(method + " sip:" + host + " SIP/2.0\r\n");
            sb.Append("Via: SIP/2.0/" + tr + " " + local + ";branch=z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,12) + ";rport\r\n");
            sb.Append("Max-Forwards: 70\r\n");
            sb.Append("From: <" + from + ">;tag=" + fromTag + "\r\n");
            sb.Append("To: <" + to + ">\r\n");
            sb.Append("Call-ID: " + callId + "\r\n");
            sb.Append("CSeq: " + cseqN + " " + method + "\r\n");
            sb.Append("Contact: <" + contact + ">\r\n");
            if (authHeader != null) sb.Append(authHeader + "\r\n");
            if (!string.IsNullOrEmpty(expires)) sb.Append("Expires: " + expires + "\r\n");
            foreach (var line in extra.Replace("\r","").Split('\n')) { string l=line.Trim(); if (l.Length>0) sb.Append(l + "\r\n"); }
            sb.Append("User-Agent: LosaTermVoip\r\n");
            if (body.Length>0) sb.Append("Content-Type: application/sdp\r\n");
            sb.Append("Content-Length: " + Encoding.ASCII.GetByteCount(body) + "\r\n\r\n");
            sb.Append(body);
            return sb.ToString();
        }

        // ── Digest MD5 ──
        static string Digest(string user, string pass, string realm, string nonce, string qop, string opaque, string method, string uri)
        {
            string ha1 = Md5(user + ":" + realm + ":" + pass);
            string ha2 = Md5(method + ":" + uri);
            string resp; var sb = new StringBuilder();
            sb.Append("Digest username=\"" + user + "\", realm=\"" + realm + "\", nonce=\"" + nonce + "\", uri=\"" + uri + "\"");
            if (qop != null && qop.ToLower().Contains("auth"))
            {
                string nc = "00000001"; string cnonce = Guid.NewGuid().ToString("N").Substring(0,16);
                resp = Md5(ha1 + ":" + nonce + ":" + nc + ":" + cnonce + ":auth:" + ha2);
                sb.Append(", response=\"" + resp + "\", algorithm=MD5, qop=auth, nc=" + nc + ", cnonce=\"" + cnonce + "\"");
            }
            else { resp = Md5(ha1 + ":" + nonce + ":" + ha2); sb.Append(", response=\"" + resp + "\", algorithm=MD5"); }
            if (!string.IsNullOrEmpty(opaque)) sb.Append(", opaque=\"" + opaque + "\"");
            return sb.ToString();
        }
        static string Md5(string s){ using (var m=MD5.Create()){ var h=m.ComputeHash(Encoding.UTF8.GetBytes(s)); var b=new StringBuilder(); foreach (var x in h) b.Append(x.ToString("x2")); return b.ToString(); } }
        static int StatusCode(string s){ var m=Regex.Match(s ?? "", @"^SIP/2\.0\s+(\d{3})"); return m.Success?int.Parse(m.Groups[1].Value):0; }
        static string HeaderValue(string s, string name){ var m=Regex.Match(s ?? "", @"(?im)^" + Regex.Escape(name) + @"\s*:\s*(.+)$"); return m.Success?m.Groups[1].Value.Trim():""; }
        static string Param(string h, string k){ var m=Regex.Match(h ?? "", k + @"\s*=\s*""?([^"",]+)""?", RegexOptions.IgnoreCase); return m.Success?m.Groups[1].Value.Trim():null; }

        string NoResp() { return L.B("✗ Nessuna risposta (timeout).","✗ No response (timeout)."); }
        void ShowOut(string res) { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { txtOut.Text = res; btnSend.Enabled = true; }); }

        static Label Lbl(string t,int x,int y){ return new Label{ Text=t, Location=new Point(x,y), AutoSize=true, ForeColor=Color.LightGray }; }
        TextBox Tx(int x,int y,int w,string val){ return new TextBox{ Location=new Point(x,y), Width=w, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text=val }; }
        ComboBox Cmb(int x,int y,int w,string[] items){ var c=new ComboBox{ Location=new Point(x,y), Width=w, DropDownStyle=ComboBoxStyle.DropDownList, BackColor=CIn, ForeColor=Color.White, FlatStyle=FlatStyle.Flat }; c.Items.AddRange(items); c.SelectedIndex=0; return c; }
    }
}
