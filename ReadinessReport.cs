using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LosaTermVoip
{
    // 🧾 Network Readiness Report — orchestratore attivo.
    // Spunti i check che vuoi (ambiente + target + vendor), premi Esegui, e lui li
    // lancia da solo aggregando tutto in un unico report HTML brandizzato.
    // Riusa i motori esistenti (STUN, DNS nativo, Environment Check checklist).
    public class ReadinessReportPanel : Form
    {
        enum RSev { Info, Ok, Warn, Err }

        class Res
        {
            public RSev Sev;
            public string Title;
            public string[] Lines;
            public Res(RSev sev, string title, string[] lines) { Sev = sev; Title = title; Lines = lines; }
        }

        class Item
        {
            public string Id;
            public string Label;
            public bool NeedsTarget;
            public CheckBox Box;
            public Item(string id, string label, bool needsTarget) { Id = id; Label = label; NeedsTarget = needsTarget; }
        }

        static readonly Color CBg = Color.FromArgb(22, 22, 32), CIn = Color.FromArgb(45, 55, 80);

        ComboBox cmbVendor;
        TextBox  txtTarget;
        RichTextBox rtb;
        Button   btnGo, btnReport;
        ProgressBar pb;
        readonly List<Item> items = new List<Item>();
        string lastReport = "";

        public ReadinessReportPanel()
        {
            Text = "LosaTermVoip — Network Readiness Report";
            Size = new Size(1000, 660);
            MinimumSize = new Size(820, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            // ── Barra superiore: target + vendor + azioni ──
            var top = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Color.FromArgb(28, 35, 55), Padding = new Padding(12) };

            top.Controls.Add(new Label { Text = L.B("Target (SBC / dominio SIP):", "Target (SBC / SIP domain):"), Location = new Point(14, 14), AutoSize = true, ForeColor = Color.LightGray });
            txtTarget = new TextBox { Location = new Point(200, 11), Width = 240, BackColor = CIn, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            top.Controls.Add(txtTarget);

            top.Controls.Add(new Label { Text = L.B("Vendor:", "Vendor:"), Location = new Point(460, 14), AutoSize = true, ForeColor = Color.LightGray });
            cmbVendor = new ComboBox { Location = new Point(510, 11), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = CIn, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbVendor.Items.AddRange(EnvCheckPanel.Vendors);
            cmbVendor.SelectedIndex = 0;
            top.Controls.Add(cmbVendor);

            btnGo = new Button { Text = L.B("▶ Esegui selezionati", "▶ Run selected"), Location = new Point(14, 46), Width = 200, Height = 28,
                FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(30, 100, 40), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnGo.FlatAppearance.BorderSize = 0;
            btnGo.Click += delegate { Start(); };
            top.Controls.Add(btnGo);

            btnReport = ReportHelper.MakeButton(224, 47);
            btnReport.Click += delegate { ReportHelper.ExportText(this, "Network Readiness Report", lastReport); };
            top.Controls.Add(btnReport);

            pb = new ProgressBar { Location = new Point(336, 49), Width = 180, Height = 22, Style = ProgressBarStyle.Marquee, Visible = false };
            top.Controls.Add(pb);

            top.Controls.Add(new Label {
                Text = L.B("Spunta cosa verificare → un unico report. I check 'Target' usano l'host qui sopra.",
                           "Tick what to check → one combined report. 'Target' checks use the host above."),
                Location = new Point(530, 52), AutoSize = true, ForeColor = Color.Gray });

            // ── Colonna sinistra: checklist ──
            var left = new Panel { Dock = DockStyle.Left, Width = 340, BackColor = Color.FromArgb(18, 20, 30), AutoScroll = true, Padding = new Padding(10) };
            int y = 8;
            AddGroup(left, ref y, L.B("AMBIENTE (nessun target)", "ENVIRONMENT (no target)"));
            AddItem(left, ref y, new Item("env.local", L.B("IP locali / interfacce / gateway", "Local IPs / interfaces / gateway"), false), true);
            AddItem(left, ref y, new Item("env.dns",   L.B("DNS resolver di sistema", "System DNS resolvers"), false), true);
            AddItem(left, ref y, new Item("env.proxy", L.B("Proxy di sistema (WinINET / PAC)", "System proxy (WinINET / PAC)"), false), true);
            AddItem(left, ref y, new Item("env.pubip", L.B("IP pubblico + tipo NAT (via STUN) ⚠ esce online", "Public IP + NAT type (via STUN) ⚠ goes online"), false), false);

            y += 8;
            AddGroup(left, ref y, L.B("TARGET (richiede SBC / dominio)", "TARGET (requires SBC / domain)"));
            AddItem(left, ref y, new Item("t.dns",   L.B("DNS: NAPTR / SRV / A / AAAA", "DNS: NAPTR / SRV / A / AAAA"), true), true);
            AddItem(left, ref y, new Item("t.reach", L.B("Raggiungibilità SIP: OPTIONS 5060 + porte", "SIP reachability: OPTIONS 5060 + ports"), true), true);
            AddItem(left, ref y, new Item("t.tls",   L.B("Certificato TLS 5061 (CN/SAN/scadenza)", "TLS 5061 certificate (CN/SAN/expiry)"), true), true);
            AddItem(left, ref y, new Item("t.trace", L.B("Traceroute L3 al target (lento)", "L3 traceroute to target (slow)"), true), false);

            y += 8;
            AddGroup(left, ref y, L.B("VENDOR READINESS", "VENDOR READINESS"));
            AddItem(left, ref y, new Item("v.check", L.B("Checklist vendor + comandi debug", "Vendor checklist + debug commands"), false), true);

            // ── Area risultati ──
            rtb = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(8, 12, 22), ForeColor = Color.Gainsboro, Font = new Font("Consolas", 9.5f),
                WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };

            // Ordine di aggiunta = z-order: il Fill (rtb) va aggiunto per PRIMO (in fondo),
            // poi Left, poi Top, così gli edge-dock riservano lo spazio e il Fill riempie il resto.
            Controls.Add(rtb);
            Controls.Add(left);
            Controls.Add(top);
        }

        void AddGroup(Panel host, ref int y, string title)
        {
            host.Controls.Add(new Label { Text = "── " + title + " ──", Location = new Point(6, y), AutoSize = true,
                ForeColor = Color.FromArgb(120, 200, 160), Font = new Font("Segoe UI", 9, FontStyle.Bold) });
            y += 24;
        }

        void AddItem(Panel host, ref int y, Item it, bool defChecked)
        {
            it.Box = new CheckBox { Text = it.Label, Location = new Point(14, y), AutoSize = true, ForeColor = Color.Gainsboro, Checked = defChecked };
            host.Controls.Add(it.Box);
            items.Add(it);
            y += 26;
        }

        void Start()
        {
            string target = (txtTarget.Text ?? "").Trim();
            string vendor = cmbVendor.Text;

            // snapshot delle selezioni (thread-safe: leggiamo i .Checked ora)
            var run = new List<Item>();
            foreach (var it in items) if (it.Box.Checked) run.Add(it);
            if (run.Count == 0) { MessageBox.Show(this, L.B("Seleziona almeno un check.", "Select at least one check."), "LosaTermVoip"); return; }

            btnGo.Enabled = false; pb.Visible = true; rtb.Clear();
            var th = new Thread(delegate () { RunAll(run, target, vendor); }) { IsBackground = true };
            th.Start();
        }

        void RunAll(List<Item> run, string target, string vendor)
        {
            var body = new StringBuilder();
            int nOk = 0, nWarn = 0, nErr = 0;

            // Teams: se non c'è target, usa il primo PSTN hub come default sensato
            string effTarget = target;
            if (effTarget.Length == 0 && vendor.StartsWith("Microsoft Teams")) effTarget = "sip.pstnhub.microsoft.com";

            try
            {
                Log("═══ Network Readiness Report ═══\r\n", Color.White);
                Log("Host PC : " + Environment.MachineName + "\r\n", Color.Gray);
                Log("Data    : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\r\n", Color.Gray);
                Log("Target  : " + (effTarget.Length > 0 ? effTarget : "—") + "\r\n", Color.Gray);
                Log("Vendor  : " + vendor + "\r\n", Color.Gray);
                Log(new string('=', 56) + "\r\n\r\n", Color.DimGray);

                foreach (var it in run)
                {
                    if (it.NeedsTarget && effTarget.Length == 0)
                    {
                        Emit(new Res(RSev.Warn, it.Label, new string[] { L.B("Saltato: nessun target indicato.", "Skipped: no target set.") }), body);
                        nWarn++;
                        continue;
                    }
                    Res r = Dispatch(it.Id, effTarget, vendor);
                    Emit(r, body);
                    if (r.Sev == RSev.Ok) nOk++; else if (r.Sev == RSev.Warn) nWarn++; else if (r.Sev == RSev.Err) nErr++;
                }

                string summary = "Riepilogo: " + nOk + " ✅ · " + nWarn + " ⚠️ · " + nErr + " ⛔";
                string summaryEn = "Summary: " + nOk + " ✅ · " + nWarn + " ⚠️ · " + nErr + " ⛔";
                string sm = L.B(summary, summaryEn);
                Log(new string('=', 56) + "\r\n", Color.DimGray);
                Log(sm + "\r\n", nErr > 0 ? Color.OrangeRed : (nWarn > 0 ? Color.Khaki : Color.LightGreen));

                var report = new StringBuilder();
                report.AppendLine("Network Readiness Report");
                report.AppendLine("Host PC : " + Environment.MachineName);
                report.AppendLine("Data    : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                report.AppendLine("Target  : " + (effTarget.Length > 0 ? effTarget : "—"));
                report.AppendLine("Vendor  : " + vendor);
                report.AppendLine(new string('=', 56));
                report.AppendLine(sm);
                report.AppendLine(new string('=', 56));
                report.AppendLine();
                report.Append(body.ToString());
                lastReport = report.ToString();
                ReportHelper.Set("Network Readiness", lastReport);
            }
            catch (Exception ex) { Log("[ERR] " + ex.Message + "\r\n", Color.OrangeRed); }
            finally
            {
                if (IsHandleCreated)
                    BeginInvoke((MethodInvoker)delegate { btnGo.Enabled = true; pb.Visible = false; });
            }
        }

        Res Dispatch(string id, string target, string vendor)
        {
            try
            {
                switch (id)
                {
                    case "env.local": return ChkLocal();
                    case "env.dns":   return ChkResolvers();
                    case "env.proxy": return ChkProxy(target);
                    case "env.pubip": return ChkPubIp();
                    case "t.dns":     return ChkDns(target);
                    case "t.reach":   return ChkReach(target);
                    case "t.tls":     return ChkTls(target, 5061);
                    case "t.trace":   return ChkTrace(target);
                    case "v.check":   return ChkVendor(vendor);
                }
            }
            catch (Exception ex) { return new Res(RSev.Err, id, new string[] { ex.Message }); }
            return new Res(RSev.Info, id, new string[] { "n/a" });
        }

        // ════════════ CHECK: AMBIENTE ════════════

        Res ChkLocal()
        {
            var lines = new List<string>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var ipp = ni.GetIPProperties();
                var v4 = new List<string>();
                foreach (var ua in ipp.UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork) v4.Add(ua.Address.ToString());
                if (v4.Count == 0) continue;
                var gw = new List<string>();
                foreach (var g in ipp.GatewayAddresses)
                    if (g.Address.AddressFamily == AddressFamily.InterNetwork && g.Address.ToString() != "0.0.0.0") gw.Add(g.Address.ToString());
                lines.Add(ni.Name + " (" + ni.NetworkInterfaceType + "): " + string.Join(", ", v4.ToArray())
                        + (gw.Count > 0 ? "  → gw " + string.Join(",", gw.ToArray()) : ""));
            }
            if (lines.Count == 0) lines.Add(L.B("nessuna interfaccia IPv4 attiva", "no active IPv4 interface"));
            return new Res(RSev.Info, L.B("IP locali / interfacce", "Local IPs / interfaces"), lines.ToArray());
        }

        Res ChkResolvers()
        {
            var seen = new List<string>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var d in ni.GetIPProperties().DnsAddresses)
                    if (d.AddressFamily == AddressFamily.InterNetwork && !seen.Contains(d.ToString())) seen.Add(d.ToString());
            }
            if (seen.Count == 0) seen.Add(L.B("nessun DNS configurato", "no DNS configured"));
            return new Res(RSev.Info, L.B("DNS resolver di sistema", "System DNS resolvers"), seen.ToArray());
        }

        Res ChkProxy(string target)
        {
            var lines = new List<string>();
            RSev sev = RSev.Ok;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k != null)
                    {
                        object pe = k.GetValue("ProxyEnable");
                        object ps = k.GetValue("ProxyServer");
                        object pac = k.GetValue("AutoConfigURL");
                        bool on = pe != null && Convert.ToInt32(pe) != 0;
                        if (on && ps != null) { lines.Add(L.B("Proxy WinINET ATTIVO: ", "WinINET proxy ON: ") + ps); sev = RSev.Warn; }
                        else lines.Add(L.B("Proxy WinINET: disattivato", "WinINET proxy: off"));
                        if (pac != null && pac.ToString().Length > 0) { lines.Add("PAC (AutoConfigURL): " + pac); sev = RSev.Warn; }
                    }
                }
            }
            catch (Exception ex) { lines.Add("registry: " + ex.Message); }

            try
            {
                string h = target.Length > 0 ? target : "example.com";
                var uri = new Uri("http://" + h);
                var sp = WebRequest.GetSystemWebProxy();
                var pr = sp.GetProxy(uri);
                if (pr != null && pr.Host != uri.Host)
                { lines.Add(L.B("Proxy di sistema per ", "System proxy for ") + uri.Host + ": " + pr.Host + ":" + pr.Port); sev = RSev.Warn; }
                else lines.Add(L.B("Nessun proxy per ", "No proxy for ") + uri.Host + L.B(" (HTTP diretto)", " (direct HTTP)"));
            }
            catch { }
            lines.Add(L.B("Nota: il SIP non passa da un proxy HTTP; qui conta per provisioning/HTTP.",
                          "Note: SIP does not traverse an HTTP proxy; relevant for provisioning/HTTP."));
            return new Res(sev, L.B("Proxy di sistema", "System proxy"), lines.ToArray());
        }

        Res ChkPubIp()
        {
            string srvA = "stun.l.google.com:19302";
            string srvB = "stun.cloudflare.com:3478";
            var lines = new List<string>();
            lines.Add(L.B("Server STUN contattati: ", "STUN servers contacted: ") + srvA + " , " + srvB);
            string la, lb, ea, eb;
            IPEndPoint ma = StunTesterPanel.GetMapped(srvA, out la, out ea);
            IPEndPoint mb = StunTesterPanel.GetMapped(srvB, out lb, out eb);
            if (ma == null && mb == null)
                return new Res(RSev.Err, L.B("IP pubblico + NAT", "Public IP + NAT"),
                    new string[] { L.B("Nessuna risposta STUN (UDP 3478/19302 bloccato in uscita?).", "No STUN response (UDP 3478/19302 blocked outbound?)."), "A: " + ea, "B: " + eb });

            IPEndPoint mm = ma != null ? ma : mb;
            string loc = ma != null ? la : lb;
            lines.Add(L.B("IP locale: ", "Local IP: ") + loc);
            lines.Add(L.B("IP pubblico riflesso: ", "Reflexive public IP: ") + mm.Address + ":" + mm.Port);
            RSev sev = RSev.Ok;
            if (ma != null && mb != null)
            {
                bool natted = la != ma.Address.ToString();
                if (!natted) lines.Add(L.B("NAT: nessuno (IP pubblico diretto)", "NAT: none (direct public IP)"));
                else if (ma.Port == mb.Port) lines.Add(L.B("NAT: cone / non simmetrico — RTP ok con STUN/keepalive", "NAT: cone / non-symmetric — RTP ok with STUN/keepalive"));
                else { lines.Add(L.B("NAT: SIMMETRICO (" + ma.Port + " vs " + mb.Port + ") — RTP one-way senza TURN/SBC", "NAT: SYMMETRIC (" + ma.Port + " vs " + mb.Port + ") — RTP one-way without TURN/SBC")); sev = RSev.Warn; }
            }
            else lines.Add(L.B("(un solo server STUN ha risposto: NAT non classificabile)", "(only one STUN server replied: NAT not classifiable)"));
            return new Res(sev, L.B("IP pubblico + NAT", "Public IP + NAT"), lines.ToArray());
        }

        // ════════════ CHECK: TARGET ════════════

        Res ChkDns(string host)
        {
            var lines = new List<string>();
            RSev sev = RSev.Ok;
            string bare = StripScheme(host);

            // Target è un IP? NAPTR/SRV/A si interrogano su un DOMINIO, non su un IP.
            IPAddress ipTmp;
            if (IPAddress.TryParse(bare, out ipTmp))
            {
                lines.Add(L.B("Il target è un indirizzo IP: NAPTR/SRV/A non si applicano (si interrogano su un dominio).",
                              "Target is an IP address: NAPTR/SRV/A do not apply (they are queried on a domain)."));
                try
                {
                    var he = Dns.GetHostEntry(ipTmp);
                    if (!string.IsNullOrEmpty(he.HostName) && he.HostName != bare)
                        lines.Add("PTR (reverse): " + he.HostName);
                    else
                        lines.Add(L.B("PTR (reverse): nessun nome", "PTR (reverse): no name"));
                }
                catch { lines.Add(L.B("PTR (reverse): nessun nome", "PTR (reverse): no name")); }
                lines.Add(L.B("→ Per CUCM/PBX interni usa DNS/SRV solo se il trunk è configurato per FQDN.",
                              "→ For internal CUCM/PBX use DNS/SRV only if the trunk is configured by FQDN."));
                return new Res(RSev.Info, L.B("DNS del target — ", "Target DNS — ") + bare, lines.ToArray());
            }

            // A / AAAA
            var a = DnsQ.Query(bare, 1);
            var aaaa = DnsQ.Query(bare, 28);
            if (a.Count > 0) lines.Add("A    : " + string.Join(", ", a.ToArray()));
            if (aaaa.Count > 0) lines.Add("AAAA : " + string.Join(", ", aaaa.ToArray()));
            if (a.Count == 0 && aaaa.Count == 0) { lines.Add(L.B("A/AAAA: nessun record", "A/AAAA: no record")); sev = RSev.Warn; }

            // NAPTR
            string err;
            var naptr = DnsQ.Naptr(bare, out err);
            if (naptr.Count > 0)
                foreach (var n in naptr) lines.Add("NAPTR: " + n.Order + " " + n.Pref + " " + n.Flags + " " + n.Service + " → " + n.Replacement);
            else lines.Add("NAPTR: —");

            // SRV comuni
            string[] srvNames = { "_sip._udp." + bare, "_sip._tcp." + bare, "_sips._tcp." + bare };
            bool anySrv = false;
            foreach (var sn in srvNames)
            {
                string e2;
                var srv = DnsQ.Srv(sn, out e2);
                foreach (var r in srv) { lines.Add("SRV " + sn + " → " + r.Target + ":" + r.Port + " (p" + r.Priority + " w" + r.Weight + ")"); anySrv = true; }
            }
            if (!anySrv) lines.Add(L.B("SRV _sip/_sips: nessuno (normale se si usa A-record)", "SRV _sip/_sips: none (normal if using A-record)"));

            return new Res(sev, L.B("DNS del target — ", "Target DNS — ") + bare, lines.ToArray());
        }

        Res ChkReach(string host)
        {
            string bare = StripScheme(host);
            var lines = new List<string>();
            IPAddress ip = FirstV4(bare);
            if (ip == null) return new Res(RSev.Err, L.B("Raggiungibilità SIP", "SIP reachability"), new string[] { L.B("DNS: impossibile risolvere ", "DNS: cannot resolve ") + bare });
            lines.Add(bare + " → " + ip);
            lines.Add("TCP 5061 (TLS): " + (TcpOpen(ip, 5061, 4000) ? "open" : L.B("chiusa/filtrata", "closed/filtered")));
            lines.Add("TCP 5060 (SIP): " + (TcpOpen(ip, 5060, 4000) ? "open" : L.B("chiusa/filtrata", "closed/filtered")));

            string opt = OptionsUdp(bare, ip, 5060);
            lines.Add("OPTIONS UDP 5060: " + opt);
            // Qualsiasi risposta SIP (200/4xx/5xx) = host raggiungibile a livello SIP.
            // "nessuna risposta"/errore = non raggiungibile.
            string noReply = L.B("nessuna risposta", "no response");
            bool gotReply = opt != noReply && !opt.StartsWith("err:");
            bool alsoTcp = TcpOpen(ip, 5060, 1500) || TcpOpen(ip, 5061, 1500);
            RSev sev;
            if (gotReply)
            {
                sev = RSev.Ok;
                lines.Add(L.B("→ Risposta SIP ricevuta: host raggiungibile a livello SIP", "→ SIP reply received: host reachable at SIP level"));
                if (opt.IndexOf("503") >= 0) lines.Add(L.B("  (503 = servizio momentaneamente non disponibile lato peer)", "  (503 = service temporarily unavailable on the peer)"));
                else if (opt.StartsWith("4") || opt.IndexOf(" 4") >= 0) lines.Add(L.B("  (4xx = raggiunto ma la richiesta è stata rifiutata: auth/routing)", "  (4xx = reached but request rejected: auth/routing)"));
            }
            else if (alsoTcp)
            {
                sev = RSev.Warn;
                lines.Add(L.B("Nessuna risposta a OPTIONS UDP, ma TCP aperto: SIP UDP filtrato o OPTIONS non gestito.", "No reply to UDP OPTIONS, but TCP open: SIP UDP filtered or OPTIONS not handled."));
            }
            else
            {
                sev = RSev.Err;
                lines.Add(L.B("Nessuna risposta e porte chiuse: trunk giù, IP non whitelistato o firewall.", "No reply and ports closed: trunk down, IP not whitelisted, or firewall."));
            }
            return new Res(sev, L.B("Raggiungibilità SIP", "SIP reachability"), lines.ToArray());
        }

        Res ChkTls(string host, int port)
        {
            string bare = StripScheme(host);
            var lines = new List<string>();
            try
            {
                using (var tcp = new TcpClient())
                {
                    var ar = tcp.BeginConnect(bare, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(5000))
                        return new Res(RSev.Err, L.B("Certificato TLS " + port, "TLS certificate " + port), new string[] { L.B("timeout (firewall in uscita?)", "timeout (outbound firewall?)") });
                    tcp.EndConnect(ar);
                    X509Certificate2 cert = null;
                    RemoteCertificateValidationCallback cb = delegate (object sn, X509Certificate crt, X509Chain ch, SslPolicyErrors er) { if (crt != null) cert = new X509Certificate2(crt); return true; };
                    using (var ssl = new SslStream(tcp.GetStream(), false, cb))
                    {
                        ssl.AuthenticateAsClient(bare, null, SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                        lines.Add(L.B("Handshake: ", "Handshake: ") + ssl.SslProtocol);
                        RSev sev = ssl.SslProtocol == SslProtocols.Tls12 ? RSev.Ok : RSev.Warn;
                        if (cert != null)
                        {
                            lines.Add("Subject (CN): " + cert.GetNameInfo(X509NameType.SimpleName, false));
                            lines.Add("Issuer      : " + cert.GetNameInfo(X509NameType.SimpleName, true));
                            lines.Add("SAN         : " + Short(GetExt(cert, "2.5.29.17"), 180));
                            lines.Add("EKU         : " + Short(GetExt(cert, "2.5.29.37"), 120));
                            double days = (cert.NotAfter - DateTime.Now).TotalDays;
                            lines.Add(L.B("Scadenza    : ", "Expiry      : ") + cert.NotAfter.ToString("yyyy-MM-dd") + "  (" + (int)days + L.B(" giorni)", " days)"));
                            if (days < 0) { lines.Add(L.B("⛔ CERTIFICATO SCADUTO", "⛔ CERTIFICATE EXPIRED")); sev = RSev.Err; }
                            else if (days < 30) { lines.Add(L.B("⚠️ scade tra meno di 30 giorni", "⚠️ expires in less than 30 days")); if (sev == RSev.Ok) sev = RSev.Warn; }
                        }
                        return new Res(sev, L.B("Certificato TLS " + port, "TLS certificate " + port), lines.ToArray());
                    }
                }
            }
            catch (Exception ex) { return new Res(RSev.Err, L.B("Certificato TLS " + port, "TLS certificate " + port), new string[] { ex.Message }); }
        }

        Res ChkTrace(string host)
        {
            string bare = StripScheme(host);
            IPAddress ip = FirstV4(bare);
            if (ip == null) return new Res(RSev.Err, "Traceroute", new string[] { L.B("DNS fallito", "DNS failed") });
            var lines = new List<string>();
            byte[] buf = Encoding.ASCII.GetBytes("LosaTerm");
            using (var ping = new Ping())
            {
                for (int ttl = 1; ttl <= 20; ttl++)
                {
                    try
                    {
                        var opt = new PingOptions(ttl, true);
                        PingReply r = ping.Send(ip, 1500, buf, opt);
                        if (r == null) { lines.Add(ttl.ToString("00") + "  *"); continue; }
                        string addr = r.Address != null ? r.Address.ToString() : "*";
                        if (r.Status == IPStatus.Success) { lines.Add(ttl.ToString("00") + "  " + addr + "  " + r.RoundtripTime + "ms  [target]"); break; }
                        if (r.Status == IPStatus.TtlExpired || r.Status == IPStatus.TimeExceeded) lines.Add(ttl.ToString("00") + "  " + addr + "  " + r.RoundtripTime + "ms");
                        else lines.Add(ttl.ToString("00") + "  " + addr + "  " + r.Status);
                    }
                    catch { lines.Add(ttl.ToString("00") + "  *"); }
                }
            }
            return new Res(RSev.Info, "Traceroute → " + bare, lines.ToArray());
        }

        // ════════════ CHECK: VENDOR ════════════

        Res ChkVendor(string vendor)
        {
            var lines = new List<string>();
            foreach (var ln in EnvCheckPanel.Checklist(vendor).Replace("\r\n", "\n").Split('\n')) lines.Add(ln);
            string cheat = EnvCheckPanel.FindCheatSheet(vendor);
            if (cheat != null)
            {
                lines.Add("");
                lines.Add(L.B("── Comandi debug/trace ──", "── Debug/trace commands ──"));
                foreach (var ln in cheat.Replace("\r\n", "\n").Split('\n')) lines.Add(ln);
            }
            return new Res(RSev.Info, L.B("Checklist ", "Checklist ") + vendor, lines.ToArray());
        }

        // ════════════ HELPER ════════════

        static string StripScheme(string h)
        {
            h = (h ?? "").Trim();
            if (h.StartsWith("sip:", StringComparison.OrdinalIgnoreCase)) h = h.Substring(4);
            if (h.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)) h = h.Substring(5);
            int at = h.IndexOf('@'); if (at >= 0) h = h.Substring(at + 1);
            int col = h.IndexOf(':'); if (col >= 0) h = h.Substring(0, col);
            int sl = h.IndexOf('/'); if (sl >= 0) h = h.Substring(0, sl);
            return h;
        }

        static IPAddress FirstV4(string host)
        {
            try { foreach (var a in Dns.GetHostAddresses(host)) if (a.AddressFamily == AddressFamily.InterNetwork) return a; }
            catch { }
            return null;
        }

        static bool TcpOpen(IPAddress ip, int port, int timeoutMs)
        {
            try
            {
                using (var tcp = new TcpClient())
                {
                    var ar = tcp.BeginConnect(ip, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) return false;
                    tcp.EndConnect(ar); return true;
                }
            }
            catch { return false; }
        }

        static string OptionsUdp(string host, IPAddress ip, int port)
        {
            try
            {
                string local = "0.0.0.0";
                try { using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) { s.Connect(ip, port); local = ((IPEndPoint)s.LocalEndPoint).Address.ToString(); } }
                catch { }
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
                    int nl = text.IndexOf("\r\n");
                    return (nl > 0 ? text.Substring(0, nl) : text).Replace("SIP/2.0 ", "");
                }
            }
            catch (SocketException) { return L.B("nessuna risposta", "no response"); }
            catch (Exception ex) { return "err: " + ex.Message; }
        }

        static string GetExt(X509Certificate2 cert, string oid)
        {
            foreach (X509Extension ext in cert.Extensions)
                if (ext.Oid != null && ext.Oid.Value == oid)
                    try { return new AsnEncodedData(ext.Oid, ext.RawData).Format(false); } catch { return "?"; }
            return "—";
        }

        static string Short(string s, int max)
        {
            if (s == null) return "—";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > max ? s.Substring(0, max) + "…" : s;
        }

        void Emit(Res r, StringBuilder body)
        {
            string icon = r.Sev == RSev.Ok ? "✅" : r.Sev == RSev.Warn ? "⚠️" : r.Sev == RSev.Err ? "⛔" : "•";
            Color c = r.Sev == RSev.Ok ? Color.LightGreen : r.Sev == RSev.Warn ? Color.Khaki : r.Sev == RSev.Err ? Color.OrangeRed : Color.LightCyan;
            Log(icon + " " + r.Title + "\r\n", c);
            body.AppendLine(icon + " " + r.Title);
            if (r.Lines != null)
                foreach (var ln in r.Lines) { Log("   " + ln + "\r\n", Color.Gainsboro); body.AppendLine("   " + ln); }
            Log("\r\n", Color.Gray); body.AppendLine();
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
