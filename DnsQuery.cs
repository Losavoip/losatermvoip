using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Helper DNS condiviso (dnsapi.dll, resolver nativo Windows).
    //  Usato da DnsVoipPanel e da HealthCheckPanel. Nessuna dipendenza.
    // ════════════════════════════════════════════════════════════════════════
    public static class DnsQ
    {
        public class NaptrRec { public ushort Order, Pref; public string Flags, Service, Regexp, Replacement; }
        public struct SrvRec  { public ushort Priority, Weight, Port; public string Target; }

        const int DnsFreeRecordList = 1;
        [DllImport("dnsapi.dll", EntryPoint="DnsQuery_W", CharSet=CharSet.Unicode, SetLastError=true)]
        static extern int DnsQuery_W(string name, ushort type, uint options, IntPtr extra, ref IntPtr results, IntPtr reserved);
        [DllImport("dnsapi.dll")]
        static extern void DnsRecordListFree(IntPtr recordList, int freeType);

        [StructLayout(LayoutKind.Sequential)]
        struct DnsHeader { public IntPtr pNext; public IntPtr pName; public ushort wType; public ushort wDataLength; public uint flags; public uint dwTtl; public uint dwReserved; }

        static int HdrSize { get { return Marshal.SizeOf(typeof(DnsHeader)); } }

        // A(1)/AAAA(28)/NS(2)/MX(15)/TXT(16)/CNAME(5)
        public static List<string> Query(string name, ushort type)
        {
            var list = new List<string>();
            IntPtr results = IntPtr.Zero;
            if (DnsQuery_W(name, type, 0, IntPtr.Zero, ref results, IntPtr.Zero) != 0) return list;
            try
            {
                int H = HdrSize, P = IntPtr.Size;
                IntPtr ptr = results;
                while (ptr != IntPtr.Zero)
                {
                    var h = (DnsHeader)Marshal.PtrToStructure(ptr, typeof(DnsHeader));
                    if (h.wType == type)
                    {
                        if (type == 1) { uint ip = (uint)Marshal.ReadInt32(ptr, H); list.Add(new IPAddress(BitConverter.GetBytes(ip)).ToString()); }
                        else if (type == 28) { byte[] b = new byte[16]; Marshal.Copy(new IntPtr(ptr.ToInt64()+H), b, 0, 16); list.Add(new IPAddress(b).ToString()); }
                        else if (type == 2 || type == 5) { list.Add(Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, H)) ?? ""); }
                        else if (type == 15) { string ex = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, H)) ?? ""; ushort pref = (ushort)Marshal.ReadInt16(ptr, H + P); list.Add(pref + " " + ex); }
                        else if (type == 16) { int cnt = Marshal.ReadInt32(ptr, H); int sp = H + (P==8?8:4); var parts=new List<string>(); for (int i=0;i<cnt;i++){ var s=Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, sp + i*P)); if(s!=null) parts.Add(s);} list.Add("\"" + string.Join(" ", parts.ToArray()) + "\""); }
                    }
                    ptr = h.pNext;
                }
            }
            catch { }
            finally { DnsRecordListFree(results, DnsFreeRecordList); }
            return list;
        }

        public static List<SrvRec> Srv(string name, out string err)
        {
            err = null;
            var list = new List<SrvRec>();
            IntPtr results = IntPtr.Zero;
            int ret = DnsQuery_W(name, 33, 0, IntPtr.Zero, ref results, IntPtr.Zero);
            if (ret != 0) { if (ret==9501) return list; if (ret==9003){ err="NXDOMAIN"; return list; } err="errore DNS " + ret; return list; }
            try
            {
                int H = HdrSize, P = IntPtr.Size;
                IntPtr ptr = results;
                while (ptr != IntPtr.Zero)
                {
                    var h = (DnsHeader)Marshal.PtrToStructure(ptr, typeof(DnsHeader));
                    if (h.wType == 33)
                    {
                        var r = new SrvRec();
                        r.Target   = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, H)) ?? "";
                        r.Priority = (ushort)Marshal.ReadInt16(ptr, H + P);
                        r.Weight   = (ushort)Marshal.ReadInt16(ptr, H + P + 2);
                        r.Port     = (ushort)Marshal.ReadInt16(ptr, H + P + 4);
                        list.Add(r);
                    }
                    ptr = h.pNext;
                }
            }
            catch (Exception ex) { err = ex.Message; }
            finally { DnsRecordListFree(results, DnsFreeRecordList); }
            return list;
        }

        public static List<NaptrRec> Naptr(string name, out string err)
        {
            err = null;
            var list = new List<NaptrRec>();
            IntPtr results = IntPtr.Zero;
            int ret = DnsQuery_W(name, 35, 0, IntPtr.Zero, ref results, IntPtr.Zero);
            if (ret != 0) { if (ret==9501) return list; if (ret==9003){ err="NXDOMAIN"; return list; } err="errore DNS " + ret; return list; }
            try
            {
                int H = HdrSize, P = IntPtr.Size;
                int pb = H + (P==8?8:4);
                IntPtr ptr = results;
                while (ptr != IntPtr.Zero)
                {
                    var h = (DnsHeader)Marshal.PtrToStructure(ptr, typeof(DnsHeader));
                    if (h.wType == 35)
                    {
                        var n = new NaptrRec();
                        n.Order = (ushort)Marshal.ReadInt16(ptr, H);
                        n.Pref  = (ushort)Marshal.ReadInt16(ptr, H + 2);
                        n.Flags       = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, pb)) ?? "";
                        n.Service     = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, pb + P)) ?? "";
                        n.Regexp      = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, pb + 2*P)) ?? "";
                        n.Replacement = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr, pb + 3*P)) ?? "";
                        list.Add(n);
                    }
                    ptr = h.pNext;
                }
            }
            catch (Exception ex) { err = ex.Message; }
            finally { DnsRecordListFree(results, DnsFreeRecordList); }
            return list;
        }
    }
}
