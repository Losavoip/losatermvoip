using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

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

        // ── Export PDF via Microsoft Edge headless (preinstallato su Win10/11) ──
        static string FindEdge()
        {
            string[] paths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Microsoft", "Edge", "Application", "msedge.exe"),
            };
            foreach (var p in paths) if (File.Exists(p)) return p;
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"))
                    if (k != null) { string v = k.GetValue(null) as string; if (!string.IsNullOrEmpty(v) && File.Exists(v)) return v; }
            }
            catch { }
            return null;
        }

        // Converte HTML → PDF usando Edge headless. Ritorna false + err se non riesce.
        public static bool HtmlToPdf(string html, string pdfPath, out string err)
        {
            err = null;
            string edge = FindEdge();
            if (edge == null) { err = L.B("Microsoft Edge non trovato", "Microsoft Edge not found"); return false; }
            string tmpHtml = Path.Combine(Path.GetTempPath(), "losaterm_" + Guid.NewGuid().ToString("N") + ".html");
            string userData = Path.Combine(Path.GetTempPath(), "losaterm_edge_" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(tmpHtml, html, Encoding.UTF8);
                var psi = new ProcessStartInfo(edge,
                    "--headless --disable-gpu --no-pdf-header-footer --user-data-dir=\"" + userData + "\" --print-to-pdf=\"" + pdfPath + "\" \"file:///" + tmpHtml.Replace("\\", "/") + "\"")
                { UseShellExecute = false, CreateNoWindow = true };
                var pr = Process.Start(psi);
                if (!pr.WaitForExit(30000)) { try { pr.Kill(); } catch { } err = "timeout"; return false; }
                if (File.Exists(pdfPath)) return true;
                err = L.B("Edge non ha prodotto il PDF", "Edge did not produce the PDF");
                return false;
            }
            catch (Exception ex) { err = ex.Message; return false; }
            finally
            {
                try { File.Delete(tmpHtml); } catch { }
                try { if (Directory.Exists(userData)) Directory.Delete(userData, true); } catch { }
            }
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
            using (var dlg = new SaveFileDialog { Filter = "HTML report|*.html|PDF report|*.pdf|Text|*.txt", FileName = "LosaTerm_" + Safe(toolName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                SaveHistory(toolName, "", Wrap(toolName, content));
                if (dlg.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { ExportPdf(owner, Wrap(toolName, content), dlg.FileName); return; }
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
            using (var dlg = new SaveFileDialog { Filter = "HTML report|*.html|PDF report|*.pdf", FileName = "LosaTerm_Full_VoIP_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmm") })
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return;
                SaveHistory(L.B("Report VoIP completo", "Full VoIP report"), "", CombinedHtml());
                if (dlg.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { ExportPdf(owner, CombinedHtml(), dlg.FileName); return; }
                try { File.WriteAllText(dlg.FileName, CombinedHtml(), Encoding.UTF8); Process.Start(dlg.FileName); }
                catch (Exception ex) { MessageBox.Show(owner, ex.Message, "LosaTermVoip"); }
            }
        }

        // Genera il PDF (Edge headless) e lo apre; fallback guidato se Edge manca/fallisce
        static void ExportPdf(IWin32Window owner, string html, string pdfPath)
        {
            string err;
            if (HtmlToPdf(html, pdfPath, out err)) { try { Process.Start(pdfPath); } catch { } return; }
            MessageBox.Show(owner,
                L.B("Export PDF non riuscito: ", "PDF export failed: ") + err +
                L.B("\n\nSuggerimento: salva in HTML e usa 'Stampa → Salva come PDF' dal browser.",
                    "\n\nTip: save as HTML and use 'Print → Save as PDF' from the browser."),
                "LosaTermVoip", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Cronologia test/diagnosi (snapshot HTML su disco) ──────────────────
        public static string HistoryDir()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LosaTermVoip", "history");
            try { if (!Directory.Exists(d)) Directory.CreateDirectory(d); } catch { }
            return d;
        }

        // Salva uno snapshot HTML del test nella cronologia (best-effort, non blocca mai)
        public static void SaveHistory(string tool, string target, string html)
        {
            if (string.IsNullOrEmpty(html)) return;
            try
            {
                string dir = HistoryDir();
                string name = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "__" + Safe(tool)
                            + (string.IsNullOrEmpty(target) ? "" : "__" + Safe(target)) + ".html";
                File.WriteAllText(Path.Combine(dir, name), html, Encoding.UTF8);
                PruneHistory(dir, 300);
            }
            catch { }
        }

        // Come SaveHistory ma prende testo semplice e lo avvolge nell'HTML brandizzato
        public static void SaveHistoryText(string tool, string target, string content)
        {
            if (string.IsNullOrEmpty(content)) return;
            SaveHistory(tool, target, Wrap(tool + (string.IsNullOrEmpty(target) ? "" : " — " + target), content));
        }

        static void PruneHistory(string dir, int keep)
        {
            try
            {
                var files = Directory.GetFiles(dir, "*.html");
                if (files.Length <= keep) return;
                Array.Sort(files);   // il nome inizia col timestamp → ordine cronologico
                for (int i = 0; i < files.Length - keep; i++) try { File.Delete(files[i]); } catch { }
            }
            catch { }
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

    // 📜 Cronologia test/diagnosi — elenca gli snapshot HTML salvati su disco.
    public class HistoryPanel : Form
    {
        ListView lv;

        public HistoryPanel()
        {
            Text = "LosaTermVoip — " + L.B("Cronologia test", "Test history");
            Size = new Size(780, 520);
            MinimumSize = new Size(560, 360);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(22, 22, 32); ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
            LoadEntries();
        }

        Button Mk(string text, int x, Color c)
        {
            var b = new Button { Text = text, Location = new Point(x, 8), Width = 104, Height = 28,
                FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = c, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(28, 35, 55) };
            var btnOpen = Mk(L.B("📂 Apri", "📂 Open"), 8, Color.FromArgb(40, 80, 140)); btnOpen.Click += delegate { OpenSel(); };
            var btnDel = Mk(L.B("🗑 Elimina", "🗑 Delete"), 118, Color.FromArgb(120, 60, 60)); btnDel.Click += delegate { DelSel(); };
            var btnFolder = Mk(L.B("🗂 Cartella", "🗂 Folder"), 228, Color.FromArgb(60, 60, 90)); btnFolder.Click += delegate { try { Process.Start(ReportHelper.HistoryDir()); } catch { } };
            var btnRefresh = Mk(L.B("🔄 Aggiorna", "🔄 Refresh"), 338, Color.FromArgb(60, 60, 90)); btnRefresh.Click += delegate { LoadEntries(); };
            top.Controls.AddRange(new Control[] { btnOpen, btnDel, btnFolder, btnRefresh });

            lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = true,
                BackColor = Color.FromArgb(12, 16, 24), ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.None };
            lv.Columns.Add(L.B("Data/ora", "Date/time"), 150);
            lv.Columns.Add(L.B("Strumento", "Tool"), 230);
            lv.Columns.Add("Target", 340);
            lv.DoubleClick += delegate { OpenSel(); };

            Controls.Add(lv);
            Controls.Add(top);
        }

        void LoadEntries()
        {
            lv.Items.Clear();
            try
            {
                var files = Directory.GetFiles(ReportHelper.HistoryDir(), "*.html");
                Array.Sort(files); Array.Reverse(files);   // più recente in cima
                foreach (var f in files)
                {
                    string bn = Path.GetFileNameWithoutExtension(f);
                    string[] p = bn.Split(new string[] { "__" }, StringSplitOptions.None);
                    string when = p.Length > 0 ? FmtStamp(p[0]) : bn;
                    string tool = p.Length > 1 ? p[1].Replace('_', ' ') : "";
                    string tgt = p.Length > 2 ? p[2].Replace('_', ' ') : "";
                    var it = new ListViewItem(new string[] { when, tool, tgt }); it.Tag = f;
                    lv.Items.Add(it);
                }
                if (files.Length == 0) lv.Items.Add(new ListViewItem(L.B("(nessun test salvato — esegui o esporta un report)", "(no saved tests — run or export a report)")));
            }
            catch (Exception ex) { lv.Items.Add(new ListViewItem(ex.Message)); }
        }

        static string FmtStamp(string s)
        {
            DateTime dt;
            if (DateTime.TryParseExact(s, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                return dt.ToString("yyyy-MM-dd HH:mm:ss");
            return s;
        }

        void OpenSel()
        {
            foreach (ListViewItem it in lv.SelectedItems)
                if (it.Tag != null) try { Process.Start(it.Tag.ToString()); } catch { }
        }

        void DelSel()
        {
            if (lv.SelectedItems.Count == 0) return;
            if (MessageBox.Show(this, L.B("Eliminare gli elementi selezionati?", "Delete the selected items?"), "LosaTermVoip", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            foreach (ListViewItem it in lv.SelectedItems)
                if (it.Tag != null) try { File.Delete(it.Tag.ToString()); } catch { }
            LoadEntries();
        }
    }
}
