using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Percorso di rete — scopre lo switch di access (LLDP/CDP, via Wireshark)
    //  e i salti L3 (traceroute): PC → switch/porta → gateway → core → FW → net.
    //  Bilingue IT/EN.
    // ════════════════════════════════════════════════════════════════════════
    public class NetPathPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        ComboBox cmbIface;
        TextBox txtDur, txtTarget;
        Button btnGo, btnRefresh;
        RichTextBox rtb;

        public NetPathPanel()
        {
            Text = "LosaTermVoip — Network Path (LLDP/CDP + traceroute)";
            Size = new Size(820, 560);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(34,34,46) };
            int y=10;
            top.Controls.Add(Lbl(L.B("Interfaccia:","Interface:"), 12, y));
            cmbIface = new ComboBox { Location=new Point(110,y-2), Width=430, DropDownStyle=ComboBoxStyle.DropDownList, BackColor=CIn, ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
            top.Controls.Add(cmbIface);
            btnRefresh = Btn("🔄", 548, y-3, 40, Color.FromArgb(60,60,80)); btnRefresh.Click += (s,e)=>RefreshIfaces(); top.Controls.Add(btnRefresh);
            y+=36;
            top.Controls.Add(Lbl(L.B("Ascolto LLDP/CDP (s):","Listen LLDP/CDP (s):"), 12, y));
            txtDur = new TextBox { Location=new Point(170,y-2), Width=50, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text="35" };
            top.Controls.Add(txtDur);
            top.Controls.Add(Lbl(L.B("Traceroute →:","Traceroute →:"), 240, y));
            txtTarget = new TextBox { Location=new Point(330,y-2), Width=120, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text="8.8.8.8" };
            top.Controls.Add(txtTarget);
            btnGo = Btn(L.B("🗺️ Scopri percorso","🗺️ Discover path"), 470, y-3, 160, Color.FromArgb(30,110,30)); btnGo.Click += (s,e)=>Run(); top.Controls.Add(btnGo);
            var btnR = ReportHelper.MakeButton(636, y-4);
            btnR.Click += (s,e)=>ReportHelper.ExportText(this, "Network Path (LLDP/CDP)", rtb.Text);
            top.Controls.Add(btnR);

            rtb = new RichTextBox { Dock=DockStyle.Fill, ReadOnly=true, BackColor=Color.FromArgb(12,16,24),
                ForeColor=Color.Gainsboro, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };
            rtb.TextChanged += (s,e)=>ReportHelper.Set("Network Path (LLDP/CDP)", rtb.Text);

            Controls.Add(rtb);
            Controls.Add(top);
            RefreshIfaces();
        }

        string Dumpcap()
        {
            string t = PcapAnalyzer.FindTshark();
            if (t == null) return null;
            string d = Path.Combine(Path.GetDirectoryName(t), "dumpcap.exe");
            return File.Exists(d) ? d : null;
        }

        class IfaceItem { public string Num, Label; public override string ToString(){ return Label; } }

        void RefreshIfaces()
        {
            cmbIface.Items.Clear();
            string dc = Dumpcap();
            if (dc == null) { cmbIface.Items.Add(new IfaceItem{ Num="", Label=L.B("(dumpcap non trovato — LLDP/CDP non disponibile)","(dumpcap not found — LLDP/CDP unavailable)") }); cmbIface.SelectedIndex=0; return; }
            try
            {
                string outp = Run(dc, "-D");
                var ipMap = IfaceIpMap();
                foreach (var line in outp.Replace("\r","").Split('\n'))
                {
                    string ln = line.Trim(); if (ln.Length == 0) continue;
                    int dot = ln.IndexOf('.'); if (dot <= 0) continue;
                    string num = ln.Substring(0, dot).Trim();
                    string rest = ln.Substring(dot+1).Trim();
                    int op = rest.LastIndexOf('('); int cp = rest.LastIndexOf(')');
                    string friendly = (op>=0 && cp>op) ? rest.Substring(op+1, cp-op-1) : rest;
                    string ipStr = "";
                    var gm = Regex.Match(rest, @"\{[0-9A-Fa-f\-]+\}");
                    if (gm.Success && ipMap.ContainsKey(gm.Value)) ipStr = "  (" + ipMap[gm.Value] + ")";
                    cmbIface.Items.Add(new IfaceItem { Num = num, Label = num + ": " + friendly + ipStr });
                }
                if (cmbIface.Items.Count > 0) cmbIface.SelectedIndex = 0;
            }
            catch { }
        }

        static Dictionary<string,string> IfaceIpMap()
        {
            var map = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) {
                    string ipv4 = null;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses) if (ua.Address.AddressFamily == AddressFamily.InterNetwork) { ipv4 = ua.Address.ToString(); break; }
                    if (ipv4 != null && !map.ContainsKey(ni.Id)) map[ni.Id] = ipv4;
                } } catch { }
            return map;
        }

        void Run()
        {
            rtb.Clear();
            btnGo.Enabled = false;
            string target = (txtTarget.Text ?? "8.8.8.8").Trim();
            int dur; if (!int.TryParse(txtDur.Text, out dur) || dur < 5) dur = 35;
            var ifc = cmbIface.SelectedItem as IfaceItem;
            string ifaceNum = ifc != null ? ifc.Num : "";
            string ifaceLabel = ifc != null ? ifc.Label : "?";

            Line("══════════ " + L.B("PERCORSO DI RETE","NETWORK PATH") + " ══════════\r\n", Color.White, true);
            Line(L.B("Il tuo PC : ","Your PC   : ") + GetPrimaryIp() + "   [" + ifaceLabel + "]\r\n", Color.FromArgb(120,200,255), false);

            ThreadPool.QueueUserWorkItem(_ => {
                // ── 1. LLDP / CDP ──
                Section(L.B("1) Switch di access (LLDP / CDP)","1) Access switch (LLDP / CDP)"));
                string dc = Dumpcap();
                if (dc == null || ifaceNum.Length == 0)
                    Dim(L.B("  (richiede Wireshark + Npcap per leggere i frame LLDP/CDP)","  (requires Wireshark + Npcap to read LLDP/CDP frames)"));
                else
                {
                    Dim(L.B("  Ascolto per "+dur+" s… (lo switch annuncia LLDP ~30s, CDP ~60s)","  Listening for "+dur+" s… (switch sends LLDP ~30s, CDP ~60s)"));
                    DiscoverSwitch(dc, ifaceNum, dur);
                }

                // ── 2. Gateway ──
                Section(L.B("2) Gateway predefinito","2) Default gateway"));
                string gw = GetGateway();
                Line("  " + (gw ?? L.B("(non trovato)","(not found)")) + "\r\n", Color.LimeGreen, false);

                // ── 3. Traceroute ──
                Section(L.B("3) Salti L3 verso ","3) L3 hops toward ") + target + L.B("  (core → firewall → uscita)","  (core → firewall → edge)"));
                RunTracert(target);

                Line("\r\n" + L.B("Fine.","Done."), Color.Gray, true);
                if (btnGo.IsHandleCreated) btnGo.BeginInvoke((MethodInvoker)delegate { btnGo.Enabled = true; });
            });
        }

        void DiscoverSwitch(string dumpcap, string ifaceNum, int dur)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "losaterm_lldp_" + DateTime.Now.ToString("HHmmss") + ".pcap");
            try
            {
                // cattura solo LLDP (0x88cc) e CDP (dst 01:00:0c:cc:cc:cc)
                var psi = new ProcessStartInfo(dumpcap, "-i " + ifaceNum + " -f \"ether proto 0x88cc or ether host 01:00:0c:cc:cc:cc\" -a duration:" + dur + " -w \"" + tmp + "\"")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                using (var p = Process.Start(psi)) { p.BeginErrorReadLine(); p.ErrorDataReceived += (s,e)=>{}; p.WaitForExit(); }

                string tshark = PcapAnalyzer.FindTshark();
                bool found = false;
                // LLDP
                string lldp = Run(tshark, "-r \"" + tmp + "\" -Y lldp -T fields -e lldp.tlv.system.name -e lldp.port.id -e lldp.port.desc -e lldp.ieee.802_1.port_vlan.id -e lldp.mgn.addr.ip4 -e lldp.tlv.system.desc -E separator=| -c 1");
                var lf = FirstNonEmpty(lldp);
                if (lf != null)
                {
                    var f = (lf + "|||||").Split('|');
                    Ok("LLDP → switch: " + Val(f[0]) + "   " + L.B("porta: ","port: ") + Val(SomeOf(f[1],f[2])) + (Has(f[3])?"   VLAN: "+f[3].Trim():"") );
                    if (Has(f[4])) Dim("   mgmt IP : " + f[4].Trim());
                    if (Has(f[5])) Dim("   " + L.B("modello : ","model   : ") + Short(f[5]));
                    found = true;
                }
                // CDP
                string cdp = Run(tshark, "-r \"" + tmp + "\" -Y cdp -T fields -e cdp.deviceid -e cdp.portid -e cdp.platform -e cdp.native_vlan -E separator=| -c 1");
                var cf = FirstNonEmpty(cdp);
                if (cf != null)
                {
                    var f = (cf + "||||").Split('|');
                    Ok("CDP  → switch: " + Val(f[0]) + "   " + L.B("porta: ","port: ") + Val(f[1]) + (Has(f[3])?"   VLAN: "+f[3].Trim():""));
                    if (Has(f[2])) Dim("   " + L.B("modello : ","model   : ") + Short(f[2]));
                    found = true;
                }
                if (!found)
                    Warn(L.B("Nessun LLDP/CDP ricevuto: lo switch non li annuncia, la porta non li abilita, oppure sei su WiFi/hub. Prova ad aumentare i secondi (es. 65 per il CDP).","No LLDP/CDP received: the switch doesn't advertise them, the port has them disabled, or you're on WiFi/a hub. Try increasing the seconds (e.g. 65 for CDP)."));
            }
            catch (Exception ex) { Warn("✗ " + ex.Message); }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch {} }
        }

        void RunTracert(string target)
        {
            try
            {
                var psi = new ProcessStartInfo("tracert", "-d -h 15 -w 1200 " + target)
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.Default };
                using (var p = Process.Start(psi))
                {
                    string line;
                    while ((line = p.StandardOutput.ReadLine()) != null)
                    {
                        string t = line.Trim();
                        if (t.Length == 0) continue;
                        if (t.StartsWith("Traccia") || t.StartsWith("Tracing") || t.StartsWith("over a") || t.StartsWith("su un") || t.StartsWith("Rilevazione") || t.Contains("massimo") || t.Contains("maximum")) { Dim("  " + t); continue; }
                        Line("  " + t + "\r\n", Color.Gainsboro, false);
                    }
                    p.WaitForExit();
                }
            }
            catch (Exception ex) { Warn("✗ tracert: " + ex.Message); }
        }

        // ── util ──
        static string GetPrimaryIp()
        {
            try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect("8.8.8.8", 65530); return ((System.Net.IPEndPoint)s.LocalEndPoint).Address.ToString(); } }
            catch { return "n/d"; }
        }
        static string GetGateway()
        {
            try { foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var g in ni.GetIPProperties().GatewayAddresses)
                        if (g.Address != null && g.Address.AddressFamily == AddressFamily.InterNetwork && g.Address.ToString() != "0.0.0.0")
                            return g.Address.ToString() + "   [" + ni.Name + "]";
                } } catch { }
            return null;
        }
        static string FirstNonEmpty(string outp){ foreach (var l in (outp ?? "").Replace("\r","").Split('\n')) if (l.Trim().Replace("|","").Length>0) return l; return null; }
        static bool Has(string s){ return s != null && s.Trim().Length > 0; }
        static string Val(string s){ return Has(s) ? s.Trim() : "?"; }
        static string SomeOf(string a, string b){ return Has(a)?a:b; }
        static string Short(string s){ s=(s??"").Trim(); return s.Length>60?s.Substring(0,60)+"…":s; }

        void Section(string t){ Line("\r\n" + t + "\r\n", Color.FromArgb(120,200,255), true); }
        void Ok(string t){ Line("  ✅ " + t + "\r\n", Color.LimeGreen, false); }
        void Dim(string t){ Line(t + "\r\n", Color.Gray, false); }
        void Warn(string t){ Line("  ⚠ " + t + "\r\n", Color.Orange, false); }
        void Line(string text, Color c, bool bold)
        {
            if (!rtb.IsHandleCreated) return;
            rtb.BeginInvoke((MethodInvoker)delegate {
                rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0; rtb.SelectionColor = c;
                rtb.SelectionFont = new Font("Consolas", 9.5f, bold ? FontStyle.Bold : FontStyle.Regular);
                rtb.AppendText(text); rtb.ScrollToCaret();
            });
        }

        static string Run(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute=false, RedirectStandardOutput=true, CreateNoWindow=true, StandardOutputEncoding=Encoding.UTF8 };
            using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return o; }
        }

        static Label Lbl(string t,int x,int y){ return new Label{ Text=t, Location=new Point(x,y+2), AutoSize=true, ForeColor=Color.LightGray }; }
        Button Btn(string t,int x,int y,int w,Color c){ var b=new Button{ Text=t, Location=new Point(x,y), Width=w, Height=28, FlatStyle=FlatStyle.Flat, BackColor=c, ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold)}; b.FlatAppearance.BorderSize=0; return b; }
    }
}
