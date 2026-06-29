using System;
using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  WebRTC / SIP over WebSocket (RFC 7118) — testa una connessione ws/wss a
    //  un SIP server WebSocket (subprotocollo "sip") e invia un OPTIONS. Nativo
    //  (ClientWebSocket). Bilingue IT/EN.
    // ════════════════════════════════════════════════════════════════════════
    public class WebRtcPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtUrl, txtOut;
        CheckBox chkOptions;
        Button btnGo;

        public WebRtcPanel()
        {
            Text = "LosaTermVoip — SIP over WebSocket (WebRTC)";
            Size = new Size(820, 500);
            MinimumSize = new Size(620, 380);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(34,34,46) };
            top.Controls.Add(new Label { Text=L.B("URL WebSocket:","WebSocket URL:"), Location=new Point(12,14), AutoSize=true, ForeColor=Color.LightGray });
            txtUrl = new TextBox { Location=new Point(120,11), Width=380, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text="wss://sip.example.com:7443" };
            top.Controls.Add(txtUrl);
            btnGo = new Button { Text=L.B("🌐 Connetti","🌐 Connect"), Location=new Point(512,9), Width=130, Height=28, FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(40,80,140), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btnGo.FlatAppearance.BorderSize=0; btnGo.Click += (s,e)=>Run(); top.Controls.Add(btnGo);
            chkOptions = new CheckBox { Text=L.B("Invia un SIP OPTIONS dopo la connessione","Send a SIP OPTIONS after connecting"), Location=new Point(120,40), AutoSize=true, ForeColor=Color.LightGray, Checked=true };
            top.Controls.Add(chkOptions);

            txtOut = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Both, WordWrap=false,
                BackColor=Color.FromArgb(12,16,24), ForeColor=Color.LimeGreen, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };

            Controls.Add(txtOut);
            Controls.Add(top);
        }

        void Run()
        {
            string url = (txtUrl.Text ?? "").Trim();
            bool opt = chkOptions.Checked;
            btnGo.Enabled = false;
            txtOut.Text = L.B("Connessione a ","Connecting to ") + url + " …\r\n";
            ThreadPool.QueueUserWorkItem(_ => { string res = DoConnect(url, opt); ShowOut(res); });
        }

        string DoConnect(string url, bool sendOptions)
        {
            var sb = new StringBuilder();
            Uri uri;
            try { uri = new Uri(url); } catch { return L.B("✗ URL non valido (usa ws:// o wss://).","✗ Invalid URL (use ws:// or wss://)."); }
            bool wss = uri.Scheme.ToLower() == "wss";
            string host = uri.Host;

            ClientWebSocket ws = null;
            try
            {
                ws = new ClientWebSocket();
                ws.Options.AddSubProtocol("sip");
                using (var cts = new CancellationTokenSource(7000))
                {
                    try { ws.ConnectAsync(uri, cts.Token).Wait(); }
                    catch (AggregateException ae) { return L.B("✗ Handshake fallito: ","✗ Handshake failed: ") + Inner(ae) + L.B("\r\n(server WS SIP irraggiungibile, certificato TLS, o subprotocollo 'sip' non supportato)","\r\n(WS SIP server unreachable, TLS cert, or 'sip' subprotocol unsupported)"); }
                }
                if (ws.State != WebSocketState.Open)
                    return L.B("✗ Connessione non aperta (stato: ","✗ Connection not open (state: ") + ws.State + ").";

                sb.AppendLine(L.B("✅ Connesso.","✅ Connected."));
                sb.AppendLine("   Subprotocol : " + (string.IsNullOrEmpty(ws.SubProtocol) ? L.B("(nessuno — il server non ha negoziato 'sip')","(none — server did not negotiate 'sip')") : ws.SubProtocol));
                sb.AppendLine("   " + L.B("Trasporto   : ","Transport   : ") + (wss ? "WSS (TLS)" : "WS"));
                sb.AppendLine();

                if (sendOptions)
                {
                    string fake = Guid.NewGuid().ToString("N").Substring(0,10) + ".invalid";
                    string sip =
                        "OPTIONS sip:" + host + " SIP/2.0\r\n" +
                        "Via: SIP/2.0/" + (wss?"WSS":"WS") + " " + fake + ";branch=z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,12) + "\r\n" +
                        "Max-Forwards: 70\r\n" +
                        "To: <sip:" + host + ">\r\n" +
                        "From: <sip:losaterm@" + host + ">;tag=" + Guid.NewGuid().ToString("N").Substring(0,8) + "\r\n" +
                        "Call-ID: " + Guid.NewGuid().ToString("N").Substring(0,16) + "\r\n" +
                        "CSeq: 1 OPTIONS\r\n" +
                        "Contact: <sip:losaterm@" + fake + ";transport=ws>\r\n" +
                        "User-Agent: LosaTermVoip\r\nContent-Length: 0\r\n\r\n";
                    sb.AppendLine(">>> SIP OPTIONS over WebSocket");
                    byte[] data = Encoding.UTF8.GetBytes(sip);
                    using (var cts = new CancellationTokenSource(5000))
                    {
                        try
                        {
                            ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token).Wait();
                            var buf = new byte[8192];
                            var r = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token).Result;
                            sb.AppendLine("<<< ");
                            sb.AppendLine(Encoding.UTF8.GetString(buf, 0, r.Count));
                        }
                        catch (AggregateException) { sb.AppendLine(L.B("✗ Nessuna risposta SIP (timeout).","✗ No SIP response (timeout).")); }
                    }
                }

                try { using (var cts = new CancellationTokenSource(2000)) ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token).Wait(); } catch { }
            }
            catch (Exception ex) { return "✗ " + ex.Message; }
            finally { try { if (ws != null) ws.Dispose(); } catch { } }
            return sb.ToString();
        }

        static string Inner(AggregateException ae)
        {
            Exception e = ae; while (e.InnerException != null) e = e.InnerException; return e.Message;
        }

        void ShowOut(string res) { if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { txtOut.Text = res; btnGo.Enabled = true; }); }
    }
}
