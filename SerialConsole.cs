using System;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Console SERIALE — apre PuTTY in modalità seriale (terminale vero, stabile).
    //  Niente gestione SerialPort in-process (che in WinForms va in deadlock):
    //  delega a PuTTY, coerente con l'SSH. Default Cisco: 9600 8N1, no flow.
    // ════════════════════════════════════════════════════════════════════════
    public class SerialConsolePanel : Form
    {
        ComboBox cmbPort, cmbBaud;
        CheckBox chkStandalone;
        public string SelectedCom, SelectedBaud;
        public bool Standalone;

        public SerialConsolePanel()
        {
            Text = "Console Seriale (PuTTY)";
            Size = new Size(440, 250);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(24, 24, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            Controls.Add(new Label { Text = "Porta COM:", Location = new Point(20, 26), AutoSize = true, ForeColor = Color.LightGray });
            cmbPort = new ComboBox { Location = new Point(120, 23), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 60), ForeColor = Color.White };
            Controls.Add(cmbPort);
            var btnRefresh = MkBtn("🔄", 280, 22, 40, Color.FromArgb(60, 60, 80));
            btnRefresh.Click += (s, e) => RefreshPorts();
            Controls.Add(btnRefresh);

            Controls.Add(new Label { Text = "Baud:", Location = new Point(20, 66), AutoSize = true, ForeColor = Color.LightGray });
            cmbBaud = new ComboBox { Location = new Point(120, 63), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 60), ForeColor = Color.White };
            cmbBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            cmbBaud.SelectedIndex = 0;   // Cisco console default
            Controls.Add(cmbBaud);

            chkStandalone = new CheckBox { Text = "🪟 Finestra separata (più stabile)", Location = new Point(20, 92),
                AutoSize = true, ForeColor = Color.LightGray, Checked = true };
            new ToolTip().SetToolTip(chkStandalone, "Apre PuTTY come finestra a sé (consigliato per la seriale). Deseleziona per embeddarla in un tab.");
            Controls.Add(chkStandalone);

            var btnGo = MkBtn("▶ Apri console", 20, 126, 300, Color.FromArgb(30, 110, 30));
            btnGo.Click += (s, e) => Launch();
            AcceptButton = btnGo;
            Controls.Add(btnGo);

            Controls.Add(new Label { Text = "Console seriale via PuTTY — 8N1, niente flow control.",
                Location = new Point(20, 168), AutoSize = true, ForeColor = Color.Gray });

            RefreshPorts();
        }

        void RefreshPorts()
        {
            string cur = cmbPort.SelectedItem as string;
            cmbPort.Items.Clear();
            try { foreach (var p in SerialPort.GetPortNames()) cmbPort.Items.Add(p); } catch { }
            if (cur != null && cmbPort.Items.Contains(cur)) cmbPort.SelectedItem = cur;
            else if (cmbPort.Items.Count > 0) cmbPort.SelectedIndex = 0;
        }

        void Launch()
        {
            string com = cmbPort.SelectedItem as string;
            if (string.IsNullOrEmpty(com))
            { MessageBox.Show("Nessuna porta COM trovata. Collega un adattatore USB-seriale e premi 🔄.", "Console Seriale"); return; }
            SelectedCom  = com;
            SelectedBaud = (cmbBaud.SelectedItem as string) ?? "9600";
            Standalone   = chkStandalone.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }

        Button MkBtn(string t, int x, int y, int w, Color c)
        {
            var b = new Button { Text = t, Location = new Point(x, y), Width = w, Height = 28,
                FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}
