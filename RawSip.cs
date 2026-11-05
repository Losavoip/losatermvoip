using System;
using System.Collections.Generic;
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
    //  invia. Tutti gli header sono modificabili (R-URI, Via, Call-ID,
    //  Max-Forwards, User-Agent, NAT IP) con segnaposto; un header in Extra con
    //  lo stesso nome sostituisce quello automatico. Auth Digest automatica sui
    //  401/407. UDP/TCP/TLS. Bilingue IT/EN.
    // ════════════════════════════════════════════════════════════════════════
    public class RawSipPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtTarget, txtFrom, txtTo, txtContact, txtCseq, txtExpires, txtExtra, txtBody, txtAuthUser, txtAuthPass, txtOut;
        TextBox txtNatIp, txtCallId, txtMaxFwd, txtUA, txtRuri, txtVia;
        ComboBox cmbTransport, cmbMethod;
        Button btnSend;

        public RawSipPanel()
        {
            Text = "LosaTermVoip — SIP Builder / Header tester";
            Size = new Size(920, 760);
            MinimumSize = new Size(700, 630);
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

            var form = new Panel { Dock = DockStyle.Top, Height = 342, BackColor = Color.FromArgb(28,28,40) };
            int y=12;
            form.Controls.Add(Lbl(L.B("Metodo:","Method:"), 12, y));
            cmbMethod = Cmb(90, y-2, 120, new[]{ "OPTIONS","INVITE","REGISTER","SUBSCRIBE","NOTIFY","MESSAGE","INFO","REFER","PUBLISH" }); form.Controls.Add(cmbMethod);
            form.Controls.Add(Lbl("CSeq:", 230, y)); txtCseq = Tx(278, y-2, 45, "1"); form.Controls.Add(txtCseq);
            form.Controls.Add(Lbl("Expires:", 335, y)); txtExpires = Tx(390, y-2, 45, ""); form.Controls.Add(txtExpires);
            form.Controls.Add(Lbl("Auth:", 452, y)); txtAuthUser = Tx(492, y-2, 95, ""); form.Controls.Add(txtAuthUser);
            txtAuthPass = Tx(590, y-2, 95, ""); txtAuthPass.UseSystemPasswordChar=true; form.Controls.Add(txtAuthPass);
            form.Controls.Add(new Label { Text=L.B("(user/pass per 401)","(user/pass for 401)"), Location=new Point(690,y+2), AutoSize=true, ForeColor=Color.Gray });
            y+=34;
            form.Controls.Add(Lbl("NAT IP:", 12, y)); txtNatIp = Tx(90, y-2, 130, ""); form.Controls.Add(txtNatIp);
            form.Controls.Add(Lbl("Call-ID:", 232, y)); txtCallId = Tx(285, y-2, 190, "$CALLID$"); form.Controls.Add(txtCallId);
            form.Controls.Add(Lbl("Max-Fwd:", 485, y)); txtMaxFwd = Tx(545, y-2, 40, "70"); form.Controls.Add(txtMaxFwd);
            form.Controls.Add(Lbl("User-Agent:", 597, y)); txtUA = Tx(670, y-2, 210, "LosaTermVoip"); txtUA.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtUA); y+=30;
            form.Controls.Add(Lbl("R-URI:", 12, y)); txtRuri = Tx(90, y-2, 790, "sip:$TARGET$"); txtRuri.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtRuri); y+=30;
            form.Controls.Add(Lbl("Via:", 12, y)); txtVia = Tx(90, y-2, 790, "SIP/2.0/$TR$ $LOCAL$;branch=$BRAnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right$;rport"); txtVia.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtVia); y+=30;
            form.Controls.Add(Lbl("From:", 12, y)); txtFrom = Tx(90, y-2, 790, "sip:losaterm@$LOCALIP$"); txtFrom.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtFrom); y+=30;
            form.Controls.Add(Lbl("To:", 12, y)); txtTo = Tx(90, y-2, 790, "sip:$TARGET$"); txtTo.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtTo); y+=30;
            form.Controls.Add(Lbl("Contact:", 12, y)); txtContact = Tx(90, y-2, 790, "sip:losaterm@$LOCAL$"); txtContact.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; form.Controls.Add(txtContact); y+=30;
            form.Controls.Add(Lbl(L.B("Header extra:","Extra headers:"), 12, y));
            txtExtra = new TextBox { Location=new Point(90,y-2), Width=790, Height=44, Multiline=true, ScrollBars=ScrollBars.Vertical, BackColor=CIn, ForeColor=Color.White, Font=new Font("Consolas",9), BorderStyle=BorderStyle.FixedSingle, Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            form.Controls.Add(txtExtra); y+=50;
            form.Controls.Add(Lbl(L.B("Body / SDP:","Body / SDP:"), 12, y));
            txtBody = new TextBox { Location=new Point(90,y-2), Width=790, Height=44, Multiline=true, ScrollBars=ScrollBars.Vertical, BackColor=CIn, ForeColor=Color.White, Font=new Font("Consolas",9), BorderStyle=BorderStyle.FixedSingle, Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            form.Controls.Add(txtBody);

            var tt = new ToolTip { AutoPopDelay = 30000, InitialDelay = 400 };
            string tipTxt = L.B(
                "Segnaposto:\n$TARGET$  host destinazione\n$LOCALIP$  IP locale (o NAT IP se compilato)\n$LOCAL$  IP:porta locale (o NAT IP[:porta])\n$REALIP$  IP locale reale\n$LPORT$  porta locale\n$TR$  trasporto (UDP/TCP/TLS)\n$BRANCH$  branch z9hG4bK… nuovo per ogni richiesta\n$CALLID$  Call-ID generato\n$TAG$  tag del From\n\nCampo vuoto = header omesso.\nUn header in Extra con lo stesso nome sostituisce quello automatico.\nURI senza <> viene racchiuso automaticamente; usa <> per header-param.",
                "Placeholders:\n$TARGET$  target host\n$LOCALIP$  local IP (or NAT IP if set)\n$LOCAL$  local IP:port (or NAT IP[:port])\n$REALIP$  real local IP\n$LPORT$  local port\n$TR$  transport (UDP/TCP/TLS)\n$BRANCH$  z9hG4bK… branch, new for each request\n$CALLID$  generated Call-ID\n$TAG$  From tag\n\nEmpty field = header omitted.\nAn Extra header with the same name replaces the automatic one.\nURIs without <> are wrapped automatically; use <> for header params.");
            foreach (var tb in new[]{ txtNatIp, txtCallId, txtMaxFwd, txtUA, txtRuri, txtVia, txtFrom, txtTo, txtContact, txtExtra, txtBody }) tt.SetToolTip(tb, tipTxt);
            tt.SetToolTip(txtNatIp, L.B("IP pubblico da scrivere al posto dell'IP locale ($LOCALIP$/$LOCAL$) in Via, Contact, From, SDP… Formato IP o IP:porta.\nNB: l'IP sorgente reale del pacchetto non cambia.\n\n","Public IP written instead of the local IP ($LOCALIP$/$LOCAL$) in Via, Contact, From, SDP… Format IP or IP:port.\nNote: the real source IP of the packet does not change.\n\n") + tipTxt);

            var hdrR = new Label { Text=L.B("  Messaggio inviato ↑  /  Risposta ↓","  Sent message ↑  /  Response ↓"), Dock=DockStyle.Top, Height=22, ForeColor=Color.LightGray, BackColor=Color.FromArgb(30,30,45), TextAlign=ContentAlignment.MiddleLeft };
            txtOut = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Both, WordWrap=false,
                BackColor=Color.FromArgb(12,16,24), ForeColor=Color.Gainsboro, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };
            txtOut.TextChanged += (s,e)=>ReportHelper.Set("Raw SIP tester", txtOut.Text);

            Controls.Add(txtOut);
            Controls.Add(hdrR);
            Controls.Add(form);
            Controls.Add(top);
        }

        class SipTpl
        {
            public string Method, From, To, Contact, Extra, Body, Expires, NatIp, CallId, MaxFwd, UA, Ruri, Via;
            public string RealIp, FromTag, CallIdGen;
        }

        void Send()
        {
            string tgt = (txtTarget.Text ?? "").Trim();
            string host = tgt; int port = 5060;
            int c = tgt.LastIndexOf(':'); if (c>0){ host=tgt.Substring(0,c); int.TryParse(tgt.Substring(c+1), out port); }
            string tr = cmbTransport.Text;
            if (port<=0) port = tr=="TLS"?5061:5060;
            var t = new SipTpl {
                Method = cmbMethod.Text, From = txtFrom.Text ?? "", To = txtTo.Text ?? "", Contact = txtContact.Text ?? "",
                Extra = txtExtra.Text ?? "", Body = txtBody.Text ?? "", Expires = (txtExpires.Text ?? "").Trim(),
                NatIp = (txtNatIp.Text ?? "").Trim(), CallId = (txtCallId.Text ?? "").Trim(), MaxFwd = (txtMaxFwd.Text ?? "").Trim(),
                UA = (txtUA.Text ?? "").Trim(), Ruri = (txtRuri.Text ?? "").Trim(), Via = (txtVia.Text ?? "").Trim()
            };
            int cseqN; if (!int.TryParse(txtCseq.Text, out cseqN)) cseqN = 1;
            string authUser = (txtAuthUser.Text ?? "").Trim(), authPass = txtAuthPass.Text ?? "";
            btnSend.Enabled=false; txtOut.Text = L.B("Invio…","Sending…") + "\r\n";

            ThreadPool.QueueUserWorkItem(_ => {
                var sb = new StringBuilder();
                try
                {
                    IPAddress ip=null; foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily==AddressFamily.InterNetwork){ ip=a; break; }
                    if (ip==null) { ShowOut(L.B("✗ DNS: impossibile risolvere ","✗ DNS: cannot resolve ") + host); return; }
                    string localIp="0.0.0.0"; try { using (var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,0)){ s.Connect(ip,port); localIp=((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch {}

                    t.RealIp = localIp;
                    t.FromTag = Guid.NewGuid().ToString("N").Substring(0,8);
                    t.CallIdGen = Guid.NewGuid().ToString("N").Substring(0,16) + "@" + NatHost(t);
                    if (t.NatIp.Length>0)
                        sb.Append(L.B("ℹ NAT IP " + t.NatIp + " usato negli header al posto di " + localIp + " (l'IP sorgente reale del pacchetto resta invariato)\r\n\r\n",
                                      "ℹ NAT IP " + t.NatIp + " used in headers instead of " + localIp + " (the real source IP of the packet is unchanged)\r\n\r\n"));

                    string sent1, ruri1, resp1 = SendOnce(tr, ip, port, host, t, cseqN, null, out sent1, out ruri1);
                    sb.Append(">>> " + sent1 + "\r\n<<< \r\n" + (resp1.Length>0?resp1:NoResp()));
                    if (resp1.Length>0) sb.Append("\r\n" + SipValidator.ReportRaw(resp1));

                    int code = StatusCode(resp1);
                    if ((code==401 || code==407) && authUser.Length>0)
                    {
                        string hv = HeaderValue(resp1, code==407 ? "Proxy-Authenticate" : "WWW-Authenticate");
                        string realm=Param(hv,"realm"), nonce=Param(hv,"nonce"), qop=Param(hv,"qop"), opaque=Param(hv,"opaque");
                        string auth = (code==407?"Proxy-Authorization: ":"Authorization: ") + Digest(authUser, authPass, realm, nonce, qop, opaque, t.Method, ruri1);
                        string sent2, ruri2, resp2 = SendOnce(tr, ip, port, host, t, cseqN+1, auth, out sent2, out ruri2);
                        sb.Append("\r\n\r\n══ " + L.B("Ritento con Digest (CSeq "+(cseqN+1)+")","Retry with Digest (CSeq "+(cseqN+1)+")") + " ══\r\n\r\n");
                        sb.Append(">>> " + sent2 + "\r\n<<< \r\n" + (resp2.Length>0?resp2:NoResp()));
                        if (resp2.Length>0) sb.Append("\r\n" + SipValidator.ReportRaw(resp2));
                    }
                }
                catch (Exception ex) { sb.Append("✗ " + ex.Message); }
                ShowOut(sb.ToString());
            });
        }

        static string SendOnce(string tr, IPAddress ip, int port, string host, SipTpl t, int cseqN, string authHeader,
                               out string sentMsg, out string ruri)
        {
            sentMsg = ""; ruri = "sip:" + host;
            if (tr == "UDP")
            {
                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 4000;
                    int lport = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
                    sentMsg = BuildMsg(host, lport, tr, t, cseqN, authHeader, out ruri);
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
                sentMsg = BuildMsg(host, lport, tr, t, cseqN, authHeader, out ruri);
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

        // NAT IP: "1.2.3.4" oppure "1.2.3.4:5060"
        static string NatHost(SipTpl t)
        {
            if (string.IsNullOrEmpty(t.NatIp)) return t.RealIp;
            int k = t.NatIp.LastIndexOf(':'); return k>0 ? t.NatIp.Substring(0,k) : t.NatIp;
        }
        static string NatPort(SipTpl t, int lport)
        {
            if (!string.IsNullOrEmpty(t.NatIp)) { int k = t.NatIp.LastIndexOf(':'); int p; if (k>0 && int.TryParse(t.NatIp.Substring(k+1), out p) && p>0) return p.ToString(); }
            return lport.ToString();
        }

        static string Ph(string s, SipTpl t, string host, int lport, string tr, string branch)
        {
            string pubIp = NatHost(t), pubPort = NatPort(t, lport);
            return (s ?? "").Replace("$TARGET$", host).Replace("$REALIP$", t.RealIp)
                .Replace("$LOCALIP$", pubIp).Replace("$LOCAL$", pubIp + ":" + pubPort).Replace("$LPORT$", pubPort)
                .Replace("$TR$", tr).Replace("$BRANCH$", branch).Replace("$CALLID$", t.CallIdGen).Replace("$TAG$", t.FromTag ?? "");
        }

        // "sip:a@b" → "<sip:a@b>"; "sip:a@b;tag=x" → "<sip:a@b>;tag=x"; con <> lascia com'è
        static string NameAddr(string v, string tag)
        {
            v = (v ?? "").Trim(); if (v.Length==0) return "";
            if (v.IndexOf('<') < 0)
            {
                int k = v.IndexOf(";tag=", StringComparison.OrdinalIgnoreCase);
                v = k>=0 ? "<" + v.Substring(0,k) + ">" + v.Substring(k) : "<" + v + ">";
            }
            if (tag != null && v.IndexOf(";tag=", StringComparison.OrdinalIgnoreCase) < 0) v += ";tag=" + tag;
            return v;
        }

        static string Canon(string n)
        {
            switch (n.Trim().ToLowerInvariant())
            {
                case "v": return "via";          case "f": return "from";
                case "t": return "to";           case "i": return "call-id";
                case "m": return "contact";      case "l": return "content-length";
                case "c": return "content-type"; case "k": return "supported";
                default:  return n.Trim().ToLowerInvariant();
            }
        }

        static string BuildMsg(string host, int lport, string tr, SipTpl t, int cseqN, string authHeader, out string ruri)
        {
            string branch = "z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,12);
            Func<string,string> ph = s => Ph(s, t, host, lport, tr, branch);

            ruri = ph(t.Ruri).Trim(); if (ruri.Length==0) ruri = "sip:" + host;
            string body = ph(t.Body);

            // Header extra: se hanno lo stesso nome di uno automatico, lo sostituiscono
            var extra = new List<string>(); var over = new HashSet<string>();
            foreach (var line in (t.Extra ?? "").Replace("\r","").Split('\n'))
            {
                string l = ph(line).Trim(); if (l.Length==0) continue;
                extra.Add(l); int k = l.IndexOf(':'); if (k>0) over.Add(Canon(l.Substring(0,k)));
            }
            Func<string,bool> auto = n => !over.Contains(n);

            var sb = new StringBuilder();
            sb.Append(t.Method + " " + ruri + " SIP/2.0\r\n");
            string via = ph(t.Via).Trim();
            if (auto("via") && via.Length>0) sb.Append("Via: " + via + "\r\n");
            if (auto("max-forwards") && t.MaxFwd.Length>0) sb.Append("Max-Forwards: " + ph(t.MaxFwd) + "\r\n");
            string from = NameAddr(ph(t.From), t.FromTag);
            if (auto("from") && from.Length>0) sb.Append("From: " + from + "\r\n");
            string to = NameAddr(ph(t.To), null);
            if (auto("to") && to.Length>0) sb.Append("To: " + to + "\r\n");
            string cid = ph(t.CallId).Trim();
            if (auto("call-id") && cid.Length>0) sb.Append("Call-ID: " + cid + "\r\n");
            if (auto("cseq")) sb.Append("CSeq: " + cseqN + " " + t.Method + "\r\n");
            string contact = NameAddr(ph(t.Contact), null);
            if (auto("contact") && contact.Length>0) sb.Append("Contact: " + contact + "\r\n");
            if (authHeader != null) sb.Append(authHeader + "\r\n");
            if (auto("expires") && !string.IsNullOrEmpty(t.Expires)) sb.Append("Expires: " + t.Expires + "\r\n");
            foreach (var l in extra) sb.Append(l + "\r\n");
            if (auto("user-agent") && t.UA.Length>0) sb.Append("User-Agent: " + ph(t.UA) + "\r\n");
            if (auto("content-type") && body.Length>0) sb.Append("Content-Type: application/sdp\r\n");
            if (auto("content-length")) sb.Append("Content-Length: " + Encoding.ASCII.GetByteCount(body) + "\r\n");
            sb.Append("\r\n");
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
