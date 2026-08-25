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
    // 🟦 Teams Direct Routing — readiness in 1 click.
    // Verifica DNS-A + handshake TLS 5061 verso i 3 PSTN hub Microsoft e mostra
    // la checklist dei requisiti DR. Vendor-neutral, nativo, nessuna dipendenza.
    public class TeamsDrPanel : Form
    {
        static readonly string[] Proxies = {
            "sip.pstnhub.microsoft.com",
            "sip2.pstnhub.microsoft.com",
            "sip3.pstnhub.microsoft.com"
        };

        RichTextBox rtb;
        Button btnGo;

        public TeamsDrPanel()
        {
            Text = L.B("Teams Direct Routing — Readiness", "Teams Direct Routing — Readiness");
            Size = new Size(820, 600);
            MinimumSize = new Size(640, 460);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(22, 22, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);
            BuildUI();
        }

        void BuildUI()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(28, 35, 55), Padding = new Padding(14) };
            top.Controls.Add(new Label {
                Text = L.B("🟦 Verifica raggiungibilità dei PSTN hub Microsoft (DNS-A + TLS 5061) e requisiti Direct Routing.",
                           "🟦 Check Microsoft PSTN hub reachability (DNS-A + TLS 5061) and Direct Routing requirements."),
                Location = new Point(14, 10), AutoSize = true, ForeColor = Color.LightGray });

            btnGo = new Button {
                Text = L.B("▶ Verifica readiness", "▶ Check readiness"),
                Location = new Point(14, 38), Width = 200, Height = 28,
                FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(30, 90, 160),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnGo.FlatAppearance.BorderSize = 0;
            btnGo.Click += (s, e) => Start();
            top.Controls.Add(btnGo);

            var btnR = ReportHelper.MakeButton(224, 38);
            btnR.Click += (s, e) => ReportHelper.ExportText(this, "Teams Direct Routing readiness", rtb.Text);
            top.Controls.Add(btnR);

            rtb = new RichTextBox {
                Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(8, 12, 22), ForeColor = Color.Gainsboro,
                Font = new Font("Consolas", 9.5f), WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both
            };

            Controls.Add(rtb);
            Controls.Add(top);
        }

        void Start()
        {
            btnGo.Enabled = false;
            rtb.Clear();
            var th = new Thread(Run) { IsBackground = true };
            th.Start();
        }

        void Run()
        {
            try
            {
                Log(L.B("═══ Teams Direct Routing — readiness ═══\r\n\r\n", "═══ Teams Direct Routing — readiness ═══\r\n\r\n"), Color.White);
                int okCount = 0;

                foreach (var fqdn in Proxies)
                {
                    Log("● " + fqdn + "\r\n", Color.LightCyan);

                    // 1) DNS A (Teams usa A-record, NON SRV)
                    string ips = "";
                    try
                    {
                        var addrs = Dns.GetHostAddresses(fqdn);
                        var sb = new StringBuilder();
                        foreach (var a in addrs)
                            if (a.AddressFamily == AddressFamily.InterNetwork || a.AddressFamily == AddressFamily.InterNetworkV6)
                            { if (sb.Length > 0) sb.Append(", "); sb.Append(a.ToString()); }
                        ips = sb.ToString();
                        if (ips.Length > 0) Log("   ✔ DNS A: " + ips + "\r\n", Color.LightGreen);
                        else { Log(L.B("   ✗ DNS: nessun A-record\r\n", "   ✗ DNS: no A record\r\n"), Color.OrangeRed); Log("\r\n", Color.Gray); continue; }
                    }
                    catch (Exception ex) { Log("   ✗ DNS: " + ex.Message + "\r\n\r\n", Color.OrangeRed); continue; }

                    // 2) TLS 5061
                    try
                    {
                        using (var tcp = new TcpClient())
                        {
                            var ar = tcp.BeginConnect(fqdn, 5061, null, null);
                            if (!ar.AsyncWaitHandle.WaitOne(5000))
                            { Log(L.B("   ✗ TLS 5061: connessione scaduta (firewall in uscita?)\r\n\r\n", "   ✗ TLS 5061: connection timed out (outbound firewall?)\r\n\r\n"), Color.OrangeRed); continue; }
                            tcp.EndConnect(ar);

                            X509Certificate2 cert = null;
                            RemoteCertificateValidationCallback cb = delegate (object snd, X509Certificate crt, X509Chain chn, SslPolicyErrors err)
                            { if (crt != null) cert = new X509Certificate2(crt); return true; };
                            using (var ssl = new SslStream(tcp.GetStream(), false, cb))
                            {
                                ssl.AuthenticateAsClient(fqdn, null, SslProtocols.Tls12, false);
                                Log("   ✔ TLS 5061: " + ssl.SslProtocol + " · " + ssl.CipherAlgorithm + " " + ssl.CipherStrength + " bit\r\n", Color.LightGreen);
                                if (cert != null)
                                {
                                    string cn = cert.GetNameInfo(X509NameType.SimpleName, false);
                                    double days = (cert.NotAfter - DateTime.Now).TotalDays;
                                    Log("     " + L.B("cert Microsoft: ", "Microsoft cert: ") + cn + "  (" + L.B("scade ", "expires ") + cert.NotAfter.ToString("yyyy-MM-dd") + ", " + (int)days + L.B(" giorni)\r\n", " days)\r\n"), Color.Gray);
                                }
                                okCount++;
                            }
                        }
                    }
                    catch (Exception ex) { Log(L.B("   ✗ TLS 5061 fallito: ", "   ✗ TLS 5061 failed: ") + ex.Message + "\r\n", Color.OrangeRed); }
                    Log("\r\n", Color.Gray);
                }

                // Verdetto
                string verdict = okCount == Proxies.Length
                    ? L.B("✅ RAGGIUNGIBILE — tutti i 3 PSTN hub rispondono in TLS 5061 da questa rete.", "✅ REACHABLE — all 3 PSTN hubs answer over TLS 5061 from this network.")
                    : okCount > 0
                        ? L.B("⚠️ PARZIALE — solo " + okCount + "/3 hub raggiungibili: verifica firewall/DNS.", "⚠️ PARTIAL — only " + okCount + "/3 hubs reachable: check firewall/DNS.")
                        : L.B("⛔ BLOCCATO — nessun hub raggiungibile in TLS 5061 (firewall in uscita o DNS).", "⛔ BLOCKED — no hub reachable over TLS 5061 (outbound firewall or DNS).");
                Log(verdict + "\r\n\r\n", okCount == Proxies.Length ? Color.LightGreen : (okCount > 0 ? Color.Khaki : Color.OrangeRed));

                // Checklist requisiti DR (riferimento)
                Log(L.B("── Checklist requisiti Direct Routing ──\r\n", "── Direct Routing requirements checklist ──\r\n"), Color.LightCyan);
                Log(L.B(
"  • Certificato SBC: da CA pubblica trusted, con l'FQDN dell'SBC in CN/SAN, TLS 1.2, catena completa\r\n" +
"    (la SCADENZA del certificato è la causa #1 di down su Teams DR → controllala nel tab SBC Health)\r\n" +
"  • Segnalazione: TLS 5061 verso sip/sip2/sip3.pstnhub.microsoft.com (verificato qui sopra)\r\n" +
"  • Media: UDP 49152–53247 in entrambe le direzioni verso i media processor Microsoft\r\n" +
"  • Range IP Microsoft: 52.112.0.0/14 e 52.120.0.0/14\r\n" +
"  • DNS: Teams usa gli A-record del PSTN hub (NON usa SRV)\r\n" +
"  • SBC accoppiato nel tenant: Get-CsOnlinePSTNGateway (stato/attività) — vedi Debug Cmds › Teams\r\n" +
"  • Utente abilitato: EnterpriseVoiceEnabled + OnlineVoiceRoutingPolicy + numero assegnato + licenza Teams Phone\r\n",
"  • SBC certificate: from a trusted public CA, with the SBC FQDN in CN/SAN, TLS 1.2, full chain\r\n" +
"    (certificate EXPIRY is the #1 cause of Teams DR down → check it in the SBC Health tab)\r\n" +
"  • Signaling: TLS 5061 to sip/sip2/sip3.pstnhub.microsoft.com (checked above)\r\n" +
"  • Media: UDP 49152–53247 in both directions to the Microsoft media processors\r\n" +
"  • Microsoft IP ranges: 52.112.0.0/14 and 52.120.0.0/14\r\n" +
"  • DNS: Teams uses the PSTN hub A-records (does NOT use SRV)\r\n" +
"  • SBC paired in the tenant: Get-CsOnlinePSTNGateway (status/activity) — see Debug Cmds › Teams\r\n" +
"  • User enabled: EnterpriseVoiceEnabled + OnlineVoiceRoutingPolicy + assigned number + Teams Phone license\r\n"),
                    Color.Gray);
            }
            catch (Exception ex) { Log("[ERR] " + ex.Message + "\r\n", Color.OrangeRed); }
            finally
            {
                if (btnGo.IsHandleCreated) btnGo.BeginInvoke((MethodInvoker)delegate {
                    btnGo.Enabled = true;
                    ReportHelper.Set("Teams Direct Routing readiness", rtb.Text);   // per il report combinato
                });
            }
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
