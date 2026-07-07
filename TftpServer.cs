using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  TFTP Server (RFC 1350) nativo — per provisioning telefoni (opt 150),
    //  firmware, config. Supporta RRQ (download) e WRQ (upload), modo octet.
    //  Nessuna dipendenza esterna.
    // ════════════════════════════════════════════════════════════════════════
    public class TftpServer
    {
        UdpClient mainSock;
        Thread listenThread;
        volatile bool running;
        string root;

        public bool IsRunning { get { return running; } }
        public event Action<string> LogLine;
        void Log(string m) { var h = LogLine; if (h != null) h(m); }

        public void Start(int port, string rootDir)
        {
            if (running) return;
            root = rootDir;
            Directory.CreateDirectory(root);
            mainSock = new UdpClient();
            mainSock.ExclusiveAddressUse = false;
            mainSock.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            mainSock.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            running = true;
            listenThread = new Thread(ListenLoop) { IsBackground = true };
            listenThread.Start();
            Log(L.B("▶ TFTP in ascolto su UDP/","▶ TFTP listening on UDP/") + port + "   root = " + root);
        }

        public void Stop()
        {
            running = false;
            try { if (mainSock != null) mainSock.Close(); } catch { }
            mainSock = null;
            Log(L.B("⏹ TFTP fermato.","⏹ TFTP stopped."));
        }

        void ListenLoop()
        {
            while (running)
            {
                try
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] pkt = mainSock.Receive(ref ep);
                    var client = ep; var data = pkt;
                    ThreadPool.QueueUserWorkItem(delegate { Handle(data, client); });
                }
                catch { if (running) Thread.Sleep(50); else break; }
            }
        }

        void Handle(byte[] pkt, IPEndPoint client)
        {
            if (pkt.Length < 4) return;
            int op = (pkt[0] << 8) | pkt[1];
            int i = 2;
            var fn = new StringBuilder();
            while (i < pkt.Length && pkt[i] != 0) fn.Append((char)pkt[i++]);
            string filename = fn.ToString();
            string safe = SafePath(filename);
            if (safe == null) { SendError(client, 2, "Access violation"); Log("✗ path negato: " + filename); return; }

            if (op == 1)      { Log("⬇ RRQ '" + filename + "' da " + client.Address);  SendFile(client, safe, filename); }
            else if (op == 2) { Log("⬆ WRQ '" + filename + "' da " + client.Address);  RecvFile(client, safe, filename); }
        }

        // Impedisce path traversal: il file deve restare dentro la root.
        string SafePath(string filename)
        {
            try
            {
                filename = filename.Replace('/', '\\').TrimStart('\\');
                string full = Path.GetFullPath(Path.Combine(root, filename));
                string rootFull = Path.GetFullPath(root).TrimEnd('\\') + "\\";
                if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(full, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                    return null;
                return full;
            }
            catch { return null; }
        }

        void SendFile(IPEndPoint client, string path, string name)
        {
            if (!File.Exists(path)) { SendError(client, 1, "File not found"); Log(L.B("✗ non trovato: ","✗ not found: ") + name); return; }
            try
            {
                byte[] file = File.ReadAllBytes(path);
                using (var sock = new UdpClient(0))   // porta effimera (TID server)
                {
                    sock.Client.ReceiveTimeout = 2000;
                    sock.Connect(client);
                    int block = 1, offset = 0;
                    while (true)
                    {
                        int len = Math.Min(512, file.Length - offset);
                        byte[] data = new byte[4 + len];
                        data[0] = 0; data[1] = 3; data[2] = (byte)(block >> 8); data[3] = (byte)(block & 0xFF);
                        Array.Copy(file, offset, data, 4, len);
                        bool acked = false;
                        for (int retry = 0; retry < 5 && !acked; retry++)
                        {
                            sock.Send(data, data.Length);
                            try
                            {
                                var rep = new IPEndPoint(IPAddress.Any, 0);
                                byte[] ack = sock.Receive(ref rep);
                                if (ack.Length >= 4 && ack[1] == 4 && ((ack[2] << 8) | ack[3]) == block) acked = true;
                            }
                            catch { }
                        }
                        if (!acked) { Log(L.B("✗ timeout sul blocco ","✗ timeout on block ") + block + L.B(" di '"," of '") + name + "'"); return; }
                        offset += len; block++;
                        if (len < 512) break;   // ultimo blocco
                    }
                    Log(L.B("✔ inviato '","✔ sent '") + name + "'  (" + file.Length + L.B(" byte)"," bytes)"));
                }
            }
            catch (Exception ex) { SendError(client, 0, ex.Message); Log(L.B("✗ errore invio: ","✗ send error: ") + ex.Message); }
        }

        void RecvFile(IPEndPoint client, string path, string name)
        {
            try
            {
                using (var sock = new UdpClient(0))
                {
                    sock.Client.ReceiveTimeout = 3000;
                    sock.Connect(client);
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    using (var fs = File.Create(path))
                    {
                        int block = 0;
                        SendAck(sock, 0);   // ACK della WRQ
                        while (true)
                        {
                            var rep = new IPEndPoint(IPAddress.Any, 0);
                            byte[] data;
                            try { data = sock.Receive(ref rep); } catch { Log(L.B("✗ timeout ricezione '","✗ receive timeout '") + name + "'"); return; }
                            if (data.Length >= 4 && data[1] == 3)
                            {
                                int b = (data[2] << 8) | data[3];
                                if (b == block + 1)
                                {
                                    fs.Write(data, 4, data.Length - 4);
                                    block = b;
                                    SendAck(sock, block);
                                    if (data.Length - 4 < 512) break;   // ultimo blocco
                                }
                                else SendAck(sock, block);   // duplicato → ri-ack
                            }
                        }
                    }
                    Log(L.B("✔ ricevuto '","✔ received '") + name + "'");
                }
            }
            catch (Exception ex) { SendError(client, 0, ex.Message); Log(L.B("✗ errore ricezione: ","✗ receive error: ") + ex.Message); }
        }

        static void SendAck(UdpClient sock, int block)
        {
            byte[] ack = { 0, 4, (byte)(block >> 8), (byte)(block & 0xFF) };
            sock.Send(ack, ack.Length);
        }

        void SendError(IPEndPoint client, int code, string msg)
        {
            try
            {
                using (var sock = new UdpClient(0))
                {
                    sock.Connect(client);
                    var b = new List<byte> { 0, 5, (byte)(code >> 8), (byte)(code & 0xFF) };
                    foreach (char c in msg) b.Add((byte)c);
                    b.Add(0);
                    sock.Send(b.ToArray(), b.Count);
                }
            }
            catch { }
        }
    }
}
