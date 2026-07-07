using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  DHCP Server (RFC 2131) nativo — SOLO PER LAB / banco provisioning.
    //  Distribuisce IP + subnet + gateway + DNS + option 150/66 (TFTP) ai
    //  telefoni. ⚠ PERICOLOSO su reti di produzione: un DHCP "ribelle" causa
    //  disservizi. Spento di default, avvio con conferma esplicita.
    // ════════════════════════════════════════════════════════════════════════
    public class DhcpServer
    {
        UdpClient sock;
        Thread listenThread;
        volatile bool running;
        IPAddress localIp, mask, gateway, dns, tftp;
        uint poolStart, poolEnd, nextIp;
        int leaseSec;
        readonly Dictionary<string, uint> leases = new Dictionary<string, uint>(); // MAC → IP

        public bool IsRunning { get { return running; } }
        public event Action<string> LogLine;
        void Log(string m) { var h = LogLine; if (h != null) h(m); }

        public void Start(string localIpStr, string poolStartStr, string poolEndStr, string maskStr,
                          string gwStr, string dnsStr, string tftpStr, int leaseSeconds)
        {
            if (running) return;
            localIp  = IPAddress.Parse(localIpStr);
            mask     = IPAddress.Parse(maskStr);
            gateway  = string.IsNullOrEmpty(gwStr)   ? null : IPAddress.Parse(gwStr);
            dns      = string.IsNullOrEmpty(dnsStr)  ? null : IPAddress.Parse(dnsStr);
            tftp     = string.IsNullOrEmpty(tftpStr) ? null : IPAddress.Parse(tftpStr);
            poolStart = ToUint(IPAddress.Parse(poolStartStr));
            poolEnd   = ToUint(IPAddress.Parse(poolEndStr));
            nextIp    = poolStart;
            leaseSec  = leaseSeconds;
            leases.Clear();

            sock = new UdpClient();
            sock.ExclusiveAddressUse = false;
            sock.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sock.EnableBroadcast = true;
            sock.Client.Bind(new IPEndPoint(IPAddress.Any, 67));   // Any per ricevere le DISCOVER in broadcast
            running = true;
            listenThread = new Thread(Loop) { IsBackground = true };
            listenThread.Start();
            Log(L.B("▶ DHCP LAB attivo — server ","▶ DHCP LAB active — server ") + localIp + " (UDP/67). Pool " + poolStartStr + " ÷ " + poolEndStr +
                (tftp != null ? "  opt150=" + tftp : ""));
        }

        public void Stop()
        {
            running = false;
            try { if (sock != null) sock.Close(); } catch { }
            sock = null;
            Log(L.B("⏹ DHCP fermato.","⏹ DHCP stopped."));
        }

        void Loop()
        {
            while (running)
            {
                try
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] pkt = sock.Receive(ref ep);
                    Handle(pkt);
                }
                catch { if (running) Thread.Sleep(50); else break; }
            }
        }

        void Handle(byte[] p)
        {
            if (p.Length < 240 || p[0] != 1) return;                       // dev'essere una BOOTREQUEST
            if (!(p[236] == 0x63 && p[237] == 0x82 && p[238] == 0x53 && p[239] == 0x63)) return; // magic cookie

            byte[] macB = new byte[6]; Array.Copy(p, 28, macB, 0, 6);
            string mac = BitConverter.ToString(macB);

            int msgType = 0;
            int i = 240;
            while (i < p.Length && p[i] != 255)
            {
                int opt = p[i++]; if (opt == 0) continue;
                if (i >= p.Length) break;
                int len = p[i++];
                if (opt == 53 && len >= 1) msgType = p[i];
                i += len;
            }

            if (msgType == 1)        // DISCOVER → OFFER
            {
                uint ip = Assign(mac);
                Log("DISCOVER " + mac + " → OFFER " + FromUint(ip));
                SendReply(p, ip, 2);
            }
            else if (msgType == 3)   // REQUEST → ACK
            {
                uint ip = Assign(mac);
                Log("REQUEST " + mac + " → ACK " + FromUint(ip));
                SendReply(p, ip, 5);
            }
            else if (msgType == 7) Log("RELEASE " + mac);
        }

        uint Assign(string mac)
        {
            if (leases.ContainsKey(mac)) return leases[mac];
            uint ip = nextIp;
            while (ip <= poolEnd && ContainsValue(ip)) ip++;
            if (ip > poolEnd) ip = poolStart;   // pool esaurito (lab): riusa
            leases[mac] = ip;
            nextIp = (ip + 1 <= poolEnd) ? ip + 1 : poolStart;
            return ip;
        }

        bool ContainsValue(uint ip) { foreach (var v in leases.Values) if (v == ip) return true; return false; }

        void SendReply(byte[] req, uint yiaddr, int msgType)
        {
            try
            {
                byte[] r = new byte[300];
                r[0] = 2; r[1] = 1; r[2] = 6; r[3] = 0;     // BOOTREPLY, ethernet, hlen 6
                Array.Copy(req, 4, r, 4, 4);                 // xid
                r[10] = req[10]; r[11] = req[11];            // flags (mantieni bit broadcast)
                PutUint(r, 16, yiaddr);                      // yiaddr
                PutUint(r, 20, ToUint(localIp));             // siaddr
                Array.Copy(req, 28, r, 28, 16);              // chaddr
                r[236] = 0x63; r[237] = 0x82; r[238] = 0x53; r[239] = 0x63;

                int o = 240;
                r[o++] = 53; r[o++] = 1; r[o++] = (byte)msgType;          // message type
                r[o++] = 54; r[o++] = 4; PutUint(r, o, ToUint(localIp)); o += 4; // server id
                r[o++] = 51; r[o++] = 4;
                r[o++] = (byte)(leaseSec >> 24); r[o++] = (byte)(leaseSec >> 16);
                r[o++] = (byte)(leaseSec >> 8);  r[o++] = (byte)leaseSec; // lease
                r[o++] = 1;  r[o++] = 4; PutUint(r, o, ToUint(mask)); o += 4;    // subnet mask
                if (gateway != null) { r[o++] = 3; r[o++] = 4; PutUint(r, o, ToUint(gateway)); o += 4; }
                if (dns     != null) { r[o++] = 6; r[o++] = 4; PutUint(r, o, ToUint(dns));     o += 4; }
                if (tftp    != null)
                {
                    r[o++] = 150; r[o++] = 4; PutUint(r, o, ToUint(tftp)); o += 4;   // Cisco opt 150
                    string t = tftp.ToString();
                    r[o++] = 66; r[o++] = (byte)t.Length; foreach (char c in t) r[o++] = (byte)c; // opt 66
                }
                r[o++] = 255;                                // end

                sock.Send(r, o, new IPEndPoint(IPAddress.Broadcast, 68));
            }
            catch (Exception ex) { Log(L.B("✗ errore invio reply: ","✗ reply send error: ") + ex.Message); }
        }

        static uint ToUint(IPAddress ip) { byte[] b = ip.GetAddressBytes(); return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3]; }
        static string FromUint(uint v) { return new IPAddress(new byte[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }).ToString(); }
        static void PutUint(byte[] a, int o, uint v) { a[o] = (byte)(v >> 24); a[o + 1] = (byte)(v >> 16); a[o + 2] = (byte)(v >> 8); a[o + 3] = (byte)v; }
    }
}
