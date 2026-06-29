using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  SIP Health Check 1-click — DNS NAPTR/SRV/A → TLS+cert → SIP OPTIONS,
    //  report con punteggio 0-100. Bilingue IT/EN via L.B(). Vendor-neutral.
    // ════════════════════════════════════════════════════════════════════════
    public class HealthCheckPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtTarget;
        RichTextBox rtb;
        Label lblScore;
        Button btnGo;

        int score; readonly List<string> warnings = new List<string>();

        public HealthCheckPanel()
        {
            Text = "LosaTermVoip — SIP Health Check";
            Size = new Size(820, 600);
            MinimumSize = new Size(640, 460);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(34,34,46) };
            top.Controls.Add(new Label { Text = L.B("Dominio / sip:utente@dominio:","Domain / sip:user@domain:"), Location=new Point(12,18), AutoSize=true, ForeColor=Color.LightGray });
            txtTarget = new TextBox { Location=new Point(190,15), Width=300, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text="azienda.it" };
            txtTarget.KeyDown += (s,e)=>{ if(e.KeyCode==Keys.Enter){ Run(); e.Handled=e.SuppressKeyPress=true; } };
            top.Controls.Add(txtTarget);
            btnGo = new Button { Text = L.B("🩺 Diagnosi","🩺 Diagnose"), Location=new Point(500,13), Width=140, Height=28, FlatStyle=FlatStyle.Flat,
                BackColor=Color.FromArgb(30,110,30), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btnGo.FlatAppearance.BorderSize=0; btnGo.Click += (s,e)=>Run();
            top.Controls.Add(btnGo);
            var btnSave = new Button { Text=L.B("💾 Salva","💾 Save"), Location=new Point(648,13), Width=95, Height=28, FlatStyle=FlatStyle.Flat,
                BackColor=Color.FromArgb(60,60,80), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btnSave.FlatAppearance.BorderSize=0; btnSave.Click += (s,e)=>SaveReport(); top.Controls.Add(btnSave);

            lblScore = new Label { Dock=DockStyle.Bottom, Height=44, TextAlign=ContentAlignment.MiddleCenter,
                Font=new Font("Segoe UI",14,FontStyle.Bold), ForeColor=Color.Gray, BackColor=Color.FromArgb(18,18,28), Text="—" };

            rtb = new RichTextBox { Dock=DockStyle.Fill, ReadOnly=true, BackColor=Color.FromArgb(12,16,24),
                ForeColor=Color.Gainsboro, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };

            Controls.Add(rtb);
            Controls.Add(lblScore);
            Controls.Add(top);
        }

        void Run()
        {
            string input = (txtTarget.Text ?? "").Trim();
            if (input.Length == 0) return;
            string dom = ParseDomain(input);
            rtb.Clear(); score = 100; warnings.Clear();
            lblScore.Text = L.B("diagnosi in corso…","diagnosing…"); lblScore.ForeColor = Color.Gray;
            btnGo.Enabled = false;
            Line("══════════ SIP HEALTH CHECK — " + dom + " ══════════\r\n", Color.White, true);
            ThreadPool.QueueUserWorkItem(_ => Diagnose(dom));
        }

        static string ParseDomain(string s)
        {
            s = s.Trim();
            int at = s.IndexOf('@');
            if (at >= 0) s = s.Substring(at + 1);
            if (s.StartsWith("sip:")) s = s.Substring(4);
            if (s.StartsWith("sips:")) s = s.Substring(5);
            int col = s.IndexOf(':'); if (col > 0) s = s.Substring(0, col);
            int sl = s.IndexOf('/'); if (sl > 0) s = s.Substring(0, sl);
            return s.TrimEnd('.');
        }

        void Diagnose(string dom)
        {
            try
            {
                // ── 1. DNS NAPTR ──
                Section("1) DNS NAPTR (RFC 3263)");
                string nerr; var naptr = DnsQ.Naptr(dom, out nerr);
                if (naptr.Count > 0) Ok(L.B("NAPTR presenti: ","NAPTR found: ") + naptr.Count + L.B(" record (discovery transport)"," records (transport discovery)"));
                else Warn(L.B("Nessun NAPTR — si userà direttamente SRV","No NAPTR — SRV will be used directly"), 3);

                // ── 2. SRV ──
                Section("2) DNS SRV");
                string sipsTgt = null; int sipsPort = 5061;
                string sipTgt = null;  int sipPort = 5060;
                bool anySrv = false;
                foreach (var sn in new[] { "_sips._tcp."+dom, "_sip._tcp."+dom, "_sip._udp."+dom })
                {
                    string e; var recs = DnsQ.Srv(sn, out e);
                    if (recs.Count == 0) { Dim("  " + sn + L.B(" : (nessuno)"," : (none)")); continue; }
                    anySrv = true;
                    var r = recs[0];
                    Ok("  " + sn + " → " + r.Target.TrimEnd('.') + ":" + r.Port + " (prio " + r.Priority + ")");
                    if (sn.StartsWith("_sips") && sipsTgt == null) { sipsTgt = r.Target.TrimEnd('.'); sipsPort = r.Port; }
                    if (sn.StartsWith("_sip.") && sipTgt == null)  { sipTgt  = r.Target.TrimEnd('.'); sipPort  = r.Port; }
                }
                if (!anySrv) Warn(L.B("Nessun SRV SIP — l'endpoint userà A-record sulla 5060","No SIP SRV — the endpoint will use the A-record on 5060"), 10);

                // ── 3. A / AAAA ──
                Section(L.B("3) Risoluzione A / AAAA","3) A / AAAA resolution"));
                string aHost = sipsTgt ?? sipTgt ?? dom;
                var a = DnsQ.Query(aHost, 1); var aaaa = DnsQ.Query(aHost, 28);
                if (a.Count > 0) Ok(aHost + " → A: " + string.Join(", ", a.ToArray()));
                else Fail(aHost + L.B(" → nessun A-record! Il dominio non risolve."," → no A-record! The domain does not resolve."), 50);
                Dim("  AAAA: " + (aaaa.Count>0 ? string.Join(", ", aaaa.ToArray()) : L.B("— (no IPv6)","— (no IPv6)")));

                // ── 4. TLS + certificato ──
                Section(L.B("4) TLS / certificato","4) TLS / certificate"));
                string tlsHost = sipsTgt ?? dom; int tlsPort = sipsTgt != null ? sipsPort : 5061;
                CheckTls(tlsHost, tlsPort, sipsTgt != null);

                // ── 5. SIP OPTIONS ──
                Section(L.B("5) SIP OPTIONS (raggiungibilità)","5) SIP OPTIONS (reachability)"));
                string optHost = sipTgt ?? sipsTgt ?? dom; int optPort = sipTgt != null ? sipPort : 5060;
                long rtt; string st;
                bool ok = SendOptions(optHost, optPort, out rtt, out st);
                if (ok)
                {
                    if (st.StartsWith("200")) Ok("OPTIONS → " + st + "  (" + rtt + " ms)  " + L.B("trunk attivo","trunk up"));
                    else Warn("OPTIONS → " + st + " (" + rtt + " ms): " + L.B("risponde ma rifiuta (ACL/auth?)","responds but rejects (ACL/auth?)"), 8);
                }
                else Fail("OPTIONS → " + L.B("nessuna risposta","no response") + " (" + st + "): " + L.B("trunk irraggiungibile o UDP "+optPort+" filtrato","trunk unreachable or UDP "+optPort+" filtered"), 25);

                Done(dom);
            }
            catch (Exception ex) { Line("\r\n✗ " + L.B("Errore: ","Error: ") + ex.Message + "\r\n", Color.OrangeRed, false); EnableBtn(); }
        }

        void CheckTls(string host, int port, bool sipsKnown)
        {
            TcpClient tcp = new TcpClient();
            try
            {
                var ar = tcp.BeginConnect(host, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(3000)) { try{tcp.Close();}catch{}
                    if (sipsKnown) Fail(L.B("Connessione TLS a ","TLS connection to ") + host + ":" + port + L.B(" fallita (timeout)"," failed (timeout)"), 25);
                    else Dim(L.B("  TLS non offerto sulla 5061 (nessun _sips SRV): trunk probabilmente UDP/TCP.","  TLS not offered on 5061 (no _sips SRV): trunk is probably UDP/TCP."));
                    return;
                }
                tcp.EndConnect(ar);
            }
            catch
            {
                if (sipsKnown) Fail(L.B("Connessione TLS a ","TLS connection to ") + host + ":" + port + L.B(" rifiutata"," refused"), 25);
                else Dim(L.B("  TLS non offerto sulla 5061 (nessun _sips SRV).","  TLS not offered on 5061 (no _sips SRV)."));
                return;
            }

            try
            {
                using (var ssl = new SslStream(tcp.GetStream(), false, (s,c,ch,er)=>true))
                {
                    ssl.AuthenticateAsClient(host);
                    var cert = new X509Certificate2(ssl.RemoteCertificate);
                    string proto = ssl.SslProtocol.ToString();
                    Ok("TLS " + proto + " · cipher " + ssl.CipherAlgorithm + " " + ssl.CipherStrength + " bit");
                    if (proto.Contains("Tls11") || proto.Contains("Tls10") || proto.Contains("Ssl"))
                        Warn(L.B("Versione TLS obsoleta (","Obsolete TLS version (") + proto + L.B("): usa TLS 1.2+","): use TLS 1.2+"), 15);

                    string cn = cert.GetNameInfo(X509NameType.SimpleName, false) ?? "";
                    string san = ""; foreach (var ext in cert.Extensions) if (ext.Oid != null && ext.Oid.Value == "2.5.29.17") san = ext.Format(false);
                    int days = (int)(cert.NotAfter - DateTime.Now).TotalDays;
                    Dim("  CN: " + cn + "   Issuer: " + cert.GetNameInfo(X509NameType.SimpleName, true));
                    Dim("  SAN: " + (san.Length>0 ? san.Replace("\r\n"," ").Trim() : L.B("(nessuna)","(none)")));

                    if (days < 0) Fail(L.B("Certificato SCADUTO da ","Certificate EXPIRED ") + (-days) + L.B(" giorni!"," days ago!"), 30);
                    else if (days < 14) Warn(L.B("Certificato in scadenza tra ","Certificate expiring in ") + days + L.B(" giorni"," days"), 12);
                    else if (days < 30) Warn(L.B("Certificato in scadenza tra ","Certificate expiring in ") + days + L.B(" giorni"," days"), 5);
                    else Ok(L.B("Certificato valido per ","Certificate valid for ") + days + L.B(" giorni (fino al "," days (until ") + cert.NotAfter.ToString("yyyy-MM-dd") + ")");

                    bool match = NameMatches(host, cn) || (san.ToLower().Contains(host.ToLower()));
                    if (!match && san.Length>0)
                    {
                        string parent = host.Contains(".") ? host.Substring(host.IndexOf('.')) : host;
                        if (san.ToLower().Contains("*"+parent.ToLower())) match = true;
                    }
                    if (match) Ok(L.B("Hostname combacia con CN/SAN","Hostname matches CN/SAN"));
                    else Warn(L.B("Hostname "+host+" NON combacia con CN/SAN del certificato","Hostname "+host+" does NOT match the certificate CN/SAN"), 15);
                }
            }
            catch (Exception ex) { Fail(L.B("Handshake TLS fallito: ","TLS handshake failed: ") + ex.Message, 20); }
            finally { try { tcp.Close(); } catch {} }
        }

        static bool NameMatches(string host, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return false;
            pattern = pattern.Trim().ToLower(); host = host.ToLower();
            if (pattern.StartsWith("*.")) { string suf = pattern.Substring(1); return host.EndsWith(suf); }
            return pattern == host;
        }

        static bool SendOptions(string host, int port, out long rttMs, out string status)
        {
            rttMs = -1; status = "no response";
            try
            {
                IPAddress ip = null;
                foreach (var ad in Dns.GetHostAddresses(host)) if (ad.AddressFamily == AddressFamily.InterNetwork) { ip = ad; break; }
                if (ip == null) { status = "DNS failed"; return false; }
                string local = "0.0.0.0";
                try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect(ip, port); local = ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { }
                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 2500;
                    int lport = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
                    string br = "z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,12);
                    string msg = "OPTIONS sip:" + host + " SIP/2.0\r\n" +
                        "Via: SIP/2.0/UDP " + local + ":" + lport + ";branch=" + br + "\r\n" +
                        "Max-Forwards: 70\r\nFrom: <sip:losaterm@" + local + ">;tag=" + Guid.NewGuid().ToString("N").Substring(0,8) + "\r\n" +
                        "To: <sip:" + host + ">\r\nCall-ID: " + Guid.NewGuid().ToString("N").Substring(0,16) + "@" + local + "\r\n" +
                        "CSeq: 1 OPTIONS\r\nContact: <sip:losaterm@" + local + ":" + lport + ">\r\nUser-Agent: LosaTermVoip\r\nContent-Length: 0\r\n\r\n";
                    byte[] data = Encoding.ASCII.GetBytes(msg);
                    var sw = Stopwatch.StartNew();
                    udp.Send(data, data.Length, new IPEndPoint(ip, port));
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref remote);
                    sw.Stop(); rttMs = sw.ElapsedMilliseconds;
                    string text = Encoding.ASCII.GetString(resp);
                    int nl = text.IndexOf("\r\n");
                    string fl = nl > 0 ? text.Substring(0, nl) : text;
                    if (fl.StartsWith("SIP/2.0")) { status = fl.Substring(8).Trim(); return true; }
                    status = "non-SIP reply"; return false;
                }
            }
            catch (SocketException) { status = "timeout"; return false; }
            catch (Exception ex) { status = ex.Message; return false; }
        }

        void Section(string t) { Line("\r\n" + t + "\r\n", Color.FromArgb(120,200,255), true); }
        void Ok(string t)   { Line("  ✅ " + t + "\r\n", Color.LimeGreen, false); }
        void Dim(string t)  { Line(t + "\r\n", Color.Gray, false); }
        void Warn(string t, int penalty) { score -= penalty; warnings.Add("⚠ " + t); Line("  ⚠ " + t + "\r\n", Color.Orange, false); }
        void Fail(string t, int penalty) { score -= penalty; warnings.Add("❌ " + t); Line("  ❌ " + t + "\r\n", Color.OrangeRed, false); }

        void Line(string text, Color c, bool bold)
        {
            if (!rtb.IsHandleCreated) return;
            rtb.BeginInvoke((MethodInvoker)delegate {
                rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0;
                rtb.SelectionColor = c;
                rtb.SelectionFont = new Font("Consolas", 9.5f, bold ? FontStyle.Bold : FontStyle.Regular);
                rtb.AppendText(text); rtb.ScrollToCaret();
            });
        }

        void Done(string dom)
        {
            if (score < 0) score = 0; if (score > 100) score = 100;
            string verdict = score>=90?L.B("OTTIMO","EXCELLENT"):score>=75?L.B("BUONO","GOOD"):score>=50?L.B("DA SISTEMARE","NEEDS WORK"):L.B("CRITICO","CRITICAL");
            Color col = score>=90?Color.LimeGreen:score>=75?Color.YellowGreen:score>=50?Color.Orange:Color.OrangeRed;
            Line("\r\n══════════ " + L.B("RISULTATO","RESULT") + " ══════════\r\n", Color.White, true);
            if (warnings.Count == 0) Line("  " + L.B("Nessun problema rilevato.","No issues found.") + "\r\n", Color.LimeGreen, false);
            foreach (var w in warnings) Line("  " + w + "\r\n", w.StartsWith("❌")?Color.OrangeRed:Color.Orange, false);
            if (lblScore.IsHandleCreated)
                lblScore.BeginInvoke((MethodInvoker)delegate {
                    lblScore.Text = L.B("Punteggio: ","Score: ") + score + "/100   —   " + verdict;
                    lblScore.ForeColor = col;
                });
            EnableBtn();
        }

        void EnableBtn() { if (btnGo.IsHandleCreated) btnGo.BeginInvoke((MethodInvoker)delegate { btnGo.Enabled = true; }); }

        void SaveReport()
        {
            if (rtb.TextLength == 0) { MessageBox.Show(L.B("Esegui prima una diagnosi.","Run a diagnosis first."), "LosaTermVoip"); return; }
            using (var d = new SaveFileDialog { Filter = "Text (*.txt)|*.txt|HTML (*.html)|*.html", FileName = "sip_healthcheck_" + DateTime.Now.ToString("yyyyMMdd_HHmm") })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string body = rtb.Text + "\r\n" + lblScore.Text + "\r\n\r\n— LosaTermVoip · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " —\r\n";
                    if (d.FileName.ToLower().EndsWith(".html"))
                        body = "<!doctype html><meta charset=utf-8><title>SIP Health Check</title>" +
                               "<body style='background:#0d1117;color:#e6edf3;font-family:Consolas,monospace;padding:20px'><pre>" +
                               System.Security.SecurityElement.Escape(body) + "</pre></body>";
                    System.IO.File.WriteAllText(d.FileName, body, Encoding.UTF8);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "LosaTermVoip"); }
            }
        }
    }
}
