using System;
using System.Drawing;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // 🧪 Environment Check — wizard di readiness guidato per vendor.
    // Tier-A automatico (DNS/TLS/OPTIONS, nativo, no credenziali) + Tier-B guidato
    // (checklist vendor + cheat-sheet Debug Cmds + paste-back). Vendor-neutral.
    public class EnvCheckPanel : Form
    {
        // Vendor supportati (chiave = keyword per trovare la cheat-sheet in VoipCodes.DebugCmds)
        static readonly string[] Vendors = {
            "Microsoft Teams (Direct Routing)",
            "AudioCodes (Mediant SBC)",
            "Ribbon (SBC Edge / SWe Lite)",
            "Cisco CUBE / IOS Gateway",
            "Cisco CUCM (CallManager)",
            "Asterisk / FreePBX",
            "FreeSWITCH",
            "Alcatel-Lucent OXE / OXO",
            "Yeastar",
            "3CX",
            "Generic SBC / IP-PBX"
        };

        ComboBox cmbVendor;
        TextBox  txtTarget;
        RichTextBox rtb;
        Button   btnGo;
        Label    lblTarget;

        public EnvCheckPanel()
        {
            Text = "LosaTermVoip — Environment Check";
            Size = new Size(880, 640);
            MinimumSize = new Size(680, 480);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(22, 22, 32); ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(28, 35, 55), Padding = new Padding(12) };

            top.Controls.Add(new Label { Text = L.B("Vendor / piattaforma:", "Vendor / platform:"), Location = new Point(14, 12), AutoSize = true, ForeColor = Color.LightGray });
            cmbVendor = new ComboBox { Location = new Point(160, 9), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 55, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbVendor.Items.AddRange(Vendors);
            cmbVendor.SelectedIndex = 0;
            cmbVendor.SelectedIndexChanged += (s, e) => UpdateTargetVisibility();
            top.Controls.Add(cmbVendor);

            lblTarget = new Label { Text = L.B("SBC / host:", "SBC / host:"), Location = new Point(460, 12), AutoSize = true, ForeColor = Color.LightGray };
            top.Controls.Add(lblTarget);
            txtTarget = new TextBox { Location = new Point(540, 9), Width = 200, BackColor = Color.FromArgb(45, 55, 80), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            top.Controls.Add(txtTarget);

            btnGo = new Button { Text = L.B("▶ Verifica ambiente", "▶ Check environment"), Location = new Point(14, 44), Width = 200, Height = 28,
                FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(30, 100, 40), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnGo.FlatAppearance.BorderSize = 0;
            btnGo.Click += (s, e) => Start();
            top.Controls.Add(btnGo);

            var btnR = ReportHelper.MakeButton(224, 45);
            btnR.Click += (s, e) => ReportHelper.ExportText(this, "Environment Check — " + cmbVendor.Text, rtb.Text);
            top.Controls.Add(btnR);

            top.Controls.Add(new Label {
                Text = L.B("Check automatici (DNS/TLS/OPTIONS) + checklist e comandi del vendor. Nessuna credenziale richiesta.",
                           "Automated checks (DNS/TLS/OPTIONS) + vendor checklist and commands. No credentials required."),
                Location = new Point(340, 50), AutoSize = true, ForeColor = Color.Gray });

            rtb = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(8, 12, 22), ForeColor = Color.Gainsboro, Font = new Font("Consolas", 9.5f),
                WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
            rtb.TextChanged += (s, e) => ReportHelper.Set("Environment Check", rtb.Text);

            Controls.Add(rtb);
            Controls.Add(top);
            UpdateTargetVisibility();
        }

        bool IsTeams { get { return cmbVendor.SelectedIndex == 0; } }

        void UpdateTargetVisibility()
        {
            bool needTarget = !IsTeams;   // Teams usa i PSTN hub Microsoft, non un host tuo
            lblTarget.Visible = needTarget; txtTarget.Visible = needTarget;
        }

        void Start()
        {
            btnGo.Enabled = false; rtb.Clear();
            string vendor = cmbVendor.Text;
            string host   = (txtTarget.Text ?? "").Trim();
            var th = new Thread(delegate () { Run(vendor, host); }) { IsBackground = true };
            th.Start();
        }

        void Run(string vendor, string host)
        {
            try
            {
                Log("═══ Environment Check · " + vendor + " ═══\r\n\r\n", Color.White);

                if (vendor.StartsWith("Microsoft Teams"))
                {
                    RunTeams();
                }
                else
                {
                    if (host.Length == 0)
                    {
                        Log(L.B("⚠ Inserisci l'host/IP dell'SBC o PBX da verificare.\r\n\r\n", "⚠ Enter the SBC or PBX host/IP to check.\r\n\r\n"), Color.Khaki);
                    }
                    else
                    {
                        Log(L.B("Target: ", "Target: ") + host + "\r\n\r\n", Color.LightCyan);
                        CheckDns(host);
                        CheckTls(host, 5061);
                        CheckOptions(host, 5060);
                        Log("\r\n", Color.Gray);
                    }
                }

                // Checklist vendor
                Log(L.B("── Checklist ", "── Checklist ") + vendor + " ──\r\n", Color.LightCyan);
                Log(Checklist(vendor) + "\r\n", Color.Gray);

                // Cheat-sheet Debug Cmds del vendor
                string cheat = FindCheatSheet(vendor);
                if (cheat != null)
                {
                    Log(L.B("── Comandi debug/trace del vendor ──\r\n", "── Vendor debug/trace commands ──\r\n"), Color.LightCyan);
                    Log(cheat + "\r\n\r\n", Color.Gray);
                }

                Log(L.B("💡 Tier-B (stato tenant/config): esegui i comandi qui sopra e incolla l'output nei tool dedicati per la diagnosi.\r\n",
                        "💡 Tier-B (tenant/config state): run the commands above and paste the output into the dedicated tools for diagnosis.\r\n"), Color.DimGray);
            }
            catch (Exception ex) { Log("[ERR] " + ex.Message + "\r\n", Color.OrangeRed); }
            finally { if (btnGo.IsHandleCreated) btnGo.BeginInvoke((MethodInvoker)delegate { btnGo.Enabled = true; }); }
        }

        // ── Teams Direct Routing (PSTN hub Microsoft) ──
        static readonly string[] TeamsProxies = { "sip.pstnhub.microsoft.com", "sip2.pstnhub.microsoft.com", "sip3.pstnhub.microsoft.com" };
        void RunTeams()
        {
            int ok = 0;
            foreach (var fqdn in TeamsProxies)
            {
                Log("● " + fqdn + "\r\n", Color.LightCyan);
                if (CheckDns(fqdn) && CheckTls(fqdn, 5061)) ok++;
                Log("\r\n", Color.Gray);
            }
            string v = ok == TeamsProxies.Length
                ? L.B("✅ RAGGIUNGIBILE — tutti i 3 PSTN hub rispondono in TLS 5061.\r\n\r\n", "✅ REACHABLE — all 3 PSTN hubs answer over TLS 5061.\r\n\r\n")
                : ok > 0 ? L.B("⚠️ PARZIALE — " + ok + "/3 hub raggiungibili.\r\n\r\n", "⚠️ PARTIAL — " + ok + "/3 hubs reachable.\r\n\r\n")
                         : L.B("⛔ BLOCCATO — nessun hub raggiungibile (firewall/DNS).\r\n\r\n", "⛔ BLOCKED — no hub reachable (firewall/DNS).\r\n\r\n");
            Log(v, ok == TeamsProxies.Length ? Color.LightGreen : (ok > 0 ? Color.Khaki : Color.OrangeRed));
        }

        // ── Check automatici (Tier-A) ──
        bool CheckDns(string host)
        {
            try
            {
                var addrs = Dns.GetHostAddresses(host);
                var sb = new StringBuilder();
                foreach (var a in addrs)
                    if (a.AddressFamily == AddressFamily.InterNetwork || a.AddressFamily == AddressFamily.InterNetworkV6)
                    { if (sb.Length > 0) sb.Append(", "); sb.Append(a.ToString()); }
                if (sb.Length > 0) { Log("   ✔ DNS A: " + sb + "\r\n", Color.LightGreen); return true; }
                Log(L.B("   ✗ DNS: nessun A-record\r\n", "   ✗ DNS: no A record\r\n"), Color.OrangeRed); return false;
            }
            catch (Exception ex) { Log("   ✗ DNS: " + ex.Message + "\r\n", Color.OrangeRed); return false; }
        }

        bool CheckTls(string host, int port)
        {
            try
            {
                using (var tcp = new TcpClient())
                {
                    var ar = tcp.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(5000)) { Log(L.B("   ✗ TLS " + port + ": timeout (firewall in uscita?)\r\n", "   ✗ TLS " + port + ": timeout (outbound firewall?)\r\n"), Color.OrangeRed); return false; }
                    tcp.EndConnect(ar);
                    X509Certificate2 cert = null;
                    RemoteCertificateValidationCallback cb = delegate (object sn, X509Certificate crt, X509Chain ch, SslPolicyErrors er) { if (crt != null) cert = new X509Certificate2(crt); return true; };
                    using (var ssl = new SslStream(tcp.GetStream(), false, cb))
                    {
                        ssl.AuthenticateAsClient(host, null, SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                        Log("   ✔ TLS " + port + ": " + ssl.SslProtocol + "\r\n", Color.LightGreen);
                        if (cert != null)
                        {
                            double days = (cert.NotAfter - DateTime.Now).TotalDays;
                            Log("     cert: " + cert.GetNameInfo(X509NameType.SimpleName, false) + "  (" + L.B("scade ", "expires ") + cert.NotAfter.ToString("yyyy-MM-dd") + ", " + (int)days + L.B(" giorni)\r\n", " days)\r\n"), days < 30 ? Color.Khaki : Color.Gray);
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex) { Log(L.B("   ✗ TLS " + port + " fallito: ", "   ✗ TLS " + port + " failed: ") + ex.Message + "\r\n", Color.OrangeRed); return false; }
        }

        void CheckOptions(string host, int port)
        {
            try
            {
                IPAddress ip = null;
                foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a; break; }
                if (ip == null) { Log(L.B("   ✗ OPTIONS: DNS fallito\r\n", "   ✗ OPTIONS: DNS failed\r\n"), Color.OrangeRed); return; }
                string local = "0.0.0.0";
                try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect(ip, port); local = ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } } catch { }
                using (var udp = new UdpClient(0))
                {
                    udp.Client.ReceiveTimeout = 2500;
                    int lport = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
                    string branch = "z9hG4bK" + Guid.NewGuid().ToString("N").Substring(0, 12);
                    string msg = "OPTIONS sip:" + host + " SIP/2.0\r\n" +
                                 "Via: SIP/2.0/UDP " + local + ":" + lport + ";branch=" + branch + "\r\n" +
                                 "Max-Forwards: 70\r\nFrom: <sip:losaterm@" + local + ">;tag=" + Guid.NewGuid().ToString("N").Substring(0, 8) + "\r\n" +
                                 "To: <sip:" + host + ">\r\nCall-ID: " + Guid.NewGuid().ToString("N").Substring(0, 16) + "@" + local + "\r\n" +
                                 "CSeq: 1 OPTIONS\r\nContact: <sip:losaterm@" + local + ":" + lport + ">\r\nUser-Agent: LosaTermVoip\r\nContent-Length: 0\r\n\r\n";
                    byte[] data = Encoding.ASCII.GetBytes(msg);
                    udp.Send(data, data.Length, new IPEndPoint(ip, port));
                    var rep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref rep);
                    string text = Encoding.ASCII.GetString(resp);
                    int nl = text.IndexOf("\r\n"); string first = nl > 0 ? text.Substring(0, nl) : text;
                    Log("   ✔ OPTIONS UDP " + port + ": " + first.Replace("SIP/2.0 ", "") + "\r\n", Color.LightGreen);
                }
            }
            catch (SocketException) { Log(L.B("   ✗ OPTIONS UDP " + port + ": nessuna risposta (trunk giù, IP non whitelistato, firewall)\r\n", "   ✗ OPTIONS UDP " + port + ": no response (trunk down, IP not whitelisted, firewall)\r\n"), Color.OrangeRed); }
            catch (Exception ex) { Log("   ✗ OPTIONS: " + ex.Message + "\r\n", Color.OrangeRed); }
        }

        // Cheat-sheet Debug Cmds del vendor (da VoipCodes.DebugCmds, match per keyword)
        static string FindCheatSheet(string vendor)
        {
            string key = vendor.Split(' ')[0].ToLowerInvariant();   // "audiocodes", "ribbon", "cisco", "asterisk", …
            foreach (var row in VoipCodes.DebugCmds)
                if (row.Length >= 2 && row[0].ToLowerInvariant().Contains(key)) return row[1];
            // fallback per Teams/Alcatel/3CX/FreeSWITCH ecc.
            foreach (var row in VoipCodes.DebugCmds)
                if (row.Length >= 2 && vendor.ToLowerInvariant().Contains(row[0].Split(' ')[0].ToLowerInvariant())) return row[1];
            return null;
        }

        static string Checklist(string vendor)
        {
            if (vendor.StartsWith("Microsoft Teams"))
                return L.B(
"  • Dominio: FQDN dell'SBC in un dominio M365 VERIFICATO (NON *.onmicrosoft.com) e registrato nel tenant\r\n" +
"  • Certificato SBC: CN o SAN = FQDN dell'SBC, CA nel Microsoft Trusted Root Program, EKU 'Server Authentication', TLS 1.2 (wildcard ok per RFC 2818; la SCADENZA è la causa #1 di down)\r\n" +
"  • Segnalazione: SBC → Microsoft TCP 5061 (TLS); Microsoft → SBC sulla porta configurata sull'SBC\r\n" +
"  • Media UDP/SRTP: 3478–3481 e 49152–53247 in ENTRAMBE le direzioni (≥2 porte media per chiamata)\r\n" +
"  • Range IP Microsoft (segnalazione+media): 52.112.0.0/14 e 52.120.0.0/14 — apri TUTTI i range, non solo gli IP restituiti dal DNS (GCC High media 52.127.88.0/21, DoD 52.127.64.0/21)\r\n" +
"  • DNS: A-record dei PSTN hub (Teams NON usa SRV) · Codec: SILK, G.711, G.722, G.729\r\n" +
"  • Tenant: SBC solo certificato Microsoft · Get-CsOnlinePSTNGateway (pairing/stato) · utente EnterpriseVoiceEnabled + OnlineVoiceRoutingPolicy + numero + licenza Teams + Teams Phone",
"  • Domain: SBC FQDN in a VERIFIED M365 domain (NOT *.onmicrosoft.com) and registered in the tenant\r\n" +
"  • SBC certificate: CN or SAN = SBC FQDN, CA in the Microsoft Trusted Root Program, 'Server Authentication' EKU, TLS 1.2 (wildcard ok per RFC 2818; EXPIRY is the #1 cause of down)\r\n" +
"  • Signaling: SBC → Microsoft TCP 5061 (TLS); Microsoft → SBC on the port configured on the SBC\r\n" +
"  • Media UDP/SRTP: 3478–3481 and 49152–53247 in BOTH directions (≥2 media ports per concurrent call)\r\n" +
"  • Microsoft IP ranges (signaling+media): 52.112.0.0/14 and 52.120.0.0/14 — open ALL ranges, not only the IPs returned by DNS (GCC High media 52.127.88.0/21, DoD 52.127.64.0/21)\r\n" +
"  • DNS: PSTN hub A-records (Teams does NOT use SRV) · Codecs: SILK, G.711, G.722, G.729\r\n" +
"  • Tenant: Microsoft-certified SBC only · Get-CsOnlinePSTNGateway (pairing/status) · user EnterpriseVoiceEnabled + OnlineVoiceRoutingPolicy + number + Teams + Teams Phone license");

            if (vendor.StartsWith("AudioCodes"))
                return L.B(
"  • Verso Teams il DR è SOLO TLS (il TCP non è supportato) → SIP Interface TLS su 5061\r\n" +
"  • TLS Context (Setup > IP Network > Security): certificato con Subject/SAN = FQDN SBC, root/intermediate della CA caricati, TLS 1.2\r\n" +
"  • Proxy Set verso sip.pstnhub.microsoft.com (+ sip2/sip3), transport TLS, con Proxy Keep-Alive (OPTIONS) abilitato\r\n" +
"  • IP Group (Server) = Teams + IP Group per il carrier; Classification / IP-to-IP Routing coerenti\r\n" +
"  • Media Realm + port range media; Coders allineati a Teams (SILK/G.711/G.722/G.729); NAT se dietro NAT\r\n" +
"  • NTP configurato (essenziale per validare i certificati) · Syslog verso l'IP di questo PC (Syslog server LosaTerm), Debug Level 5, Debug Recording per segnalazione+media",
"  • To Teams, DR is TLS-ONLY (TCP not supported) → SIP Interface on TLS 5061\r\n" +
"  • TLS Context (Setup > IP Network > Security): certificate Subject/SAN = SBC FQDN, CA root/intermediate loaded, TLS 1.2\r\n" +
"  • Proxy Set to sip.pstnhub.microsoft.com (+ sip2/sip3), TLS transport, with Proxy Keep-Alive (OPTIONS) enabled\r\n" +
"  • IP Group (Server) = Teams + IP Group for the carrier; consistent Classification / IP-to-IP Routing\r\n" +
"  • Media Realm + media port range; Coders aligned with Teams (SILK/G.711/G.722/G.729); NAT if behind NAT\r\n" +
"  • NTP configured (essential to validate certificates) · Syslog to this PC's IP (LosaTerm Syslog server), Debug Level 5, Debug Recording for signaling+media");

            if (vendor.StartsWith("Ribbon"))
                return L.B(
"  • Verso Teams solo TLS. TLS Profile: 'TLS 1.2 Only', certificato pubblico valido, 'Validate Client FQDN' = Disabled\r\n" +
"  • Certificato: l'SBC Edge tiene UN solo certificato per volta → importa Public CA Root/Intermediate + Microsoft CA cert + certificato SBC (CN/SAN = FQDN SBC)\r\n" +
"  • Signaling Group verso Teams (i 3 pstnhub nel Server/SIP Server table) + SIP Profile (session timer, header, option tags)\r\n" +
"  • Transformation / Routing / Call Routing tables coerenti verso il trunk\r\n" +
"  • Media: SRTP; RTP/SRTP e SIP/TLS sull'interfaccia Ethernet; port range; NAT\r\n" +
"  • Logging: Diagnostics > Log Viewer; Packet Capture → analizza il pcap qui in LosaTerm",
"  • To Teams TLS only. TLS Profile: 'TLS 1.2 Only', valid public certificate, 'Validate Client FQDN' = Disabled\r\n" +
"  • Certificate: SBC Edge holds ONE certificate at a time → import Public CA Root/Intermediate + Microsoft CA cert + SBC certificate (CN/SAN = SBC FQDN)\r\n" +
"  • Signaling Group to Teams (the 3 pstnhub in the SIP Server table) + SIP Profile (session timers, headers, option tags)\r\n" +
"  • Consistent Transformation / Routing / Call Routing tables to the trunk\r\n" +
"  • Media: SRTP; RTP/SRTP and SIP/TLS on the Ethernet interface; port range; NAT\r\n" +
"  • Logging: Diagnostics > Log Viewer; Packet Capture → analyze the pcap here in LosaTerm");

            if (vendor.StartsWith("Cisco CUBE"))
                return L.B(
"  • CUBE con FQDN pubblico + IP pubblico/privato; certificato firmato da CA pubblica (crypto signaling default trustpoint)\r\n" +
"  • Verso Teams: crypto pki trustpool (cabundle) + attenzione a revocation-check (gotcha frequente); session transport tcp tls; server-group con i 3 pstnhub\r\n" +
"  • voice-class sip-profiles per la manipolazione header (necessari per Teams DR) · voice-class codec (SILK/G.711/G.722/G.729) · SRTP se richiesto\r\n" +
"  • voice-class sip options-keepalive sul dial-peer (monitor trunk) · source-interface control+media coerenti\r\n" +
"  • Diagnosi: debug ccsip messages + debug voip ccapi inout (flusso + cause code) · show sip-ua calls / show call active voice brief",
"  • CUBE with public FQDN + public/private IP; certificate signed by a public CA (crypto signaling default trustpoint)\r\n" +
"  • To Teams: crypto pki trustpool (cabundle) + watch revocation-check (common gotcha); session transport tcp tls; server-group with the 3 pstnhub\r\n" +
"  • voice-class sip-profiles for header manipulation (required for Teams DR) · voice-class codec (SILK/G.711/G.722/G.729) · SRTP if required\r\n" +
"  • voice-class sip options-keepalive on the dial-peer (trunk monitor) · consistent source-interface for control+media\r\n" +
"  • Diagnosis: debug ccsip messages + debug voip ccapi inout (flow + cause codes) · show sip-ua calls / show call active voice brief");

            if (vendor.StartsWith("Cisco CUCM"))
                return L.B(
"  • SIP Trunk verso CUBE/SBC: SIP Trunk Security Profile (transport TCP/TLS + porta), 'SIP OPTIONS Ping' abilitato per lo stato trunk, SIP Profile corretto\r\n" +
"  • Route Pattern → Route List → Route Group verso il trunk; Partition/CSS; Called/Calling Party Transformation\r\n" +
"  • Region/Location (codec e banda) e Device Pool coerenti; MRGL (MTP/Transcoder) se serve transcoding\r\n" +
"  • Trace SDL/SDI = Detailed → raccogli con RTMT → apri in TranslatorX · utils dbreplication runtimestate · stato servizi CallManager/CTIManager",
"  • SIP Trunk to CUBE/SBC: SIP Trunk Security Profile (TCP/TLS transport + port), 'SIP OPTIONS Ping' enabled for trunk status, correct SIP Profile\r\n" +
"  • Route Pattern → Route List → Route Group to the trunk; Partition/CSS; Called/Calling Party Transformation\r\n" +
"  • Consistent Region/Location (codec and bandwidth) and Device Pool; MRGL (MTP/Transcoder) if transcoding needed\r\n" +
"  • SDL/SDI trace = Detailed → collect with RTMT → open in TranslatorX · utils dbreplication runtimestate · CallManager/CTIManager service status");

            if (vendor.StartsWith("Asterisk"))
                return L.B(
"  • pjsip: oggetti transport (udp/tcp/tls) + endpoint + aor + auth + identify/match coerenti; trunk registrato o qualify=yes 'Reachable' (OPTIONS)\r\n" +
"  • Codec (disallow=all → allow=ulaw,alaw,g722…) e DTMF (rfc4733/dtmf_mode) allineati col peer\r\n" +
"  • NAT: external_media_address / external_signaling_address + local_net; direct_media=no per evitare audio monodirezionale; RTP port range in rtp.conf aperto sul firewall\r\n" +
"  • Diagnosi: pjsip set logger on (segnalazione), rtp set debug on; /var/log/asterisk/full · FreePBX: alza il log level",
"  • pjsip: transport (udp/tcp/tls) + endpoint + aor + auth + identify/match objects consistent; trunk registered or qualify=yes 'Reachable' (OPTIONS)\r\n" +
"  • Codecs (disallow=all → allow=ulaw,alaw,g722…) and DTMF (rfc4733/dtmf_mode) aligned with the peer\r\n" +
"  • NAT: external_media_address / external_signaling_address + local_net; direct_media=no to avoid one-way audio; RTP port range in rtp.conf open on the firewall\r\n" +
"  • Diagnosis: pjsip set logger on (signaling), rtp set debug on; /var/log/asterisk/full · FreePBX: raise the log level");

            if (vendor.StartsWith("FreeSWITCH"))
                return L.B(
"  • sofia status profile <nome>: profilo RUNNING e gateway 'UP/REGED'; codec-prefs coerenti; TLS se richiesto (tls-only, cert)\r\n" +
"  • NAT: ext-sip-ip / ext-rtp-ip (auto-nat o IP pubblico); apply-inbound-acl per autorizzare il peer; RTP port range aperto\r\n" +
"  • Dialplan/Gateway: register true/false, caller-id, from-domain corretti\r\n" +
"  • Diagnosi: sofia global siptrace on / sofia profile <nome> siptrace on; sofia loglevel all 9; /usr/local/freeswitch/log/freeswitch.log",
"  • sofia status profile <name>: profile RUNNING and gateway 'UP/REGED'; consistent codec-prefs; TLS if required (tls-only, cert)\r\n" +
"  • NAT: ext-sip-ip / ext-rtp-ip (auto-nat or public IP); apply-inbound-acl to authorize the peer; RTP port range open\r\n" +
"  • Dialplan/Gateway: correct register true/false, caller-id, from-domain\r\n" +
"  • Diagnosis: sofia global siptrace on / sofia profile <name> siptrace on; sofia loglevel all 9; /usr/local/freeswitch/log/freeswitch.log");

            if (vendor.StartsWith("Alcatel"))
                return L.B(
"  • OXE: SIP Trunk Group + un External Gateway per ogni IP/FQDN atteso in P-Asserted-Identity/From; SIP Remote Domain = IP/FQDN dell'SBC/peer\r\n" +
"  • IP > IP Parameters: Fast Start = true (requisito); DTMF RFC2833; codec coerenti; Diversion (System/Other/External Signalling) secondo scenario\r\n" +
"  • Licenze sufficienti (SIP Gateway, SIP/IP users); spesso un SBC (Ingate/Neo-Gate…) tra OXE e il SIP pubblico\r\n" +
"  • Diagnosi: mtracer / incviewer; tcpdump -i eth0 -w /tmp/cap.pcap → analizza il pcap qui · OXO: gestione via OMC",
"  • OXE: SIP Trunk Group + one External Gateway per each IP/FQDN expected in P-Asserted-Identity/From; SIP Remote Domain = SBC/peer IP/FQDN\r\n" +
"  • IP > IP Parameters: Fast Start = true (requirement); DTMF RFC2833; consistent codecs; Diversion (System/Other/External Signalling) per scenario\r\n" +
"  • Enough licenses (SIP Gateway, SIP/IP users); often an SBC (Ingate/Neo-Gate…) between OXE and the public SIP\r\n" +
"  • Diagnosis: mtracer / incviewer; tcpdump -i eth0 -w /tmp/cap.pcap → analyze the pcap here · OXO: managed via OMC");

            if (vendor.StartsWith("Yeastar"))
                return L.B(
"  • Tipo trunk: Register (credenziali) / Peer (IP-based) / Account; scegli quello del provider; stato 'Registered/OK'\r\n" +
"  • Transport: abilita TCP nei SIP Settings globali PRIMA di usarlo (richiede RIAVVIO del PBX); supporta DNS-NAPTR per discovery/failover\r\n" +
"  • Codec: allinea la lista preferita del trunk a quella dell'ITSP; DTMF (RFC2833)\r\n" +
"  • NAT/sicurezza: per le extension remote gestisci il NAT (evita one-way); NON esporre il PBX su internet, abilita la protezione brute-force; per il trunk in uscita di solito nessun port forwarding\r\n" +
"  • Inbound/Outbound Routes coerenti · Diagnosi: abilita il SIP trace nel PBX; cattura pcap → analizza qui",
"  • Trunk type: Register (credentials) / Peer (IP-based) / Account; pick the provider's; status 'Registered/OK'\r\n" +
"  • Transport: enable TCP in global SIP Settings BEFORE using it (requires a PBX REBOOT); supports DNS-NAPTR for discovery/failover\r\n" +
"  • Codecs: align the trunk's preferred list with the ITSP's; DTMF (RFC2833)\r\n" +
"  • NAT/security: for remote extensions handle NAT (avoid one-way); do NOT expose the PBX to the internet, enable brute-force protection; outbound trunk usually needs no port forwarding\r\n" +
"  • Consistent Inbound/Outbound Routes · Diagnosis: enable the SIP trace in the PBX; capture a pcap → analyze here");

            if (vendor.StartsWith("3CX"))
                return L.B(
"  • Provider supportato (template pre-configurato) o Custom trunk; SIP Trunk 'Registered'/OPTIONS OK; codec e DTMF\r\n" +
"  • Firewall: SIP 5060/UDP e 5060-5061/TCP in ingresso, RTP 9000-10999/UDP (2 porte per chiamata); esegui prima il Firewall Checker di 3CX\r\n" +
"  • IP pubblico STATICO (per i trunk 3CX NON consiglia STUN); DISABILITA SIP ALG sul router (causa #1 di audio monodirezionale/registrazioni cadute)\r\n" +
"  • Diagnosi: Activity Log / Event Log, logging SIP verbose; cattura pcap → analizza qui",
"  • Supported provider (pre-configured template) or Custom trunk; SIP Trunk 'Registered'/OPTIONS OK; codecs and DTMF\r\n" +
"  • Firewall: SIP 5060/UDP and 5060-5061/TCP inbound, RTP 9000-10999/UDP (2 ports per call); run the 3CX Firewall Checker first\r\n" +
"  • STATIC public IP (3CX does NOT recommend STUN for trunks); DISABLE SIP ALG on the router (#1 cause of one-way audio/dropped registrations)\r\n" +
"  • Diagnosis: Activity Log / Event Log, verbose SIP logging; capture a pcap → analyze here");

            // Generic
            return L.B(
"  • DNS: l'FQDN dell'SBC/PBX risolve? (usa A-record; SRV solo se previsto)\r\n" +
"  • Segnalazione: 5060 UDP/TCP o 5061 TLS raggiungibile (verificato sopra)\r\n" +
"  • Certificato TLS: CN/SAN corretto, catena completa, scadenza\r\n" +
"  • Media: port range RTP aperto in entrambe le direzioni; NAT gestito\r\n" +
"  • Orologio/NTP allineato (un clock sfasato rompe TLS/SRTP e i CDR)",
"  • DNS: does the SBC/PBX FQDN resolve? (use A-records; SRV only if expected)\r\n" +
"  • Signaling: 5060 UDP/TCP or 5061 TLS reachable (checked above)\r\n" +
"  • TLS certificate: correct CN/SAN, full chain, expiry\r\n" +
"  • Media: RTP port range open both directions; NAT handled\r\n" +
"  • Clock/NTP in sync (a skewed clock breaks TLS/SRTP and CDRs)");
        }

        void Log(string text, Color c)
        {
            if (rtb == null) return;
            if (rtb.InvokeRequired) { rtb.BeginInvoke((MethodInvoker)delegate { Log(text, c); }); return; }
            rtb.SelectionStart = rtb.TextLength; rtb.SelectionLength = 0; rtb.SelectionColor = c;
            rtb.AppendText(text); rtb.ScrollToCaret();
        }
    }
}
