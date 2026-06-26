using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  RTP Player + DTMF — riproduce l'audio G.711 da un PCAP + DTMF. Bilingue.
    //  Usa tshark per leggere i pacchetti.
    // ════════════════════════════════════════════════════════════════════════
    public class RtpPlayerPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CIn = Color.FromArgb(45,45,60);

        class Stream { public string Ssrc, Src, Dst; public int Pt; public int Pkts; }

        string pcapFile;
        ListView lv;
        TextBox txtOut;
        Button btnOpen, btnPlay, btnSave, btnStop, btnDtmf;
        SoundPlayer player;
        string lastWav;

        public RtpPlayerPanel()
        {
            Text = "LosaTermVoip — RTP Player & DTMF";
            Size = new Size(820, 540);
            MinimumSize = new Size(620, 380);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            Build();
            FormClosed += (s,e)=>{ try { if (player!=null) player.Stop(); } catch {} };
        }

        void Build()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(34,34,46) };
            btnOpen = Btn(L.B("📂 Apri PCAP","📂 Open PCAP"), 8, 6, 120, Color.FromArgb(40,80,140)); btnOpen.Click += (s,e)=>OpenPcap(); top.Controls.Add(btnOpen);
            btnPlay = Btn(L.B("▶ Riproduci","▶ Play"), 134, 6, 110, Color.FromArgb(30,110,30)); btnPlay.Click += (s,e)=>PlaySelected(); btnPlay.Enabled=false; top.Controls.Add(btnPlay);
            btnStop = Btn("⏹ Stop", 248, 6, 80, Color.FromArgb(120,30,30)); btnStop.Click += (s,e)=>{ try{ if(player!=null) player.Stop(); }catch{} }; btnStop.Enabled=false; top.Controls.Add(btnStop);
            btnSave = Btn(L.B("💾 Salva WAV","💾 Save WAV"), 332, 6, 110, Color.FromArgb(60,60,80)); btnSave.Click += (s,e)=>SaveSelected(); btnSave.Enabled=false; top.Controls.Add(btnSave);
            btnDtmf = Btn("🔢 DTMF", 446, 6, 90, Color.FromArgb(80,60,120)); btnDtmf.Click += (s,e)=>ExtractDtmf(); btnDtmf.Enabled=false; top.Controls.Add(btnDtmf);

            lv = new ListView { Dock=DockStyle.Top, Height=220, View=View.Details, FullRowSelect=true, GridLines=true,
                BackColor=Color.FromArgb(18,18,30), ForeColor=Color.White };
            lv.Columns.Add(L.B("Sorgente","Source"), 170); lv.Columns.Add(L.B("Destinazione","Destination"), 170); lv.Columns.Add("SSRC", 110); lv.Columns.Add("Codec", 180); lv.Columns.Add(L.B("Pacchetti","Packets"), 90);

            txtOut = new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical,
                BackColor=Color.FromArgb(12,16,24), ForeColor=Color.LimeGreen, Font=new Font("Consolas",10), BorderStyle=BorderStyle.None,
                Text=L.B("Apri un PCAP per elencare gli stream RTP.\r\nRiproduzione audio: solo G.711 (PCMU/PCMA). DTMF: RFC 2833.","Open a PCAP to list RTP streams.\r\nAudio playback: G.711 only (PCMU/PCMA). DTMF: RFC 2833.") };

            Controls.Add(txtOut);
            Controls.Add(lv);
            Controls.Add(top);
        }

        void OpenPcap()
        {
            using (var d = new OpenFileDialog { Filter = L.B("Cattura (*.pcap;*.pcapng;*.cap)|*.pcap;*.pcapng;*.cap|Tutti i file|*.*","Capture (*.pcap;*.pcapng;*.cap)|*.pcap;*.pcapng;*.cap|All files|*.*") })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                pcapFile = d.FileName;
                LoadStreams();
            }
        }

        string Tshark()
        {
            string t = PcapAnalyzer.FindTshark();
            if (t == null)
                MessageBox.Show(L.B("tshark.exe non trovato.\nInstalla Wireshark: https://www.wireshark.org/download.html","tshark.exe not found.\nInstall Wireshark: https://www.wireshark.org/download.html"), L.B("tshark mancante","tshark missing"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return t;
        }

        void LoadStreams()
        {
            string t = Tshark(); if (t == null) return;
            txtOut.Text = L.B("Lettura stream RTP da ","Reading RTP streams from ") + Path.GetFileName(pcapFile) + " ...\r\n";
            lv.Items.Clear(); btnPlay.Enabled = btnSave.Enabled = false; btnDtmf.Enabled = true;
            ThreadPool.QueueUserWorkItem(_ => {
                var streams = ListStreams(t, pcapFile);
                if (!lv.IsHandleCreated) return;
                lv.BeginInvoke((MethodInvoker)delegate {
                    foreach (var s in streams)
                    {
                        var it = new ListViewItem(s.Src);
                        it.SubItems.Add(s.Dst);
                        it.SubItems.Add(s.Ssrc);
                        it.SubItems.Add(PtName(s.Pt));
                        it.SubItems.Add(s.Pkts.ToString());
                        it.Tag = s;
                        lv.Items.Add(it);
                    }
                    txtOut.AppendText(streams.Count + L.B(" stream RTP trovati. Seleziona uno stream G.711 e premi ▶ Riproduci.\r\n"," RTP streams found. Select a G.711 stream and press ▶ Play.\r\n"));
                    if (lv.Items.Count > 0) { lv.Items[0].Selected = true; UpdateButtons(); }
                });
            });
            lv.SelectedIndexChanged -= LvSel; lv.SelectedIndexChanged += LvSel;
        }

        void LvSel(object s, EventArgs e) { UpdateButtons(); }
        void UpdateButtons()
        {
            var st = Selected();
            bool g711 = st != null && (st.Pt == 0 || st.Pt == 8);
            btnPlay.Enabled = g711; btnSave.Enabled = g711;
        }

        Stream Selected() { return lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as Stream : null; }

        List<Stream> ListStreams(string tshark, string pcap)
        {
            var map = new Dictionary<string, Stream>();
            var order = new List<string>();
            try
            {
                string outp = Run(tshark, "-r \"" + pcap + "\" -Y rtp -T fields -e rtp.ssrc -e ip.src -e udp.srcport -e ip.dst -e udp.dstport -e rtp.p_type -E separator=, -E occurrence=f");
                foreach (var line in outp.Replace("\r","").Split('\n'))
                {
                    if (line.Length == 0) continue;
                    var f = line.Split(',');
                    if (f.Length < 6) continue;
                    string ssrc = f[0].Trim(); if (ssrc.Length == 0) continue;
                    Stream st;
                    if (!map.TryGetValue(ssrc, out st))
                    {
                        st = new Stream { Ssrc = ssrc, Src = f[1].Trim()+":"+f[2].Trim(), Dst = f[3].Trim()+":"+f[4].Trim() };
                        int pt; int.TryParse(f[5].Trim(), out pt); st.Pt = pt;
                        map[ssrc] = st; order.Add(ssrc);
                    }
                    st.Pkts++;
                }
            }
            catch { }
            var list = new List<Stream>();
            foreach (var k in order) list.Add(map[k]);
            return list;
        }

        void PlaySelected()
        {
            var st = Selected(); if (st == null) return;
            string t = Tshark(); if (t == null) return;
            txtOut.AppendText(L.B("Decodifica audio SSRC ","Decoding audio SSRC ") + st.Ssrc + " (" + PtName(st.Pt) + ")...\r\n");
            btnPlay.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ => {
                string wav = BuildWav(t, st);
                if (!IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate {
                    btnPlay.Enabled = true;
                    if (wav == null) { txtOut.AppendText(L.B("✗ Nessun payload audio estratto.\r\n","✗ No audio payload extracted.\r\n")); return; }
                    lastWav = wav;
                    try { if (player != null) player.Stop(); player = new SoundPlayer(wav); player.Play(); btnStop.Enabled = true;
                          txtOut.AppendText(L.B("▶ In riproduzione (","▶ Playing (") + PtName(st.Pt) + ", 8 kHz).\r\n"); }
                    catch (Exception ex) { txtOut.AppendText(L.B("✗ Errore riproduzione: ","✗ Playback error: ") + ex.Message + "\r\n"); }
                });
            });
        }

        void SaveSelected()
        {
            var st = Selected(); if (st == null) return;
            string t = Tshark(); if (t == null) return;
            using (var d = new SaveFileDialog { Filter = "WAV (*.wav)|*.wav", FileName = "rtp_" + st.Ssrc + ".wav" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string dest = d.FileName;
                ThreadPool.QueueUserWorkItem(_ => {
                    string wav = BuildWav(t, st);
                    if (!IsHandleCreated) return;
                    BeginInvoke((MethodInvoker)delegate {
                        if (wav == null) { txtOut.AppendText(L.B("✗ Nessun payload estratto.\r\n","✗ No payload extracted.\r\n")); return; }
                        try { File.Copy(wav, dest, true); txtOut.AppendText(L.B("💾 Salvato: ","💾 Saved: ") + dest + "\r\n"); }
                        catch (Exception ex) { txtOut.AppendText("✗ " + ex.Message + "\r\n"); }
                    });
                });
            }
        }

        string BuildWav(string tshark, Stream st)
        {
            try
            {
                string hex = Run(tshark, "-r \"" + pcapFile + "\" -Y \"rtp.ssrc==" + st.Ssrc + "\" -T fields -e rtp.payload");
                var pcm = new List<short>(hex.Length/2);
                foreach (var line in hex.Replace("\r","").Split('\n'))
                {
                    if (line.Length == 0) continue;
                    string h = line.Replace(":","").Replace(" ","").Trim();
                    for (int i = 0; i+1 < h.Length; i += 2)
                    {
                        byte b;
                        if (!byte.TryParse(h.Substring(i,2), System.Globalization.NumberStyles.HexNumber, null, out b)) continue;
                        pcm.Add(st.Pt == 8 ? ALawToLinear(b) : MuLawToLinear(b));
                    }
                }
                if (pcm.Count == 0) return null;
                string path = Path.Combine(Path.GetTempPath(), "losaterm_rtp_" + st.Ssrc.Replace("0x","") + ".wav");
                WriteWav(path, pcm, 8000);
                return path;
            }
            catch { return null; }
        }

        void ExtractDtmf()
        {
            string t = Tshark(); if (t == null) return;
            txtOut.AppendText(L.B("Estrazione DTMF (RFC 2833)...\r\n","Extracting DTMF (RFC 2833)...\r\n"));
            ThreadPool.QueueUserWorkItem(_ => {
                string res;
                try
                {
                    string outp = Run(t, "-r \"" + pcapFile + "\" -Y \"rtpevent\" -T fields -e rtpevent.event_id -e rtpevent.end_of_event");
                    var sb = new StringBuilder();
                    foreach (var line in outp.Replace("\r","").Split('\n'))
                    {
                        if (line.Length == 0) continue;
                        var f = line.Split('\t');
                        if (f.Length < 2) continue;
                        bool end = f[1].Trim() == "1" || f[1].Trim().ToLower() == "true";
                        if (!end) continue;
                        int id; if (!int.TryParse(f[0].Trim(), out id)) continue;
                        sb.Append(DtmfChar(id));
                    }
                    res = sb.Length > 0 ? L.B("🔢 Cifre DTMF: ","🔢 DTMF digits: ") + sb.ToString() : L.B("Nessun DTMF RFC2833 trovato nella cattura.","No RFC2833 DTMF found in the capture.");
                }
                catch (Exception ex) { res = "✗ " + ex.Message; }
                if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { txtOut.AppendText(res + "\r\n"); });
            });
        }

        static string DtmfChar(int id)
        {
            if (id >= 0 && id <= 9) return id.ToString();
            switch (id) { case 10: return "*"; case 11: return "#"; case 12: return "A"; case 13: return "B"; case 14: return "C"; case 15: return "D"; case 16: return "(flash)"; }
            return "?";
        }

        static string PtName(int pt)
        {
            string na = L.B(" (no audio)"," (no audio)");
            switch (pt) {
                case 0: return "G.711 µ-law (PCMU)";
                case 8: return "G.711 A-law (PCMA)";
                case 9: return "G.722" + na;
                case 18: return "G.729" + na;
                case 3: return "GSM" + na;
                case 4: return "G.723" + na;
            }
            if (pt >= 96) return L.B("dinamico ","dynamic ") + pt + na;
            return "PT " + pt;
        }

        static string Run(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
            using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return o; }
        }

        static short MuLawToLinear(byte u)
        {
            u = (byte)~u;
            int t = ((u & 0x0F) << 3) + 0x84;
            t <<= ((u & 0x70) >> 4);
            return (short)(((u & 0x80) != 0) ? (0x84 - t) : (t - 0x84));
        }
        static short ALawToLinear(byte a)
        {
            a ^= 0x55;
            int t = (a & 0x0F) << 4;
            int seg = (a & 0x70) >> 4;
            switch (seg) { case 0: t += 8; break; case 1: t += 0x108; break; default: t += 0x108; t <<= (seg - 1); break; }
            return (short)(((a & 0x80) != 0) ? t : -t);
        }

        static void WriteWav(string path, List<short> samples, int rate)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int dataLen = samples.Count * 2;
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + dataLen); bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
                bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
                bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(dataLen);
                foreach (var s in samples) bw.Write(s);
            }
        }

        static Button Btn(string t,int x,int y,int w,Color c){ var b=new Button{ Text=t, Location=new Point(x,y), Width=w, Height=28, FlatStyle=FlatStyle.Flat, BackColor=c, ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold)}; b.FlatAppearance.BorderSize=0; return b; }
    }
}
