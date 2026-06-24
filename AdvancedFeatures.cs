using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CISCO DOCS PANEL
    //  Quick-access alla documentazione Cisco per VoIP engineers:
    //  CUBE IOS-XE, CUCM, CME, MGCP — selezionabile per versione
    // ═══════════════════════════════════════════════════════════════════════════

    public class CiscoDocsPanel : Form
    {
        WebBrowser wb;
        ComboBox cmbProduct, cmbVersion;
        TextBox txtSearch;
        ListBox lbLinks;

        static readonly Dictionary<string, string[]> Products = new Dictionary<string, string[]>
        {
            { "CUBE (IOS-XE)",        new[]{ "17.15","17.14","17.13","17.12","17.9","17.6","17.3","16.12","16.9" }},
            { "CUCM",                  new[]{ "15.0","14.0","12.5","12.0","11.5","10.5" }},
            { "Cisco CME",             new[]{ "15.9","15.6","12.2" }},
            { "Cisco MGCP Gateway",    new[]{ "IOS 15.x","IOS 12.4" }},
            { "Cisco UBE (SRST)",      new[]{ "17.x","16.x","15.x" }},
            { "Cisco WebEx Calling",   new[]{ "Latest" }},
        };

        // Link diretti per prodotto (indipendenti dalla versione — versione viene in URL)
        static readonly Dictionary<string, string> BaseUrls = new Dictionary<string, string>
        {
            { "CUBE (IOS-XE)",      "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book.html" },
            { "CUCM",               "https://www.cisco.com/c/en/us/support/unified-communications/unified-communications-manager-callmanager/products-installation-and-configuration-guides-list.html" },
            { "Cisco CME",          "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/callmgr/configuration/callmgr-xe-book.html" },
            { "Cisco MGCP Gateway", "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/mgcp/configuration/15-mt/voice-mgcp-15-mt-book.html" },
            { "Cisco UBE (SRST)",   "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/srst/configuration/15-mt/srst-15-mt-book.html" },
            { "Cisco WebEx Calling","https://help.webex.com/en-us/article/n4a2h3ob/Webex-Calling" },
        };

        // Quick links per ogni prodotto (titolo → URL)
        static readonly Dictionary<string, Dictionary<string,string>> QuickLinks =
            new Dictionary<string, Dictionary<string,string>>
        {
            { "CUBE (IOS-XE)", new Dictionary<string,string> {
                { "CUBE Configuration Guide",           "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book.html" },
                { "SIP Trunk Configuration",            "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book/voi-cube-sip-trunk.html" },
                { "Codec / SDP Negotiation",            "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book/voi-cube-codecs.html" },
                { "Media Flow-Around / Flow-Through",   "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book/voi-cube-media.html" },
                { "SRTP / TLS Configuration",           "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book/voi-cube-security.html" },
                { "Dial-Peer Configuration",            "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/dial_peer/configuration/15-mt/voi-dial-peer-15-mt-book.html" },
                { "Troubleshooting CUBE",               "https://www.cisco.com/c/en/us/support/docs/voice-unified-communications/unified-border-element/200589-CUBE-Troubleshooting-Guide.html" },
                { "debug ccsip messages",               "https://www.cisco.com/c/en/us/support/docs/voice-unified-communications/unified-border-element/211994-CUBE-SIP-Debugging-Guide.html" },
                { "H.323 on CUBE",                      "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book/voi-cube-h323.html" },
                { "SIP-I / SS7 Interworking",           "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book/voi-cube-sip-i.html" },
                { "DTMF / RFC 2833",                    "https://www.cisco.com/c/en/us/support/docs/voice-unified-communications/unified-border-element/200594-Configure-DTMF-Relay-on-Cisco-Unified.html" },
                { "T.38 Fax",                           "https://www.cisco.com/c/en/us/support/docs/voice-unified-communications/unified-border-element/200595-Configure-T38-Fax-Relay-on-CUBE.html" },
                { "Q.850 Cause Codes",                  "https://www.cisco.com/c/en/us/support/docs/voice-unified-communications/voice-gateways/26077-understanding-cause-codes.html" },
                { "CUBE Release Notes 17.x",            "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book/cube-new-features.html" },
            }},
            { "CUCM", new Dictionary<string,string> {
                { "CUCM Admin Guide",                   "https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/admin/15_0_1/cucm_b_cisco-unified-cm-admin-guide-1501.html" },
                { "SIP Trunk Configuration",            "https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/admin/15_0_1/cucm_b_cisco-unified-cm-admin-guide-1501/cucm_m_sip-trunk.html" },
                { "Route Plan & Dial Plan",             "https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/admin/15_0_1/cucm_b_cisco-unified-cm-admin-guide-1501/cucm_m_call-routing.html" },
                { "CUCM Troubleshooting",               "https://www.cisco.com/c/en/us/support/docs/voice-unified-communications/unified-communications-manager-callmanager/200589-CUCM-Troubleshooting-Guide.html" },
                { "SRST & Fallback",                    "https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/srnd/collab12/collab12_srnd/fallback.html" },
                { "Certificate Management",             "https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/security/15_0_1/cucm_b_cisco-unified-cm-security-guide-1501.html" },
                { "CTI / JTAPI Guide",                  "https://www.cisco.com/c/en/us/support/unified-communications/unified-communications-manager-callmanager/products-programming-reference-guides-list.html" },
                { "Media Resources (MTP/XCODER)",       "https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/admin/15_0_1/cucm_b_cisco-unified-cm-admin-guide-1501/cucm_m_media-resources.html" },
                { "CUCM Release Notes 15.0",            "https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/rel_notes/15_0_1/cucm_b_cisco-unified-cm-release-notes-1501.html" },
                { "Bulk Admin Tool (BAT)",               "https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/bat/15_0_1/cucm_b_bulk-admin-guide-1501.html" },
            }},
            { "Cisco CME", new Dictionary<string,string> {
                { "CME Configuration Guide",            "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/callmgr/configuration/callmgr-xe-book.html" },
                { "ephone / ephone-dn",                 "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/callmgr/configuration/callmgr-xe-book/voi-cme-ephone.html" },
                { "SIP Phone on CME",                   "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/callmgr/configuration/callmgr-xe-book/voi-cme-sip.html" },
                { "CME Dial Plan",                      "https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/callmgr/configuration/callmgr-xe-book/voi-cme-dial-plan.html" },
            }},
        };

        public CiscoDocsPanel()
        {
            Text      = "📚 Cisco Documentation — VoIP";
            Size      = new Size(1100, 720);
            MinimumSize = new Size(800, 500);
            BackColor = Color.FromArgb(18, 18, 28);
            ForeColor = Color.White;
            Font      = new Font("Segoe UI", 9);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUI();
        }

        void BuildUI()
        {
            // ── Barra selezione ──────────────────────────────────────────────
            var top = new Panel { Dock = DockStyle.Top, Height = 36,
                BackColor = Color.FromArgb(28,35,55), Padding = new Padding(6,4,4,2) };

            top.Controls.Add(DL("Prodotto:", 6, 8, 60));
            cmbProduct = new ComboBox { Location = new Point(66,4), Width=180,
                DropDownStyle=ComboBoxStyle.DropDownList,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
            foreach (var p in Products.Keys) cmbProduct.Items.Add(p);
            cmbProduct.SelectedIndex = 0;
            cmbProduct.SelectedIndexChanged += (s,e) => RefreshVersions();
            top.Controls.Add(cmbProduct);

            top.Controls.Add(DL("Versione:", 256, 8, 65));
            cmbVersion = new ComboBox { Location = new Point(321,4), Width=90,
                DropDownStyle=ComboBoxStyle.DropDownList,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
            top.Controls.Add(cmbVersion);

            top.Controls.Add(DL("Cerca:", 422, 8, 46));
            txtSearch = new TextBox { Location = new Point(468,4), Width=220,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White,
                BorderStyle=BorderStyle.FixedSingle };
            txtSearch.KeyDown += (s,e) => { if(e.KeyCode==Keys.Enter) SearchDocs(); };
            top.Controls.Add(txtSearch);

            var btnSearch = DB("🔍 Cerca", 696, 4, 90);
            btnSearch.Click += (s,e) => SearchDocs();
            top.Controls.Add(btnSearch);

            var btnCisco = DB("🌐 Cisco Search", 794, 4, 120, Color.FromArgb(0,80,160));
            btnCisco.Click += (s,e) => OpenCiscoSearch();
            top.Controls.Add(btnCisco);

            // ── Split: link list sinistra + browser destra ───────────────────
            var split = new SplitContainer { Dock=DockStyle.Fill };
            try { split.SplitterDistance = 280; } catch { }

            // Sinistra: quick links
            lbLinks = new ListBox { Dock=DockStyle.Fill,
                BackColor=Color.FromArgb(22,28,45), ForeColor=Color.LightCyan,
                BorderStyle=BorderStyle.None, Font=new Font("Segoe UI",9) };
            lbLinks.DoubleClick += (s,e) => OpenSelectedLink();

            var lblLinks = new Label { Text="  Quick Links (doppio-click)",
                Dock=DockStyle.Top, Height=22,
                BackColor=Color.FromArgb(30,70,140), ForeColor=Color.White,
                Font=new Font("Segoe UI",8,FontStyle.Bold), TextAlign=ContentAlignment.MiddleLeft,
                Padding=new Padding(4,0,0,0) };
            split.Panel1.Controls.Add(lbLinks);
            split.Panel1.Controls.Add(lblLinks);

            // Destra: browser embedded
            wb = new WebBrowser { Dock=DockStyle.Fill, ScriptErrorsSuppressed=true };
            split.Panel2.Controls.Add(wb);

            Controls.Add(split);
            Controls.Add(top);

            RefreshVersions();
        }

        void RefreshVersions()
        {
            string prod = cmbProduct.SelectedItem as string;
            cmbVersion.Items.Clear();
            if (prod != null && Products.ContainsKey(prod))
                foreach (var v in Products[prod]) cmbVersion.Items.Add(v);
            if (cmbVersion.Items.Count > 0) cmbVersion.SelectedIndex = 0;
            RefreshLinks();
        }

        void RefreshLinks()
        {
            lbLinks.Items.Clear();
            string prod = cmbProduct.SelectedItem as string;
            if (prod == null || !QuickLinks.ContainsKey(prod)) return;
            foreach (var kv in QuickLinks[prod])
                lbLinks.Items.Add(kv.Key);
        }

        void OpenSelectedLink()
        {
            string prod = cmbProduct.SelectedItem as string;
            string sel  = lbLinks.SelectedItem as string;
            if (prod == null || sel == null) return;
            if (!QuickLinks.ContainsKey(prod)) return;
            string url;
            if (QuickLinks[prod].TryGetValue(sel, out url))
                wb.Navigate(url);
        }

        void SearchDocs()
        {
            string q    = txtSearch.Text.Trim();
            string prod = cmbProduct.SelectedItem as string ?? "CUBE";
            if (string.IsNullOrEmpty(q)) return;
            string url = string.Format(
                "https://www.cisco.com/search#stq={0}+{1}&stp=1",
                Uri.EscapeDataString(prod.Split(' ')[0]),
                Uri.EscapeDataString(q));
            wb.Navigate(url);
        }

        void OpenCiscoSearch()
        {
            string prod = cmbProduct.SelectedItem as string ?? "CUBE";
            string url;
            if (!BaseUrls.TryGetValue(prod, out url)) url = "https://www.cisco.com/c/en/us/support/index.html";
            wb.Navigate(url);
        }

        static Label DL(string t, int x, int y, int w)
        {
            return new Label { Text=t, Location=new Point(x,y), Width=w, ForeColor=Color.LightGray, AutoSize=false };
        }
        static Button DB(string t, int x, int y, int w, Color? bg=null)
        {
            var b = new Button { Text=t, Location=new Point(x,y), Width=w, Height=26,
                FlatStyle=FlatStyle.Flat, ForeColor=Color.White,
                BackColor=bg??Color.FromArgb(50,50,80) };
            b.FlatAppearance.BorderSize=0;
            return b;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TRANSLATORX — Call Simulator / SIP Message Generator
    //  Ispired by TranslatorX: genera messaggi SIP, simula chiamate,
    //  analizza cause di fallimento, traduce codici SIP/Q.850/ISUP
    // ═══════════════════════════════════════════════════════════════════════════

    public class TranslatorXPanel : Form
    {
        // ── UI ────────────────────────────────────────────────────────────────
        TabControl tabs;
        // Tab 1: SIP Simulator
        TextBox txtSipDst, txtSipFrom, txtSipTo, txtSipProxy, txtSipOutput;
        TextBox txtSipPort;
        ComboBox cmbSipMethod;
        Button btnSipSend;
        // Tab 2: Cause Code Translator
        TextBox txtCauseIn, txtCauseOut;
        ComboBox cmbCauseFrom, cmbCauseTo;
        // Tab 3: SDP / Codec Analyzer
        TextBox txtSdpIn, txtSdpOut;

        public TranslatorXPanel()
        {
            Text = "Losaterm";
            Size = new Size(820, 580);
            MinimumSize = new Size(700, 500);
            BackColor = Color.FromArgb(24,24,32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUI();
        }

        void BuildUI()
        {
            tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildSimulatorTab());
            tabs.TabPages.Add(BuildCauseTab());
            tabs.TabPages.Add(BuildSdpTab());
            tabs.TabPages.Add(BuildDebugTab());
            Controls.Add(tabs);
        }

        // ── Tab 1: SIP Simulator ─────────────────────────────────────────────
        TabPage BuildSimulatorTab()
        {
            var page = new TabPage("📤  SIP Simulator") {
                BackColor=Color.FromArgb(22,22,32), ForeColor=Color.White, Padding=new Padding(8) };

            var cfg = new Panel { Dock=DockStyle.Top, Height=230,
                BackColor=Color.FromArgb(28,35,55), Padding=new Padding(12) };

            int y=10;
            cfg.Controls.Add(ML(L.T("sim.dest"), 12, y));
            txtSipDst = MT(140, y, 200); txtSipDst.Text = "192.168.1.1:5060";
            cfg.Controls.Add(txtSipDst);

            cfg.Controls.Add(new Label { Text=L.T("sim.local_port"), Location=new Point(360,y+2), Width=90, ForeColor=Color.LightGray });
            txtSipPort = MT(450, y, 80); txtSipPort.Text = "5060";
            cfg.Controls.Add(txtSipPort);

            y+=34;
            cfg.Controls.Add(ML("From (SIP URI):", 12, y));
            txtSipFrom = MT(140, y, 200); txtSipFrom.Text = "sip:1000@" + GetLocalIp();
            cfg.Controls.Add(txtSipFrom);

            y+=34;
            cfg.Controls.Add(ML("To (SIP URI):", 12, y));
            txtSipTo = MT(140, y, 200); txtSipTo.Text = "sip:2000@192.168.1.1";
            cfg.Controls.Add(txtSipTo);

            y+=34;
            cfg.Controls.Add(ML("Proxy/Outbound:", 12, y));
            txtSipProxy = MT(140, y, 200); txtSipProxy.Text = "";
            cfg.Controls.Add(txtSipProxy);
            var lblProxyHint = new Label { Text=L.T("sim.proxy_hint"),
                Location=new Point(348,y+2), Width=200, ForeColor=Color.Gray };
            cfg.Controls.Add(lblProxyHint);

            y+=34;
            cfg.Controls.Add(ML(L.T("sim.method"), 12, y));
            cmbSipMethod = new ComboBox { Location=new Point(140,y-2), Width=130,
                DropDownStyle=ComboBoxStyle.DropDownList,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
            cmbSipMethod.Items.AddRange(new object[]{
                "INVITE","OPTIONS","REGISTER","SUBSCRIBE","NOTIFY","BYE","CANCEL","INFO","MESSAGE"});
            cmbSipMethod.SelectedIndex = 0;
            cfg.Controls.Add(cmbSipMethod);

            y+=38;
            btnSipSend = new Button { Text=L.T("sim.send"),
                Location=new Point(12,y), Width=200, Height=28,
                FlatStyle=FlatStyle.Flat, ForeColor=Color.White,
                BackColor=Color.FromArgb(30,100,30) };
            btnSipSend.FlatAppearance.BorderSize=0;
            btnSipSend.Click += BtnSipSend_Click;
            cfg.Controls.Add(btnSipSend);

            var btnClear = new Button { Text=L.T("sim.clear"), Location=new Point(220,y),
                Width=80, Height=28, FlatStyle=FlatStyle.Flat, ForeColor=Color.White,
                BackColor=Color.FromArgb(70,40,40) };
            btnClear.FlatAppearance.BorderSize=0;
            btnClear.Click += (s,e) => txtSipOutput.Clear();
            cfg.Controls.Add(btnClear);

            page.Controls.Add(cfg);

            var lblOut = new Label { Text="  📋 SIP Trace:",
                Dock=DockStyle.Top, Height=22, ForeColor=Color.LightGray,
                BackColor=Color.FromArgb(30,30,45), TextAlign=ContentAlignment.MiddleLeft,
                Padding=new Padding(4,0,0,0) };
            page.Controls.Add(lblOut);

            txtSipOutput = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true,
                BackColor=Color.FromArgb(8,12,22), ForeColor=Color.LimeGreen,
                Font=new Font("Consolas",8.5f), ScrollBars=ScrollBars.Both, WordWrap=false,
                BorderStyle=BorderStyle.None };
            page.Controls.Add(txtSipOutput);

            page.Controls.SetChildIndex(cfg,    0);
            page.Controls.SetChildIndex(lblOut, 1);
            page.Controls.SetChildIndex(txtSipOutput, 2);
            return page;
        }

        void BtnSipSend_Click(object sender, EventArgs e)
        {
            string dst     = txtSipDst.Text.Trim();
            string from    = txtSipFrom.Text.Trim();
            string to      = txtSipTo.Text.Trim();
            string method  = cmbSipMethod.SelectedItem as string ?? "OPTIONS";
            int    localPt;
            if (!int.TryParse(txtSipPort.Text.Trim(), out localPt) || localPt < 1 || localPt > 65535) localPt = 5060;

            if (string.IsNullOrEmpty(dst)) {
                MessageBox.Show("Inserire indirizzo destinazione.", "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string dstHost = dst;
            int    dstPort = 5060;
            if (dst.Contains(":")) {
                var parts = dst.Split(':');
                dstHost = parts[0];
                int.TryParse(parts[1], out dstPort);
            }

            // Costruisce messaggio SIP
            string callId  = Guid.NewGuid().ToString("N").Substring(0,12) + "@" + GetLocalIp();
            string branch  = "z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0,8);
            string cseq    = "1";
            string localIp = GetLocalIp();

            var sb = new StringBuilder();
            sb.AppendLine(method + " " + to + " SIP/2.0");
            sb.AppendLine("Via: SIP/2.0/UDP " + localIp + ":" + localPt + ";branch=" + branch);
            sb.AppendLine("Max-Forwards: 70");
            sb.AppendLine("From: <" + from + ">;tag=" + Guid.NewGuid().ToString("N").Substring(0,8));
            sb.AppendLine("To: <" + to + ">");
            sb.AppendLine("Call-ID: " + callId);
            sb.AppendLine("CSeq: " + cseq + " " + method);
            sb.AppendLine("Contact: <sip:" + localIp + ":" + localPt + ">");
            sb.AppendLine("User-Agent: LosaTermVoip/1.0");
            sb.AppendLine("Allow: INVITE,ACK,CANCEL,BYE,OPTIONS,NOTIFY,REGISTER,SUBSCRIBE,INFO");
            sb.AppendLine("Content-Length: 0");
            sb.AppendLine();

            string sipMsg = sb.ToString();

            // Log
            string ts = DateTime.Now.ToString("HH:mm:ss.fff");
            txtSipOutput.AppendText("─── [" + ts + "] SEND → " + dstHost + ":" + dstPort + " ───────────\r\n");
            txtSipOutput.AppendText(sipMsg + "\r\n");

            // Invia via UDP
            ThreadPool.QueueUserWorkItem(_ => {
                try {
                    using (var udp = new UdpClient(localPt))
                    {
                        byte[] data = Encoding.UTF8.GetBytes(sipMsg);
                        udp.Send(data, data.Length, dstHost, dstPort);
                        // Aspetta risposta max 5s
                        udp.Client.ReceiveTimeout = 5000;
                        try {
                            var ep = new IPEndPoint(IPAddress.Any, 0);
                            byte[] resp = udp.Receive(ref ep);
                            string respStr = Encoding.UTF8.GetString(resp);
                            string rts = DateTime.Now.ToString("HH:mm:ss.fff");
                            AppendOutput("─── [" + rts + "] RECV ← " + ep + " ────────────────\r\n" + respStr + "\r\n");
                        } catch {
                            AppendOutput("[" + DateTime.Now.ToString("HH:mm:ss") + "] ⚠ Nessuna risposta entro 5s\r\n");
                        }
                    }
                } catch (Exception ex) {
                    AppendOutput("[ERR] " + ex.Message + "\r\n");
                }
            });
        }

        void AppendOutput(string s)
        {
            if (txtSipOutput.InvokeRequired)
                txtSipOutput.BeginInvoke((Action<string>)AppendOutput, s);
            else
                txtSipOutput.AppendText(s);
        }

        // ── Tab 2: Cause Code Translator ─────────────────────────────────────
        TabPage BuildDebugTab()
        {
            var page = new TabPage("🐞  Debug Cmds") {
                BackColor=Color.FromArgb(22,22,32), ForeColor=Color.White, Padding=new Padding(8) };

            var top = new Panel { Dock=DockStyle.Top, Height=44,
                BackColor=Color.FromArgb(28,35,55), Padding=new Padding(10) };
            top.Controls.Add(new Label { Text="Vendor:", Location=new Point(10,13), AutoSize=true, ForeColor=Color.LightGray });
            var cmb = new ComboBox { Location=new Point(70,10), Width=260, DropDownStyle=ComboBoxStyle.DropDownList,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White };
            foreach (var row in VoipCodes.DebugCmds) cmb.Items.Add(row[0]);
            top.Controls.Add(cmb);

            var btnCopy = new Button { Text="📋 Copia", Location=new Point(340,9), Width=90, Height=26,
                FlatStyle=FlatStyle.Flat, ForeColor=Color.White, BackColor=Color.FromArgb(50,70,110) };
            btnCopy.FlatAppearance.BorderSize=0;
            top.Controls.Add(btnCopy);

            var info = new Label { Dock=DockStyle.Bottom, Height=22, ForeColor=Color.Gray, TextAlign=ContentAlignment.MiddleLeft,
                Text="  Comandi di debug/logging per vendor. ⚠ Alza i livelli solo per la diagnosi e riportali a default a fine analisi." };

            var txt = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true,
                ScrollBars=ScrollBars.Both, WordWrap=false, BackColor=Color.FromArgb(8,12,22),
                ForeColor=Color.LimeGreen, Font=new Font("Consolas",9), BorderStyle=BorderStyle.None };

            cmb.SelectedIndexChanged += (s,e) => { int i=cmb.SelectedIndex; if (i>=0) txt.Text = VoipCodes.DebugCmds[i][1]; };
            btnCopy.Click += (s,e) => { try { if (txt.Text.Length>0) Clipboard.SetText(txt.Text); } catch { } };

            page.Controls.Add(txt);
            page.Controls.Add(info);
            page.Controls.Add(top);
            if (cmb.Items.Count>0) cmb.SelectedIndex=0;
            return page;
        }

        TabPage BuildCauseTab()
        {
            var page = new TabPage("🔢  Cause Code Translator") {
                BackColor=Color.FromArgb(22,22,32), ForeColor=Color.White, Padding=new Padding(8) };

            var pnl = new Panel { Dock=DockStyle.Top, Height=260,
                BackColor=Color.FromArgb(28,35,55), Padding=new Padding(14) };

            pnl.Controls.Add(new Label { Text="Traduci codice causa tra protocolli VoIP:",
                Location=new Point(14,12), Width=400, ForeColor=Color.LightCyan,
                Font=new Font("Segoe UI",9,FontStyle.Bold) });

            int y=42;
            pnl.Controls.Add(ML("Da protocollo:", 14, y));
            cmbCauseFrom = new ComboBox { Location=new Point(130,y-2), Width=130,
                DropDownStyle=ComboBoxStyle.DropDownList,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White };
            cmbCauseFrom.Items.AddRange(new object[]{ "Q.850","SIP","H.323","ISUP","ANSI" });
            cmbCauseFrom.SelectedIndex=0;
            pnl.Controls.Add(cmbCauseFrom);

            pnl.Controls.Add(ML("A protocollo:", 280, y));
            cmbCauseTo = new ComboBox { Location=new Point(380,y-2), Width=130,
                DropDownStyle=ComboBoxStyle.DropDownList,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White };
            cmbCauseTo.Items.AddRange(new object[]{ "SIP","Q.850","H.323","ISUP","ANSI" });
            cmbCauseTo.SelectedIndex=0;
            pnl.Controls.Add(cmbCauseTo);

            y+=38;
            pnl.Controls.Add(ML("Codice input:", 14, y));
            txtCauseIn = MT(130, y, 100);
            txtCauseIn.Text="16";
            pnl.Controls.Add(txtCauseIn);

            var btnTrans = new Button { Text="🔄 Traduci", Location=new Point(240,y-2),
                Width=100, Height=26, FlatStyle=FlatStyle.Flat, ForeColor=Color.White,
                BackColor=Color.FromArgb(30,80,150) };
            btnTrans.FlatAppearance.BorderSize=0;
            btnTrans.Click += BtnTranslate_Click;
            pnl.Controls.Add(btnTrans);

            y+=38;
            pnl.Controls.Add(ML("Risultato:", 14, y));
            txtCauseOut = new TextBox { Location=new Point(130,y-2), Width=460, Height=100,
                Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
                BackColor=Color.FromArgb(8,12,22), ForeColor=Color.LimeGreen,
                Font=new Font("Consolas",9), BorderStyle=BorderStyle.FixedSingle };
            pnl.Controls.Add(txtCauseOut);

            page.Controls.Add(pnl);

            // Tabella di riferimento Q.850
            var lblRef = new Label { Text="  📋 Tabella Q.850 / SIP rapida",
                Dock=DockStyle.Top, Height=22, ForeColor=Color.LightGray,
                BackColor=Color.FromArgb(30,30,45), Padding=new Padding(4,0,0,0),
                TextAlign=ContentAlignment.MiddleLeft };
            page.Controls.Add(lblRef);

            var lv = new ListView { Dock=DockStyle.Fill, View=View.Details,
                FullRowSelect=true, GridLines=true,
                BackColor=Color.FromArgb(18,18,30), ForeColor=Color.White };
            lv.Columns.Add("Q.850", 60); lv.Columns.Add("SIP", 80);
            lv.Columns.Add("Descrizione", 300); lv.Columns.Add("H.323 Reason", 140);
            foreach (var row in CauseTable)
                lv.Items.Add(new ListViewItem(row));
            page.Controls.Add(lv);

            page.Controls.SetChildIndex(pnl, 0);
            page.Controls.SetChildIndex(lblRef, 1);
            page.Controls.SetChildIndex(lv, 2);
            return page;
        }

        void BtnTranslate_Click(object sender, EventArgs e)
        {
            string code    = txtCauseIn.Text.Trim();
            string from    = cmbCauseFrom.SelectedItem as string ?? "Q.850";
            string to      = cmbCauseTo.SelectedItem   as string ?? "SIP";
            var result = new StringBuilder();

            // Normalizza a Q.850 prima, poi verso il target
            int q850 = -1;
            if (from == "Q.850") { int.TryParse(code, out q850); }
            else if (from == "SIP")
            {
                // Trova la riga SIP → Q.850
                foreach (var row in CauseTable)
                    if (row[1].Contains(code)) { int.TryParse(row[0], out q850); break; }
            }

            if (q850 < 0)
            {
                // Fallback: codice SIP non mappato in tabella Q.850 → mostra comunque il dettaglio
                int sipDirect; string[] sd;
                if (from == "SIP" && int.TryParse(code, out sipDirect) && VoipCodes.Sip.TryGetValue(sipDirect, out sd))
                {
                    result.AppendLine("── SIP " + sipDirect + " " + sd[0] + " ──");
                    result.AppendLine("Significato: " + sd[1]);
                    result.AppendLine("Da controllare: " + sd[2]);
                    txtCauseOut.Text = result.ToString();
                    return;
                }
                txtCauseOut.Text = "Codice non trovato."; return;
            }

            // Cerca nella tabella
            bool found = false;
            foreach (var row in CauseTable)
            {
                int rq; int.TryParse(row[0], out rq);
                if (rq == q850)
                {
                    result.AppendLine("Q.850 Cause: " + row[0]);
                    result.AppendLine("SIP:         " + row[1]);
                    result.AppendLine("Descrizione: " + row[2]);
                    result.AppendLine("H.323:       " + (row.Length > 3 ? row[3] : "-"));
                    if (to == "SIP") result.AppendLine("\n▶ RISPOSTA SIP: " + row[1]);
                    else if (to == "Q.850") result.AppendLine("\n▶ Q.850 CAUSE: " + row[0]);
                    else if (to == "H.323") result.AppendLine("\n▶ H.323: " + (row.Length > 3 ? row[3] : "releaseCompleteReason=undefinedReason"));

                    // Dettaglio: significato + cosa controllare (da VoipCodes)
                    int sipNum;
                    string[] sv;
                    if (int.TryParse(row[1].Split(' ')[0], out sipNum) && VoipCodes.Sip.TryGetValue(sipNum, out sv))
                    {
                        result.AppendLine();
                        result.AppendLine("── SIP " + sipNum + " " + sv[0] + " ──");
                        result.AppendLine("Significato: " + sv[1]);
                        result.AppendLine("Da controllare: " + sv[2]);
                    }
                    string[] qv;
                    if (VoipCodes.Q850.TryGetValue(q850, out qv))
                    {
                        result.AppendLine();
                        result.AppendLine("── Q.850 " + q850 + " ──");
                        result.AppendLine("Significato: " + qv[0]);
                        result.AppendLine("Da controllare: " + qv[1]);
                    }
                    found = true; break;
                }
            }
            if (!found) result.AppendLine("Codice Q.850 " + q850 + " non in tabella.");
            txtCauseOut.Text = result.ToString();
        }

        // Tabella Q.850 ↔ SIP ↔ H.323
        static readonly string[][] CauseTable = new string[][] {
            new[]{ "1",  "404 Not Found",               "Numero non assegnato/inesistente",               "undefinedReason" },
            new[]{ "2",  "404 Not Found",               "Numero non esiste (no route to network)",         "undefinedReason" },
            new[]{ "3",  "404 Not Found",               "Nessun percorso verso destinazione",              "undefinedReason" },
            new[]{ "16", "200 OK / BYE",                "Normal call clearing",                            "normalCallClearing" },
            new[]{ "17", "486 Busy Here",               "User busy",                                       "calledPartyBusy" },
            new[]{ "18", "480 Temporarily Unavail.",    "No user responding (ring timeout)",               "noAnswer" },
            new[]{ "19", "480 Temporarily Unavail.",    "No answer from user on time",                    "noAnswer" },
            new[]{ "20", "480 Temporarily Unavail.",    "Subscriber absent",                               "undefinedReason" },
            new[]{ "21", "403 Forbidden",               "Call rejected",                                   "undefinedReason" },
            new[]{ "22", "410 Gone",                    "Number changed",                                  "undefinedReason" },
            new[]{ "27", "502 Bad Gateway",             "Destination out of order",                        "unreachableDestination" },
            new[]{ "28", "484 Address Incomplete",      "Invalid number format",                           "undefinedReason" },
            new[]{ "29", "501 Not Implemented",         "Facility rejected",                               "undefinedReason" },
            new[]{ "31", "480 Temporarily Unavail.",    "Normal unspecified",                              "undefinedReason" },
            new[]{ "34", "503 Service Unavailable",     "No circuit available",                            "undefinedReason" },
            new[]{ "38", "503 Service Unavailable",     "Network out of order",                            "unreachableDestination" },
            new[]{ "41", "503 Service Unavailable",     "Temporary failure",                               "undefinedReason" },
            new[]{ "42", "503 Service Unavailable",     "Switching equipment congestion",                  "undefinedReason" },
            new[]{ "44", "503 Service Unavailable",     "Requested circuit unavailable",                   "undefinedReason" },
            new[]{ "47", "503 Service Unavailable",     "Resource unavailable unspecified",                "undefinedReason" },
            new[]{ "55", "403 Forbidden",               "Incoming calls barred within CUG",               "undefinedReason" },
            new[]{ "57", "403 Forbidden",               "Bearer capability not authorized",                "undefinedReason" },
            new[]{ "58", "503 Service Unavailable",     "Bearer capability not presently available",       "undefinedReason" },
            new[]{ "65", "488 Not Acceptable Here",     "Bearer capability not implemented",               "undefinedReason" },
            new[]{ "69", "501 Not Implemented",         "Requested facility not implemented",              "undefinedReason" },
            new[]{ "79", "501 Not Implemented",         "Service or option not implemented",               "undefinedReason" },
            new[]{ "87", "403 Forbidden",               "User not member of CUG",                         "undefinedReason" },
            new[]{ "88", "488 Not Acceptable Here",     "Incompatible destination (codec/bearer)",         "undefinedReason" },
            new[]{ "95", "400 Bad Request",             "Invalid message unspecified",                     "undefinedReason" },
            new[]{ "96", "400 Bad Request",             "Mandatory IE missing",                            "undefinedReason" },
            new[]{ "97", "501 Not Implemented",         "Message type nonexistent",                        "undefinedReason" },
            new[]{ "99", "400 Bad Request",             "IE nonexistent or not implemented",               "undefinedReason" },
            new[]{ "100","400 Bad Request",             "Invalid IE contents",                             "undefinedReason" },
            new[]{ "101","408 Request Timeout",         "Message not compatible with state",               "undefinedReason" },
            new[]{ "102","408 Request Timeout",         "Recovery on timer expiry",                        "undefinedReason" },
            new[]{ "111","500 Server Internal Error",   "Protocol error unspecified",                      "undefinedReason" },
            new[]{ "127","500 Server Internal Error",   "Interworking unspecified",                        "undefinedReason" },
        };

        // ── Tab 3: SDP / Codec Analyzer ──────────────────────────────────────
        TabPage BuildSdpTab()
        {
            var page = new TabPage("🎵  SDP / Codec Analyzer") {
                BackColor=Color.FromArgb(22,22,32), ForeColor=Color.White, Padding=new Padding(8) };

            var split = new SplitContainer { Dock=DockStyle.Fill };
            try { split.SplitterDistance = 400; } catch { }

            // Sinistra: input SDP
            var lblIn = new Label { Text="  Incolla SDP (da wireshark / PCAP):",
                Dock=DockStyle.Top, Height=22, ForeColor=Color.LightGray,
                BackColor=Color.FromArgb(30,40,60), Padding=new Padding(4,0,0,0),
                TextAlign=ContentAlignment.MiddleLeft };
            txtSdpIn = new TextBox { Dock=DockStyle.Fill, Multiline=true, ScrollBars=ScrollBars.Both,
                BackColor=Color.FromArgb(8,12,22), ForeColor=Color.White,
                Font=new Font("Consolas",8.5f), WordWrap=false, BorderStyle=BorderStyle.None };
            txtSdpIn.TextChanged += (s,e) => AnalyzeSdp();
            split.Panel1.Controls.Add(txtSdpIn);
            split.Panel1.Controls.Add(lblIn);

            // Destra: analisi
            var lblOut = new Label { Text="  📊 Analisi SDP:",
                Dock=DockStyle.Top, Height=22, ForeColor=Color.LightGray,
                BackColor=Color.FromArgb(30,40,60), Padding=new Padding(4,0,0,0),
                TextAlign=ContentAlignment.MiddleLeft };
            txtSdpOut = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true,
                ScrollBars=ScrollBars.Vertical, BackColor=Color.FromArgb(8,12,22),
                ForeColor=Color.LimeGreen, Font=new Font("Consolas",8.5f),
                WordWrap=false, BorderStyle=BorderStyle.None };
            split.Panel2.Controls.Add(txtSdpOut);
            split.Panel2.Controls.Add(lblOut);

            page.Controls.Add(split);
            return page;
        }

        void AnalyzeSdp()
        {
            string sdp = txtSdpIn.Text;
            if (string.IsNullOrWhiteSpace(sdp)) { txtSdpOut.Clear(); return; }
            var sb = new StringBuilder();

            // Connection
            var mc = Regex.Match(sdp, @"c=IN IP4 ([\d\.]+)");
            if (mc.Success) sb.AppendLine("🌐 Media IP:      " + mc.Groups[1].Value);

            // Media lines
            foreach (Match mm in Regex.Matches(sdp, @"m=(\S+)\s+(\d+)\s+(\S+)\s+(.+)"))
            {
                sb.AppendLine("📡 Media:         " + mm.Groups[1].Value.ToUpper()
                    + "  porta " + mm.Groups[2].Value
                    + "  transport " + mm.Groups[3].Value);
                string payloads = mm.Groups[4].Value;
                // Cerca rtpmap per ogni payload
                foreach (var pt in payloads.Split(' '))
                {
                    var mr = Regex.Match(sdp, @"a=rtpmap:" + Regex.Escape(pt) + @"\s+(\S+)");
                    string codec = mr.Success ? mr.Groups[1].Value : "PT " + pt;
                    sb.AppendLine("    🎵 Codec:     " + codec + "  (PT=" + pt + ")" + DescribeCodec(codec));
                }
            }

            // Attributi principali
            if (Regex.IsMatch(sdp, @"a=sendrecv", RegexOptions.IgnoreCase)) sb.AppendLine("↔  Direzione:     sendrecv");
            else if (Regex.IsMatch(sdp, @"a=sendonly", RegexOptions.IgnoreCase)) sb.AppendLine("→  Direzione:     sendonly");
            else if (Regex.IsMatch(sdp, @"a=recvonly", RegexOptions.IgnoreCase)) sb.AppendLine("←  Direzione:     recvonly");
            else if (Regex.IsMatch(sdp, @"a=inactive", RegexOptions.IgnoreCase)) sb.AppendLine("⏸  Direzione:     inactive (on hold)");

            // DTMF
            var mDtmf = Regex.Match(sdp, @"a=rtpmap:(\d+)\s+telephone-event/(\d+)");
            if (mDtmf.Success) sb.AppendLine("🔢 DTMF RFC2833:  PT=" + mDtmf.Groups[1].Value + "  rate=" + mDtmf.Groups[2].Value);

            // SRTP / crypto
            foreach (Match mc2 in Regex.Matches(sdp, @"a=crypto:(\d+)\s+(\S+)"))
                sb.AppendLine("🔒 SRTP crypto:   suite=" + mc2.Groups[2].Value);

            // ICE
            if (Regex.IsMatch(sdp, @"a=ice-")) sb.AppendLine("🧊 ICE:           presente (WebRTC/Lync)");

            // ptime
            var mp = Regex.Match(sdp, @"a=ptime:(\d+)");
            if (mp.Success) sb.AppendLine("⏱  ptime:         " + mp.Groups[1].Value + " ms");

            txtSdpOut.Text = sb.ToString();
        }

        string DescribeCodec(string codec)
        {
            string c = codec.ToUpper().Split('/')[0];
            switch (c)
            {
                case "PCMU":    return "  [G.711 μ-law 64kbps — USA/Japan]";
                case "PCMA":    return "  [G.711 A-law 64kbps — Europa]";
                case "G729":
                case "G7290":
                case "G729A":   return "  [G.729 8kbps — bassa banda, licenza]";
                case "G722":    return "  [G.722 HD 64kbps wideband]";
                case "G7221":   return "  [G.722.1 Siren14 wideband]";
                case "G728":    return "  [G.728 16kbps LD-CELP]";
                case "G723":    return "  [G.723.1 5.3/6.3kbps — bassa banda]";
                case "ILBC":    return "  [iLBC 13.3/15.2kbps — robustezza packet loss]";
                case "OPUS":    return "  [Opus 6-510kbps — WebRTC standard]";
                case "AMR":     return "  [AMR 4.75-12.2kbps — mobile]";
                case "AMR-WB":  return "  [AMR-WB (G.722.2) wideband mobile]";
                case "EVS":     return "  [EVS 3GPP Enhanced Voice Services]";
                case "SPEEX":   return "  [Speex open source variable bitrate]";
                case "CN":      return "  [Comfort Noise]";
                default:        return "";
            }
        }

        static string GetLocalIp()
        {
            try {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                { s.Connect("8.8.8.8", 65530); return ((IPEndPoint)s.LocalEndPoint).Address.ToString(); }
            } catch { return "127.0.0.1"; }
        }

        static Label ML(string t, int x, int y)
        {
            return new Label { Text=t, Location=new Point(x,y+2), Width=120, ForeColor=Color.LightGray };
        }
        static TextBox MT(int x, int y, int w)
        {
            return new TextBox { Location=new Point(x,y), Width=w,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White,
                BorderStyle=BorderStyle.FixedSingle };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SIPCAPTURE / HOMER Integration
    //  Invia pacchetti SIP a un server Homer via HEP3 (UDP/TCP)
    //  + visualizza sessioni da API REST Homer
    // ═══════════════════════════════════════════════════════════════════════════

    public class SipCapturePanel : Form
    {
        TextBox txtHomerIp, txtHomerUser, txtHomerPass, txtHepLog;
        NumericUpDown numHomerPort, numHepPort;
        CheckBox chkHepEnabled;
        Button btnHomerConnect, btnHomerSearch;
        WebBrowser wbHomer;
        static UdpClient hepUdp;
        static string hepHost;
        static int    hepPort;
        static bool   hepEnabled;

        public SipCapturePanel()
        {
            Text = "🎯 SIPCapture / Homer Integration";
            Size = new Size(1000, 650);
            BackColor = Color.FromArgb(24,24,32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI",9);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUI();
        }

        void BuildUI()
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildHepTab());
            tabs.TabPages.Add(BuildHomerWebTab());
            Controls.Add(tabs);
        }

        TabPage BuildHepTab()
        {
            var page = new TabPage("📡  HEP Sender (cattura SIP)") {
                BackColor=Color.FromArgb(22,22,32), ForeColor=Color.White };

            var cfg = new Panel { Dock=DockStyle.Top, Height=200,
                BackColor=Color.FromArgb(28,35,55), Padding=new Padding(14) };

            cfg.Controls.Add(new Label { Text="HEP (Homer Encapsulation Protocol) — invia copie SIP a Homer Server:",
                Location=new Point(14,10), Width=560, ForeColor=Color.LightCyan,
                Font=new Font("Segoe UI",9,FontStyle.Bold) });

            int y=40;
            cfg.Controls.Add(CL("Homer HEP IP:", 14, y));
            txtHomerIp = CT(130, y, 160); txtHomerIp.Text="192.168.1.100";
            cfg.Controls.Add(txtHomerIp);

            cfg.Controls.Add(CL("Porta HEP:", 310, y));
            numHepPort = new NumericUpDown { Location=new Point(400,y-2), Width=70,
                Minimum=1, Maximum=65535, Value=9060,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White };
            cfg.Controls.Add(numHepPort);

            y+=34;
            chkHepEnabled = new CheckBox { Text="Abilita invio HEP automatico (sessioni SSH live)",
                Location=new Point(14,y), Width=420, ForeColor=Color.LightGreen };
            chkHepEnabled.CheckedChanged += ChkHepEnabled_Changed;
            cfg.Controls.Add(chkHepEnabled);

            y+=34;
            var btnTest = new Button { Text="▶ Test HEP (invia OPTIONS fittizio)",
                Location=new Point(14,y), Width=240, Height=26,
                FlatStyle=FlatStyle.Flat, ForeColor=Color.White,
                BackColor=Color.FromArgb(30,80,30) };
            btnTest.FlatAppearance.BorderSize=0;
            btnTest.Click += BtnTestHep_Click;
            cfg.Controls.Add(btnTest);

            y+=34;
            cfg.Controls.Add(new Label { Text=
                "ℹ  Homer (SIPCapture) riceve i pacchetti e li visualizza nella sua web UI.\n" +
                "   Porta HEP default: 9060 (UDP). Versioni supportate: Homer 5, 7.",
                Location=new Point(14,y), Width=560, ForeColor=Color.Gray });

            page.Controls.Add(cfg);

            var lblLog = new Label { Text="  📋 HEP Log:",
                Dock=DockStyle.Top, Height=22, ForeColor=Color.LightGray,
                BackColor=Color.FromArgb(28,35,55), TextAlign=ContentAlignment.MiddleLeft,
                Padding=new Padding(4,0,0,0) };
            page.Controls.Add(lblLog);

            txtHepLog = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true,
                BackColor=Color.FromArgb(8,12,22), ForeColor=Color.Cyan,
                Font=new Font("Consolas",8), ScrollBars=ScrollBars.Both,
                WordWrap=false, BorderStyle=BorderStyle.None };
            page.Controls.Add(txtHepLog);

            page.Controls.SetChildIndex(cfg,0); page.Controls.SetChildIndex(lblLog,1); page.Controls.SetChildIndex(txtHepLog,2);
            return page;
        }

        void ChkHepEnabled_Changed(object sender, EventArgs e)
        {
            hepEnabled = chkHepEnabled.Checked;
            if (hepEnabled)
            {
                hepHost = txtHomerIp.Text.Trim();
                hepPort = (int)numHepPort.Value;
                AppendHepLog("✔ HEP abilitato → " + hepHost + ":" + hepPort);
            }
            else AppendHepLog("✗ HEP disabilitato.");
        }

        void BtnTestHep_Click(object sender, EventArgs e)
        {
            string ip   = txtHomerIp.Text.Trim();
            int    port = (int)numHepPort.Value;
            string fakeOptions = "OPTIONS sip:test@" + ip + " SIP/2.0\r\nVia: SIP/2.0/UDP 127.0.0.1:5060;branch=z9hG4bKtest\r\nFrom: <sip:losatermvoip@127.0.0.1>;tag=test\r\nTo: <sip:test@" + ip + ">\r\nCall-ID: test-hep-" + DateTime.Now.Ticks + "@127.0.0.1\r\nCSeq: 1 OPTIONS\r\nContent-Length: 0\r\n\r\n";
            ThreadPool.QueueUserWorkItem(_ => SendHep3(ip, port, Encoding.UTF8.GetBytes(fakeOptions), "127.0.0.1", ip, 5060, port));
        }

        // Invia un pacchetto HEP3 (formato Homer)
        public static void SendHep3(string hHost, int hPort, byte[] sipData,
                                     string srcIp, string dstIp, int srcPt, int dstPt)
        {
            if (!hepEnabled || string.IsNullOrEmpty(hHost)) return;
            try
            {
                // HEP3 header semplificato (chunk-based)
                var hep = new List<byte>();
                // Magic "HEP3"
                hep.AddRange(new byte[] { 0x48, 0x45, 0x50, 0x33 });
                // Total length placeholder (riempito dopo)
                hep.AddRange(new byte[] { 0,0 });

                Action<ushort, byte[]> addChunk = (type, data) => {
                    // Vendor ID (0x0000) + Type + Length (header 6 + data)
                    ushort len = (ushort)(6 + data.Length);
                    hep.AddRange(new byte[] { 0,0 });           // vendor
                    hep.Add((byte)(type >> 8)); hep.Add((byte)(type & 0xFF)); // type
                    hep.Add((byte)(len >> 8));  hep.Add((byte)(len & 0xFF));  // length
                    hep.AddRange(data);
                };

                addChunk(0x0001, new byte[] { 2 });             // IP family: IPv4
                addChunk(0x0002, new byte[] { 17 });            // Protocol: UDP=17
                addChunk(0x0003, ParseIpBytes(srcIp));          // Src IP
                addChunk(0x0004, ParseIpBytes(dstIp));          // Dst IP
                addChunk(0x0007, new byte[] { (byte)(srcPt>>8),(byte)(srcPt&0xFF) }); // Src port
                addChunk(0x0008, new byte[] { (byte)(dstPt>>8),(byte)(dstPt&0xFF) }); // Dst port
                long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                addChunk(0x0009, new byte[] { (byte)(ts>>24),(byte)(ts>>16),(byte)(ts>>8),(byte)(ts&0xFF) }); // Timestamp
                addChunk(0x000B, new byte[] { 1 });             // Protocol type: SIP=1
                addChunk(0x000F, sipData);                      // SIP payload

                // Fill total length
                byte[] raw = hep.ToArray();
                raw[4] = (byte)(raw.Length >> 8);
                raw[5] = (byte)(raw.Length & 0xFF);

                using (var udp = new UdpClient())
                    udp.Send(raw, raw.Length, hHost, hPort);
            }
            catch { }
        }

        static byte[] ParseIpBytes(string ip)
        {
            try { return IPAddress.Parse(ip).GetAddressBytes(); }
            catch { return new byte[]{ 127,0,0,1 }; }
        }

        void AppendHepLog(string s)
        {
            if (txtHepLog.InvokeRequired)
                txtHepLog.BeginInvoke((Action<string>)AppendHepLog, s);
            else
                txtHepLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s + "\r\n");
        }

        TabPage BuildHomerWebTab()
        {
            var page = new TabPage("🌐  Homer Web UI") {
                BackColor=Color.FromArgb(22,22,32), ForeColor=Color.White };

            var top = new Panel { Dock=DockStyle.Top, Height=36,
                BackColor=Color.FromArgb(28,35,55), Padding=new Padding(6,4,4,2) };

            top.Controls.Add(CL("Homer URL:", 6,8));
            var txtUrl = CT(88,4,220); txtUrl.Text="http://192.168.1.100:9080";
            top.Controls.Add(txtUrl);

            top.Controls.Add(CL("Utente:", 316,8));
            txtHomerUser = CT(374,4,90); txtHomerUser.Text="admin";
            top.Controls.Add(txtHomerUser);

            top.Controls.Add(CL("Pass:", 472,8));
            txtHomerPass = CT(510,4,80); txtHomerPass.PasswordChar='●';
            top.Controls.Add(txtHomerPass);

            var btnGo = new Button { Text="🌐 Apri", Location=new Point(596,4),
                Width=80, Height=26, FlatStyle=FlatStyle.Flat, ForeColor=Color.White,
                BackColor=Color.FromArgb(30,80,150) };
            btnGo.FlatAppearance.BorderSize=0;
            btnGo.Click += (s,e) => {
                wbHomer.Navigate(txtUrl.Text.Trim());
            };
            top.Controls.Add(btnGo);

            wbHomer = new WebBrowser { Dock=DockStyle.Fill, ScriptErrorsSuppressed=true };
            wbHomer.Navigate("about:blank");

            page.Controls.Add(wbHomer);
            page.Controls.Add(top);
            return page;
        }

        static Label CL(string t, int x, int y)
        {
            return new Label { Text=t, Location=new Point(x,y), Width=80, ForeColor=Color.LightGray };
        }
        static TextBox CT(int x, int y, int w)
        {
            return new TextBox { Location=new Point(x,y), Width=w,
                BackColor=Color.FromArgb(45,55,80), ForeColor=Color.White,
                BorderStyle=BorderStyle.FixedSingle };
        }
    }
}
