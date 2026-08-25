using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // 🏷️ Template vendor: profili pre-compilati (connessione + parametri SIP) che
    // creano un profilo di connessione e aprono Environment Check / Readiness preconfigurati.
    public class VendorTemplate
    {
        public string Name;       // etichetta
        public string EnvVendor;  // voce corrispondente in EnvCheckPanel.Vendors (per Env Check / Readiness)
        public string AdminProto; // protocollo admin consigliato
        public int    AdminPort;  // porta per il profilo di connessione (SSH)
        public string WebUi;      // web admin (nota)
        public string SipPorts;   // porte di segnalazione
        public string Media;      // range media
        public string Notes;      // note operative

        public VendorTemplate(string n, string env, string proto, int port, string web, string sip, string media, string notes)
        { Name = n; EnvVendor = env; AdminProto = proto; AdminPort = port; WebUi = web; SipPorts = sip; Media = media; Notes = notes; }
    }

    public static class VendorTemplates
    {
        public static readonly List<VendorTemplate> All = new List<VendorTemplate>
        {
            new VendorTemplate("Cisco CUBE / IOS Gateway", "Cisco CUBE / IOS Gateway",
                "SSH / console", 22, "-",
                "5060 UDP/TCP · 5061 TLS", "RTP 8000-48198 (default)",
                "SSH/console IOS. Verso Teams: session transport tcp tls, sip-profiles, options-keepalive."),

            new VendorTemplate("Cisco CUCM (CallManager)", "Cisco CUCM (CallManager)",
                "SSH (platform CLI) + HTTPS", 22, "HTTPS 8443 (CCMAdmin) · 443",
                "5060 · 5061 (SIP trunk)", "-",
                "CLI VOS via SSH (utils/file get activelog). Web: CCMAdmin/Serviceability. Trace SDL/SDI in TranslatorX."),

            new VendorTemplate("AudioCodes (Mediant SBC)", "AudioCodes (Mediant SBC)",
                "SSH + HTTPS", 22, "HTTPS 443",
                "5060 UDP/TCP · 5061 TLS", "Media Realm (port range)",
                "Verso Teams solo TLS 5061. NTP obbligatorio per i certificati. Syslog verso LosaTerm."),

            new VendorTemplate("Ribbon (SBC Edge / SWe Lite)", "Ribbon (SBC Edge / SWe Lite)",
                "SSH + HTTPS", 22, "HTTPS 443",
                "5060 · 5061 TLS", "RTP/SRTP",
                "SBC Edge tiene UN solo certificato alla volta. TLS 1.2 Only, Validate Client FQDN = Disabled."),

            new VendorTemplate("Asterisk / FreePBX", "Asterisk / FreePBX",
                "SSH + HTTPS", 22, "HTTPS 443 (FreePBX)",
                "5060 UDP/TCP · 5061 TLS", "RTP 10000-20000",
                "chan_pjsip: transport/endpoint/aor/auth. NAT: direct_media=no, external_media_address."),

            new VendorTemplate("FreeSWITCH", "FreeSWITCH",
                "SSH (fs_cli / ESL)", 22, "-",
                "5060 (internal) · 5080 (external)", "RTP 16384-32768",
                "sofia status; ESL 8021 su loopback. NAT: ext-sip-ip / ext-rtp-ip."),

            new VendorTemplate("Kamailio", "Generic SBC / IP-PBX",
                "SSH", 22, "-",
                "5060 UDP/TCP · 5061 TLS", "rtpengine / rtpproxy",
                "Config in kamailio.cfg; media relay via rtpengine. Nessun tool vendor-specifico: usa Generic."),

            new VendorTemplate("Microsoft Teams (Direct Routing)", "Microsoft Teams (Direct Routing)",
                "Cloud (nessun host locale)", 0, "admin.teams.microsoft.com",
                "TLS 5061 → sip/sip2/sip3.pstnhub.microsoft.com", "3478-3481 + 49152-53247",
                "Nessun host locale da connettere: verifica lato SBC (SBC Health) + PowerShell (Get-CsOnlinePSTNGateway)."),

            new VendorTemplate("Yeastar", "Yeastar",
                "SSH + HTTPS", 8022, "HTTPS 8088 / 443",
                "5060 UDP/TCP", "RTP 10000-12000",
                "Abilitare TCP richiede il REBOOT del PBX. Disabilita SIP ALG; gestisci il NAT per le extension remote."),

            new VendorTemplate("3CX", "3CX",
                "SSH + HTTPS", 22, "HTTPS 5001",
                "5060 UDP/TCP", "RTP 9000-10999",
                "IP pubblico STATICO (no STUN per i trunk). Disabilita SIP ALG (causa #1 di audio monodirezionale)."),

            new VendorTemplate("Alcatel-Lucent OXE / OXO", "Alcatel-Lucent OXE / OXO",
                "SSH (mtcl / swinst)", 22, "-",
                "5060", "-",
                "OXE: mtcl/swinst/mgr; Fast Start = true. Spesso un SBC davanti verso il SIP pubblico. OXO via OMC."),
        };
    }

    // Pannello libreria template vendor
    public class VendorTemplatesPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(22, 22, 32), CIn = Color.FromArgb(45, 55, 80);
        readonly MainForm owner;
        ListBox list;
        TextBox detail;

        public VendorTemplatesPanel(MainForm mainForm)
        {
            owner = mainForm;
            Text = "LosaTermVoip — " + L.B("Template vendor", "Vendor templates");
            Size = new Size(820, 520);
            MinimumSize = new Size(640, 400);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.FromArgb(28, 35, 55) };
            var btnConn = Mk(L.B("🖥️ Aggiungi a Connessioni", "🖥️ Add to Connections"), 8, 210, Color.FromArgb(40, 80, 140)); btnConn.Click += delegate { AddToConnections(); };
            var btnEnv = Mk(L.B("🧪 Environment Check", "🧪 Environment Check"), 226, 170, Color.FromArgb(30, 100, 40)); btnEnv.Click += delegate { OpenEnv(); };
            var btnRdy = Mk(L.B("🧾 Readiness", "🧾 Readiness"), 404, 130, Color.FromArgb(45, 95, 120)); btnRdy.Click += delegate { OpenRdy(); };
            top.Controls.AddRange(new Control[] { btnConn, btnEnv, btnRdy });

            list = new ListBox { Dock = DockStyle.Left, Width = 260, BackColor = Color.FromArgb(12, 16, 24), ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9) };
            foreach (var t in VendorTemplates.All) list.Items.Add(t.Name);
            list.SelectedIndexChanged += delegate { ShowDetail(); };

            detail = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(8, 12, 22), ForeColor = Color.Gainsboro, Font = new Font("Consolas", 9.5f), BorderStyle = BorderStyle.None };

            Controls.Add(detail);
            Controls.Add(list);
            Controls.Add(top);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }

        Button Mk(string text, int x, int w, Color c)
        {
            var b = new Button { Text = text, Location = new Point(x, 9), Width = w, Height = 28,
                FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = c, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        VendorTemplate Sel()
        {
            int i = list.SelectedIndex;
            return (i >= 0 && i < VendorTemplates.All.Count) ? VendorTemplates.All[i] : null;
        }

        void ShowDetail()
        {
            var t = Sel(); if (t == null) { detail.Text = ""; return; }
            var nl = "\r\n";
            detail.Text =
                "═══ " + t.Name + " ═══" + nl + nl +
                L.B("Admin        : ", "Admin        : ") + t.AdminProto + (t.AdminPort > 0 ? "  (SSH " + t.AdminPort + ")" : "") + nl +
                L.B("Web admin    : ", "Web admin    : ") + t.WebUi + nl +
                L.B("Segnalazione : ", "Signaling    : ") + t.SipPorts + nl +
                L.B("Media        : ", "Media        : ") + t.Media + nl + nl +
                L.B("Note", "Notes") + nl + t.Notes + nl + nl +
                L.B("→ 'Aggiungi a Connessioni' crea un profilo SSH pre-compilato (porta corretta): completa host e credenziali nella lista connessioni.",
                    "→ 'Add to Connections' creates a pre-filled SSH profile (correct port): fill in host and credentials in the connection list.") + nl +
                L.B("→ 'Environment Check' / 'Readiness' aprono i pannelli con questo vendor già selezionato.",
                    "→ 'Environment Check' / 'Readiness' open the panels with this vendor preselected.");
        }

        void AddToConnections()
        {
            var t = Sel(); if (t == null) return;
            if (t.AdminPort <= 0)
            {
                MessageBox.Show(this, L.B("Questo template è cloud (nessun host locale da connettere).", "This template is cloud-based (no local host to connect to)."), "LosaTermVoip");
                return;
            }
            if (owner == null)
            {
                MessageBox.Show(this, L.B("Impossibile accedere alla lista connessioni.", "Cannot access the connection list."), "LosaTermVoip");
                return;
            }
            var c = new Connection { Name = "[" + t.Name + "]", Protocol = "SSH", Port = t.AdminPort, Username = "" };
            owner.AddConnectionTemplate(c);
            MessageBox.Show(this, L.B("Profilo aggiunto alle Connessioni. Completa host e credenziali nella lista a sinistra della finestra principale.",
                                      "Profile added to Connections. Fill in host and credentials in the list on the main window."), "LosaTermVoip");
        }

        void OpenEnv()
        {
            var t = Sel(); if (t == null) return;
            try { var f = new EnvCheckPanel(t.EnvVendor); try { f.Icon = AppIcon.Shared; } catch { } f.Show(); f.BringToFront(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "LosaTermVoip"); }
        }

        void OpenRdy()
        {
            var t = Sel(); if (t == null) return;
            try { var f = new ReadinessReportPanel(t.EnvVendor, ""); try { f.Icon = AppIcon.Shared; } catch { } f.Show(); f.BringToFront(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "LosaTermVoip"); }
        }
    }
}
