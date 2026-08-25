using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Apertura di un LOG in TranslatorX (Cisco Collaboration Trace Translator)
    //  TranslatorX non accetta argomenti CLI né file-paste: usa File>Open (Ctrl+O).
    //  Strategia: lancia/porta-avanti TranslatorX → Ctrl+O → incolla path → Invio.
    // ════════════════════════════════════════════════════════════════════════
    public static class TranslatorXLauncher
    {
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);

        public static bool OpenLog(string exe, string filePath, out string err)
        {
            err = null;
            try
            {
                Process p = null;
                var existing = Process.GetProcessesByName("TranslatorX");
                if (existing.Length > 0) p = existing[0];
                if (p == null || p.MainWindowHandle == IntPtr.Zero)
                {
                    p = Process.Start(exe);
                    for (int i = 0; i < 50 && (p == null || p.MainWindowHandle == IntPtr.Zero); i++)
                    { Thread.Sleep(250); if (p != null) p.Refresh(); }
                }
                if (p == null || p.MainWindowHandle == IntPtr.Zero) { err = L.B("TranslatorX non si è avviato in tempo.","TranslatorX did not start in time."); return false; }

                ShowWindow(p.MainWindowHandle, 9);          // SW_RESTORE
                SetForegroundWindow(p.MainWindowHandle);
                Thread.Sleep(500);
                SendKeys.SendWait("^o");                    // File > Open
                Thread.Sleep(1300);                         // attendi il dialog
                // incolla il path nel campo "Nome file" (evita problemi di escaping)
                Clipboard.SetText(filePath);
                Thread.Sleep(200);
                SendKeys.SendWait("^v");
                Thread.Sleep(300);
                SendKeys.SendWait("{ENTER}");
                return true;
            }
            catch (Exception ex) { err = ex.Message; return false; }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Persistenza layout finestra (dimensioni, stato, splitter)
    // ════════════════════════════════════════════════════════════════════════
    [DataContract]
    public class LayoutData
    {
        [DataMember] public int X = -1;
        [DataMember] public int Y = -1;
        [DataMember] public int W = 1100;
        [DataMember] public int H = 680;
        [DataMember] public int State = 0;          // 0=Normal 2=Maximized
        [DataMember] public int SplitMain = 510;    // splitter lista|tab
    }

    public static class AppLayout
    {
        static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LosaTermVoip", "layout.json");

        public static LayoutData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new LayoutData();
                var ser = new DataContractJsonSerializer(typeof(LayoutData));
                using (var fs = File.OpenRead(FilePath))
                    return (LayoutData)ser.ReadObject(fs);
            }
            catch { return new LayoutData(); }
        }

        public static void Save(LayoutData d)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var ser = new DataContractJsonSerializer(typeof(LayoutData));
                using (var fs = File.Create(FilePath)) ser.WriteObject(fs, d);
            }
            catch { }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Persistenza splitter dei pannelli interni (Analyzer + Ladder).
    //  File separato da layout.json per non entrare in conflitto col salvataggio
    //  della main form: una volta regolati, valgono per tutte le sessioni.
    // ════════════════════════════════════════════════════════════════════════
    [DataContract]
    public class PaneLayoutData
    {
        [DataMember] public int Analyzer = 300;   // findings | tab (Dettaglio/CallFlow/Ladder)
        [DataMember] public int Ladder   = 640;   // diagramma ladder | dettaglio messaggio
    }

    public static class PaneLayout
    {
        static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LosaTermVoip", "panes.json");

        public static PaneLayoutData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new PaneLayoutData();
                var ser = new DataContractJsonSerializer(typeof(PaneLayoutData));
                using (var fs = File.OpenRead(FilePath)) return (PaneLayoutData)ser.ReadObject(fs);
            }
            catch { return new PaneLayoutData(); }
        }

        public static void Save(PaneLayoutData d)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var ser = new DataContractJsonSerializer(typeof(PaneLayoutData));
                using (var fs = File.Create(FilePath)) ser.WriteObject(fs, d);
            }
            catch { }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Tab "Doc." — raccolta link documentazione editabile a mano
    //  (sostituisce il vecchio Cisco Docs)
    // ════════════════════════════════════════════════════════════════════════
    [DataContract]
    public class DocLink
    {
        [DataMember] public string Title { get; set; }
        [DataMember] public string Url   { get; set; }
        [DataMember] public string Cat   { get; set; }
    }

    public static class DocStore
    {
        static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LosaTermVoip", "doclinks.json");

        public static List<DocLink> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return Seed();
                var ser = new DataContractJsonSerializer(typeof(List<DocLink>));
                using (var fs = File.OpenRead(FilePath))
                {
                    var list = (List<DocLink>)ser.ReadObject(fs);
                    return (list != null && list.Count > 0) ? list : Seed();
                }
            }
            catch { return Seed(); }
        }

        public static void Save(List<DocLink> list)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var ser = new DataContractJsonSerializer(typeof(List<DocLink>));
                using (var fs = File.Create(FilePath)) ser.WriteObject(fs, list);
            }
            catch { }
        }

        // Preset iniziali — l'utente può cancellarli/aggiungerne
        static List<DocLink> Seed()
        {
            return new List<DocLink> {
                new DocLink { Cat="CUCM", Title="CUCM 15 SRND (Solution Reference Network Design)", Url="https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/srnd/collab15/collab15.html" },
                new DocLink { Cat="CUCM", Title="CUCM 14 SRND", Url="https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/srnd/collab14/collab14.html" },
                new DocLink { Cat="CUCM", Title="CUCM 12.x SRND", Url="https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cucm/srnd/collab12/collab12.html" },
                new DocLink { Cat="CUBE", Title="CUBE Configuration Guide (IOS-XE)", Url="https://www.cisco.com/c/en/us/td/docs/ios-xml/ios/voice/cube/configuration/cube-book.html" },
                new DocLink { Cat="AudioCodes", Title="AudioCodes Mediant SBC User's Manual", Url="https://www.audiocodes.com/library/technical-documents?productGroup=1647" },
                new DocLink { Cat="AudioCodes", Title="AudioCodes Syslog & Debug Guide", Url="https://www.audiocodes.com/media/13602/syslog-and-debug-guides-ver-72.pdf" },
                new DocLink { Cat="AudioCodes", Title="AudioCodes SBC Configuration Notes", Url="https://www.audiocodes.com/library/technical-documents" },
            };
        }
    }

    public class DocLinksPanel : Form
    {
        List<DocLink> links;
        ListView lv;
        ComboBox cmbCat;

        public DocLinksPanel()
        {
            Text = "LosaTermVoip — " + L.T("doc.title");
            Size = new Size(820, 520);
            MinimumSize = new Size(560, 360);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(24, 24, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);

            links = DocStore.Load();
            BuildUI();
            ReloadCats();
            Reload();
        }

        void BuildUI()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(38, 38, 52), Padding = new Padding(8, 6, 8, 6) };

            var lblF = new Label { Text = L.T("doc.category"), AutoSize = true, Location = new Point(8, 11), ForeColor = Color.LightGray };
            cmbCat = new ComboBox { Location = new Point(78, 8), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCat.SelectedIndexChanged += (s, e) => Reload();

            var bAdd  = MkBtn(L.T("doc.add"), 250, Color.FromArgb(30, 100, 30));
            var bEdit = MkBtn(L.T("btn.edit"), 360, Color.FromArgb(50, 70, 110));
            var bDel  = MkBtn(L.T("doc.remove"), 470, Color.FromArgb(120, 40, 40));
            var bOpen = MkBtn(L.T("doc.open"), 580, Color.FromArgb(40, 90, 120));
            bAdd.Click  += (s, e) => Add();
            bEdit.Click += (s, e) => EditSel();
            bDel.Click  += (s, e) => DelSel();
            bOpen.Click += (s, e) => OpenSel();

            top.Controls.AddRange(new Control[] { lblF, cmbCat, bAdd, bEdit, bDel, bOpen });

            lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, BackColor = Color.FromArgb(22, 22, 30), ForeColor = Color.White };
            lv.Columns.Add(L.T("doc.col_cat"), 130);
            lv.Columns.Add(L.T("doc.col_title"), 420);
            lv.Columns.Add("URL", 230);
            lv.DoubleClick += (s, e) => OpenSel();

            var hint = new Label { Dock = DockStyle.Bottom, Height = 22, Text = L.T("doc.hint"), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft };

            Controls.Add(lv);
            Controls.Add(hint);
            Controls.Add(top);
        }

        Button MkBtn(string t, int x, Color c)
        {
            return new Button { Text = t, Location = new Point(x, 6), Width = 104, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White };
        }

        void ReloadCats()
        {
            string cur = cmbCat.SelectedItem as string;
            cmbCat.Items.Clear();
            cmbCat.Items.Add(L.T("doc.all"));
            foreach (var cat in links.Select(l => l.Cat ?? "").Distinct().OrderBy(x => x))
                if (!string.IsNullOrEmpty(cat)) cmbCat.Items.Add(cat);
            cmbCat.SelectedItem = (cur != null && cmbCat.Items.Contains(cur)) ? cur : L.T("doc.all");
            if (cmbCat.SelectedIndex < 0) cmbCat.SelectedIndex = 0;
        }

        void Reload()
        {
            lv.Items.Clear();
            string filt = cmbCat.SelectedItem as string;
            foreach (var l in links)
            {
                if (filt != null && filt != L.T("doc.all") && (l.Cat ?? "") != filt) continue;
                var it = new ListViewItem(new[] { l.Cat ?? "", l.Title ?? "", l.Url ?? "" });
                it.Tag = l;
                lv.Items.Add(it);
            }
        }

        DocLink Selected()
        {
            return lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as DocLink : null;
        }

        void Add()
        {
            using (var d = new DocEditDialog(null))
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    links.Add(d.Result);
                    DocStore.Save(links);
                    ReloadCats(); Reload();
                }
        }

        void EditSel()
        {
            var l = Selected();
            if (l == null) { MessageBox.Show(L.T("doc.select_link")); return; }
            using (var d = new DocEditDialog(l))
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    l.Title = d.Result.Title; l.Url = d.Result.Url; l.Cat = d.Result.Cat;
                    DocStore.Save(links);
                    ReloadCats(); Reload();
                }
        }

        void DelSel()
        {
            var l = Selected();
            if (l == null) { MessageBox.Show(L.T("doc.select_link")); return; }
            if (MessageBox.Show(L.T("doc.confirm_remove", l.Title), L.T("gen.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                links.Remove(l);
                DocStore.Save(links);
                ReloadCats(); Reload();
            }
        }

        void OpenSel()
        {
            var l = Selected();
            if (l == null) return;
            try { System.Diagnostics.Process.Start(l.Url); }
            catch (Exception ex) { MessageBox.Show(L.B("Impossibile aprire:\n","Cannot open:\n") + ex.Message); }
        }
    }

    // Dialog di edit per un singolo link
    public class DocEditDialog : Form
    {
        TextBox txtCat, txtTitle, txtUrl;
        public DocLink Result;

        public DocEditDialog(DocLink existing)
        {
            Text = existing == null ? L.T("doc.new_link") : L.T("doc.edit_link");
            Size = new Size(560, 220);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(32, 32, 44);
            ForeColor = Color.White;

            Controls.Add(new Label { Text = L.T("doc.category"), Location = new Point(14, 18), AutoSize = true, ForeColor = Color.LightGray });
            txtCat = new TextBox { Location = new Point(110, 15), Width = 410 };
            Controls.Add(txtCat);

            Controls.Add(new Label { Text = L.T("doc.dtitle"), Location = new Point(14, 56), AutoSize = true, ForeColor = Color.LightGray });
            txtTitle = new TextBox { Location = new Point(110, 53), Width = 410 };
            Controls.Add(txtTitle);

            Controls.Add(new Label { Text = "URL:", Location = new Point(14, 94), AutoSize = true, ForeColor = Color.LightGray });
            txtUrl = new TextBox { Location = new Point(110, 91), Width = 410 };
            Controls.Add(txtUrl);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(320, 138), Width = 90, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 100, 30) };
            var cancel = new Button { Text = L.T("conn.cancel"), DialogResult = DialogResult.Cancel, Location = new Point(425, 138), Width = 95, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(80, 80, 90) };
            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text) || string.IsNullOrWhiteSpace(txtUrl.Text))
                { MessageBox.Show(L.T("doc.req")); DialogResult = DialogResult.None; return; }
                string url = txtUrl.Text.Trim();
                if (!url.Contains("://")) url = "https://" + url;
                Result = new DocLink { Cat = txtCat.Text.Trim(), Title = txtTitle.Text.Trim(), Url = url };
            };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;

            if (existing != null)
            {
                txtCat.Text = existing.Cat; txtTitle.Text = existing.Title; txtUrl.Text = existing.Url;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Dialog di scelta azione per drag & drop di un file
    // ════════════════════════════════════════════════════════════════════════
    public enum DropChoice { None, AnalyzeHere, TranslatorX, Syslog }

    public class DropActionDialog : Form
    {
        public DropChoice Choice = DropChoice.None;

        public DropActionDialog(string fileName, bool isPcap)
        {
            Text = L.T("drop.title");
            Size = new Size(500, 230);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 42);
            ForeColor = Color.White;

            Controls.Add(new Label {
                Text = L.T("drop.what") + "\n" + fileName + " ?",
                Location = new Point(16, 14), Size = new Size(460, 44), ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            });

            int y = 66;
            if (isPcap)
            {
                // TranslatorX gestisce i log, non i pcap → per i pcap solo il Ladder interno
                AddChoiceBtn(L.T("drop.analyze_here"), y, DropChoice.AnalyzeHere); y += 42;
                AddChoiceBtn(L.T("drop.syslog"), y, DropChoice.Syslog); y += 42;
            }
            else
            {
                // i log/trace testuali: TranslatorX (trace SDL Cisco CUCM) o viewer Syslog (AudioCodes/Cisco)
                AddChoiceBtn(L.T("drop.translatorx"), y, DropChoice.TranslatorX); y += 42;
                AddChoiceBtn(L.T("drop.syslog_ac"), y, DropChoice.Syslog); y += 42;
            }

            var cancel = new Button { Text = L.T("conn.cancel"), DialogResult = DialogResult.Cancel, Location = new Point(384, y), Width = 90, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(70, 70, 80) };
            Controls.Add(cancel);
            CancelButton = cancel;
        }

        void AddChoiceBtn(string text, int y, DropChoice choice)
        {
            var b = new Button { Text = text, Location = new Point(16, y), Width = 458, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(45, 70, 110), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            b.Click += (s, e) => { Choice = choice; DialogResult = DialogResult.OK; };
            Controls.Add(b);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Tab "Syslog" — server syslog UDP in stile AudioCodes
    //  (sostituisce il vecchio Homer)
    // ════════════════════════════════════════════════════════════════════════
    public class SyslogServerPanel : Form
    {
        UdpClient udp;
        Thread listenThread;
        volatile bool running;
        int port = 514;

        NumericUpDown numPort;
        Button btnStart, btnStop, btnClear, btnSave;
        CheckBox chkAutoScroll, chkSipOnly, chkLogFile;
        TextBox txtFilter;
        ListView lv;
        Label lblStatus, lblCount;
        StreamWriter logWriter;
        int rxCount;

        public SyslogServerPanel()
        {
            Text = "LosaTermVoip — Syslog Server (AudioCodes / Cisco)";
            Size = new Size(1000, 600);
            MinimumSize = new Size(700, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(24, 24, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);
            BuildUI();
            FormClosed += (s, e) => StopServer();
        }

        void BuildUI()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(34, 34, 46), Padding = new Padding(8) };

            top.Controls.Add(new Label { Text = L.T("sys.udp_port"), Location = new Point(8, 12), AutoSize = true, ForeColor = Color.LightGray });
            numPort = new NumericUpDown { Location = new Point(82, 9), Width = 70, Minimum = 1, Maximum = 65535, Value = 514, BackColor = Color.FromArgb(45, 45, 60), ForeColor = Color.White };
            top.Controls.Add(numPort);

            btnStart = MkBtn(L.T("sys.start"), 162, Color.FromArgb(30, 110, 30), 110);
            btnStop  = MkBtn(L.T("sys.stop"), 278, Color.FromArgb(120, 40, 40), 80);
            btnStop.Enabled = false;
            btnStart.Click += (s, e) => StartServer();
            btnStop.Click  += (s, e) => StopServer();
            top.Controls.Add(btnStart);
            top.Controls.Add(btnStop);

            lblStatus = new Label { Text = L.T("sys.stopped"), Location = new Point(370, 13), AutoSize = true, ForeColor = Color.OrangeRed, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            top.Controls.Add(lblStatus);

            lblCount = new Label { Text = "0 msg", Location = new Point(470, 13), AutoSize = true, ForeColor = Color.Gray };
            top.Controls.Add(lblCount);

            btnClear = MkBtn(L.T("sys.clear"), 560, Color.FromArgb(60, 60, 75), 90);
            btnClear.Click += (s, e) => { lv.Items.Clear(); rxCount = 0; UpdateCount(); };
            top.Controls.Add(btnClear);

            btnSave = MkBtn(L.T("sys.export"), 656, Color.FromArgb(50, 70, 110), 90);
            btnSave.Click += (s, e) => ExportLog();
            top.Controls.Add(btnSave);

            // Riga 2: filtri
            chkAutoScroll = new CheckBox { Text = L.T("sys.autoscroll"), Location = new Point(8, 48), AutoSize = true, Checked = true, ForeColor = Color.LightGray };
            chkSipOnly = new CheckBox { Text = L.T("sys.sip_only"), Location = new Point(110, 48), AutoSize = true, ForeColor = Color.LightGray };
            chkSipOnly.CheckedChanged += (s, e) => ApplyVisualFilter();
            chkLogFile = new CheckBox { Text = L.T("sys.to_file"), Location = new Point(228, 48), AutoSize = true, ForeColor = Color.LightGray };
            top.Controls.Add(chkAutoScroll);
            top.Controls.Add(chkSipOnly);
            top.Controls.Add(chkLogFile);

            top.Controls.Add(new Label { Text = L.T("sys.text_filter"), Location = new Point(348, 50), AutoSize = true, ForeColor = Color.LightGray });
            txtFilter = new TextBox { Location = new Point(428, 47), Width = 240, BackColor = Color.FromArgb(45, 45, 60), ForeColor = Color.White };
            txtFilter.TextChanged += (s, e) => ApplyVisualFilter();
            top.Controls.Add(txtFilter);

            lv = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, MultiSelect = true, BackColor = Color.FromArgb(16, 16, 22), ForeColor = Color.White, Font = new Font("Consolas", 9) };
            lv.Columns.Add(L.T("sys.col_time"), 90);
            lv.Columns.Add(L.T("sys.col_source"), 120);
            lv.Columns.Add("Sev.", 70);
            lv.Columns.Add(L.T("sys.col_msg"), 1400);

            var hint = new Label { Dock = DockStyle.Bottom, Height = 22, ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft,
                Text = L.T("sys.hint") };

            Controls.Add(lv);
            Controls.Add(hint);
            Controls.Add(top);
        }

        Button MkBtn(string t, int x, Color c, int w)
        {
            return new Button { Text = t, Location = new Point(x, 8), Width = w, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White };
        }

        void StartServer()
        {
            port = (int)numPort.Value;
            try
            {
                // Bind manuale con SO_REUSEADDR + ExclusiveAddressUse=false:
                // così il server parte anche se un socket "fantasma" (di un'istanza
                // chiusa male) è ancora agganciato alla porta.
                udp = new UdpClient();
                udp.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            }
            catch (Exception ex)
            {
                try { if (udp != null) udp.Close(); } catch { }
                udp = null;
                MessageBox.Show(L.B("Impossibile aprire la porta UDP ","Cannot open UDP port ") + port + ":\n" + ex.Message +
                    L.B("\n\nPossibili cause:\n" +
                    "  • Un'altra app usa già questa porta (es. un altro syslog server)\n" +
                    "  • Socket \"orfano\" di una sessione chiusa male → si libera al riavvio del PC\n" +
                    "  • Porte <1024 possono richiedere privilegi amministrativi\n\n" +
                    "Soluzione rapida: usa un'altra porta (es. 1514) e imposta il device\n" +
                    "per inviare i syslog su quella porta.",
                    "\n\nPossible causes:\n" +
                    "  • Another app is already using this port (e.g. another syslog server)\n" +
                    "  • An \"orphan\" socket from a badly closed session → frees on PC restart\n" +
                    "  • Ports <1024 may require administrator privileges\n\n" +
                    "Quick fix: use another port (e.g. 1514) and set the device\n" +
                    "to send syslog to that port."),
                    "Syslog", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (chkLogFile.Checked)
            {
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LosaTermVoip_Syslog");
                    Directory.CreateDirectory(dir);
                    string fn = Path.Combine(dir, "syslog_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                    logWriter = new StreamWriter(fn, true) { AutoFlush = true };
                }
                catch { logWriter = null; }
            }

            running = true;
            listenThread = new Thread(ListenLoop) { IsBackground = true };
            listenThread.Start();

            btnStart.Enabled = false; btnStop.Enabled = true; numPort.Enabled = false;
            lblStatus.Text = L.B("● In ascolto :","● Listening :") + port; lblStatus.ForeColor = Color.LimeGreen;
        }

        void StopServer()
        {
            running = false;
            // chiude e rilascia esplicitamente il socket → libera subito la porta
            try { if (udp != null) { udp.Close(); } } catch { }
            try { if (udp != null) { ((IDisposable)udp).Dispose(); } } catch { }
            udp = null;
            // attende che il thread di ascolto termini (max 1s) così la porta è davvero libera
            try
            {
                if (listenThread != null && listenThread.IsAlive && Thread.CurrentThread != listenThread)
                    listenThread.Join(1000);
            }
            catch { }
            listenThread = null;
            try { if (logWriter != null) { logWriter.Close(); logWriter = null; } } catch { }
            if (btnStart != null)
            {
                if (btnStart.InvokeRequired) { try { btnStart.BeginInvoke((Action)(() => SetStopped())); } catch { } }
                else SetStopped();
            }
        }

        void SetStopped()
        {
            btnStart.Enabled = true; btnStop.Enabled = false; numPort.Enabled = true;
            lblStatus.Text = "● Fermo"; lblStatus.ForeColor = Color.OrangeRed;
        }

        void ListenLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = udp.Receive(ref any);
                    string text = Encoding.UTF8.GetString(data);
                    string src = any.Address.ToString();
                    if (logWriter != null) { try { logWriter.WriteLine(text); } catch { } }
                    foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string l = line;
                        if (!IsDisposed && lv != null && lv.IsHandleCreated)
                            lv.BeginInvoke((Action)(() => AddRow(src, l)));
                    }
                }
                catch
                {
                    if (!running) break;
                }
            }
        }

        // Parse <PRI> per estrarre severity syslog
        static string Severity(string msg, out Color color)
        {
            int sev = 6; // default notice/info
            if (msg.Length > 2 && msg[0] == '<')
            {
                int end = msg.IndexOf('>');
                int pri;
                if (end > 1 && int.TryParse(msg.Substring(1, end - 1), out pri))
                    sev = pri & 0x7;
            }
            switch (sev)
            {
                case 0: color = Color.Magenta;   return "EMERG";
                case 1: color = Color.Magenta;   return "ALERT";
                case 2: color = Color.Red;       return "CRIT";
                case 3: color = Color.OrangeRed; return "ERROR";
                case 4: color = Color.Gold;      return "WARN";
                case 5: color = Color.Khaki;     return "NOTICE";
                case 6: color = Color.LightGreen;return "INFO";
                default: color = Color.Gray;     return "DEBUG";
            }
        }

        // Riconosce SOLO le righe che fanno parte di un messaggio SIP vero.
        // NB: evitare marcatori troppo larghi (es. "[S=", "SBC", "CALL"): negli
        // AudioCodes compaiono su OGNI riga di log e renderebbero il filtro inutile.
        static bool LooksSip(string msg)
        {
            string u = msg.ToUpperInvariant();
            return u.Contains("SIP/2.0") || u.Contains("SIP:") ||          // Via / status / URI
                   u.Contains("INVITE")  || u.Contains("REGISTER") ||
                   u.Contains("SUBSCRIBE")|| u.Contains("NOTIFY") ||
                   u.Contains("CANCEL")  || u.Contains("OPTIONS") ||
                   u.Contains("BYE")     || u.Contains("PRACK") ||
                   u.Contains("REFER")   || u.Contains("UPDATE") ||
                   u.Contains("CALL-ID") || u.Contains("CSEQ") ||         // header SIP
                   u.Contains("SDP")     || u.Contains("M=AUDIO") ||      // corpo SDP
                   u.Contains("A=RTPMAP")|| u.Contains("C=IN IP");
        }

        void AddRow(string src, string msg)
        {
            Color col;
            string sev = Severity(msg, out col);
            // togli il <PRI> iniziale per leggibilità
            string clean = msg;
            if (clean.Length > 2 && clean[0] == '<')
            {
                int end = clean.IndexOf('>');
                if (end > 0 && end < 6) clean = clean.Substring(end + 1);
            }
            var it = new ListViewItem(new[] { DateTime.Now.ToString("HH:mm:ss"), src, sev, clean });
            it.UseItemStyleForSubItems = false;
            it.SubItems[2].ForeColor = col;
            if (LooksSip(clean)) it.SubItems[3].ForeColor = Color.FromArgb(120, 200, 255);
            it.Tag = clean;

            // filtro visivo
            if (!PassesFilter(clean)) return;

            lv.Items.Add(it);
            rxCount++;
            UpdateCount();
            if (chkAutoScroll.Checked && lv.Items.Count > 0)
                lv.EnsureVisible(lv.Items.Count - 1);
            // limita a 5000 righe per non saturare memoria
            if (lv.Items.Count > 5000) lv.Items.RemoveAt(0);
        }

        bool PassesFilter(string msg)
        {
            if (chkSipOnly.Checked && !LooksSip(msg)) return false;
            string f = txtFilter.Text;
            if (!string.IsNullOrEmpty(f) && msg.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        void ApplyVisualFilter()
        {
            // ricostruisce la vista applicando i filtri sui Tag salvati
            lv.BeginUpdate();
            var all = lv.Items.Cast<ListViewItem>().ToList();
            lv.Items.Clear();
            foreach (var it in all)
            {
                string m = it.Tag as string ?? it.SubItems[3].Text;
                if (PassesFilter(m)) lv.Items.Add(it);
            }
            lv.EndUpdate();
        }

        void UpdateCount() { lblCount.Text = lv.Items.Count + " msg"; }

        // Carica un file di log esistente nel viewer (usato dal drag&drop)
        public void LoadFromFile(string path)
        {
            try
            {
                foreach (var line in File.ReadAllLines(path))
                    if (!string.IsNullOrWhiteSpace(line))
                        AddRow(Path.GetFileName(path), line);
                Text = "LosaTermVoip — Syslog Viewer — " + Path.GetFileName(path);
            }
            catch (Exception ex) { MessageBox.Show(L.B("Errore lettura file:\n","File read error:\n") + ex.Message); }
        }

        void ExportLog()
        {
            using (var d = new SaveFileDialog { Filter = L.B("Testo|*.txt","Text|*.txt"), FileName = "syslog_export.txt" })
                if (d.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var sw = new StreamWriter(d.FileName))
                            foreach (ListViewItem it in lv.Items)
                                sw.WriteLine(it.SubItems[0].Text + " " + it.SubItems[1].Text + " " + it.SubItems[2].Text + " " + it.SubItems[3].Text);
                        MessageBox.Show(L.B("Esportato: ","Exported: ") + d.FileName);
                    }
                    catch (Exception ex) { MessageBox.Show("Errore: " + ex.Message); }
                }
        }
    }
}
