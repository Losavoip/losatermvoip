using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Calcolatori VoIP — banda per codec, budget MOS (E-model), QoS. Bilingue.
    // ════════════════════════════════════════════════════════════════════════
    public class VoipCalcPanel : Form
    {
        static readonly Color CBg = Color.FromArgb(24,24,32), CCard = Color.FromArgb(34,34,46), CIn = Color.FromArgb(45,45,60);

        public VoipCalcPanel()
        {
            Text = "LosaTermVoip — VoIP Calculators";
            Size = new Size(720, 560);
            MinimumSize = new Size(560, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg; ForeColor = Color.White; Font = new Font("Segoe UI", 9);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildBandwidthTab());
            tabs.TabPages.Add(BuildMosTab());
            tabs.TabPages.Add(BuildQosTab());
            Controls.Add(tabs);
        }

        class Codec { public string Name; public double Kbps; public int Ptime; public Codec(string n,double k,int p){Name=n;Kbps=k;Ptime=p;} public override string ToString(){return Name;} }
        static readonly Codec[] Codecs = new[] {
            new Codec("G.711 (PCMU/PCMA) 64k", 64, 20),
            new Codec("G.729 8k", 8, 20),
            new Codec("G.722 64k", 64, 20),
            new Codec("G.726-32 32k", 32, 20),
            new Codec("iLBC 15.2k", 15.2, 20),
            new Codec("iLBC 13.3k", 13.33, 30),
            new Codec("G.723.1 6.3k", 6.3, 30),
            new Codec("Opus 24k", 24, 20),
            new Codec("Opus 32k", 32, 20),
        };

        ComboBox cmbCodec, cmbPtime, cmbL2;
        TextBox txtCalls;
        CheckBox chkVad;
        Label lblBwOut;

        TabPage BuildBandwidthTab()
        {
            var page = new TabPage(L.B("📶  Banda per codec","📶  Bandwidth per codec")) { BackColor = CBg };
            int x1=18, x2=190, y=22;

            page.Controls.Add(Lbl("Codec:", x1, y));
            cmbCodec = Combo(x2, y, 220); cmbCodec.Items.AddRange(Codecs); cmbCodec.SelectedIndex = 0;
            page.Controls.Add(cmbCodec); y+=38;

            page.Controls.Add(Lbl(L.B("Ptime (ms/pacchetto):","Ptime (ms/packet):"), x1, y));
            cmbPtime = Combo(x2, y, 120); cmbPtime.Items.AddRange(new object[]{ "10","20","30","40","60" }); cmbPtime.SelectedIndex=1;
            page.Controls.Add(cmbPtime); y+=38;

            page.Controls.Add(Lbl(L.B("Overhead Livello 2:","Layer-2 overhead:"), x1, y));
            cmbL2 = Combo(x2, y, 220);
            cmbL2.Items.AddRange(new object[]{ "Ethernet (18 B)","Ethernet+802.1Q (22 B)","PPP/HDLC (6 B)","MPLS (4 B)", L.B("Solo IP/UDP/RTP (0 B L2)","IP/UDP/RTP only (0 B L2)") });
            cmbL2.SelectedIndex=0; page.Controls.Add(cmbL2); y+=38;

            page.Controls.Add(Lbl(L.B("Chiamate simultanee:","Concurrent calls:"), x1, y));
            txtCalls = Txt(x2, y, 120); txtCalls.Text="1"; page.Controls.Add(txtCalls); y+=34;

            chkVad = new CheckBox { Text=L.B("VAD / silence suppression (~ -35% medio)","VAD / silence suppression (~ -35% avg)"), Location=new Point(x2,y), AutoSize=true, ForeColor=Color.LightGray };
            page.Controls.Add(chkVad); y+=40;

            var btn = Btn(L.B("🧮  Calcola","🧮  Compute"), x2, y, 140); btn.Click += (s,e)=>CalcBandwidth(); page.Controls.Add(btn); y+=46;

            lblBwOut = new Label { Location=new Point(x1,y), Size=new Size(660,150), ForeColor=Color.LimeGreen,
                Font=new Font("Consolas",10), BackColor=Color.FromArgb(12,16,24), BorderStyle=BorderStyle.FixedSingle, Padding=new Padding(8) };
            page.Controls.Add(lblBwOut);

            EventHandler ch = (s,e)=>CalcBandwidth();
            cmbCodec.SelectedIndexChanged += (s,e)=>{ var c=cmbCodec.SelectedItem as Codec; if(c!=null) cmbPtime.Text=c.Ptime.ToString(); CalcBandwidth(); };
            cmbPtime.SelectedIndexChanged += ch; cmbL2.SelectedIndexChanged += ch; chkVad.CheckedChanged += ch;
            CalcBandwidth();
            return page;
        }

        void CalcBandwidth()
        {
            if (lblBwOut == null) return;
            var c = cmbCodec.SelectedItem as Codec; if (c==null) return;
            double ptime; if (!double.TryParse(cmbPtime.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out ptime) || ptime<=0) ptime=20;
            int calls; if (!int.TryParse(txtCalls.Text, out calls) || calls<1) calls=1;
            int l2 = cmbL2.SelectedIndex==0?18 : cmbL2.SelectedIndex==1?22 : cmbL2.SelectedIndex==2?6 : cmbL2.SelectedIndex==3?4 : 0;

            double pps = 1000.0/ptime;
            double payload = c.Kbps*1000.0/8.0 * ptime/1000.0;
            double frame = payload + 12 + 8 + 20 + l2;
            double kbps = frame*8.0*pps/1000.0;
            double vadFactor = chkVad.Checked ? 0.65 : 1.0;
            double perCall = kbps*vadFactor;
            double total = perCall*calls;

            lblBwOut.Text =
                "Codec            : " + c.Name + "\r\n" +
                L.B("Pacchetti/sec    : ","Packets/sec      : ") + pps.ToString("0.#") + L.B("   (payload ","   (payload ") + payload.ToString("0") + " B/pkt)\r\n" +
                L.B("Dim. frame       : ","Frame size       : ") + frame.ToString("0") + " B  (payload + RTP12 + UDP8 + IP20 + L2 " + l2 + ")\r\n" +
                "──────────────────────────────────────────\r\n" +
                L.B("Banda 1 chiamata : ","1-call bandwidth : ") + perCall.ToString("0.0") + " kbps" + (chkVad.Checked?"  (VAD)":"") + "\r\n" +
                L.B("Banda ","Bandwidth ") + calls + L.B(" chiamate : "," calls : ") + total.ToString("0.0") + " kbps  (" + (total/1000.0).ToString("0.000") + " Mbps)";
        }

        ComboBox cmbMosCodec; TextBox txtDelay, txtJitter, txtLoss; Label lblMosOut;
        class MosCodec { public string Name; public double Ie, Bpl; public MosCodec(string n,double ie,double bpl){Name=n;Ie=ie;Bpl=bpl;} public override string ToString(){return Name;} }
        static readonly MosCodec[] MosCodecs = new[] {
            new MosCodec("G.711",      0, 25.1),
            new MosCodec("G.711+PLC",  0, 4.3),
            new MosCodec("G.729A",    11, 19.0),
            new MosCodec("G.723.1",   15, 16.1),
            new MosCodec("G.722",      0, 17.0),
            new MosCodec("iLBC",     11.0, 22.5),
        };

        TabPage BuildMosTab()
        {
            var page = new TabPage("🎚️  Budget MOS (E-model)") { BackColor = CBg };
            int x1=18, x2=210, y=22;
            page.Controls.Add(Lbl("Codec:", x1, y));
            cmbMosCodec = Combo(x2,y,160); cmbMosCodec.Items.AddRange(MosCodecs); cmbMosCodec.SelectedIndex=1; page.Controls.Add(cmbMosCodec); y+=38;
            page.Controls.Add(Lbl(L.B("Ritardo one-way (ms):","One-way delay (ms):"), x1, y)); txtDelay = Txt(x2,y,120); txtDelay.Text="40"; page.Controls.Add(txtDelay); y+=38;
            page.Controls.Add(Lbl("Jitter (ms):", x1, y)); txtJitter = Txt(x2,y,120); txtJitter.Text="10"; page.Controls.Add(txtJitter); y+=38;
            page.Controls.Add(Lbl("Packet loss (%):", x1, y)); txtLoss = Txt(x2,y,120); txtLoss.Text="1"; page.Controls.Add(txtLoss); y+=42;
            var btn = Btn(L.B("🎚️  Calcola MOS","🎚️  Compute MOS"), x2, y, 150); btn.Click += (s,e)=>CalcMos(); page.Controls.Add(btn); y+=48;
            lblMosOut = new Label { Location=new Point(x1,y), Size=new Size(640,170), ForeColor=Color.LimeGreen,
                Font=new Font("Consolas",10), BackColor=Color.FromArgb(12,16,24), BorderStyle=BorderStyle.FixedSingle, Padding=new Padding(8) };
            page.Controls.Add(lblMosOut);
            CalcMos();
            return page;
        }

        void CalcMos()
        {
            if (lblMosOut == null) return;
            var c = cmbMosCodec.SelectedItem as MosCodec; if (c==null) return;
            double delay, jit, loss;
            double.TryParse(txtDelay.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out delay);
            double.TryParse(txtJitter.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out jit);
            double.TryParse(txtLoss.Text,  NumberStyles.Any, CultureInfo.InvariantCulture, out loss);
            if (loss<0) loss=0; if (loss>100) loss=100;

            double Ta = delay + 2.0*jit;
            double Id = 0.024*Ta + 0.11*(Ta-177.3)*(Ta>177.3?1:0);
            double Ie_eff = c.Ie + (95.0 - c.Ie) * (loss / (loss + c.Bpl));
            double R = 93.2 - Id - Ie_eff;
            if (R<0) R=0; if (R>100) R=100;
            double mos = 1 + 0.035*R + R*(R-60)*(100-R)*7e-6;
            if (mos<1) mos=1; if (mos>4.5) mos=4.5;

            string verdict = mos>=4.3?L.B("Eccellente","Excellent"):mos>=4.0?L.B("Buona","Good"):mos>=3.6?L.B("Accettabile","Acceptable"):mos>=3.1?L.B("Mediocre (utenti lamentano)","Poor (users complain)"):L.B("Scarsa (inaccettabile)","Bad (unacceptable)");
            lblMosOut.Text =
                "Codec            : " + c.Name + "\r\n" +
                L.B("Ritardo eff.(Ta) : ","Eff. delay (Ta)  : ") + Ta.ToString("0") + L.B(" ms  (delay + 2×jitter)"," ms  (delay + 2×jitter)") + "\r\n" +
                L.B("Id (ritardo)     : ","Id (delay)       : ") + Id.ToString("0.0") + "\r\n" +
                "Ie-eff (loss)    : " + Ie_eff.ToString("0.0") + "\r\n" +
                "R-factor         : " + R.ToString("0.0") + "\r\n" +
                "──────────────────────────────────────────\r\n" +
                L.B("MOS stimato      : ","Estimated MOS    : ") + mos.ToString("0.00") + "   →  " + verdict;
        }

        TabPage BuildQosTab()
        {
            var page = new TabPage("🏷️  QoS / DSCP") { BackColor = CBg };
            var lv = new ListView { Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true, GridLines=true,
                BackColor=Color.FromArgb(18,18,30), ForeColor=Color.White };
            lv.Columns.Add(L.B("Uso","Use"), 230); lv.Columns.Add("DSCP", 110); lv.Columns.Add("Dec", 60); lv.Columns.Add("TOS/IP-Prec", 130); lv.Columns.Add(L.B("Note","Notes"), 220);
            string[][] rows = new[] {
                new[]{L.B("Voce RTP","RTP voice"),"EF","46","0xB8 / 5",L.B("Bearer audio (priorità max)","Audio bearer (top priority)")},
                new[]{L.B("Video interattivo","Interactive video"),"AF41","34","0x88 / 4","Telepresence/video"},
                new[]{L.B("Segnalazione SIP/call","SIP/call signaling"),"CS3","24","0x60 / 3","SIP/H.323/SCCP"},
                new[]{L.B("Segnalazione (alt.)","Signaling (alt.)"),"AF31","26","0x68 / 3",L.B("Alcuni vendor","Some vendors")},
                new[]{"Network control","CS6","48","0xC0 / 6","Routing/OSPF"},
                new[]{"Best effort","DF (BE)","0","0x00 / 0",L.B("Default dati","Default data")},
                new[]{L.B("Streaming/broadcast","Streaming/broadcast"),"AF21","18","0x48 / 2",L.B("Video non interattivo","Non-interactive video")},
            };
            foreach (var r in rows) { var it = new ListViewItem(r[0]); for(int i=1;i<r.Length;i++) it.SubItems.Add(r[i]); lv.Items.Add(it); }
            var hdr = new Label { Text=L.B("  Marcature QoS consigliate per Voice/UC (Cisco/Teams/AudioCodes)","  Recommended QoS marking for Voice/UC (Cisco/Teams/AudioCodes)"), Dock=DockStyle.Top, Height=26,
                ForeColor=Color.LightGray, BackColor=Color.FromArgb(30,30,45), TextAlign=ContentAlignment.MiddleLeft };
            page.Controls.Add(lv); page.Controls.Add(hdr);
            return page;
        }

        static Label Lbl(string t,int x,int y){ return new Label{ Text=t, Location=new Point(x,y+3), AutoSize=true, ForeColor=Color.LightGray }; }
        ComboBox Combo(int x,int y,int w){ return new ComboBox{ Location=new Point(x,y), Width=w, DropDownStyle=ComboBoxStyle.DropDownList, BackColor=CIn, ForeColor=Color.White, FlatStyle=FlatStyle.Flat }; }
        TextBox Txt(int x,int y,int w){ return new TextBox{ Location=new Point(x,y), Width=w, BackColor=CIn, ForeColor=Color.White, BorderStyle=BorderStyle.FixedSingle }; }
        Button Btn(string t,int x,int y,int w){ var b=new Button{ Text=t, Location=new Point(x,y), Width=w, Height=30, FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(40,80,140), ForeColor=Color.White, Font=new Font("Segoe UI",9,FontStyle.Bold)}; b.FlatAppearance.BorderSize=0; return b; }
    }
}
