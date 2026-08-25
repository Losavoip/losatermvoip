using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  SRTP / DTLS Analyzer — incolla un SDP e mostra la sicurezza del media:
    //  SDES (a=crypto), DTLS-SRTP (a=fingerprint/a=setup), suite, chiave, warning.
    //  Nativo, bilingue IT/EN.
    // ════════════════════════════════════════════════════════════════════════
    public class SrtpAnalyzerPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtIn, txtOut;

        public SrtpAnalyzerPanel()
        {
            Text = "LosaTermVoip — SRTP / DTLS Analyzer";
            Size = new Size(820, 560);
            MinimumSize = new Size(620, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Color.FromArgb(34,34,46) };
            top.Controls.Add(new Label { Text=L.B("Incolla l'SDP (offer/answer):","Paste the SDP (offer/answer):"), Location=new Point(12,8), AutoSize=true, ForeColor=Color.LightGray });
            txtIn = new TextBox { Location=new Point(12,28), Width=780, Height=80, Multiline=true, ScrollBars=ScrollBars.Vertical,
                BackColor=CIn, ForeColor=Color.White, Font=new Font("Consolas",9), BorderStyle=BorderStyle.FixedSingle };
            txtIn.Anchor = AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
            top.Controls.Add(txtIn);
            var btn = new Button { Text=L.B("🔐 Analizza","🔐 Analyze"), Location=new Point(12,116), Width=140, Height=28, FlatStyle=FlatStyle.Flat,
                BackColor=Color.FromArgb(40,80,140), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btn.FlatAppearance.BorderSize=0; btn.Click += (s,e)=>Analyze(); top.Controls.Add(btn);
            var btnR = ReportHelper.MakeButton(160, 116);
            btnR.Click += (s,e)=>ReportHelper.ExportText(this, "SRTP / DTLS Analyzer", txtOut.Text);
            top.Controls.Add(btnR);

            txtOut = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
                BackColor=Color.FromArgb(12,16,24), ForeColor=Color.Gainsboro, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };
            txtOut.TextChanged += (s,e)=>ReportHelper.Set("SRTP / DTLS Analyzer", txtOut.Text);

            Controls.Add(txtOut);
            Controls.Add(top);
        }

        void Analyze()
        {
            string sdp = txtIn.Text ?? "";
            var sb = new StringBuilder();
            sb.AppendLine("══════════ SRTP / DTLS ══════════");
            sb.AppendLine();

            // transport (m= ... RTP/SAVP, RTP/SAVPF, UDP/TLS/RTP/SAVP, RTP/AVP)
            var mlines = Regex.Matches(sdp, @"(?im)^m=(\w+)\s+\d+\s+(\S+)");
            if (mlines.Count == 0) { txtOut.Text = L.B("Nessuna riga m= trovata: SDP non valido.","No m= line found: invalid SDP."); return; }

            bool anyCrypto = false, anyDtls = false, anyClear = false;
            foreach (Match m in mlines)
            {
                string media = m.Groups[1].Value, proto = m.Groups[2].Value.ToUpper();
                sb.AppendLine("┌─ media: " + media + "   proto: " + proto);
                bool savp = proto.Contains("SAVP");
                bool tls  = proto.Contains("TLS");
                if (!savp && !tls) { sb.AppendLine(L.B("│  ⚠ RTP IN CHIARO (RTP/AVP) — nessuna cifratura media!","│  ⚠ CLEARTEXT RTP (RTP/AVP) — no media encryption!")); anyClear = true; }
                sb.AppendLine();
            }

            // a=crypto (SDES)
            foreach (Match c in Regex.Matches(sdp, @"(?im)^a=crypto:(\d+)\s+(\S+)\s+inline:([^|\s]+)(?:\|([^|\s]+))?"))
            {
                anyCrypto = true;
                string suite = c.Groups[2].Value;
                string key = c.Groups[3].Value;
                sb.AppendLine("🔑 SDES (a=crypto)  tag=" + c.Groups[1].Value);
                sb.AppendLine("   suite : " + suite);
                sb.AppendLine("   key   : " + (key.Length>10?key.Substring(0,10)+"…":key) + "  (" + L.B("lunghezza ","length ") + key.Length + " base64)");
                if (suite.Contains("SHA1_32")) sb.AppendLine(L.B("   ⚠ tag di autenticazione a 32 bit (più debole di 80).","   ⚠ 32-bit auth tag (weaker than 80)."));
                if (suite.Contains("NULL"))    sb.AppendLine(L.B("   ⚠ NULL cipher: chiavi negoziate ma media NON cifrato.","   ⚠ NULL cipher: keys negotiated but media NOT encrypted."));
                if (suite.Contains("AES_256")) sb.AppendLine(L.B("   ✅ AES-256 (forte).","   ✅ AES-256 (strong)."));
                sb.AppendLine();
            }

            // a=fingerprint + a=setup (DTLS-SRTP)
            var fp = Regex.Match(sdp, @"(?im)^a=fingerprint:(\S+)\s+(\S+)");
            if (fp.Success)
            {
                anyDtls = true;
                sb.AppendLine("🛡 DTLS-SRTP (a=fingerprint)");
                sb.AppendLine("   hash        : " + fp.Groups[1].Value);
                sb.AppendLine("   fingerprint : " + fp.Groups[2].Value);
                var setup = Regex.Match(sdp, @"(?im)^a=setup:(\S+)");
                if (setup.Success) sb.AppendLine("   setup role  : " + setup.Groups[1].Value + RoleHint(setup.Groups[1].Value));
                if (fp.Groups[1].Value.ToLower()=="sha-1") sb.AppendLine(L.B("   ⚠ fingerprint SHA-1 (debole): preferisci SHA-256.","   ⚠ SHA-1 fingerprint (weak): prefer SHA-256."));
                sb.AppendLine();
            }

            // verdetto
            sb.AppendLine("══════════ " + L.B("Verdetto","Verdict") + " ══════════");
            if (anyDtls && anyCrypto) sb.AppendLine(L.B("⚠ Presenti SIA DTLS SIA SDES: configurazione mista (best-effort?). Verifica cosa usa davvero il peer.","⚠ Both DTLS and SDES present: mixed config (best-effort?). Check what the peer actually uses."));
            else if (anyDtls)   sb.AppendLine(L.B("✅ DTLS-SRTP (tipico WebRTC / Teams). Media cifrato, chiavi via handshake DTLS.","✅ DTLS-SRTP (typical WebRTC / Teams). Encrypted media, keys via DTLS handshake."));
            else if (anyCrypto) sb.AppendLine(L.B("✅ SDES SRTP. Media cifrato; le chiavi viaggiano nell'SDP → richiede SIP/TLS per essere sicuro.","✅ SDES SRTP. Encrypted media; keys travel in the SDP → needs SIP/TLS to be safe."));
            else if (anyClear)  sb.AppendLine(L.B("❌ Media in CHIARO (RTP/AVP, niente crypto): chiunque sul percorso può ascoltare l'audio.","❌ CLEARTEXT media (RTP/AVP, no crypto): anyone on the path can listen to the audio."));
            else sb.AppendLine(L.B("Proto SAVP ma nessun a=crypto/a=fingerprint trovato: SDP incompleto?","SAVP proto but no a=crypto/a=fingerprint found: incomplete SDP?"));

            txtOut.Text = sb.ToString();
        }

        static string RoleHint(string r)
        {
            r = r.ToLower();
            if (r=="actpass") return L.B("  (offre entrambi, l'altro sceglie)","  (offers both, the other picks)");
            if (r=="active")  return L.B("  (client DTLS, avvia l'handshake)","  (DTLS client, starts the handshake)");
            if (r=="passive") return L.B("  (server DTLS, attende)","  (DTLS server, waits)");
            return "";
        }
    }
}
