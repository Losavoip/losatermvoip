using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LosaTermVoip
{
    // Severità di un finding di conformità
    public enum SipSev { Ok, Warn, Error }

    public class SipFinding
    {
        public SipSev Sev;
        public string Text;
        public SipFinding(SipSev s, string t) { Sev = s; Text = t; }
    }

    // Rappresentazione minima di un messaggio SIP per la validazione.
    // Può essere riempita dai campi già estratti da tshark (ladder/pcap)
    // oppure dal parsing del testo grezzo (Raw SIP / Simulator / trace online).
    public class SipParts
    {
        public bool   IsResponse;
        public string Method    = "";  // metodo richiesta (INVITE…) — vuoto per le response
        public int    Status;          // codice per le response
        public string ReqUri    = "";
        public bool   StartLineOk = true;

        public string Via       = "";
        public string Branch    = "";
        public string From      = "";
        public string FromTag   = "";
        public string To        = "";
        public string ToTag     = "";
        public string CallId    = "";
        public string CSeq      = "";  // "1 INVITE"
        public string MaxFwd    = "";
        public string Contact   = "";
        public string ContentLength = "";
        public int    BodyLen   = -1;  // lunghezza reale del body (solo dal testo grezzo)
    }

    // ── Conformance checker RFC 3261 — Fase 1 (presenza) + Fase 2 (ben-formattazione) ──
    public static class SipValidator
    {
        static bool Empty(string s) { return string.IsNullOrEmpty(s) || s.Trim().Length == 0; }

        // Metodo (dal CSeq) della transazione, es. "1 INVITE" → "INVITE"
        static string CSeqMethod(string cseq)
        {
            if (Empty(cseq)) return "";
            var m = Regex.Match(cseq.Trim(), @"^\s*\d+\s+([A-Za-z]+)");
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
        }

        public static List<SipFinding> Check(SipParts p)
        {
            var f = new List<SipFinding>();

            // ── Start-line ─────────────────────────────────────────────
            if (p.IsResponse)
            {
                if (p.Status < 100 || p.Status > 699)
                    f.Add(new SipFinding(SipSev.Error, L.B("Status-Line non valida (codice fuori 100–699).", "Invalid Status-Line (code outside 100–699).")));
            }
            else if (!p.StartLineOk)
            {
                f.Add(new SipFinding(SipSev.Error, L.B("Request-Line malformata (attesa: METHOD Request-URI SIP/2.0).", "Malformed Request-Line (expected: METHOD Request-URI SIP/2.0).")));
            }

            // ── Fase 1: header mandatori ───────────────────────────────
            if (Empty(p.Via))    f.Add(new SipFinding(SipSev.Error, L.B("Via mancante (header obbligatorio §8.1.1).", "Missing Via (mandatory header §8.1.1).")));
            if (Empty(p.From))   f.Add(new SipFinding(SipSev.Error, L.B("From mancante (header obbligatorio).", "Missing From (mandatory header).")));
            if (Empty(p.To))     f.Add(new SipFinding(SipSev.Error, L.B("To mancante (header obbligatorio).", "Missing To (mandatory header).")));
            if (Empty(p.CallId)) f.Add(new SipFinding(SipSev.Error, L.B("Call-ID mancante (header obbligatorio).", "Missing Call-ID (mandatory header).")));
            if (Empty(p.CSeq))   f.Add(new SipFinding(SipSev.Error, L.B("CSeq mancante (header obbligatorio).", "Missing CSeq (mandatory header).")));
            if (!p.IsResponse && Empty(p.MaxFwd))
                f.Add(new SipFinding(SipSev.Error, L.B("Max-Forwards mancante (obbligatorio nelle richieste §8.1.1.6).", "Missing Max-Forwards (mandatory in requests §8.1.1.6).")));

            // ── Fase 2: ben-formattazione ──────────────────────────────
            // Via + branch magic cookie
            if (!Empty(p.Via))
            {
                if (Empty(p.Branch))
                    f.Add(new SipFinding(SipSev.Error, L.B("Via senza parametro branch (obbligatorio §8.1.1.7).", "Via without branch parameter (mandatory §8.1.1.7).")));
                else if (!p.Branch.StartsWith("z9hG4bK", StringComparison.Ordinal))
                    f.Add(new SipFinding(SipSev.Warn, L.B("Via branch senza magic cookie 'z9hG4bK' (stack RFC 2543 / non conforme).", "Via branch without 'z9hG4bK' magic cookie (RFC 2543 / non-conformant stack).")));
            }

            // From tag (§8.1.1.3 — il UAC DEVE inserire tag)
            if (!Empty(p.From) && Empty(p.FromTag))
                f.Add(new SipFinding(SipSev.Warn, L.B("From senza parametro tag.", "From without tag parameter.")));

            // To tag nelle response (assente solo nel 100 Trying)
            if (p.IsResponse && p.Status != 100 && !Empty(p.To) && Empty(p.ToTag))
                f.Add(new SipFinding(SipSev.Warn, L.B("To senza tag nella risposta (atteso oltre il 100).", "To without tag in the response (expected beyond 100).")));

            // CSeq numerico + coerenza method
            if (!Empty(p.CSeq))
            {
                var cm = Regex.Match(p.CSeq.Trim(), @"^\s*(\d+)\s+([A-Za-z]+)\s*$");
                if (!cm.Success)
                    f.Add(new SipFinding(SipSev.Error, L.B("CSeq malformato (atteso: numero + metodo).", "Malformed CSeq (expected: number + method).")));
                else if (!p.IsResponse && !Empty(p.Method) &&
                         !string.Equals(cm.Groups[2].Value, p.Method, StringComparison.OrdinalIgnoreCase))
                    f.Add(new SipFinding(SipSev.Error, L.B("CSeq method (" + cm.Groups[2].Value + ") ≠ metodo della richiesta (" + p.Method + ").", "CSeq method (" + cm.Groups[2].Value + ") ≠ request method (" + p.Method + ").")));
            }

            // Max-Forwards numerico e ≠ 0
            if (!Empty(p.MaxFwd))
            {
                int mf;
                if (!int.TryParse(p.MaxFwd.Trim(), out mf))
                    f.Add(new SipFinding(SipSev.Error, L.B("Max-Forwards non numerico.", "Max-Forwards is not numeric.")));
                else if (mf == 0)
                    f.Add(new SipFinding(SipSev.Warn, L.B("Max-Forwards = 0 → il prossimo hop risponderà 483 (Too Many Hops).", "Max-Forwards = 0 → the next hop will reply 483 (Too Many Hops).")));
            }

            // Content-Length numerico e coerente col body (se disponibile dal testo grezzo)
            if (!Empty(p.ContentLength))
            {
                int cl;
                if (!int.TryParse(p.ContentLength.Trim(), out cl))
                    f.Add(new SipFinding(SipSev.Error, L.B("Content-Length non numerico.", "Content-Length is not numeric.")));
                else if (p.BodyLen >= 0 && cl != p.BodyLen)
                    f.Add(new SipFinding(SipSev.Warn, L.B("Content-Length (" + cl + ") ≠ body reale (" + p.BodyLen + " byte) → possibile troncamento/riscrittura SBC.", "Content-Length (" + cl + ") ≠ actual body (" + p.BodyLen + " bytes) → possible truncation/SBC rewrite.")));
            }

            // Contact obbligatorio: INVITE (richiesta) e 2xx all'INVITE (§8.1.1.8)
            string txMethod = p.IsResponse ? CSeqMethod(p.CSeq) : p.Method.ToUpperInvariant();
            if (!p.IsResponse && txMethod == "INVITE" && Empty(p.Contact))
                f.Add(new SipFinding(SipSev.Error, L.B("INVITE senza Contact → il dialogo non è instradabile (ACK/BYE).", "INVITE without Contact → dialog is not routable (ACK/BYE).")));
            if (p.IsResponse && p.Status >= 200 && p.Status < 300 && txMethod == "INVITE" && Empty(p.Contact))
                f.Add(new SipFinding(SipSev.Error, L.B("2xx all'INVITE senza Contact → ACK/BYE non instradano.", "2xx to INVITE without Contact → ACK/BYE won't route.")));

            return f;
        }

        // Verdetto sintetico
        public static string Verdict(List<SipFinding> f)
        {
            bool err = false, warn = false;
            foreach (var x in f) { if (x.Sev == SipSev.Error) err = true; else if (x.Sev == SipSev.Warn) warn = true; }
            if (err)  return L.B("⛔ MALFORMATO", "⛔ MALFORMED");
            if (warn) return L.B("⚠️ CON AVVISI", "⚠️ WARNINGS");
            return L.B("✅ CONFORME RFC 3261", "✅ RFC 3261 COMPLIANT");
        }

        // Blocco testo pronto da appendere al pannello dettaglio
        public static string Report(SipParts p)
        {
            var f = Check(p);
            var sb = new StringBuilder();
            sb.AppendLine(L.B("══ Conformità RFC 3261 ══════════════════════", "══ RFC 3261 conformance ══════════════════════"));
            sb.AppendLine(" " + Verdict(f));
            if (f.Count == 0)
                sb.AppendLine(L.B("   Tutti i campi obbligatori presenti e ben formati.", "   All mandatory fields present and well-formed."));
            else
                foreach (var x in f)
                {
                    string ic = x.Sev == SipSev.Error ? "  ⛔ " : (x.Sev == SipSev.Warn ? "  ⚠️ " : "  ✔ ");
                    sb.AppendLine(ic + x.Text);
                }
            return sb.ToString();
        }

        // ── Parsing del testo grezzo (Raw SIP / Simulator / trace online) ──
        public static SipParts Parse(string raw)
        {
            var p = new SipParts();
            if (Empty(raw)) return p;
            string norm = raw.Replace("\r\n", "\n").Replace("\r", "\n");

            int split = norm.IndexOf("\n\n");
            string head = split >= 0 ? norm.Substring(0, split) : norm;
            string body = split >= 0 ? norm.Substring(split + 2) : "";
            p.BodyLen = split >= 0 ? Encoding.UTF8.GetByteCount(body) : -1;

            // Unfolding (righe di continuazione che iniziano con spazio/tab)
            var rawLines = head.Split('\n');
            var lines = new List<string>();
            foreach (var rl in rawLines)
            {
                if (rl.Length > 0 && (rl[0] == ' ' || rl[0] == '\t') && lines.Count > 0)
                    lines[lines.Count - 1] += " " + rl.Trim();
                else
                    lines.Add(rl);
            }
            if (lines.Count == 0) return p;

            // Start-line
            string start = lines[0].Trim();
            if (start.StartsWith("SIP/2.0", StringComparison.Ordinal))
            {
                p.IsResponse = true;
                var m = Regex.Match(start, @"^SIP/2\.0\s+(\d{3})");
                if (m.Success) p.Status = int.Parse(m.Groups[1].Value);
                else p.Status = -1;
            }
            else
            {
                var m = Regex.Match(start, @"^([A-Za-z]+)\s+(\S+)\s+SIP/2\.0\s*$");
                p.StartLineOk = m.Success;
                if (m.Success) { p.Method = m.Groups[1].Value.ToUpperInvariant(); p.ReqUri = m.Groups[2].Value; }
            }

            // Header (prima occorrenza; forma compatta normalizzata)
            for (int i = 1; i < lines.Count; i++)
            {
                int c = lines[i].IndexOf(':');
                if (c <= 0) continue;
                string name = lines[i].Substring(0, c).Trim().ToLowerInvariant();
                string val  = lines[i].Substring(c + 1).Trim();
                switch (name)
                {
                    case "via": case "v":               if (Empty(p.Via)) p.Via = val; break;
                    case "from": case "f":              if (Empty(p.From)) p.From = val; break;
                    case "to": case "t":                if (Empty(p.To)) p.To = val; break;
                    case "call-id": case "i":           if (Empty(p.CallId)) p.CallId = val; break;
                    case "cseq":                        if (Empty(p.CSeq)) p.CSeq = val; break;
                    case "max-forwards":                if (Empty(p.MaxFwd)) p.MaxFwd = val; break;
                    case "contact": case "m":           if (Empty(p.Contact)) p.Contact = val; break;
                    case "content-length": case "l":    if (Empty(p.ContentLength)) p.ContentLength = val; break;
                }
            }

            // Estrai branch e tag dai rispettivi header
            var mb = Regex.Match(p.Via, @";branch=([^;\s]+)", RegexOptions.IgnoreCase);
            if (mb.Success) p.Branch = mb.Groups[1].Value;
            var ft = Regex.Match(p.From, @";tag=([^;\s]+)", RegexOptions.IgnoreCase);
            if (ft.Success) p.FromTag = ft.Groups[1].Value;
            var tt = Regex.Match(p.To, @";tag=([^;\s]+)", RegexOptions.IgnoreCase);
            if (tt.Success) p.ToTag = tt.Groups[1].Value;

            return p;
        }

        // Comodo: valida direttamente il testo grezzo
        public static List<SipFinding> ValidateRaw(string raw) { return Check(Parse(raw)); }
        public static string ReportRaw(string raw) { return Report(Parse(raw)); }
    }
}
