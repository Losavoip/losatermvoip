using System.Collections.Generic;

namespace LosaTermVoip
{
    // ════════════════════════════════════════════════════════════════════════
    //  Conoscenza condivisa: codici di risposta SIP + cause Q.850, con
    //  significato, causa tipica e "cosa controllare". Usato sia dal
    //  Cause Code Translator sia dall'auto-diagnosi del Ladder.
    //  (Testi in italiano — non ancora localizzati.)
    // ════════════════════════════════════════════════════════════════════════
    public static class VoipCodes
    {
        // code -> { nome, significato, cosa controllare }
        public static readonly Dictionary<int, string[]> Sip = new Dictionary<int, string[]>
        {
            // 1xx provvisorie (non sono fallimenti)
            { 100, new[]{ "Trying",               "Richiesta ricevuta, in elaborazione.", "Normale." } },
            { 180, new[]{ "Ringing",              "Il chiamato sta squillando (ringback locale, di solito senza SDP).", "Normale: il ringback è generato localmente dal chiamante." } },
            { 181, new[]{ "Call Is Being Forwarded","Chiamata in inoltro.", "Normale." } },
            { 182, new[]{ "Queued",               "Chiamata in coda.", "Normale (ACD/coda)." } },
            { 183, new[]{ "Session Progress",     "Avanzamento sessione con early media (SDP presente).", "Il ringback/annuncio arriva IN-BAND dal lato remoto. Se l'utente non sente nulla → verifica il taglio dell'early media (P-Early-Media), il percorso RTP e la direzione media." } },
            // 4xx errori client
            { 400, new[]{ "Bad Request",          "Messaggio SIP malformato.", "Header mancanti/errati; un SBC che riscrive male i messaggi; UA difettoso." } },
            { 401, new[]{ "Unauthorized",         "Richiesta autenticazione (challenge).", "Normale una volta (seguito da nuovo INVITE con credenziali). Se ripetuto → utente/password o registrar errati." } },
            { 402, new[]{ "Payment Required",     "Pagamento richiesto (raro).", "Policy del provider." } },
            { 403, new[]{ "Forbidden",            "Richiesta rifiutata per policy.", "Trunk non autorizzato, IP sorgente non in whitelist, calling/called number bloccato, classe di servizio." } },
            { 404, new[]{ "Not Found",            "Numero/utente inesistente o non instradabile.", "Routing/dial-peer, numero chiamato, normalizzazione cifre, piano numerico." } },
            { 405, new[]{ "Method Not Allowed",   "Metodo non consentito.", "Il peer non accetta quel metodo (es. INFO/UPDATE)." } },
            { 406, new[]{ "Not Acceptable",       "Contenuto non accettabile.", "Accept header / formato non supportato." } },
            { 407, new[]{ "Proxy Authentication Required","Challenge dal proxy.", "Come 401 ma lato proxy: credenziali del trunk." } },
            { 408, new[]{ "Request Timeout",      "Nessuna risposta entro il timeout.", "Peer irraggiungibile, firewall, NAT, OPTIONS keepalive falliti, percorso di rete." } },
            { 410, new[]{ "Gone",                 "Numero non più disponibile.", "Numero cambiato o disattivato." } },
            { 413, new[]{ "Request Entity Too Large","Messaggio troppo grande.", "MTU/frammentazione UDP; valutare TCP/TLS." } },
            { 414, new[]{ "Request-URI Too Long", "Request-URI troppo lungo.", "URI anomalo." } },
            { 415, new[]{ "Unsupported Media Type","Tipo di media non supportato.", "Codec/SDP non gestito dal peer." } },
            { 416, new[]{ "Unsupported URI Scheme","Schema URI non supportato.", "sip vs sips vs tel." } },
            { 420, new[]{ "Bad Extension",        "Estensione SIP non supportata.", "Header Require/Proxy-Require non gestito dal peer." } },
            { 421, new[]{ "Extension Required",   "Estensione richiesta.", "Il peer richiede un'estensione non offerta." } },
            { 423, new[]{ "Interval Too Brief",   "Expires troppo breve (REGISTER/SUBSCRIBE).", "Aumentare l'intervallo di registrazione." } },
            { 480, new[]{ "Temporarily Unavailable","Utente non risponde o non disponibile.", "Telefono non registrato, DND, ring-no-answer timeout, dispositivo offline." } },
            { 481, new[]{ "Call/Transaction Does Not Exist","Dialog/transazione inesistente.", "ACK/BYE su dialog già chiuso, mismatch di tag/Call-ID, race condition." } },
            { 482, new[]{ "Loop Detected",        "Loop di instradamento.", "Routing circolare tra proxy/SBC." } },
            { 483, new[]{ "Too Many Hops",        "Max-Forwards a 0.", "Loop o catena troppo lunga; controllare il routing." } },
            { 484, new[]{ "Address Incomplete",   "Numero incompleto.", "Overlap dialing, cifre mancanti, piano numerico/interdigit timeout." } },
            { 485, new[]{ "Ambiguous",            "Numero ambiguo.", "Più match possibili." } },
            { 486, new[]{ "Busy Here",            "Occupato.", "Linea occupata. Normale." } },
            { 487, new[]{ "Request Terminated",   "INVITE annullato (a seguito di CANCEL).", "Il chiamante ha riagganciato prima della risposta. Normale." } },
            { 488, new[]{ "Not Acceptable Here",  "Negoziazione media fallita.", "CODEC MISMATCH: confronta i codec offerti (m=/a=rtpmap) con quelli supportati dal peer; verifica SRTP/crypto, ptime, transcoding mancante, indirizzo/porta media." } },
            { 491, new[]{ "Request Pending",      "Re-INVITE in conflitto.", "Glare: due re-INVITE simultanei." } },
            { 493, new[]{ "Undecipherable",       "Corpo S/MIME non decifrabile.", "Cifratura SDP (raro)." } },
            // 5xx errori server
            { 500, new[]{ "Server Internal Error","Errore interno del server.", "Bug/sovraccarico SBC/PBX; controllare i log del peer." } },
            { 501, new[]{ "Not Implemented",      "Funzionalità non implementata.", "Metodo/feature non supportata dal peer." } },
            { 502, new[]{ "Bad Gateway",          "Gateway a valle non raggiungibile.", "Trunk/route a valle giù o mal configurato." } },
            { 503, new[]{ "Service Unavailable",  "Servizio non disponibile / congestione.", "Sovraccarico, manutenzione, nessun circuito, trunk OOS, licenze esaurite." } },
            { 504, new[]{ "Server Time-out",      "Timeout verso un nodo a monte.", "Il peer a monte non risponde." } },
            { 505, new[]{ "Version Not Supported","Versione SIP non supportata.", "Anomalia di versione." } },
            { 513, new[]{ "Message Too Large",    "Messaggio troppo grande.", "Valutare TCP/TLS al posto di UDP." } },
            { 580, new[]{ "Precondition Failure", "Precondizioni QoS non soddisfatte.", "SDP precondition (raro)." } },
            // 6xx fallimenti globali
            { 600, new[]{ "Busy Everywhere",      "Occupato ovunque.", "Tutte le destinazioni occupate." } },
            { 603, new[]{ "Decline",              "Chiamata rifiutata dall'utente.", "Reject manuale, DND, policy." } },
            { 604, new[]{ "Does Not Exist Anywhere","Destinazione inesistente.", "Numero non presente su alcun nodo." } },
            { 606, new[]{ "Not Acceptable",       "Sessione non accettabile.", "Capacità/banda/media non compatibili." } },
        };

        // Cause Q.850 più comuni -> { significato, cosa controllare }
        public static readonly Dictionary<int, string[]> Q850 = new Dictionary<int, string[]>
        {
            { 1,   new[]{ "Unallocated number",                 "Numero non assegnato: routing/piano numerico." } },
            { 16,  new[]{ "Normal call clearing",               "Chiusura normale. Nessun problema." } },
            { 17,  new[]{ "User busy",                          "Occupato." } },
            { 18,  new[]{ "No user responding",                 "Ring timeout: il chiamato non risponde a livello di rete." } },
            { 19,  new[]{ "No answer from user",                "Nessuna risposta entro il tempo (alerting)." } },
            { 21,  new[]{ "Call rejected",                      "Rifiutata: policy/CoS/blocco numero." } },
            { 27,  new[]{ "Destination out of order",           "Destinazione fuori servizio." } },
            { 28,  new[]{ "Invalid number format",             "Numero incompleto/non valido: normalizzazione cifre." } },
            { 31,  new[]{ "Normal, unspecified",               "Chiusura normale non specificata." } },
            { 34,  new[]{ "No circuit/channel available",      "Nessun circuito libero: capacità trunk/congestione." } },
            { 38,  new[]{ "Network out of order",              "Rete fuori servizio." } },
            { 41,  new[]{ "Temporary failure",                "Guasto temporaneo." } },
            { 42,  new[]{ "Switching equipment congestion",   "Congestione apparati." } },
            { 44,  new[]{ "Requested circuit unavailable",    "Circuito richiesto non disponibile." } },
            { 47,  new[]{ "Resource unavailable",             "Risorsa non disponibile: spesso congestione/licenze." } },
            { 58,  new[]{ "Bearer capability not available",  "Capacità di trasporto non disponibile (codec/bearer)." } },
            { 65,  new[]{ "Bearer capability not implemented","Codec/bearer non implementato." } },
            { 88,  new[]{ "Incompatible destination",         "Destinazione incompatibile: codec/bearer mismatch." } },
            { 102, new[]{ "Recovery on timer expiry",         "Timeout protocollo: nessuna risposta in tempo." } },
            { 111, new[]{ "Protocol error",                   "Errore di protocollo." } },
            { 127, new[]{ "Interworking, unspecified",        "Interworking non specificato (gateway tra reti)." } },
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
                "! --- Abilita output su sessione ---\r\n" +
                "terminal monitor            ! mostra i debug in SSH/Telnet\r\n" +
                "terminal no monitor         ! per disabilitarlo\r\n\r\n" +
                "! --- SIP ---\r\n" +
                "debug ccsip messages        ! messaggi SIP completi (IN/OUT)\r\n" +
                "debug ccsip error           ! solo errori\r\n" +
                "debug ccsip events\r\n" +
                "debug ccsip info\r\n\r\n" +
                "! --- Call control / dial-peer ---\r\n" +
                "debug voip ccapi inout      ! flusso chiamata + cause code\r\n" +
                "debug voip dialpeer         ! matching dial-peer\r\n\r\n" +
                "! --- ISDN / TDM ---\r\n" +
                "debug isdn q931             ! segnalazione ISDN (cause Q.850)\r\n\r\n" +
                "! --- Stato ---\r\n" +
                "show call active voice brief\r\n" +
                "show sip-ua calls\r\n" +
                "show dial-peer voice summary\r\n\r\n" +
                "! --- STOP ---\r\n" +
                "undebug all                 ! oppure: no debug all" },

            new[] { "Cisco CUCM (CallManager)",
                "--- Via RTMT (GUI) ---\r\n" +
                "1) Cisco Unified Serviceability > Trace > Configuration\r\n" +
                "   - Cisco CallManager: Debug Trace Level = Detailed (SDL/SDI)\r\n" +
                "   - Cisco CTIManager: Detailed (se coinvolto)\r\n" +
                "2) RTMT > Trace & Log Central > Collect Files\r\n" +
                "3) Apri le trace SDL/SDI in TranslatorX (Ctrl+O)\r\n" +
                "4) SIP Call Trace / Ladder diagram: RTMT > SIP Call Trace\r\n\r\n" +
                "--- Via CLI (SSH all'IP del nodo, utente platform admin) ---\r\n" +
                "show status\r\n" +
                "show version active\r\n" +
                "utils service list                 ! stato servizi\r\n" +
                "utils dbreplication runtimestate    ! replica DB tra nodi\r\n" +
                "show risdb query phone              ! telefoni registrati\r\n" +
                "run sql select name,description from device   ! query DB\r\n\r\n" +
                "! Trace / log (SDL/SDI)\r\n" +
                "file list activelog /cm/trace/ccm/sdl/\r\n" +
                "file tail  activelog /cm/trace/ccm/sdl/<file>   ! coda live\r\n" +
                "file view  activelog /cm/trace/ccm/sdi/<file>\r\n" +
                "file get   activelog /cm/trace/ccm/sdl/SDL*     ! scarica via SFTP\r\n\r\n" +
                "! Cattura pacchetti -> poi analizzala nel Ladder!\r\n" +
                "utils network capture eth0 count 10000 size all file capture\r\n" +
                "file get activelog platform/cli/capture.cap\r\n\r\n" +
                "Nota: riporta il trace level a 'Error' a fine analisi." },

            new[] { "Asterisk / FreePBX",
                "asterisk -rvvvvv            ! console verbose\r\n" +
                "core set verbose 5\r\n" +
                "core set debug 5\r\n\r\n" +
                "! --- SIP (chan_pjsip, moderno) ---\r\n" +
                "pjsip set logger on         ! logga i messaggi SIP\r\n" +
                "pjsip set logger off\r\n\r\n" +
                "! --- SIP (chan_sip, legacy) ---\r\n" +
                "sip set debug on\r\n" +
                "sip set debug off\r\n\r\n" +
                "! --- RTP ---\r\n" +
                "rtp set debug on\r\n\r\n" +
                "Log file: /var/log/asterisk/full\r\n" +
                "FreePBX: Settings > Asterisk Logfile Settings (alza il livello)." },

            new[] { "AudioCodes (Mediant SBC)",
                "--- Web GUI ---\r\n" +
                "Troubleshoot > Logging > Syslog Settings:\r\n" +
                "  - Enable Syslog = On\r\n" +
                "  - Syslog Server IP = IP di questo PC\r\n" +
                "  - (usa il Syslog server integrato di LosaTerm!)\r\n" +
                "  - Debug Level = 5 (Detailed)\r\n" +
                "Troubleshoot > Debug Recording (DR): cattura segnalazione + media\r\n\r\n" +
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
                "Settings > Logging Configuration: livello = Debug\r\n" +
                "Diagnostics > Log Viewer: consulta/esporta i log\r\n" +
                "Diagnostics > Packet Capture: cattura pcap (poi analizzala qui)\r\n\r\n" +
                "--- Ribbon SBC Core (SBX) ---\r\n" +
                "CLI/EMA: alza il logging del subsystem SIP (SIPFE/SIPBE)\r\n" +
                "Packet capture via EMA o CLI; esporta e analizza il pcap.\r\n\r\n" +
                "Suggerimento: per Teams DR usa anche il tab SBC Health (OPTIONS/TLS)." },

            new[] { "Microsoft Teams (Direct Routing)",
                "--- Lato Teams (PowerShell, modulo MicrosoftTeams) ---\r\n" +
                "Connect-MicrosoftTeams\r\n" +
                "Get-CsOnlinePSTNGateway                 # stato SBC/trunk\r\n" +
                "Get-CsOnlineUser -Identity <upn> | fl   # abilitazioni voce\r\n" +
                "Teams Admin Center > Voice > Direct Routing > SBC Health\r\n\r\n" +
                "--- Lato SBC: usa il tab SBC Health di LosaTerm ---\r\n" +
                "  - SIP OPTIONS verso sip.pstnhub.microsoft.com (porta 5061, TLS)\r\n" +
                "  - TLS: controlla CN/SAN e SCADENZA del certificato (causa #1 di down)\r\n" +
                "  - DNS: record A di sip.pstnhub.microsoft.com\r\n\r\n" +
                "Nota: Teams non usa SRV per il PSTN hub; verifica gli A record." },

            new[] { "Avaya (Aura CM/SM e IP Office)",
                "--- Aura Communication Manager (SAT, via SSH poi 'sat') ---\r\n" +
                "list trace station <ext>          ! traccia una stazione\r\n" +
                "list trace tac <codice-trunk>     ! traccia un trunk\r\n" +
                "status station <ext>\r\n" +
                "status trunk <gruppo>/<membro>\r\n" +
                "display ...                        ! visualizza config\r\n" +
                "mst                                ! Message Sequence Trace (ISDN/SIP)\r\n\r\n" +
                "--- Aura Session Manager (CLI) ---\r\n" +
                "traceSM                            ! tracer SIP interattivo\r\n\r\n" +
                "--- IP Office ---\r\n" +
                "System Status Application (SSA): stato risorse e chiamate\r\n" +
                "System Monitor (SysMonitor): Filters > Trace Options > SIP/H.323,\r\n" +
                "  abilita il trace e cattura la segnalazione." },

            new[] { "Mitel MiVoice Business (3300)",
                "--- System Administration Tool (ESM, web) ---\r\n" +
                "Maintenance and Diagnostics > Logs (System / SIP)\r\n" +
                "Maintenance Commands: comandi di manutenzione\r\n" +
                "IP Phone Analyzer: monitoraggio set IP e segnalazione\r\n\r\n" +
                "--- Trace SIP ---\r\n" +
                "Abilita il logging SIP sul trunk/profilo e raccogli i log.\r\n" +
                "Per la cattura usa il port mirroring sullo switch e analizza\r\n" +
                "il pcap nel Ladder." },

            new[] { "3CX",
                "--- Console di gestione ---\r\n" +
                "Dashboard > Event Log: eventi e allarmi\r\n" +
                "Activity Log: log delle chiamate\r\n" +
                "Settings: abilita il logging verbose della segnalazione SIP\r\n\r\n" +
                "--- File di log ---\r\n" +
                "Windows: C:\\ProgramData\\3CX\\Instance1\\Data\\Logs\\\r\n" +
                "Linux:   /var/lib/3cxpbx/Instance1/Data/Logs/\r\n\r\n" +
                "Cattura un pcap sul server e analizzalo nel Ladder." },

            new[] { "Alcatel-Lucent OXE / OXO",
                "--- OmniPCX Enterprise (OXE) — SSH (mtcl / swinst) ---\r\n" +
                "swinst                             ! menu installazione/manutenzione\r\n" +
                "mgr                                ! gestione configurazione\r\n" +
                "mtracer                            ! tracer\r\n" +
                "incviewer                          ! visualizzatore incidenti\r\n" +
                "tcpdump -i eth0 -w /tmp/cap.pcap   ! cattura (Linux) -> Ladder\r\n\r\n" +
                "--- OmniPCX Office (OXO) ---\r\n" +
                "Gestione via OMC (OmniPCX Office Management Console);\r\n" +
                "trace e cattura disponibili dall'OMC." },

            new[] { "FreeSWITCH",
                "fs_cli                             ! console\r\n" +
                "sofia status                       ! stato profili SIP\r\n" +
                "sofia status profile <nome>\r\n" +
                "sofia global siptrace on           ! trace SIP globale\r\n" +
                "sofia profile <nome> siptrace on\r\n" +
                "sofia loglevel all 9\r\n" +
                "console loglevel debug\r\n\r\n" +
                "Log: /usr/local/freeswitch/log/freeswitch.log" },

            new[] { "Cloud UCaaS (Webex / Zoom / RingCentral)",
                "Le piattaforme cloud non hanno una CLI di debug: la diagnosi e'\r\n" +
                "lato portale amministrativo e lato SBC (per PSTN/SIP trunk/DR).\r\n\r\n" +
                "--- Webex Calling ---\r\n" +
                "Control Hub > Analytics / Troubleshooting.\r\n" +
                "Local Gateway = CUBE on-prem -> usa la sezione Cisco CUBE/IOS.\r\n\r\n" +
                "--- Zoom Phone / RingCentral / 8x8 ---\r\n" +
                "Portale admin > Call logs / Quality of Service.\r\n" +
                "Per il SIP trunk/SBC usa il tab SBC Health (OPTIONS/TLS/DNS)." },
        };
    }
}
