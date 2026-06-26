using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Generatore traffico SIP (mini-SIPp) — flood OPTIONS/REGISTER a CPS dato.
    //  Bilingue IT/EN. ⚠ Solo su sistemi autorizzati (stress test).
    // ════════════════════════════════════════════════════════════════════════
    public class TrafficGenPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtTarget, txtCps, txtTotal;
        ComboBox cmbMethod;
        Button btnStart, btnStop;
        Label lblStats;
        System.Windows.Forms.Timer ui;

        volatile bool running;
        Thread sender, receiver;
        UdpClient udp;
        IPEndPoint dest;
        string host, local; int port, lport;
        string method;
        Stopwatch clock = new Stopwatch();

        readonly object gate = new object();
        long sent;
        readonly Dictionary<int,long> byCode = new Dictionary<int,long>();
        readonly Dictionary<string,long> pending = new Dictionary<string,long>();
        long rttCnt, rttSum, rttMin, rttMax;

        public TrafficGenPanel()
        {
            Text = "LosaTermVoip — SIP Traffic Generator (mini-SIPp)";
            Size = new Size(720, 470);
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
            FormClosed += (s,e)=>StopGen();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 120, BackColor = Color.FromArgb(34,34,46) };
            int x1=14, y=12;
            top.Controls.Add(Lbl(L.B("Target (host[:porta]):","Target (host[:port]):"), x1, y));
            txtTarget = T(170, y, 230); txtTarget.Text="sbc.azienda.it"; top.Controls.Add(txtTarget); y+=30;
            top.Controls.Add(Lbl(L.B("Metodo:","Method:"), x1, y));
            cmbMethod = new ComboBox { Location=new Point(170,y-2), Width=140, DropDownStyle=ComboBoxStyle.DropDownList, BackColor=CIn, ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
            cmbMethod.Items.AddRange(new object[]{ "OPTIONS","REGISTER" }); cmbMethod.SelectedIndex=0; top.Controls.Add(cmbMethod);
            top.Controls.Add(Lbl("CPS:", 340, y));
            txtCps = T(390, y, 70); txtCps.Text="20"; top.Controls.Add(txtCps);
            top.Controls.Add(Lbl(L.B("Totale richieste:","Total requests:"), 480, y));
            txtTotal = T(585, y, 70); txtTotal.Text="200"; top.Controls.Add(txtTotal); y+=34;
            btnStart = Btn(L.B("▶ Avvia","▶ Start"), 170, y, 110, Color.FromArgb(30,110,30)); btnStart.Click += (s,e)=>StartGen(); top.Controls.Add(btnStart);
            btnStop = Btn(L.B("■ Ferma","■ Stop"), 288, y, 100, Color.FromArgb(120,30,30)); btnStop.Enabled=false; btnStop.Click += (s,e)=>StopGen(); top.Controls.Add(btnStop);
            top.Controls.Add(new Label { Text=L.B("⚠ Solo su sistemi autorizzati (stress test).","⚠ Authorized systems only (stress test)."), Location=new Point(400,y+6), AutoSize=true, ForeColor=Color.Orange });

            lblStats = new Label { Dock=DockStyle.Fill, ForeColor=Color.LimeGreen, Font=new Font("Consolas",11), Padding=new Padding(14),
                BackColor=Color.FromArgb(12,16,24), Text=L.B("Pronto.","Ready.") };

            Controls.Add(lblStats);
            Controls.Add(top);
        }

        void StartGen()
        {
            host = (txtTarget.Text ?? "").Trim(); port=5060;
            int c = host.LastIndexOf(':'); if (c>0){ string p=host.Substring(c+1); host=host.Substring(0,c); int.TryParse(p, out port); }
            if (port<=0) port=5060;
            method = cmbMethod.SelectedItem as string ?? "OPTIONS";
            int cps; if (!int.TryParse(txtCps.Text, out cps) || cps<1) cps=10; if (cps>1000) cps=1000;
            int total; if (!int.TryParse(txtTotal.Text, out total) || total<1) total=100;

            IPAddress ip=null;
            try { foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily==AddressFamily.InterNetwork){ ip=a; break; } } catch {}
            if (ip==null) { MessageBox.Show(L.B("Impossibile risolvere ","Cannot resolve ") + host, "Generator"); return; }
            dest = new IPEndPoint(ip, port);
            try { using (var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,0)){ s.Connect(ip,port); local=((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { local="0.0.0.0"; }

            lock (gate) { sent=0; byCode.Clear(); pending.Clear(); rttCnt=0; rttSum=0; rttMin=long.MaxValue; rttMax=0; }
            try { udp = new UdpClient(0); udp.Client.ReceiveTimeout=400; lport=((IPEndPoint)udp.Client.LocalEndPoint).Port; }
            catch (Exception ex) { MessageBox.Show("Socket: " + ex.Message, "Generator"); return; }

            running=true; clock.Restart();
            btnStart.Enabled=false; btnStop.Enabled=true;
            receiver = new Thread(()=>ReceiverLoop()){ IsBackground=true }; receiver.Start();
            sender = new Thread(()=>SenderLoop(cps,total)){ IsBackground=true }; sender.Start();
            if (ui==null){ ui=new System.Windows.Forms.Timer{ Interval=400 }; ui.Tick += (s,e)=>Refresh2(); }
            ui.Start();
        }

        void StopGen()
        {
            running=false;
            try { if (ui!=null) ui.Stop(); } catch {}
            try { if (udp!=null) udp.Close(); } catch {}
            if (IsHandleCreated) { try { BeginInvoke((MethodInvoker)delegate { btnStart.Enabled=true; btnStop.Enabled=false; Refresh2(); }); } catch {} }
        }

        void SenderLoop(int cps, int total)
        {
            double interval = 1000.0/cps;
            for (int i=0; i<total && running; i++)
            {
                SendOne();
                double due = (i+1)*interval, now = clock.Elapsed.TotalMilliseconds;
                int slp = (int)(due-now);
                if (slp>0) Thread.Sleep(slp);
            }
            Thread.Sleep(800);
            running=false;
            if (IsHandleCreated) { try { BeginInvoke((MethodInvoker)delegate { btnStart.Enabled=true; btnStop.Enabled=false; Refresh2(); }); } catch {} }
        }

        void SendOne()
        {
            string branch = "z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,14);
            string cid = Guid.NewGuid().ToString("N").Substring(0,16);
            string uri = "sip:" + host;
            var sb = new StringBuilder();
            sb.Append(method + " " + uri + " SIP/2.0\r\n");
            sb.Append("Via: SIP/2.0/UDP " + local + ":" + lport + ";branch=" + branch + ";rport\r\n");
            sb.Append("Max-Forwards: 70\r\n");
            sb.Append("From: <sip:loadtest@" + local + ">;tag=" + cid.Substring(0,8) + "\r\n");
            sb.Append("To: <sip:" + (method=="REGISTER"?"loadtest@"+host:host) + ">\r\n");
            sb.Append("Call-ID: " + cid + "@" + local + "\r\n");
            sb.Append("CSeq: 1 " + method + "\r\n");
            sb.Append("Contact: <sip:loadtest@" + local + ":" + lport + ">\r\n");
            if (method=="REGISTER") sb.Append("Expires: 60\r\n");
            sb.Append("User-Agent: LosaTermVoip-loadgen\r\nContent-Length: 0\r\n\r\n");
            byte[] data = Encoding.ASCII.GetBytes(sb.ToString());
            try
            {
                lock (gate) { pending[branch] = (long)clock.Elapsed.TotalMilliseconds; sent++; }
                udp.Send(data, data.Length, dest);
            }
            catch { }
        }

        void ReceiverLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                byte[] resp;
                try { resp = udp.Receive(ref remote); }
                catch (SocketException) { continue; }
                catch { break; }
                try
                {
                    string text = Encoding.ASCII.GetString(resp);
                    var mc = Regex.Match(text, @"^SIP/2\.0\s+(\d{3})");
                    if (!mc.Success) continue;
                    int code = int.Parse(mc.Groups[1].Value);
                    if (code < 200) continue;
                    var mb = Regex.Match(text, @"branch=([^;\s>]+)");
                    string branch = mb.Success ? mb.Groups[1].Value : null;
                    lock (gate)
                    {
                        long cur; byCode.TryGetValue(code, out cur); byCode[code] = cur+1;
                        if (branch != null && pending.ContainsKey(branch))
                        {
                            long rtt = (long)clock.Elapsed.TotalMilliseconds - pending[branch];
                            pending.Remove(branch);
                            rttCnt++; rttSum += rtt; if (rtt<rttMin) rttMin=rtt; if (rtt>rttMax) rttMax=rtt;
                        }
                    }
                }
                catch { }
            }
        }

        void Refresh2()
        {
            string txt;
            lock (gate)
            {
                double secs = clock.Elapsed.TotalSeconds;
                double cps = secs>0 ? sent/secs : 0;
                long responses=0; foreach (var kv in byCode) responses+=kv.Value;
                long timeouts = sent - responses;
                var sb = new StringBuilder();
                sb.AppendLine((running?L.B("🔴 In corso","🔴 Running"):L.B("✅ Terminato","✅ Finished")) + "   target " + host + ":" + port + "  (" + method + ")");
                sb.AppendLine("");
                sb.AppendLine(L.B("Inviati          : ","Sent             : ") + sent);
                sb.AppendLine(L.B("CPS effettivi    : ","Actual CPS       : ") + cps.ToString("0.0"));
                sb.AppendLine(L.B("Risposte         : ","Responses        : ") + responses);
                var codes = new List<int>(byCode.Keys); codes.Sort();
                foreach (var k in codes) sb.AppendLine("   " + k + "            : " + byCode[k]);
                sb.AppendLine(L.B("Senza risposta   : ","No response      : ") + (timeouts>0?timeouts:0));
                sb.AppendLine("");
                if (rttCnt>0)
                    sb.AppendLine("RTT (ms)         : min " + rttMin + " · avg " + (rttSum/rttCnt) + " · max " + rttMax);
                else sb.AppendLine("RTT (ms)         : —");
                txt = sb.ToString();
            }
            if (lblStats.IsHandleCreated) lblStats.Text = txt;
        }

        static Label Lbl(string t,int x,int y){ return new Label{ Text=t, Location=new Point(x,y+2), AutoSize=true, ForeColor=Color.LightGray }; }
        TextBox T(int x,int y,int w){ return new TextBox{ Location=new Point(x,y), Width=w, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle }; }
        Button Btn(string t,int x,int y,int w,Color c){ var b=new Button{ Text=t, Location=new Point(x,y), Width=w, Height=28, FlatStyle=FlatStyle.Flat, BackColor=c, ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold)}; b.FlatAppearance.BorderSize=0; return b; }
    }
}
