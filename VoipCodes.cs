using System.Collections.Generic;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Conoscenza condivisa: codici di risposta SIP + cause Q.850, con
    //  significato, causa tipica e "cosa controllare". Usato sia dal
    //  Cause Code Translator sia dall'auto-diagnosi del Ladder.
    //  (Testi bilingui IT/EN via L.B — la lingua è fissata all'avvio.)
    // ════════════════════════════════════════════════════════════════════════
    public static class VoipCodes
    {
        // code -> { nome, significato, cosa controllare }
        public static readonly Dictionary<int, string[]> Sip = new Dictionary<int, string[]>
        {
            // 1xx provvisorie (non sono fallimenti)
            { 100, new[]{ "Trying",               L.B("Richiesta ricevuta, in elaborazione.","Request received, processing."), L.B("Normale.","Normal.") } },
            { 180, new[]{ "Ringing",              L.B("Il chiamato sta squillando (ringback locale, di solito senza SDP).","The called party is ringing (local ringback, usually without SDP)."), L.B("Normale: il ringback è generato localmente dal chiamante.","Normal: ringback is generated locally by the caller.") } },
            { 181, new[]{ "Call Is Being Forwarded",L.B("Chiamata in inoltro.","Call is being forwarded."), L.B("Normale.","Normal.") } },
            { 182, new[]{ "Queued",               L.B("Chiamata in coda.","Call queued."), L.B("Normale (ACD/coda).","Normal (ACD/queue).") } },
            { 183, new[]{ "Session Progress",     L.B("Avanzamento sessione con early media (SDP presente).","Session progress with early media (SDP present)."), L.B("Il ringback/annuncio arriva IN-BAND dal lato remoto. Se l'utente non sente nulla → verifica il taglio dell'early media (P-Early-Media), il percorso RTP e la direzione media.","Ringback/announcement arrives IN-BAND from the remote side. If the user hears nothing → check early-media cut (P-Early-Media), the RTP path and media direction.") } },
            // 4xx errori client
            { 400, new[]{ "Bad Request",          L.B("Messaggio SIP malformato.","Malformed SIP message."), L.B("Header mancanti/errati; un SBC che riscrive male i messaggi; UA difettoso.","Missing/wrong headers; an SBC rewriting messages badly; a faulty UA.") } },
            { 401, new[]{ "Unauthorized",         L.B("Richiesta autenticazione (challenge).","Authentication required (challenge)."), L.B("Normale una volta (seguito da nuovo INVITE con credenziali). Se ripetuto → utente/password o registrar errati.","Normal once (followed by a new INVITE with credentials). If repeated → wrong user/password or registrar.") } },
            { 402, new[]{ "Payment Required",     L.B("Pagamento richiesto (raro).","Payment required (rare)."), L.B("Policy del provider.","Provider policy.") } },
            { 403, new[]{ "Forbidden",            L.B("Richiesta rifiutata per policy.","Request rejected by policy."), L.B("Trunk non autorizzato, IP sorgente non in whitelist, calling/called number bloccato, classe di servizio.","Unauthorized trunk, source IP not whitelisted, blocked calling/called number, class of service.") } },
            { 404, new[]{ "Not Found",            L.B("Numero/utente inesistente o non instradabile.","Non-existent or unroutable number/user."), L.B("Routing/dial-peer, numero chiamato, normalizzazione cifre, piano numerico.","Routing/dial-peer, called number, digit normalization, numbering plan.") } },
            { 405, new[]{ "Method Not Allowed",   L.B("Metodo non consentito.","Method not allowed."), L.B("Il peer non accetta quel metodo (es. INFO/UPDATE).","The peer doesn't accept that method (e.g. INFO/UPDATE).") } },
            { 406, new[]{ "Not Acceptable",       L.B("Contenuto non accettabile.","Content not acceptable."), L.B("Accept header / formato non supportato.","Accept header / unsupported format.") } },
            { 407, new[]{ "Proxy Authentication Required",L.B("Challenge dal proxy.","Challenge from the proxy."), L.B("Come 401 ma lato proxy: credenziali del trunk.","Like 401 but on the proxy side: trunk credentials.") } },
            { 408, new[]{ "Request Timeout",      L.B("Nessuna risposta entro il timeout.","No response within the timeout."), L.B("Peer irraggiungibile, firewall, NAT, OPTIONS keepalive falliti, percorso di rete.","Unreachable peer, firewall, NAT, failed OPTIONS keepalive, network path.") } },
            { 410, new[]{ "Gone",                 L.B("Numero non più disponibile.","Number no longer available."), L.B("Numero cambiato o disattivato.","Number changed or deactivated.") } },
            { 413, new[]{ "Request Entity Too Large",L.B("Messaggio troppo grande.","Message too large."), L.B("MTU/frammentazione UDP; valutare TCP/TLS.","UDP MTU/fragmentation; consider TCP/TLS.") } },
            { 414, new[]{ "Request-URI Too Long", L.B("Request-URI troppo lungo.","Request-URI too long."), L.B("URI anomalo.","Abnormal URI.") } },
            { 415, new[]{ "Unsupported Media Type",L.B("Tipo di media non supportato.","Unsupported media type."), L.B("Codec/SDP non gestito dal peer.","Codec/SDP not handled by the peer.") } },
            { 416, new[]{ "Unsupported URI Scheme",L.B("Schema URI non supportato.","Unsupported URI scheme."), L.B("sip vs sips vs tel.","sip vs sips vs tel.") } },
            { 420, new[]{ "Bad Extension",        L.B("Estensione SIP non supportata.","Unsupported SIP extension."), L.B("Header Require/Proxy-Require non gestito dal peer.","Require/Proxy-Require header not handled by the peer.") } },
            { 421, new[]{ "Extension Required",   L.B("Estensione richiesta.","Extension required."), L.B("Il peer richiede un'estensione non offerta.","The peer requires an extension that wasn't offered.") } },
            { 423, new[]{ "Interval Too Brief",   L.B("Expires troppo breve (REGISTER/SUBSCRIBE).","Expires too short (REGISTER/SUBSCRIBE)."), L.B("Aumentare l'intervallo di registrazione.","Increase the registration interval.") } },
            { 480, new[]{ "Temporarily Unavailable",L.B("Utente non risponde o non disponibile.","User not answering or unavailable."), L.B("Telefono non registrato, DND, ring-no-answer timeout, dispositivo offline.","Phone not registered, DND, ring-no-answer timeout, device offline.") } },
            { 481, new[]{ "Call/Transaction Does Not Exist",L.B("Dialog/transazione inesistente.","Non-existent dialog/transaction."), L.B("ACK/BYE su dialog già chiuso, mismatch di tag/Call-ID, race condition.","ACK/BYE on an already-closed dialog, tag/Call-ID mismatch, race condition.") } },
            { 482, new[]{ "Loop Detected",        L.B("Loop di instradamento.","Routing loop."), L.B("Routing circolare tra proxy/SBC.","Circular routing between proxies/SBCs.") } },
            { 483, new[]{ "Too Many Hops",        L.B("Max-Forwards a 0.","Max-Forwards reached 0."), L.B("Loop o catena troppo lunga; controllare il routing.","Loop or too-long chain; check routing.") } },
            { 484, new[]{ "Address Incomplete",   L.B("Numero incompleto.","Incomplete number."), L.B("Overlap dialing, cifre mancanti, piano numerico/interdigit timeout.","Overlap dialing, missing digits, numbering plan/interdigit timeout.") } },
            { 485, new[]{ "Ambiguous",            L.B("Numero ambiguo.","Ambiguous number."), L.B("Più match possibili.","Multiple possible matches.") } },
            { 486, new[]{ "Busy Here",            L.B("Occupato.","Busy."), L.B("Linea occupata. Normale.","Line busy. Normal.") } },
            { 487, new[]{ "Request Terminated",   L.B("INVITE annullato (a seguito di CANCEL).","INVITE cancelled (following a CANCEL)."), L.B("Il chiamante ha riagganciato prima della risposta. Normale.","The caller hung up before the answer. Normal.") } },
            { 488, new[]{ "Not Acceptable Here",  L.B("Negoziazione media fallita.","Media negotiation failed."), L.B("CODEC MISMATCH: confronta i codec offerti (m=/a=rtpmap) con quelli supportati dal peer; verifica SRTP/crypto, ptime, transcoding mancante, indirizzo/porta media.","CODEC MISMATCH: compare offered codecs (m=/a=rtpmap) with those supported by the peer; check SRTP/crypto, ptime, missing transcoding, media address/port.") } },
            { 491, new[]{ "Request Pending",      L.B("Re-INVITE in conflitto.","Conflicting re-INVITE."), L.B("Glare: due re-INVITE simultanei.","Glare: two simultaneous re-INVITEs.") } },
            { 493, new[]{ "Undecipherable",       L.B("Corpo S/MIME non decifrabile.","Undecipherable S/MIME body."), L.B("Cifratura SDP (raro).","SDP encryption (rare).") } },
            // 5xx errori server
            { 500, new[]{ "Server Internal Error",L.B("Errore interno del server.","Server internal error."), L.B("Bug/sovraccarico SBC/PBX; controllare i log del peer.","Bug/overload on SBC/PBX; check the peer logs.") } },
            { 501, new[]{ "Not Implemented",      L.B("Funzionalità non implementata.","Feature not implemented."), L.B("Metodo/feature non supportata dal peer.","Method/feature not supported by the peer.") } },
            { 502, new[]{ "Bad Gateway",          L.B("Gateway a valle non raggiungibile.","Downstream gateway unreachable."), L.B("Trunk/route a valle giù o mal configurato.","Downstream trunk/route down or misconfigured.") } },
            { 503, new[]{ "Service Unavailable",  L.B("Servizio non disponibile / congestione.","Service unavailable / congestion."), L.B("Sovraccarico, manutenzione, nessun circuito, trunk OOS, licenze esaurite.","Overload, maintenance, no circuit, trunk OOS, licenses exhausted.") } },
            { 504, new[]{ "Server Time-out",      L.B("Timeout verso un nodo a monte.","Timeout towards an upstream node."), L.B("Il peer a monte non risponde.","The upstream peer isn't responding.") } },
            { 505, new[]{ "Version Not Supported",L.B("Versione SIP non supportata.","Unsupported SIP version."), L.B("Anomalia di versione.","Version anomaly.") } },
            { 513, new[]{ "Message Too Large",    L.B("Messaggio troppo grande.","Message too large."), L.B("Valutare TCP/TLS al posto di UDP.","Consider TCP/TLS instead of UDP.") } },
            { 580, new[]{ "Precondition Failure", L.B("Precondizioni QoS non soddisfatte.","QoS preconditions not met."), L.B("SDP precondition (raro).","SDP precondition (rare).") } },
            // 6xx fallimenti globali
            { 600, new[]{ "Busy Everywhere",      L.B("Occupato ovunque.","Busy everywhere."), L.B("Tutte le destinazioni occupate.","All destinations busy.") } },
            { 603, new[]{ "Decline",              L.B("Chiamata rifiutata dall'utente.","Call declined by the user."), L.B("Reject manuale, DND, policy.","Manual reject, DND, policy.") } },
            { 604, new[]{ "Does Not Exist Anywhere",L.B("Destinazione inesistente.","Non-existent destination."), L.B("Numero non presente su alcun nodo.","Number not present on any node.") } },
            { 606, new[]{ "Not Acceptable",       L.B("Sessione non accettabile.","Session not acceptable."), L.B("Capacità/banda/media non compatibili.","Incompatible capabilities/bandwidth/media.") } },
        };

        // Cause Q.850 più comuni -> { significato, cosa controllare }
        public static readonly Dictionary<int, string[]> Q850 = new Dictionary<int, string[]>
        {
            { 1,   new[]{ "Unallocated number",                 L.B("Numero non assegnato: routing/piano numerico.","Unallocated number: routing/numbering plan.") } },
            { 16,  new[]{ "Normal call clearing",               L.B("Chiusura normale. Nessun problema.","Normal clearing. No problem.") } },
            { 17,  new[]{ "User busy",                          L.B("Occupato.","Busy.") } },
            { 18,  new[]{ "No user responding",                 L.B("Ring timeout: il chiamato non risponde a livello di rete.","Ring timeout: the called party isn't responding at network level.") } },
            { 19,  new[]{ "No answer from user",                L.B("Nessuna risposta entro il tempo (alerting).","No answer within the time (alerting).") } },
            { 21,  new[]{ "Call rejected",                      L.B("Rifiutata: policy/CoS/blocco numero.","Rejected: policy/CoS/number block.") } },
            { 27,  new[]{ "Destination out of order",           L.B("Destinazione fuori servizio.","Destination out of service.") } },
            { 28,  new[]{ "Invalid number format",             L.B("Numero incompleto/non valido: normalizzazione cifre.","Incomplete/invalid number: digit normalization.") } },
            { 31,  new[]{ "Normal, unspecified",               L.B("Chiusura normale non specificata.","Normal clearing, unspecified.") } },
            { 34,  new[]{ "No circuit/channel available",      L.B("Nessun circuito libero: capacità trunk/congestione.","No free circuit: trunk capacity/congestion.") } },
            { 38,  new[]{ "Network out of order",              L.B("Rete fuori servizio.","Network out of service.") } },
            { 41,  new[]{ "Temporary failure",                L.B("Guasto temporaneo.","Temporary failure.") } },
            { 42,  new[]{ "Switching equipment congestion",   L.B("Congestione apparati.","Switching equipment congestion.") } },
            { 44,  new[]{ "Requested circuit unavailable",    L.B("Circuito richiesto non disponibile.","Requested circuit unavailable.") } },
            { 47,  new[]{ "Resource unavailable",             L.B("Risorsa non disponibile: spesso congestione/licenze.","Resource unavailable: often congestion/licenses.") } },
            { 58,  new[]{ "Bearer capability not available",  L.B("Capacità di trasporto non disponibile (codec/bearer).","Bearer capability not available (codec/bearer).") } },
            { 65,  new[]{ "Bearer capability not implemented",L.B("Codec/bearer non implementato.","Codec/bearer not implemented.") } },
            { 88,  new[]{ "Incompatible destination",         L.B("Destinazione incompatibile: codec/bearer mismatch.","Incompatible destination: codec/bearer mismatch.") } },
            { 102, new[]{ "Recovery on timer expiry",         L.B("Timeout protocollo: nessuna risposta in tempo.","Protocol timeout: no response in time.") } },
            { 111, new[]{ "Protocol error",                   L.B("Errore di protocollo.","Protocol error.") } },
            { 127, new[]{ "Interworking, unspecified",        L.B("Interworking non specificato (gateway tra reti).","Interworking, unspecified (gateway between networks).") } },
        };

        public static bool IsFailureCode(int code) { return code >= 400; }

        // "NNN Nome — significato"  (riga compatta per il ladder)
        public static string ShortSip(int code)
        {
            string[] v;
            if (Sip.TryGetValue(code, out v)) return code + " " + v[0] + " — " + v[1];
            return code.ToString();
        }

        // Cheat-sheet comandi debug/logging per vendor: { nome, comandi }
        public static readonly string[][] DebugCmds = new string[][]
        {
            new[] { "Cisco CUBE / IOS Gateway",
                "! --- Enable session output ---\r\n" +
                "terminal monitor            ! show debugs in SSH/Telnet\r\n" +
                "terminal no monitor         ! to disable it\r\n\r\n" +
                "! --- SIP ---\r\n" +
                "debug ccsip messages        ! full SIP messages (IN/OUT)\r\n" +
                "debug ccsip error           ! errors only\r\n" +
                "debug ccsip events\r\n" +
                "debug ccsip info\r\n\r\n" +
                "! --- Call control / dial-peer ---\r\n" +
                "debug voip ccapi inout      ! call flow + cause code\r\n" +
                "debug voip dialpeer         ! dial-peer matching\r\n\r\n" +
                "! --- ISDN / TDM ---\r\n" +
                "debug isdn q931             ! ISDN signaling (Q.850 causes)\r\n\r\n" +
                "! --- Status ---\r\n" +
                "show call active voice brief\r\n" +
                "show sip-ua calls\r\n" +
                "show dial-peer voice summary\r\n\r\n" +
                "! --- STOP ---\r\n" +
                "undebug all                 ! or: no debug all" },

            new[] { "Cisco CUCM (CallManager)",
                "--- Via RTMT (GUI) ---\r\n" +
                "1) Cisco Unified Serviceability > Trace > Configuration\r\n" +
                "   - Cisco CallManager: Debug Trace Level = Detailed (SDL/SDI)\r\n" +
                "   - Cisco CTIManager: Detailed (if involved)\r\n" +
                "2) RTMT > Trace & Log Central > Collect Files\r\n" +
                "3) Open the SDL/SDI traces in TranslatorX (Ctrl+O)\r\n" +
                "4) SIP Call Trace / Ladder diagram: RTMT > SIP Call Trace\r\n\r\n" +
                "--- Via CLI (SSH to the node IP, platform admin user) ---\r\n" +
                "show status\r\n" +
                "show version active\r\n" +
                "utils service list                 ! service status\r\n" +
                "utils dbreplication runtimestate    ! DB replication between nodes\r\n" +
                "show risdb query phone              ! registered phones\r\n" +
                "run sql select name,description from device   ! DB query\r\n\r\n" +
                "! Trace / log (SDL/SDI)\r\n" +
                "file list activelog /cm/trace/ccm/sdl/\r\n" +
                "file tail  activelog /cm/trace/ccm/sdl/<file>   ! live tail\r\n" +
                "file view  activelog /cm/trace/ccm/sdi/<file>\r\n" +
                "file get   activelog /cm/trace/ccm/sdl/SDL*     ! download via SFTP\r\n\r\n" +
                "! Packet capture -> then analyze it in the Ladder!\r\n" +
                "utils network capture eth0 count 10000 size all file capture\r\n" +
                "file get activelog platform/cli/capture.cap\r\n\r\n" +
                "Note: set the trace level back to 'Error' when done." },

            new[] { "Asterisk / FreePBX",
                "asterisk -rvvvvv            ! verbose console\r\n" +
                "core set verbose 5\r\n" +
                "core set debug 5\r\n\r\n" +
                "! --- SIP (chan_pjsip, modern) ---\r\n" +
                "pjsip set logger on         ! log SIP messages\r\n" +
                "pjsip set logger off\r\n\r\n" +
                "! --- SIP (chan_sip, legacy) ---\r\n" +
                "sip set debug on\r\n" +
                "sip set debug off\r\n\r\n" +
                "! --- RTP ---\r\n" +
                "rtp set debug on\r\n\r\n" +
                "Log file: /var/log/asterisk/full\r\n" +
                "FreePBX: Settings > Asterisk Logfile Settings (raise the level)." },

            new[] { "AudioCodes (Mediant SBC)",
                "--- Web GUI ---\r\n" +
                "Troubleshoot > Logging > Syslog Settings:\r\n" +
                "  - Enable Syslog = On\r\n" +
                "  - Syslog Server IP = IP of this PC\r\n" +
                "  - (use LosaTerm's built-in Syslog server!)\r\n" +
                "  - Debug Level = 5 (Detailed)\r\n" +
                "Troubleshoot > Debug Recording (DR): captures signaling + media\r\n\r\n" +
                "--- CLI ---\r\n" +
                "enable\r\n" +
                "configure system\r\n" +
                "  syslog\r\n" +
                "    syslog on\r\n" +
                "    debug-level detailed\r\n" +
                "debug capture voip physical eth-lan\r\n" +
                "debug capture voip physical start\r\n" +
                "debug capture voip physical stop  /  show" },

            new[] { "Ribbon (SBC Edge / SWe Lite)",
                "--- WebUI (SBC Edge / SWe Lite) ---\r\n" +
                "Settings > Logging Configuration: level = Debug\r\n" +
                "Diagnostics > Log Viewer: view/export logs\r\n" +
                "Diagnostics > Packet Capture: capture pcap (then analyze it here)\r\n\r\n" +
                "--- Ribbon SBC Core (SBX) ---\r\n" +
                "CLI/EMA: raise SIP subsystem logging (SIPFE/SIPBE)\r\n" +
                "Packet capture via EMA or CLI; export and analyze the pcap.\r\n\r\n" +
                "Tip: for Teams DR also use the SBC Health tab (OPTIONS/TLS)." },

            new[] { "Microsoft Teams (Direct Routing)",
                "--- Teams side (PowerShell, MicrosoftTeams module) ---\r\n" +
                "Connect-MicrosoftTeams\r\n" +
                "Get-CsOnlinePSTNGateway                 # SBC/trunk status\r\n" +
                "Get-CsOnlineUser -Identity <upn> | fl   # voice enablement\r\n" +
                "Teams Admin Center > Voice > Direct Routing > SBC Health\r\n\r\n" +
                "--- SBC side: use LosaTerm's SBC Health tab ---\r\n" +
                "  - SIP OPTIONS to sip.pstnhub.microsoft.com (port 5061, TLS)\r\n" +
                "  - TLS: check CN/SAN and certificate EXPIRY (cause #1 of down)\r\n" +
                "  - DNS: A record of sip.pstnhub.microsoft.com\r\n\r\n" +
                "Note: Teams doesn't use SRV for the PSTN hub; check the A records." },

            new[] { "Avaya (Aura CM/SM e IP Office)",
                "--- Aura Communication Manager (SAT, via SSH then 'sat') ---\r\n" +
                "list trace station <ext>          ! trace a station\r\n" +
                "list trace tac <trunk-code>       ! trace a trunk\r\n" +
                "status station <ext>\r\n" +
                "status trunk <group>/<member>\r\n" +
                "display ...                        ! display config\r\n" +
                "mst                                ! Message Sequence Trace (ISDN/SIP)\r\n\r\n" +
                "--- Aura Session Manager (CLI) ---\r\n" +
                "traceSM                            ! interactive SIP tracer\r\n\r\n" +
                "--- IP Office ---\r\n" +
                "System Status Application (SSA): resource and call status\r\n" +
                "System Monitor (SysMonitor): Filters > Trace Options > SIP/H.323,\r\n" +
                "  enable the trace and capture the signaling." },

            new[] { "Mitel MiVoice Business (3300)",
                "--- System Administration Tool (ESM, web) ---\r\n" +
                "Maintenance and Diagnostics > Logs (System / SIP)\r\n" +
                "Maintenance Commands: maintenance commands\r\n" +
                "IP Phone Analyzer: IP set and signaling monitoring\r\n\r\n" +
                "--- SIP Trace ---\r\n" +
                "Enable SIP logging on the trunk/profile and collect the logs.\r\n" +
                "For capture use port mirroring on the switch and analyze\r\n" +
                "the pcap in the Ladder." },

            new[] { "3CX",
                "--- Management console ---\r\n" +
                "Dashboard > Event Log: events and alarms\r\n" +
                "Activity Log: call log\r\n" +
                "Settings: enable verbose SIP signaling logging\r\n\r\n" +
                "--- Log files ---\r\n" +
                "Windows: C:\\ProgramData\\3CX\\Instance1\\Data\\Logs\\\r\n" +
                "Linux:   /var/lib/3cxpbx/Instance1/Data/Logs/\r\n\r\n" +
                "Capture a pcap on the server and analyze it in the Ladder." },

            new[] { "Alcatel-Lucent OXE / OXO",
                "--- OmniPCX Enterprise (OXE) — SSH (mtcl / swinst) ---\r\n" +
                "swinst                             ! install/maintenance menu\r\n" +
                "mgr                                ! configuration management\r\n" +
                "mtracer                            ! tracer\r\n" +
                "incviewer                          ! incident viewer\r\n" +
                "tcpdump -i eth0 -w /tmp/cap.pcap   ! capture (Linux) -> Ladder\r\n\r\n" +
                "--- OmniPCX Office (OXO) ---\r\n" +
                "Managed via OMC (OmniPCX Office Management Console);\r\n" +
                "trace and capture available from the OMC." },

            new[] { "FreeSWITCH",
                "fs_cli                             ! console\r\n" +
                "sofia status                       ! SIP profiles status\r\n" +
                "sofia status profile <name>\r\n" +
                "sofia global siptrace on           ! global SIP trace\r\n" +
                "sofia profile <name> siptrace on\r\n" +
                "sofia loglevel all 9\r\n" +
                "console loglevel debug\r\n\r\n" +
                "Log: /usr/local/freeswitch/log/freeswitch.log" },

            new[] { "Cloud UCaaS (Webex / Zoom / RingCentral)",
                "Cloud platforms have no debug CLI: diagnosis is on the\r\n" +
                "admin portal side and the SBC side (for PSTN/SIP trunk/DR).\r\n\r\n" +
                "--- Webex Calling ---\r\n" +
                "Control Hub > Analytics / Troubleshooting.\r\n" +
                "Local Gateway = CUBE on-prem -> use the Cisco CUBE/IOS section.\r\n\r\n" +
                "--- Zoom Phone / RingCentral / 8x8 ---\r\n" +
                "Admin portal > Call logs / Quality of Service.\r\n" +
                "For the SIP trunk/SBC use the SBC Health tab (OPTIONS/TLS/DNS)." },
        };
    }
}
