using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class SpacebarTimer : ApplicationContext
{
    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    const byte VK_SPACE = 0x20;
    const uint KEYEVENTF_KEYDOWN = 0x0000;
    const uint KEYEVENTF_KEYUP   = 0x0002;

    NotifyIcon trayIcon;
    Timer timer;
    int intervalMinutes = 5;

    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new SpacebarTimer());
    }

    public SpacebarTimer()
    {
        timer = new Timer();
        timer.Tick += OnTick;
        SetInterval(5);

        trayIcon = new NotifyIcon();
        trayIcon.Icon    = SystemIcons.Application;
        trayIcon.Text    = "SpacebarTimer - ogni 5 min";
        trayIcon.Visible = true;
        trayIcon.ContextMenuStrip = BuildMenu();
        trayIcon.MouseDoubleClick += (s, e) => ShowStatus();
    }

    ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var intervalMenu = new ToolStripMenuItem("Intervallo");
        int[] options = { 5, 10, 30, 60, 120, 240 };
        foreach (int min in options)
        {
            int m = min;
            string label = min < 60
                ? string.Format("{0} minuti", min)
                : string.Format("{0} ora/e", min / 60);
            var item = new ToolStripMenuItem(label);
            item.Click += (s, e) => {
                SetInterval(m);
                UpdateMenuChecks(intervalMenu, (ToolStripMenuItem)s);
            };
            if (min == 5) item.Checked = true;
            intervalMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(intervalMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Stato", null, (s, e) => ShowStatus());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci", null, (s, e) => {
            trayIcon.Visible = false;
            Application.Exit();
        });

        return menu;
    }

    void UpdateMenuChecks(ToolStripMenuItem parent, ToolStripMenuItem selected)
    {
        foreach (ToolStripMenuItem item in parent.DropDownItems)
            item.Checked = (item == selected);
    }

    void SetInterval(int minutes)
    {
        intervalMinutes = minutes;
        timer.Stop();
        timer.Interval = minutes * 60 * 1000;
        timer.Start();
        if (trayIcon != null)
        {
            trayIcon.Text = minutes < 60
                ? string.Format("SpacebarTimer - ogni {0} min", minutes)
                : string.Format("SpacebarTimer - ogni {0}h", minutes / 60);
        }
    }

    void OnTick(object sender, EventArgs e)
    {
        keybd_event(VK_SPACE, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        keybd_event(VK_SPACE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        trayIcon.ShowBalloonTip(2000, "SpacebarTimer",
            string.Format("Spazio premuto alle {0:HH:mm:ss}", DateTime.Now),
            ToolTipIcon.Info);
    }

    void ShowStatus()
    {
        string msg = intervalMinutes < 60
            ? string.Format("Intervallo attivo: ogni {0} minuti", intervalMinutes)
            : string.Format("Intervallo attivo: ogni {0} ora/e", intervalMinutes / 60);
        MessageBox.Show(msg, "SpacebarTimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
