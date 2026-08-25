using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  DNS VoIP Analyzer — NAPTR → SRV → A/AAAA + TXT/MX/NS, con interpretazione
    //  e warning. Più ENUM. Bilingue IT/EN. Usa l'helper condiviso DnsQ.
    // ════════════════════════════════════════════════════════════════════════
    public class DnsVoipPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtDomain, txtEnum, txtOut;

        public DnsVoipPanel()
        {
            Text = "LosaTermVoip — DNS VoIP Analyzer";
            Size = new Size(820, 560);
            MinimumSize = new Size(620, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(34,34,46) };
            top.Controls.Add(new Label { Text=L.B("Dominio SIP:","SIP domain:"), Location=new Point(12,12), AutoSize=true, ForeColor=Color.LightGray });
            txtDomain = new TextBox { Location=new Point(100,9), Width=260, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text="azienda.it" };
            txtDomain.KeyDown += (s,e)=>{ if(e.KeyCode==Keys.Enter){ Analyze(); e.Handled=e.SuppressKeyPress=true; } };
            top.Controls.Add(txtDomain);
            var btn = Btn(L.B("🔍 Analizza VoIP","🔍 Analyze VoIP"), 370, 7, 150, Color.FromArgb(40,80,140)); btn.Click += (s,e)=>Analyze(); top.Controls.Add(btn);

            top.Controls.Add(new Label { Text="ENUM (E.164):", Location=new Point(12,46), AutoSize=true, ForeColor=Color.LightGray });
            txtEnum = new TextBox { Location=new Point(100,43), Width=200, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle, Text="+390212345678" };
            txtEnum.KeyDown += (s,e)=>{ if(e.KeyCode==Keys.Enter){ DoEnum(); e.Handled=e.SuppressKeyPress=true; } };
            top.Controls.Add(txtEnum);
            var btnE = Btn("☎ ENUM", 310, 41, 100, Color.FromArgb(80,60,120)); btnE.Click += (s,e)=>DoEnum(); top.Controls.Add(btnE);
            top.Controls.Add(new Label { Text=L.B("NAPTR → SRV → A/AAAA, con warning sui record mancanti.","NAPTR → SRV → A/AAAA, with warnings for missing records."), Location=new Point(430,46), AutoSize=true, ForeColor=Color.Gray });

            var btnR = ReportHelper.MakeButton(530, 7);
            btnR.Click += (s,e)=>ReportHelper.ExportText(this, "DNS VoIP Analyzer", txtOut.Text);
            top.Controls.Add(btnR);

            txtOut = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
                BackColor=Color.FromArgb(12,16,24), ForeColor=Color.Gainsboro, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };
            txtOut.TextChanged += (s,e)=>ReportHelper.Set("DNS VoIP Analyzer", txtOut.Text);

            Controls.Add(txtOut);
            Controls.Add(top);
        }

        void Analyze()
        {
            string dom = (txtDomain.Text ?? "").Trim().TrimEnd('.');
            if (dom.Length == 0) return;
            txtOut.Text = L.B("Analisi DNS VoIP di  ","DNS VoIP analysis of  ") + dom + "  ...\r\n";
            ThreadPool.QueueUserWorkItem(_ => {
                string rep = BuildReport(dom);
                if (txtOut.IsHandleCreated) txtOut.BeginInvoke((MethodInvoker)delegate { txtOut.Text = rep; });
            });
        }

        string BuildReport(string dom)
        {
            var sb = new StringBuilder();
            sb.AppendLine("══════════ DNS VoIP — " + dom + " ══════════");
            sb.AppendLine();

            sb.AppendLine("┌─ NAPTR (RFC 3263)");
            string nerr;
            var naptrRecs = DnsQ.Naptr(dom, out nerr);
            var srvFromNaptr = new List<string>();
            if (nerr != null) sb.AppendLine("│  ✗ " + nerr);
            else if (naptrRecs.Count == 0)
            {
                sb.AppendLine(L.B("│  ❌ Nessun record NAPTR","│  ❌ No NAPTR record"));
                sb.AppendLine(L.B("│  ⚠ Verranno usati direttamente i record SRV (fallback RFC 3263).","│  ⚠ SRV records will be used directly (RFC 3263 fallback)."));
            }
            else
            {
                foreach (var n in naptrRecs)
                {
                    sb.AppendLine("│  order=" + n.Order + " pref=" + n.Pref + "  flags=\"" + n.Flags + "\"  service=\"" + n.Service + "\"");
                    sb.AppendLine("│     → " + InterpretNaptr(n) + "   replacement: " + n.Replacement);
                    if (!string.IsNullOrEmpty(n.Replacement) && n.Replacement != ".") srvFromNaptr.Add(n.Replacement.TrimEnd('.'));
                }
            }
            sb.AppendLine();

            sb.AppendLine("┌─ SRV");
            var srvNames = new List<string> { "_sip._udp." + dom, "_sip._tcp." + dom, "_sips._tcp." + dom, "_sips._tls." + dom };
            foreach (var s in srvFromNaptr) if (!srvNames.Contains(s)) srvNames.Add(s);
            var targets = new List<string>();
            bool anySrv = false;
            foreach (var sn in srvNames)
            {
                string serr;
                var recs = DnsQ.Srv(sn, out serr);
                if (serr != null) { sb.AppendLine("│  " + sn + " : ✗ " + serr); continue; }
                if (recs.Count == 0) { sb.AppendLine("│  " + sn + L.B(" : (nessuno)"," : (none)")); continue; }
                anySrv = true;
                foreach (var r in recs)
                {
                    sb.AppendLine("│  " + sn);
                    sb.AppendLine("│     prio=" + r.Priority + " weight=" + r.Weight + " port=" + r.Port + " → " + r.Target.TrimEnd('.'));
                    string tgt = r.Target.TrimEnd('.');
                    if (tgt.Length > 0 && !targets.Contains(tgt)) targets.Add(tgt);
                }
            }
            if (!anySrv) sb.AppendLine(L.B("│  ❌ Nessun SRV SIP trovato — l'endpoint userà l'A-record del dominio sulla 5060.","│  ❌ No SIP SRV found — the endpoint will use the domain A-record on 5060."));
            sb.AppendLine();

            sb.AppendLine("┌─ A / AAAA");
            if (!targets.Contains(dom)) targets.Insert(0, dom);
            foreach (var t in targets)
            {
                var a = DnsQ.Query(t, 1);
                var aaaa = DnsQ.Query(t, 28);
                sb.AppendLine("│  " + t);
                sb.AppendLine("│     A    : " + (a.Count>0 ? string.Join(", ", a.ToArray()) : L.B("❌ nessuno","❌ none")));
                sb.AppendLine("│     AAAA : " + (aaaa.Count>0 ? string.Join(", ", aaaa.ToArray()) : L.B("— (no IPv6)","— (no IPv6)")));
            }
            sb.AppendLine();

            sb.AppendLine(L.B("┌─ Altri record (","┌─ Other records (") + dom + ")");
            var ns  = DnsQ.Query(dom, 2);
            var mx  = DnsQ.Query(dom, 15);
            var txt = DnsQ.Query(dom, 16);
            sb.AppendLine("│  NS  : " + (ns.Count>0 ? string.Join(", ", ns.ToArray()) : "—"));
            sb.AppendLine("│  MX  : " + (mx.Count>0 ? string.Join(", ", mx.ToArray()) : "—"));
            sb.AppendLine("│  TXT : " + (txt.Count>0 ? string.Join(" | ", txt.ToArray()) : "—"));
            sb.AppendLine();

            sb.AppendLine("══════════ " + L.B("Verdetto","Verdict") + " ══════════");
            if (naptrRecs.Count == 0 && !anySrv)
                sb.AppendLine(L.B("⚠ Nessun NAPTR né SRV: configurazione DNS minima (solo A-record). OK per IP-PBX semplici, NON per discovery RFC 3263.","⚠ No NAPTR nor SRV: minimal DNS config (A-record only). OK for simple IP-PBX, NOT for RFC 3263 discovery."));
            else if (naptrRecs.Count == 0)
                sb.AppendLine(L.B("⚠ SRV presenti ma NAPTR mancante: il client deve sapere quale trasporto provare (no auto-discovery TLS/TCP/UDP).","⚠ SRV present but NAPTR missing: the client must know which transport to try (no TLS/TCP/UDP auto-discovery)."));
            else
                sb.AppendLine(L.B("✅ NAPTR + SRV presenti: auto-discovery RFC 3263 completo.","✅ NAPTR + SRV present: full RFC 3263 auto-discovery."));
            return sb.ToString();
        }

        void DoEnum()
        {
            string num = (txtEnum.Text ?? "").Trim();
            var digits = new StringBuilder();
            foreach (char c in num) if (c>='0' && c<='9') digits.Append(c);
            if (digits.Length < 4) { txtOut.Text = L.B("Numero E.164 non valido.","Invalid E.164 number."); return; }
            var rev = new StringBuilder();
            string d = digits.ToString();
            for (int i = d.Length-1; i>=0; i--) { rev.Append(d[i]); rev.Append('.'); }
            string enumDom = rev.ToString() + "e164.arpa";
            txtOut.Text = L.B("ENUM lookup per +","ENUM lookup for +") + d + "\r\n→ " + enumDom + "\r\n\r\n";
            ThreadPool.QueueUserWorkItem(_ => {
                var sb = new StringBuilder();
                string err;
                var recs = DnsQ.Naptr(enumDom, out err);
                if (err != null) sb.AppendLine("✗ " + err);
                else if (recs.Count == 0) sb.AppendLine(L.B("❌ Nessun record ENUM (NAPTR) per questo numero in e164.arpa pubblico.","❌ No ENUM (NAPTR) record for this number in public e164.arpa."));
                else foreach (var n in recs)
                {
                    sb.AppendLine("service=\"" + n.Service + "\"  flags=\"" + n.Flags + "\"");
                    string uri = ApplyEnumRegexp(n.Regexp, "+" + d);
                    sb.AppendLine("   → " + (uri ?? n.Regexp));
                }
                if (txtOut.IsHandleCreated) txtOut.BeginInvoke((MethodInvoker)delegate { txtOut.AppendText(sb.ToString()); });
            });
        }

        static string InterpretNaptr(DnsQ.NaptrRec n)
        {
            string s = (n.Service ?? "").ToUpperInvariant();
            if (s.Contains("SIPS+D2T")) return "SIP over TLS  → _sips._tcp";
            if (s.Contains("SIP+D2T"))  return "SIP over TCP  → _sip._tcp";
            if (s.Contains("SIP+D2U"))  return "SIP over UDP  → _sip._udp";
            if (s.Contains("SIP+D2S"))  return "SIP over SCTP → _sip._sctp";
            if (s.Contains("SIP+D2W") || s.Contains("SIPS+D2W")) return "SIP over WebSocket";
            if (s.StartsWith("E2U")) return "ENUM (E.164 → URI)";
            return L.B("servizio: ","service: ") + n.Service;
        }

        static string ApplyEnumRegexp(string regexp, string number)
        {
            if (string.IsNullOrEmpty(regexp)) return null;
            try
            {
                char sep = regexp[0];
                string[] parts = regexp.Split(sep);
                if (parts.Length >= 3 && parts[2].Length > 0) return parts[2];
            }
            catch { }
            return null;
        }

        static Button Btn(string t,int x,int y,int w,Color c){ var b=new Button{ Text=t, Location=new Point(x,y), Width=w, Height=28, FlatStyle=FlatStyle.Flat, BackColor=c, ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold)}; b.FlatAppearance.BorderSize=0; return b; }
    }
}
