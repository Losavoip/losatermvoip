using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // Helper condiviso per l'export dei report (HTML brandizzato) di ogni strumento VoIP
    // + uno "store" di sessione che alimenta il report combinato che li include tutti.
    public static class ReportHelper
    {
        static string Esc(string s) { return System.Security.SecurityElement.Escape(s ?? ""); }

        static string Style()
        {
            return "body{font-family:'Segoe UI',system-ui,Arial,sans-serif;color:#1b2430;margin:0;background:#f5f7fa}"
                 + ".wrap{max-width:960px;margin:0 auto;padding:28px}"
                 + ".hd{display:flex;align-items:center;justify-content:space-between;border-bottom:3px solid #2bb37a;padding-bottom:14px;margin-bottom:8px}"
                 + ".hd h1{font-size:20px;margin:0}.hd .meta{color:#6b7684;font-size:13px;text-align:right}"
                 + "h2{font-size:15px;color:#0f6b47;border-left:4px solid #2bb37a;padding-left:8px;margin:26px 0 10px}"
                 + "pre{background:#0d1117;color:#e6edf3;padding:14px;border-radius:8px;overflow:auto;font-family:Consolas,monospace;font-size:12.5px;line-height:1.55;white-space:pre-wrap}"
                 + ".ft{margin-top:30px;padding-top:14px;border-top:1px solid #dfe3e8;color:#8b949e;font-size:12px;text-align:center}";
        }

        static string Head(string title)
        {
            return "<!doctype html><html><head><meta charset=\"utf-8\"><title>LosaTerm · " + Esc(title) + "</title><style>"
                 + Style() + "</style></head><body><div class=\"wrap\">";
        }

        static string Footer()
        {
            return "<div class=\"ft\">" + L.B("Generato con","Generated with") + " <b>LosaTerm · Voip Terminal</b> — losavoip.github.io</div></div></body></html>";
        }

        // HTML di un singolo report (titolo + testo preformattato)
        public static string Wrap(string title, string bodyPre)
        {
            var sb = new StringBuilder();
            sb.Append(Head(title));
            sb.Append("<div class=\"hd\"><h1>🩺 LosaTerm · " + Esc(title) + "</h1><div class=\"meta\">" + Esc(DateTime.Now.ToString("yyyy-MM-dd HH:mm")) + "</div></div>");
            sb.Append("<pre>" + Esc(bodyPre) + "</pre>");
            sb.Append(Footer());
            return sb.ToString();
        }

        static string Safe(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in (s ?? "report"))
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }

        // Salva il report di un singolo strumento (.html o .txt) e lo apre
        public static void ExportText(IWin32Window owner, string toolName, string content)
        {
            if (string.IsNullOrEmpty(content) || content.Trim().Length == 0)
            {
                MessageBox.Show(owner, L.B("Nessun risultato da esportare: esegui prima un'analisi.","Nothing to export: run an analysis first."), "LosaTermVoip");
                return;
            }
            using (var dlg = new SaveFileDialog { Filter = "HTML report|*.html|Text|*.txt", FileName = "LosaTerm_" + Safe(toolName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                bool html = dlg.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
                string outp = html ? Wrap(toolName, content) : ("LosaTerm — " + toolName + " — " + DateTime.Now + "\r\n" + new string('=', 60) + "\r\n\r\n" + content);
                try { File.WriteAllText(dlg.FileName, outp, Encoding.UTF8); Process.Start(dlg.FileName); }
                catch (Exception ex) { MessageBox.Show(owner, ex.Message, "LosaTermVoip"); }
            }
        }

        // ── Store di sessione per il report combinato ──────────────────────────
        static readonly List<string> order = new List<string>();
        static readonly Dictionary<string, string>   store = new Dictionary<string, string>();
        static readonly Dictionary<string, DateTime>  stamp = new Dictionary<string, DateTime>();

        // Ogni strumento chiama Set() quando produce un risultato (ultimo vince)
        public static void Set(string tool, string content)
        {
            if (string.IsNullOrEmpty(content)) return;
            if (!store.ContainsKey(tool)) order.Add(tool);
            store[tool] = content;
            stamp[tool] = DateTime.Now;
        }

        public static bool HasAny { get { return store.Count > 0; } }
        public static int Count { get { return store.Count; } }

        public static string CombinedHtml()
        {
            var sb = new StringBuilder();
            sb.Append(Head(L.B("Report VoIP completo","Full VoIP report")));
            sb.Append("<div class=\"hd\"><h1>🧰 LosaTerm · " + L.B("Report VoIP completo","Full VoIP report") + "</h1><div class=\"meta\">"
                    + Esc(DateTime.Now.ToString("yyyy-MM-dd HH:mm")) + "<br>" + store.Count + " " + L.B("strumenti","tools") + "</div></div>");
            foreach (var tool in order)
            {
                if (!store.ContainsKey(tool)) continue;
                sb.Append("<h2>" + Esc(tool) + "  <span style='color:#8b949e;font-weight:normal;font-size:12px'>(" + stamp[tool].ToString("HH:mm") + ")</span></h2>");
                sb.Append("<pre>" + Esc(store[tool]) + "</pre>");
            }
            sb.Append(Footer());
            return sb.ToString();
        }

        // Salva il report combinato (tutti gli strumenti eseguiti nella sessione)
        public static void ExportCombined(IWin32Window owner)
        {
            if (!HasAny)
            {
                MessageBox.Show(owner, L.B("Nessuno strumento ancora eseguito in questa sessione.\nApri gli strumenti VoIP e lanciali, poi genera il report completo.","No tools run yet in this session.\nOpen the VoIP tools and run them, then generate the full report."), "LosaTermVoip");
                return;
            }
            using (var dlg = new SaveFileDialog { Filter = "HTML report|*.html", FileName = "LosaTerm_Full_VoIP_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmm") })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                try { File.WriteAllText(dlg.FileName, CombinedHtml(), Encoding.UTF8); Process.Start(dlg.FileName); }
                catch (Exception ex) { MessageBox.Show(owner, ex.Message, "LosaTermVoip"); }
            }
        }

        // Crea un pulsante "💾 Report" pronto da mettere in una toolbar
        public static Button MakeButton(int x, int y)
        {
            var b = new Button { Text = L.B("💾 Report","💾 Report"), Location = new System.Drawing.Point(x, y), Width = 100, Height = 26,
                FlatStyle = FlatStyle.Flat, ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(45, 95, 120), Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}
