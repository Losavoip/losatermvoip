# LosaTerm · Voip Terminal

**The Voice/UC engineer's multitool — one portable `.exe`, every vendor.**

A free, no-nonsense Windows tool that puts the things you reach for every day —
SBC/trunk diagnostics, SIP ladder diagrams, syslog capture, a SIP simulator and
a tabbed SSH/SFTP/SCP/FTP terminal — into a single window. Vendor-neutral by
design: the SIP / RTP / TLS / DNS tools work the same whether the box is **Cisco,
Microsoft Teams (Direct Routing), AudioCodes, Ribbon, Alcatel (OXE/OXO) or
Sangoma/Asterisk**.

> ⚠️ **Beta.** It works and it's already useful, but it's young — expect rough
> edges and please send feedback.

---

## ✨ Highlights

- **🩺 SBC Health** *(flagship)* — vendor-neutral trunk diagnostics, all native, no dependencies:
  - **SIP OPTIONS** test (UDP / TCP / TLS) — *"is the trunk up?"*
  - **TLS certificate** check — CN/SAN, **expiry**, TLS version, cipher (the #1 cause of Teams Direct Routing outages)
  - **DNS SRV / A** lookup — `_sip._tcp`, `_sips._tls`, …
- **🪜 SIP Ladder** — turn PCAPs and Cisco SDL/SDI traces into clean SIP ladder diagrams (grouped per Call-ID, like Wireshark VoIP Calls)
- **📡 Syslog server** — built-in UDP collector with live SIP filtering (AudioCodes / Cisco)
- **🔀 SIP Simulator** — craft and send SIP messages; cause-code translator; SDP/codec analyzer
- **🖥️ Terminal** — tabbed SSH / Telnet, plus SFTP / SCP / FTP and a built-in FTP/SFTP server
- **🌍 Multilingual UI** — Italian / English / French / German / Spanish (auto-detected)

## 📥 Download

Grab the latest portable build from the [**Releases**](../../releases) page, or
from the website: **https://losavoip.github.io**

It's portable: unzip and run `LosaTermVoip.exe`. No installer.

> The executable is not code-signed, so Windows SmartScreen may warn
> *"unknown app"* — that's normal for an independent project. Prefer to be sure?
> **Read the source here and build it yourself** (below).

## 🧩 Requirements

- Windows 10 / 11 (64-bit)
- .NET Framework 4.8 (already present on up-to-date Windows)
- *Optional:* **PuTTY** (for SSH/Telnet sessions), **Wireshark/tshark** (for PCAP analysis)

## 🔨 Build from source

No SDK or IDE needed — it compiles with the C# compiler shipped with .NET Framework:

```bat
build.bat
```

…or directly:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /win32icon:app.ico /out:LosaTermVoip.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll ^
  /r:System.Net.dll /r:System.Security.dll /r:System.Core.dll ^
  NetTerm.cs Analyzer.cs FtpSftpServer.cs Localization.cs AdvancedFeatures.cs Enhancements.cs SbcHealth.cs VoipCodes.cs NetTools.cs TftpServer.cs DhcpServer.cs SerialConsole.cs
```

## 🙋 About this project (full transparency)

After years of hands-on Voice/VoIP troubleshooting, I got tired of juggling five
different tools, so I built the one I always wished I had. I **designed it from
real field experience**, and I wrote the code **with the help of AI**. It's a
**free hobby project** — no company, no strings.

That's also why feedback matters so much: every bug report and feature idea
genuinely shapes where it goes. If it saves you a late-night debug session, that
already made it worth building. 🙂

## 💬 Feedback & contributing

- Found a bug or want a feature? Open an [**Issue**](../../issues), or use the feedback form on the [website](https://losavoip.github.io).
- PRs welcome — it's MIT, do your thing.

## ❤️ Support

It's free and stays free. If it's useful to you and you'd like to chip in:
**[paypal.me/DanieleLosapio](https://paypal.me/DanieleLosapio)** ☕

## 📜 License

[MIT](LICENSE) © 2026 Daniele Losapio

---

*Not affiliated with Cisco, Microsoft, AudioCodes, Ribbon, Alcatel, Sangoma or
TranslatorX. All trademarks belong to their respective owners.*
