using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Provisioning Viewer — apre un file di config telefono (Cisco/Yealink/
    //  Grandstream/Polycom: xml/cfg/cnf/ini/json) ed evidenzia i parametri VoIP
    //  chiave (con password mascherate). Vendor-agnostic, nativo, bilingue.
    // ════════════════════════════════════════════════════════════════════════
    public class ProvisioningPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);
        TextBox txtKeys, txtRaw;
        Label lblPath;

        // parole chiave VoIP (lowercase) da evidenziare
        static readonly string[] Keys = {
            "sip","proxy","registrar","outbound","server","user","auth","account","authid",
            "password","passwd","secret","pin","codec","ntp","vlan","transport","domain","realm",
            "extension","line","tftp","provision","mwi","voicemail","label","displayname","display_name",
            "stun","turn","srtp","tls","port","dialplan"
        };
        static readonly string[] SecretKeys = { "password","passwd","secret","pin","authpass" };

        public ProvisioningPanel()
        {
            Text = "LosaTermVoip — Provisioning Viewer";
            Size = new Size(860, 600);
            MinimumSize = new Size(640, 440);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(34,34,46) };
            var btn = new Button { Text=L.B("📂 Apri config","📂 Open config"), Location=new Point(8,5), Width=130, Height=26, FlatStyle=FlatStyle.Flat,
                BackColor=Color.FromArgb(40,80,140), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold) };
            btn.FlatAppearance.BorderSize=0; btn.Click += (s,e)=>Open(); top.Controls.Add(btn);
            lblPath = new Label { Text=L.B("Nessun file aperto.","No file open."), Location=new Point(150,10), AutoSize=true, ForeColor=Color.Gray };
            top.Controls.Add(lblPath);
            var btnR = ReportHelper.MakeButton(640, 6);
            btnR.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnR.Click += (s,e)=>ReportHelper.ExportText(this, "Provisioning Viewer", txtKeys.Text);
            top.Controls.Add(btnR);

            var hdrK = new Label { Text=L.B("  ⭐ Parametri VoIP rilevanti (password mascherate)","  ⭐ Relevant VoIP parameters (passwords masked)"), Dock=DockStyle.Top, Height=22,
                ForeColor=Color.LightGray, BackColor=Color.FromArgb(30,30,45), TextAlign=ContentAlignment.MiddleLeft };
            txtKeys = new TextBox { Dock=DockStyle.Top, Height=220, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
                BackColor=Color.FromArgb(12,16,24), ForeColor=Color.LimeGreen, Font=new Font("Consolas",9.5f), BorderStyle=BorderStyle.None };
            txtKeys.TextChanged += (s,e)=>ReportHelper.Set("Provisioning Viewer", txtKeys.Text);

            var hdrR = new Label { Text=L.B("  📄 Contenuto completo","  📄 Full content"), Dock=DockStyle.Top, Height=22,
                ForeColor=Color.LightGray, BackColor=Color.FromArgb(30,30,45), TextAlign=ContentAlignment.MiddleLeft };
            txtRaw = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Both, WordWrap=false,
                BackColor=Color.FromArgb(15,15,22), ForeColor=Color.Gainsboro, Font=new Font("Consolas",9), BorderStyle=BorderStyle.None };

            Controls.Add(txtRaw);
            Controls.Add(hdrR);
            Controls.Add(txtKeys);
            Controls.Add(hdrK);
            Controls.Add(top);
        }

        void Open()
        {
            using (var d = new OpenFileDialog { Filter = L.B("Config telefono (*.xml;*.cfg;*.cnf;*.ini;*.json;*.txt)|*.xml;*.cfg;*.cnf;*.ini;*.json;*.txt|Tutti i file|*.*","Phone config (*.xml;*.cfg;*.cnf;*.ini;*.json;*.txt)|*.xml;*.cfg;*.cnf;*.ini;*.json;*.txt|All files|*.*") })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string content = File.ReadAllText(d.FileName);
                    lblPath.Text = d.FileName + "   (" + (content.Length/1024) + " KB)";
                    txtRaw.Text = content.Replace("\n", "\r\n").Replace("\r\r\n","\r\n");
                    txtKeys.Text = ExtractFindings(content);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Provisioning"); }
            }
        }

        static string ExtractFindings(string content)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var raw in content.Replace("\r","").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string low = line.ToLower();
                bool hit = false;
                foreach (var k in Keys) if (low.Contains(k)) { hit = true; break; }
                if (!hit) continue;

                // maschera le password (vari formati: key=val, value="..", >..</password>, JSON)
                string outLine = line;
                bool secret = false; foreach (var sk in SecretKeys) if (low.Contains(sk)) { secret = true; break; }
                if (secret)
                {
                    outLine = Regex.Replace(outLine, @"(?i)((?:password|passwd|secret|pin|authpass)\s*[=:]\s*)\S+", "$1********");
                    outLine = Regex.Replace(outLine, @"(?i)(value\s*=\s*"")[^""]*("")", "$1********$2");
                    outLine = Regex.Replace(outLine, @"(?i)(>)[^<>]+(</[^>]*(?:password|passwd|secret|pin)[^>]*>)", "$1********$2");
                    outLine = Regex.Replace(outLine, @"(?i)((?:password|passwd|secret|pin)""\s*:\s*"")[^""]*("")", "$1********$2");
                }
                sb.AppendLine("  " + outLine);
                if (++n > 400) { sb.AppendLine(L.B("  … (troncato)","  … (truncated)")); break; }
            }
            if (n == 0) sb.AppendLine(L.B("  Nessun parametro VoIP riconosciuto in questo file.","  No recognizable VoIP parameter in this file."));
            return sb.ToString();
        }
    }
}
