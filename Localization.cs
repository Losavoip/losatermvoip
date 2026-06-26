using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  Localizzazione — IT / EN / FR / DE / ES
    //  Uso: L.T("key")  oppure  L.T("key", arg0, arg1)
    // ═══════════════════════════════════════════════════════════════════════════

    public static class L
    {
        static string _lang = "EN";   // default Inglese
        public static string CurrentLang { get { return _lang; } private set { _lang = value; } }

        // Tutte le stringhe dell'app
        // Formato: { chiave, { IT, EN, FR, DE, ES } }
        static readonly Dictionary<string, string[]> Strings = new Dictionary<string, string[]>
        {
            // Indici lingua: 0=IT  1=EN  2=FR  3=DE  4=ES
            // ── Titoli finestra ──────────────────────────────────────────────────
            { "app.title",        new[]{ "LosaTermVoip — SSH / SFTP / SCP / FTP | VoIP Terminal",
                                         "LosaTermVoip — SSH / SFTP / SCP / FTP | VoIP Terminal",
                                         "LosaTermVoip — SSH / SFTP / SCP / FTP | Terminal VoIP",
                                         "LosaTermVoip — SSH / SFTP / SCP / FTP | VoIP Terminal",
                                         "LosaTermVoip — SSH / SFTP / SCP / FTP | Terminal VoIP" }},

            // ── Menu principale ──────────────────────────────────────────────────
            { "menu.file",        new[]{ "File",       "File",      "Fichier",   "Datei",     "Archivo" }},
            { "menu.new",         new[]{ "Nuova",      "New",       "Nouveau",   "Neu",       "Nuevo" }},
            { "menu.edit",        new[]{ "Modifica",   "Edit",      "Modifier",  "Bearbeiten","Editar" }},
            { "menu.delete",      new[]{ "Elimina",    "Delete",    "Supprimer", "Löschen",   "Eliminar" }},
            { "menu.settings",    new[]{ "Impostazioni","Settings", "Paramètres","Einstellungen","Ajustes" }},
            { "menu.exit",        new[]{ "Esci",       "Exit",      "Quitter",   "Beenden",   "Salir" }},
            { "menu.language",    new[]{ "Lingua",     "Language",  "Langue",    "Sprache",   "Idioma" }},

            // ── Toolbar ──────────────────────────────────────────────────────────
            { "btn.new_conn",     new[]{ "+ Nuova",    "+ New",     "+ Nouveau", "+ Neu",     "+ Nuevo" }},
            { "btn.connect",      new[]{ "Connetti",   "Connect",   "Connecter", "Verbinden", "Conectar" }},
            { "btn.disconnect",   new[]{ "Disconnetti","Disconnect","Déconnecter","Trennen",  "Desconectar" }},
            { "btn.analyzer",     new[]{ "📊 Analizzatore","📊 Analyzer","📊 Analyseur","📊 Analyzer","📊 Analizador" }},
            { "btn.log",          new[]{ "📄 Log",     "📄 Log",    "📄 Journal","📄 Protokoll","📄 Registro" }},
            { "btn.close",        new[]{ "✕ Chiudi",   "✕ Close",   "✕ Fermer", "✕ Schließen","✕ Cerrar" }},
            { "btn.claude",       new[]{ "🤖 Claude AI","🤖 Claude AI","🤖 Claude AI","🤖 Claude AI","🤖 Claude AI" }},
            { "btn.pcap",         new[]{ "📦 PCAP",    "📦 PCAP",   "📦 PCAP",  "📦 PCAP",   "📦 PCAP" }},
            { "btn.server",       new[]{ "🖧 Server",  "🖧 Server", "🖧 Serveur","🖧 Server", "🖧 Servidor" }},
            { "btn.vpn",          new[]{ "🔐 VPN:",    "🔐 VPN:",   "🔐 VPN:",  "🔐 VPN:",   "🔐 VPN:" }},
            { "btn.edit",         new[]{ "✎ Modifica","✎ Edit",    "✎ Modifier","✎ Bearbeiten","✎ Editar" }},
            { "btn.delete",       new[]{ "✕ Elimina", "✕ Delete",  "✕ Supprimer","✕ Löschen","✕ Eliminar" }},
            { "btn.simulator",    new[]{ "🔀 Simulator","🔀 Simulator","🔀 Simulateur","🔀 Simulator","🔀 Simulador" }},
            { "btn.doc",          new[]{ "📖 Doc.",    "📖 Doc.",   "📖 Doc.",  "📖 Doku",   "📖 Doc." }},
            { "btn.syslog",       new[]{ "📡 Syslog",  "📡 Syslog", "📡 Syslog","📡 Syslog", "📡 Syslog" }},

            // ── Finestra principale (menu + toolbar + stato) ─────────────────────
            { "menu.connections", new[]{ "Connessioni","Connections","Connexions","Verbindungen","Conexiones" }},
            { "menu.tools",       new[]{ "Strumenti",  "Tools",     "Outils",    "Werkzeuge", "Herramientas" }},
            { "menu.open_config", new[]{ "Apri cartella config","Open config folder","Ouvrir dossier config","Konfig-Ordner öffnen","Abrir carpeta config" }},
            { "tools.putty_path", new[]{ "Percorso PuTTY","PuTTY path","Chemin PuTTY","PuTTY-Pfad","Ruta PuTTY" }},
            { "tools.test_conn",  new[]{ "Test connettività","Connectivity test","Test connectivité","Verbindungstest","Test de conexión" }},
            { "conn.ssh_embedded",new[]{ "SSH (integrato)","SSH (embedded)","SSH (intégré)","SSH (eingebettet)","SSH (integrado)" }},
            { "conn.ssh_window",  new[]{ "SSH (finestra)","SSH (window)","SSH (fenêtre)","SSH (Fenster)","SSH (ventana)" }},
            { "conn.scp_transfer",new[]{ "SCP Transfer","SCP Transfer","Transfert SCP","SCP-Transfer","Transferencia SCP" }},
            { "conn.ftp_browser", new[]{ "FTP Browser","FTP Browser","Navigateur FTP","FTP-Browser","Navegador FTP" }},
            { "col.port_short",   new[]{ "P.",         "P.",        "P.",        "P.",        "P." }},
            { "status.hint",      new[]{ "Doppio click = SSH integrato  |  SSH ↗ = finestra separata  |  Tasto destro = menu",
                                         "Double-click = embedded SSH  |  SSH ↗ = separate window  |  Right-click = menu",
                                         "Double-clic = SSH intégré  |  SSH ↗ = fenêtre séparée  |  Clic droit = menu",
                                         "Doppelklick = eingebettetes SSH  |  SSH ↗ = separates Fenster  |  Rechtsklick = Menü",
                                         "Doble clic = SSH integrado  |  SSH ↗ = ventana aparte  |  Clic derecho = menú" }},
            { "status.putty_notfound", new[]{ "PuTTY: non trovato","PuTTY: not found","PuTTY: introuvable","PuTTY: nicht gefunden","PuTTY: no encontrado" }},
            { "help.website",     new[]{ "🌐 Sito web","🌐 Website","🌐 Site web","🌐 Webseite","🌐 Sitio web" }},
            { "help.donate",      new[]{ "❤ Sostieni il progetto","❤ Support the project","❤ Soutenir le projet","❤ Projekt unterstützen","❤ Apoya el proyecto" }},
            { "help.about",       new[]{ "ℹ️ Informazioni","ℹ️ About","ℹ️ À propos","ℹ️ Über","ℹ️ Información" }},

            // ── Finestra Informazioni ────────────────────────────────────────────
            { "about.close",      new[]{ "Chiudi",     "Close",     "Fermer",    "Schließen", "Cerrar" }},
            { "about.bio",        new[]{
                "LosaTerm Voip Terminal nasce dall'esperienza sul campo di chi da diversi anni vive di Unified Communications, con la passione per l'analisi e il troubleshooting dei problemi Voice/VoIP.\r\n\r\nL'idea è semplice: un unico strumento, leggero e senza dipendenze, che raccolga tutto ciò che serve ogni giorno a chi lavora con centralini, SBC e gateway — multivendor, senza fronzoli.\r\n\r\nProgetto gratuito, nato come hobby: ogni feedback è prezioso e aiuta a farlo crescere.",
                "LosaTerm Voip Terminal comes from years of hands-on field experience in Unified Communications, with a passion for analyzing and troubleshooting Voice/VoIP issues.\r\n\r\nThe idea is simple: one lightweight tool with no dependencies, gathering everything you need every day when working with PBXs, SBCs and gateways — multivendor, no frills.\r\n\r\nA free project, born as a hobby: every piece of feedback is precious and helps it grow.",
                "LosaTerm Voip Terminal est né de l'expérience de terrain de quelqu'un qui vit les Unified Communications depuis des années, avec la passion de l'analyse et du dépannage des problèmes Voice/VoIP.\r\n\r\nL'idée est simple : un seul outil léger et sans dépendances, réunissant tout ce dont on a besoin au quotidien avec les PABX, SBC et passerelles — multivendeur, sans fioritures.\r\n\r\nProjet gratuit, né comme un hobby : chaque retour est précieux et l'aide à grandir.",
                "LosaTerm Voip Terminal entstand aus jahrelanger Praxiserfahrung in der Welt der Unified Communications, mit Leidenschaft für die Analyse und Fehlersuche bei Voice/VoIP-Problemen.\r\n\r\nDie Idee ist einfach: ein einziges, leichtes Tool ohne Abhängigkeiten, das alles bündelt, was man täglich mit TK-Anlagen, SBCs und Gateways braucht — herstellerneutral, ohne Schnickschnack.\r\n\r\nKostenloses Projekt, als Hobby entstanden: jedes Feedback ist wertvoll und hilft, es weiterzuentwickeln.",
                "LosaTerm Voip Terminal nace de años de experiencia de campo en Unified Communications, con pasión por el análisis y la resolución de problemas Voice/VoIP.\r\n\r\nLa idea es simple: una única herramienta ligera y sin dependencias, que reúna todo lo necesario a diario con centralitas, SBC y gateways — multifabricante, sin florituras.\r\n\r\nProyecto gratuito, nacido como hobby: cada comentario es valioso y ayuda a hacerlo crecer." }},

            // ── SBC Health ───────────────────────────────────────────────────────
            { "sbc.tab_tls",      new[]{ "TLS / Certificato","TLS / Certificate","TLS / Certificat","TLS / Zertifikat","TLS / Certificado" }},
            { "sbc.port",         new[]{ "Porta:",     "Port:",     "Port:",     "Port:",     "Puerto:" }},
            { "sbc.transport",    new[]{ "Trasporto:", "Transport:","Transport:","Transport:","Transporte:" }},
            { "sbc.timeout",      new[]{ "Timeout (ms):","Timeout (ms):","Délai (ms):","Timeout (ms):","Tiempo (ms):" }},
            { "sbc.send_options", new[]{ "▶ Invia OPTIONS","▶ Send OPTIONS","▶ Envoyer OPTIONS","▶ OPTIONS senden","▶ Enviar OPTIONS" }},
            { "sbc.check",        new[]{ "🔒 Controlla","🔒 Check",   "🔒 Vérifier","🔒 Prüfen",  "🔒 Comprobar" }},
            { "sbc.domain",       new[]{ "Dominio:",   "Domain:",   "Domaine:",  "Domäne:",   "Dominio:" }},
            { "sbc.srv_service",  new[]{ "Servizio SRV:","SRV service:","Service SRV:","SRV-Dienst:","Servicio SRV:" }},
            { "sbc.resolve",      new[]{ "🔎 Risolvi", "🔎 Resolve", "🔎 Résoudre","🔎 Auflösen","🔎 Resolver" }},
            { "sbc.opt_hint",     new[]{ "  Verifica che il SBC/trunk risponda. Un 200 OK = trunk attivo (es. health check di Teams Direct Routing).",
                                         "  Check that the SBC/trunk replies. A 200 OK = trunk up (e.g. Teams Direct Routing health check).",
                                         "  Vérifiez que le SBC/trunk répond. Un 200 OK = trunk actif (ex. health check Teams Direct Routing).",
                                         "  Prüfen, ob der SBC/Trunk antwortet. Ein 200 OK = Trunk aktiv (z. B. Teams Direct Routing Health-Check).",
                                         "  Comprueba que el SBC/trunk responde. Un 200 OK = trunk activo (p. ej. health check de Teams Direct Routing)." }},
            { "sbc.tls_hint",     new[]{ "  CN/SAN, scadenza e versione TLS. La scadenza certificato è la causa #1 di down su Teams Direct Routing.",
                                         "  CN/SAN, expiry and TLS version. Certificate expiry is the #1 cause of Teams Direct Routing outages.",
                                         "  CN/SAN, expiration et version TLS. L'expiration du certificat est la cause n°1 de panne sur Teams Direct Routing.",
                                         "  CN/SAN, Ablauf und TLS-Version. Zertifikatsablauf ist die häufigste Ursache für Teams-Direct-Routing-Ausfälle.",
                                         "  CN/SAN, caducidad y versión TLS. La caducidad del certificado es la causa nº1 de caídas en Teams Direct Routing." }},
            { "sbc.dns_hint",     new[]{ "  Risolve i record SRV (priorità/peso/porta/target) e A. Usa il resolver di Windows (dnsapi). Es: _sip._tcp + dominio.",
                                         "  Resolves SRV records (priority/weight/port/target) and A. Uses the Windows resolver (dnsapi). E.g. _sip._tcp + domain.",
                                         "  Résout les enregistrements SRV (priorité/poids/port/cible) et A. Utilise le resolver Windows (dnsapi). Ex : _sip._tcp + domaine.",
                                         "  Löst SRV-Records (Priorität/Gewicht/Port/Ziel) und A auf. Nutzt den Windows-Resolver (dnsapi). Z. B. _sip._tcp + Domäne.",
                                         "  Resuelve registros SRV (prioridad/peso/puerto/destino) y A. Usa el resolver de Windows (dnsapi). Ej: _sip._tcp + dominio." }},

            // ── SIP Simulator ────────────────────────────────────────────────────
            { "sim.dest",         new[]{ "Destinazione (IP:porta):","Destination (IP:port):","Destination (IP:port) :","Ziel (IP:Port):","Destino (IP:puerto):" }},
            { "sim.local_port",   new[]{ "Porta locale:","Local port:","Port local :","Lokaler Port:","Puerto local:" }},
            { "sim.proxy_hint",   new[]{ "(vuoto = usa Destinazione)","(empty = use Destination)","(vide = utilise Destination)","(leer = Ziel verwenden)","(vacío = usa Destino)" }},
            { "sim.method",       new[]{ "Metodo SIP:","SIP method:","Méthode SIP :","SIP-Methode:","Método SIP:" }},
            { "sim.send",         new[]{ "▶ Invia messaggio SIP","▶ Send SIP message","▶ Envoyer message SIP","▶ SIP-Nachricht senden","▶ Enviar mensaje SIP" }},
            { "sim.clear",        new[]{ "🗑 Pulisci","🗑 Clear",   "🗑 Effacer", "🗑 Löschen", "🗑 Limpiar" }},

            // ── Finestra Nuova/Modifica Connessione ──────────────────────────────
            { "ec.title_new",     new[]{ "Nuova Connessione","New Connection","Nouvelle connexion","Neue Verbindung","Nueva conexión" }},
            { "ec.title_edit",    new[]{ "Modifica: ","Edit: ",   "Modifier : ","Bearbeiten: ","Editar: " }},
            { "ec.tab_ssh",       new[]{ "SSH / Generale","SSH / General","SSH / Général","SSH / Allgemein","SSH / General" }},
            { "ec.name",          new[]{ "Nome visualizzato:","Display name:","Nom affiché :","Anzeigename:","Nombre mostrado:" }},
            { "ec.port_ssh",      new[]{ "Porta SSH:","SSH port:","Port SSH :","SSH-Port:","Puerto SSH:" }},
            { "ec.pass_ssh",      new[]{ "Password SSH:","SSH password:","Mot de passe SSH :","SSH-Passwort:","Contraseña SSH:" }},
            { "ec.identity",      new[]{ "Identity file (key):","Identity file (key):","Fichier d'identité (clé) :","Identity-Datei (Key):","Archivo de identidad (clave):" }},
            { "ec.browse_key",    new[]{ "Sfoglia key...","Browse key...","Parcourir clé...","Key durchsuchen...","Examinar clave..." }},
            { "ec.vpn_site",      new[]{ "Sito / Nome connessione:","Site / Connection name:","Site / Nom de connexion :","Site / Verbindungsname:","Sitio / Nombre conexión:" }},
            { "ec.vpn_user",      new[]{ "Utente VPN (opz.):","VPN user (opt.):","Utilisateur VPN (opt.) :","VPN-Benutzer (opt.):","Usuario VPN (opc.):" }},
            { "ec.vpn_pass",      new[]{ "Password VPN (opz.):","VPN password (opt.):","Mot de passe VPN (opt.) :","VPN-Passwort (opt.):","Contraseña VPN (opc.):" }},
            { "ec.vpn_note",      new[]{
                "Checkpoint → nome sito nel client\nWindows VPN → nome connessione Windows\nFortinet → apre FortiClient GUI\nManuale → appare bottone \"Continua\" per te",
                "Checkpoint → site name in the client\nWindows VPN → Windows connection name\nFortinet → opens FortiClient GUI\nManual → a \"Continue\" button appears for you",
                "Checkpoint → nom du site dans le client\nWindows VPN → nom de la connexion Windows\nFortinet → ouvre l'interface FortiClient\nManuel → un bouton « Continuer » apparaît",
                "Checkpoint → Site-Name im Client\nWindows VPN → Name der Windows-Verbindung\nFortinet → öffnet FortiClient-GUI\nManuell → eine Schaltfläche „Weiter\" erscheint",
                "Checkpoint → nombre del sitio en el cliente\nWindows VPN → nombre de la conexión de Windows\nFortinet → abre la GUI de FortiClient\nManual → aparece un botón \"Continuar\"" }},
            { "ec.pass_ftp",      new[]{ "Password FTP:","FTP password:","Mot de passe FTP :","FTP-Passwort:","Contraseña FTP:" }},
            { "ec.ftp_note",      new[]{
                "Utente FTP: usa lo stesso campo Utente del tab SSH",
                "FTP user: uses the same Username field as the SSH tab",
                "Utilisateur FTP : utilise le même champ Utilisateur que l'onglet SSH",
                "FTP-Benutzer: nutzt dasselbe Benutzerfeld wie der SSH-Tab",
                "Usuario FTP: usa el mismo campo Usuario de la pestaña SSH" }},
            { "ec.web_path",      new[]{ "Path (opz.):","Path (opt.):","Chemin (opt.) :","Pfad (opt.):","Ruta (opc.):" }},
            { "ec.web_note",      new[]{
                "Seleziona HTTP o HTTPS come protocollo.\nDoppioclick sulla connessione → apre il browser.\nURL: http(s)://host:porta/path\nPorta SSH usata anche per HTTP (default 80/443).",
                "Select HTTP or HTTPS as protocol.\nDouble-click the connection → opens the browser.\nURL: http(s)://host:port/path\nSSH port also used for HTTP (default 80/443).",
                "Sélectionnez HTTP ou HTTPS comme protocole.\nDouble-clic sur la connexion → ouvre le navigateur.\nURL : http(s)://hôte:port/chemin\nLe port SSH sert aussi pour HTTP (défaut 80/443).",
                "HTTP oder HTTPS als Protokoll wählen.\nDoppelklick auf die Verbindung → öffnet den Browser.\nURL: http(s)://host:port/pfad\nSSH-Port auch für HTTP (Standard 80/443).",
                "Selecciona HTTP o HTTPS como protocolo.\nDoble clic en la conexión → abre el navegador.\nURL: http(s)://host:puerto/ruta\nPuerto SSH también para HTTP (predet. 80/443)." }},
            { "ec.validate_req",  new[]{ "Nome e Host obbligatori.","Name and Host are required.","Nom et Hôte obligatoires.","Name und Host erforderlich.","Nombre y Host obligatorios." }},
            { "ec.validate_title",new[]{ "Validazione","Validation","Validation","Validierung","Validación" }},

            // ── Syslog Server ────────────────────────────────────────────────────
            { "sys.udp_port",     new[]{ "Porta UDP:","UDP port:","Port UDP :","UDP-Port:","Puerto UDP:" }},
            { "sys.start",        new[]{ "▶ Avvia server","▶ Start server","▶ Démarrer serveur","▶ Server starten","▶ Iniciar servidor" }},
            { "sys.stop",         new[]{ "■ Ferma","■ Stop","■ Arrêter","■ Stoppen","■ Detener" }},
            { "sys.stopped",      new[]{ "● Fermo","● Stopped","● Arrêté","● Gestoppt","● Detenido" }},
            { "sys.clear",        new[]{ "🧹 Pulisci","🧹 Clear","🧹 Effacer","🧹 Löschen","🧹 Limpiar" }},
            { "sys.export",       new[]{ "💾 Esporta","💾 Export","💾 Exporter","💾 Exportieren","💾 Exportar" }},
            { "sys.autoscroll",   new[]{ "Auto-scroll","Auto-scroll","Défil. auto","Auto-Scroll","Auto-desplaz." }},
            { "sys.sip_only",     new[]{ "Solo SIP/Call","SIP/Call only","SIP/Call uniquement","Nur SIP/Call","Solo SIP/Call" }},
            { "sys.to_file",      new[]{ "Scrivi su file","Write to file","Écrire dans fichier","In Datei schreiben","Escribir en archivo" }},
            { "sys.text_filter",  new[]{ "Filtro testo:","Text filter:","Filtre texte :","Textfilter:","Filtro texto:" }},
            { "sys.col_time",     new[]{ "Ora","Time","Heure","Zeit","Hora" }},
            { "sys.col_source",   new[]{ "Sorgente","Source","Source","Quelle","Origen" }},
            { "sys.col_msg",      new[]{ "Messaggio","Message","Message","Nachricht","Mensaje" }},
            { "sys.hint",         new[]{
                "  Configura il device (AudioCodes/Cisco) per inviare syslog all'IP di questo PC sulla porta scelta. Porte <1024 richiedono privilegi: se 514 fallisce usa 1514.",
                "  Configure the device (AudioCodes/Cisco) to send syslog to this PC's IP on the chosen port. Ports <1024 need privileges: if 514 fails use 1514.",
                "  Configurez l'appareil (AudioCodes/Cisco) pour envoyer le syslog vers l'IP de ce PC sur le port choisi. Ports <1024 = privilèges : si 514 échoue, utilisez 1514.",
                "  Konfigurieren Sie das Gerät (AudioCodes/Cisco), Syslog an die IP dieses PCs auf dem gewählten Port zu senden. Ports <1024 benötigen Rechte: wenn 514 fehlschlägt, 1514 verwenden.",
                "  Configura el dispositivo (AudioCodes/Cisco) para enviar syslog a la IP de este PC en el puerto elegido. Puertos <1024 requieren privilegios: si 514 falla usa 1514." }},

            // ── VPN Connect ──────────────────────────────────────────────────────
            { "vpn.continue",     new[]{ "Continua →","Continue →","Continuer →","Weiter →","Continuar →" }},

            // ── SCP Transfer ─────────────────────────────────────────────────────
            { "scp.upload",       new[]{ "Upload (locale → remoto)","Upload (local → remote)","Envoi (local → distant)","Upload (lokal → entfernt)","Subida (local → remoto)" }},
            { "scp.download",     new[]{ "Download (remoto → locale)","Download (remote → local)","Téléchargement (distant → local)","Download (entfernt → lokal)","Descarga (remoto → local)" }},
            { "scp.local_path",   new[]{ "Percorso locale:","Local path:","Chemin local :","Lokaler Pfad:","Ruta local:" }},
            { "scp.remote_path",  new[]{ "Percorso remoto:","Remote path:","Chemin distant :","Entfernter Pfad:","Ruta remota:" }},
            { "scp.recursive",    new[]{ "Ricorsivo (-r) per cartelle","Recursive (-r) for folders","Récursif (-r) pour dossiers","Rekursiv (-r) für Ordner","Recursivo (-r) para carpetas" }},
            { "scp.start",        new[]{ "Avvia trasferimento","Start transfer","Démarrer transfert","Übertragung starten","Iniciar transferencia" }},
            { "scp.hint",         new[]{
                "es. Linux:  user@host:/percorso/file   |   Cisco IOS:  user@host:flash:file.bin",
                "e.g. Linux:  user@host:/path/file   |   Cisco IOS:  user@host:flash:file.bin",
                "ex. Linux :  user@host:/chemin/fichier   |   Cisco IOS :  user@host:flash:file.bin",
                "z. B. Linux:  user@host:/pfad/datei   |   Cisco IOS:  user@host:flash:file.bin",
                "ej. Linux:  user@host:/ruta/archivo   |   Cisco IOS:  user@host:flash:file.bin" }},

            // ── FTP Browser ──────────────────────────────────────────────────────
            { "fb.up",            new[]{ "↑ Su","↑ Up","↑ Haut","↑ Hoch","↑ Subir" }},
            { "fb.go",            new[]{ "Vai","Go","Aller","Los","Ir" }},
            { "fb.newfolder",     new[]{ "+ Cartella","+ Folder","+ Dossier","+ Ordner","+ Carpeta" }},
            { "fb.col_size",      new[]{ "Dim.","Size","Taille","Größe","Tam." }},
            { "fb.col_date",      new[]{ "Data","Date","Date","Datum","Fecha" }},
            { "fb.col_type",      new[]{ "Tipo","Type","Type","Typ","Tipo" }},
            { "fb.type_folder",   new[]{ "Cartella","Folder","Dossier","Ordner","Carpeta" }},
            { "fb.ready",         new[]{ "Pronto.","Ready.","Prêt.","Bereit.","Listo." }},

            // ── Generici ─────────────────────────────────────────────────────────
            { "gen.confirm",      new[]{ "Conferma","Confirm","Confirmer","Bestätigen","Confirmar" }},

            // ── Pannello Doc ─────────────────────────────────────────────────────
            { "doc.title",        new[]{ "Documentazione (Doc.)","Documentation (Doc.)","Documentation (Doc.)","Dokumentation (Doc.)","Documentación (Doc.)" }},
            { "doc.category",     new[]{ "Categoria:","Category:","Catégorie :","Kategorie:","Categoría:" }},
            { "doc.add",          new[]{ "➕ Aggiungi","➕ Add","➕ Ajouter","➕ Hinzufügen","➕ Añadir" }},
            { "doc.remove",       new[]{ "🗑 Rimuovi","🗑 Remove","🗑 Supprimer","🗑 Entfernen","🗑 Quitar" }},
            { "doc.open",         new[]{ "🌐 Apri","🌐 Open","🌐 Ouvrir","🌐 Öffnen","🌐 Abrir" }},
            { "doc.col_cat",      new[]{ "Categoria","Category","Catégorie","Kategorie","Categoría" }},
            { "doc.col_title",    new[]{ "Titolo","Title","Titre","Titel","Título" }},
            { "doc.all",          new[]{ "(tutte)","(all)","(tous)","(alle)","(todas)" }},
            { "doc.hint",         new[]{
                "  Doppio-click per aprire nel browser. I link sono salvati e li puoi modificare a mano.",
                "  Double-click to open in the browser. Links are saved and you can edit them by hand.",
                "  Double-clic pour ouvrir dans le navigateur. Les liens sont enregistrés et modifiables à la main.",
                "  Doppelklick zum Öffnen im Browser. Links werden gespeichert und sind manuell bearbeitbar.",
                "  Doble clic para abrir en el navegador. Los enlaces se guardan y se pueden editar a mano." }},
            { "doc.select_link",  new[]{ "Seleziona un link.","Select a link.","Sélectionnez un lien.","Wähle einen Link.","Selecciona un enlace." }},
            { "doc.confirm_remove",new[]{ "Rimuovere \"{0}\"?","Remove \"{0}\"?","Supprimer « {0} » ?","„{0}\" entfernen?","¿Quitar \"{0}\"?" }},
            { "doc.new_link",     new[]{ "Nuovo link","New link","Nouveau lien","Neuer Link","Nuevo enlace" }},
            { "doc.edit_link",    new[]{ "Modifica link","Edit link","Modifier le lien","Link bearbeiten","Editar enlace" }},
            { "doc.dtitle",       new[]{ "Titolo:","Title:","Titre :","Titel:","Título:" }},
            { "doc.req",          new[]{ "Titolo e URL sono obbligatori.","Title and URL are required.","Titre et URL obligatoires.","Titel und URL erforderlich.","Título y URL obligatorios." }},

            // ── Dialog drag & drop ───────────────────────────────────────────────
            { "drop.title",       new[]{ "File trascinato","Dropped file","Fichier déposé","Datei abgelegt","Archivo soltado" }},
            { "drop.what",        new[]{ "Cosa vuoi fare con:","What do you want to do with:","Que faire avec :","Was möchtest du tun mit:","¿Qué hacer con:" }},
            { "drop.analyze_here",new[]{ "📦  Analizza qui (Ladder / SIP)","📦  Analyze here (Ladder / SIP)","📦  Analyser ici (Ladder / SIP)","📦  Hier analysieren (Ladder / SIP)","📦  Analizar aquí (Ladder / SIP)" }},
            { "drop.syslog",      new[]{ "📡  Apri nel viewer Syslog","📡  Open in Syslog viewer","📡  Ouvrir dans le viewer Syslog","📡  Im Syslog-Viewer öffnen","📡  Abrir en el visor Syslog" }},
            { "drop.translatorx", new[]{ "🔀  Apri in TranslatorX (trace SDL Cisco)","🔀  Open in TranslatorX (Cisco SDL trace)","🔀  Ouvrir dans TranslatorX (trace SDL Cisco)","🔀  In TranslatorX öffnen (Cisco SDL-Trace)","🔀  Abrir en TranslatorX (traza SDL Cisco)" }},
            { "drop.syslog_ac",   new[]{ "📡  Apri nel viewer Syslog (AudioCodes)","📡  Open in Syslog viewer (AudioCodes)","📡  Ouvrir dans le viewer Syslog (AudioCodes)","📡  Im Syslog-Viewer öffnen (AudioCodes)","📡  Abrir en el visor Syslog (AudioCodes)" }},

            // ── Form connessione ─────────────────────────────────────────────────
            { "conn.name",        new[]{ "Nome",       "Name",      "Nom",       "Name",      "Nombre" }},
            { "conn.host",        new[]{ "Host / IP",  "Host / IP", "Hôte / IP", "Host / IP", "Host / IP" }},
            { "conn.port",        new[]{ "Porta",      "Port",      "Port",      "Port",      "Puerto" }},
            { "conn.protocol",    new[]{ "Protocollo", "Protocol",  "Protocole", "Protokoll", "Protocolo" }},
            { "conn.username",    new[]{ "Utente",     "Username",  "Utilisateur","Benutzer", "Usuario" }},
            { "conn.password",    new[]{ "Password",   "Password",  "Mot de passe","Passwort","Contraseña" }},
            { "conn.vpntype",     new[]{ "Tipo VPN",   "VPN Type",  "Type VPN",  "VPN-Typ",   "Tipo VPN" }},
            { "conn.browser",     new[]{ "Browser",    "Browser",   "Navigateur","Browser",   "Navegador" }},
            { "conn.webpath",     new[]{ "Path web",   "Web path",  "Chemin web","Web-Pfad",  "Ruta web" }},
            { "conn.save",        new[]{ "Salva",      "Save",      "Enregistrer","Speichern","Guardar" }},
            { "conn.cancel",      new[]{ "Annulla",    "Cancel",    "Annuler",   "Abbrechen", "Cancelar" }},
            { "conn.test",        new[]{ "Testa connessione","Test connection","Tester la connexion","Verbindung testen","Probar conexión" }},
            { "conn.move_up",     new[]{ "Sposta su","Move up","Monter","Nach oben","Subir" }},
            { "conn.move_down",   new[]{ "Sposta giù","Move down","Descendre","Nach unten","Bajar" }},

            // ── Analizzatore ─────────────────────────────────────────────────────
            { "ana.device",       new[]{ "Device:",    "Device:",   "Appareil:", "Gerät:",    "Dispositivo:" }},
            { "ana.clear",        new[]{ "🗑 Pulisci",  "🗑 Clear",  "🗑 Effacer","🗑 Löschen","🗑 Limpiar" }},
            { "ana.save",         new[]{ "💾 Salva report","💾 Save report","💾 Sauvegarder","💾 Bericht speichern","💾 Guardar informe" }},
            { "ana.open_pcap",    new[]{ "📂 Apri PCAP","📂 Open PCAP","📂 Ouvrir PCAP","📂 PCAP öffnen","📂 Abrir PCAP" }},
            { "ana.tab_detail",   new[]{ "📋 Dettaglio","📋 Detail", "📋 Détail", "📋 Detail", "📋 Detalle" }},
            { "ana.tab_callflow", new[]{ "📞 Call Flow","📞 Call Flow","📞 Flux d'appel","📞 Anrufverlauf","📞 Flujo llamada" }},
            { "ana.tab_ladder",   new[]{ "📊 Ladder",  "📊 Ladder", "📊 Diagramme","📊 Leiter","📊 Escalera" }},
            { "ana.no_sip",       new[]{ "Nessun messaggio SIP.",
                                         "No SIP messages.",
                                         "Aucun message SIP.",
                                         "Keine SIP-Nachrichten.",
                                         "Sin mensajes SIP." }},
            { "ana.loading",      new[]{ "Caricamento PCAP...",
                                         "Loading PCAP...",
                                         "Chargement PCAP...",
                                         "PCAP wird geladen...",
                                         "Cargando PCAP..." }},

            // ── Ladder ──────────────────────────────────────────────────────────
            { "lad.all_calls",    new[]{ "Tutte le chiamate",
                                         "All calls",
                                         "Tous les appels",
                                         "Alle Anrufe",
                                         "Todas las llamadas" }},
            { "lad.filter_call",  new[]{ "Filtra per chiamata:",
                                         "Filter by call:",
                                         "Filtrer par appel:",
                                         "Nach Anruf filtern:",
                                         "Filtrar por llamada:" }},
            { "lad.filter_proto", new[]{ "Protocollo:",
                                         "Protocol:",
                                         "Protocole:",
                                         "Protokoll:",
                                         "Protocolo:" }},
            { "lad.all_protos",   new[]{ "Tutti",     "All",       "Tous",      "Alle",      "Todos" }},
            { "lad.msg_count",    new[]{ "Messaggi: ", "Messages: ","Messages: ","Nachrichten: ","Mensajes: " }},
            { "lad.endpoints",    new[]{ "Endpoint: ", "Endpoints: ","Points: ",  "Endpunkte: ","Endpoints: " }},

            // ── FTP Server ──────────────────────────────────────────────────────
            { "ftp.title",        new[]{ "📂  FTP Server",  "📂  FTP Server",  "📂  Serveur FTP", "📂  FTP-Server",  "📂  Servidor FTP" }},
            { "ftp.port",         new[]{ "Porta FTP:",      "FTP Port:",       "Port FTP:",       "FTP-Port:",       "Puerto FTP:" }},
            { "ftp.root",         new[]{ "Cartella root:",  "Root folder:",    "Dossier racine:", "Wurzelordner:",   "Carpeta raíz:" }},
            { "ftp.user",         new[]{ "Utente:",         "Username:",       "Utilisateur:",    "Benutzer:",       "Usuario:" }},
            { "ftp.pass",         new[]{ "Password:",       "Password:",       "Mot de passe:",   "Passwort:",       "Contraseña:" }},
            { "ftp.anon",         new[]{ "Anonimo",         "Anonymous",       "Anonyme",         "Anonym",          "Anónimo" }},
            { "ftp.readonly",     new[]{ "Sola lettura",    "Read only",       "Lecture seule",   "Nur lesen",       "Solo lectura" }},
            { "ftp.start",        new[]{ "▶ Avvia FTP Server","▶ Start FTP Server","▶ Démarrer FTP","▶ FTP starten","▶ Iniciar FTP" }},
            { "ftp.stop",         new[]{ "■ Ferma",         "■ Stop",          "■ Arrêter",       "■ Stoppen",       "■ Detener" }},
            { "ftp.running",      new[]{ "▶ In ascolto su porta ", "▶ Listening on port ", "▶ Écoute sur port ", "▶ Hört auf Port ", "▶ Escuchando en puerto " }},
            { "ftp.stopped",      new[]{ "⏹ Fermo",         "⏹ Stopped",       "⏹ Arrêté",        "⏹ Gestoppt",      "⏹ Detenido" }},
            { "ftp.log",          new[]{ "📋 Log connessioni:","📋 Connection log:","📋 Journal connexions:","📋 Verbindungsprotokoll:","📋 Registro conexiones:" }},
            { "tftp.title",       new[]{ "📡  TFTP Server","📡  TFTP Server","📡  Serveur TFTP","📡  TFTP-Server","📡  Servidor TFTP" }},
            { "tftp.port",        new[]{ "Porta TFTP:","TFTP port:","Port TFTP :","TFTP-Port:","Puerto TFTP:" }},
            { "tftp.hint",        new[]{
                "  Provisioning telefoni (DHCP option 150), firmware, config. Porte <1024 richiedono privilegi: se la 69 fallisce usa 6969.",
                "  Phone provisioning (DHCP option 150), firmware, config. Ports <1024 need privileges: if 69 fails use 6969.",
                "  Provisioning des téléphones (option DHCP 150), firmware, config. Ports <1024 = privilèges : si 69 échoue, utilisez 6969.",
                "  Telefon-Provisioning (DHCP-Option 150), Firmware, Config. Ports <1024 benötigen Rechte: wenn 69 fehlschlägt, 6969 verwenden.",
                "  Aprovisionamiento de teléfonos (opción DHCP 150), firmware, config. Puertos <1024 requieren privilegios: si 69 falla usa 6969." }},

            // ── SFTP / OpenSSH ──────────────────────────────────────────────────
            { "sftp.title",       new[]{ "🔒  SFTP Server (OpenSSH)",
                                         "🔒  SFTP Server (OpenSSH)",
                                         "🔒  Serveur SFTP (OpenSSH)",
                                         "🔒  SFTP-Server (OpenSSH)",
                                         "🔒  Servidor SFTP (OpenSSH)" }},
            { "sftp.install",     new[]{ "📦 Installa OpenSSH Server","📦 Install OpenSSH Server","📦 Installer OpenSSH","📦 OpenSSH installieren","📦 Instalar OpenSSH" }},
            { "sftp.start",       new[]{ "▶ Avvia servizio SSH","▶ Start SSH service","▶ Démarrer SSH","▶ SSH starten","▶ Iniciar SSH" }},
            { "sftp.stop",        new[]{ "■ Ferma servizio SSH","■ Stop SSH service","■ Arrêter SSH","■ SSH stoppen","■ Detener SSH" }},
            { "sftp.autostart",   new[]{ "⚙ Avvia come automatico all'avvio","⚙ Set automatic startup","⚙ Démarrage automatique","⚙ Autostart einrichten","⚙ Inicio automático" }},
            { "sftp.refresh",     new[]{ "🔄 Aggiorna stato","🔄 Refresh status","🔄 Actualiser état","🔄 Status aktualisieren","🔄 Actualizar estado" }},
            { "sftp.intro1",      new[]{ "LosaTermVoip usa il server OpenSSH integrato in Windows 10/11.","LosaTermVoip uses the OpenSSH server built into Windows 10/11.","LosaTermVoip utilise le serveur OpenSSH intégré à Windows 10/11.","LosaTermVoip nutzt den in Windows 10/11 integrierten OpenSSH-Server.","LosaTermVoip usa el servidor OpenSSH integrado en Windows 10/11." }},
            { "sftp.intro2",      new[]{ "Una volta installato e avviato, puoi collegarti con qualsiasi client SFTP","Once installed and started, you can connect with any SFTP client","Une fois installé et démarré, connectez-vous avec n'importe quel client SFTP","Nach Installation und Start kannst du dich mit jedem SFTP-Client verbinden","Una vez instalado e iniciado, conéctate con cualquier cliente SFTP" }},
            { "sftp.intro3",      new[]{ "usando la porta 22 e le credenziali Windows dell'utente corrente.","using port 22 and the current user's Windows credentials.","en utilisant le port 22 et les identifiants Windows de l'utilisateur courant.","über Port 22 und die Windows-Anmeldedaten des aktuellen Benutzers.","usando el puerto 22 y las credenciales de Windows del usuario actual." }},
            { "sftp.checking",    new[]{ "⏳ Verifica stato...","⏳ Checking status...","⏳ Vérification de l'état...","⏳ Status wird geprüft...","⏳ Comprobando estado..." }},
            { "sftp.conn_params", new[]{ "── Parametri di connessione SFTP ──","── SFTP connection parameters ──","── Paramètres de connexion SFTP ──","── SFTP-Verbindungsparameter ──","── Parámetros de conexión SFTP ──" }},
            { "sftp.share_label", new[]{ "Condividi una cartella (crea una scorciatoia nella tua home):","Share a folder (creates a shortcut in your home):","Partager un dossier (crée un raccourci dans votre home) :","Ordner freigeben (erstellt eine Verknüpfung im Home):","Comparte una carpeta (crea un acceso directo en tu home):" }},
            { "sftp.create_link", new[]{ "🔗 Crea scorciatoia nella home","🔗 Create shortcut in home","🔗 Créer un raccourci dans home","🔗 Verknüpfung im Home erstellen","🔗 Crear acceso directo en home" }},
            { "sftp.open_home",   new[]{ "📂 Apri home","📂 Open home","📂 Ouvrir home","📂 Home öffnen","📂 Abrir home" }},
            { "sftp.dedicated",   new[]{ "── Utente SFTP dedicato (user/password tuoi) ──","── Dedicated SFTP user (your own user/password) ──","── Utilisateur SFTP dédié (vos identifiants) ──","── Dedizierter SFTP-Benutzer (eigene Zugangsdaten) ──","── Usuario SFTP dedicado (tus credenciales) ──" }},
            { "sftp.dedicated_hint",new[]{ "Crea un account Windows locale dedicato all'SFTP, con credenziali a tua scelta:","Create a dedicated local Windows account for SFTP, with credentials of your choice:","Créez un compte Windows local dédié au SFTP, avec les identifiants de votre choix :","Erstelle ein lokales Windows-Konto für SFTP mit eigenen Zugangsdaten:","Crea una cuenta local de Windows dedicada a SFTP, con las credenciales que elijas:" }},
            { "sftp.username",    new[]{ "Username:","Username:","Identifiant :","Benutzername:","Usuario:" }},
            { "sftp.show",        new[]{ "mostra","show","afficher","anzeigen","mostrar" }},
            { "sftp.create_user", new[]{ "👤 Crea / aggiorna utente","👤 Create / update user","👤 Créer / mettre à jour","👤 Benutzer anlegen / aktualisieren","👤 Crear / actualizar usuario" }},
            { "sftp.pass_req",    new[]{
                "⚠ La password deve rispettare la policy del PC/dominio: in genere ≥ 8 caratteri con MAIUSCOLE, minuscole, numeri e un simbolo, e non una usata di recente.",
                "⚠ The password must meet the PC/domain policy: usually ≥ 8 chars with UPPERCASE, lowercase, digits and a symbol, and not a recently used one.",
                "⚠ Le mot de passe doit respecter la stratégie du PC/domaine : en général ≥ 8 caractères avec MAJUSCULES, minuscules, chiffres et un symbole, et non récemment utilisé.",
                "⚠ Das Passwort muss der PC-/Domänenrichtlinie entsprechen: meist ≥ 8 Zeichen mit GROSSBUCHSTABEN, Kleinbuchstaben, Ziffern und Sonderzeichen, und nicht kürzlich verwendet.",
                "⚠ La contraseña debe cumplir la política del PC/dominio: normalmente ≥ 8 caracteres con MAYÚSCULAS, minúsculas, números y un símbolo, y no una usada recientemente." }},

            // ── Messaggi generici ────────────────────────────────────────────────
            { "msg.ok",           new[]{ "OK",           "OK",         "OK",          "OK",            "OK" }},
            { "msg.cancel",       new[]{ "Annulla",      "Cancel",     "Annuler",     "Abbrechen",     "Cancelar" }},
            { "msg.yes",          new[]{ "Sì",           "Yes",        "Oui",         "Ja",            "Sí" }},
            { "msg.no",           new[]{ "No",           "No",         "Non",         "Nein",          "No" }},
            { "msg.error",        new[]{ "Errore",       "Error",      "Erreur",      "Fehler",        "Error" }},
            { "msg.warning",      new[]{ "Attenzione",   "Warning",    "Avertissement","Warnung",      "Advertencia" }},
            { "msg.info",         new[]{ "Informazione", "Information","Information", "Information",   "Información" }},
            { "msg.putty_missing",new[]{ "putty.exe non trovato.\n\nCopia putty.exe nella stessa cartella di LosaTermVoip.exe\noppure in C:\\Program Files\\PuTTY\\",
                                         "putty.exe not found.\n\nCopy putty.exe to the same folder as LosaTermVoip.exe\nor to C:\\Program Files\\PuTTY\\",
                                         "putty.exe introuvable.\n\nCopiez putty.exe dans le même dossier que LosaTermVoip.exe\nou dans C:\\Program Files\\PuTTY\\",
                                         "putty.exe nicht gefunden.\n\nKopieren Sie putty.exe in den Ordner von LosaTermVoip.exe\noder nach C:\\Program Files\\PuTTY\\",
                                         "putty.exe no encontrado.\n\nCopie putty.exe en la misma carpeta que LosaTermVoip.exe\no en C:\\Program Files\\PuTTY\\" }},
            { "msg.tshark_missing",new[]{ "tshark.exe non trovato.\n\nInstalla Wireshark (include tshark):\nhttps://www.wireshark.org/download.html\n\nPoi riavvia LosaTermVoip.",
                                          "tshark.exe not found.\n\nInstall Wireshark (includes tshark):\nhttps://www.wireshark.org/download.html\n\nThen restart LosaTermVoip.",
                                          "tshark.exe introuvable.\n\nInstallez Wireshark (inclut tshark):\nhttps://www.wireshark.org/download.html\n\nRedémarrez LosaTermVoip.",
                                          "tshark.exe nicht gefunden.\n\nInstallieren Sie Wireshark (enthält tshark):\nhttps://www.wireshark.org/download.html\n\nStarten Sie LosaTermVoip neu.",
                                          "tshark.exe no encontrado.\n\nInstale Wireshark (incluye tshark):\nhttps://www.wireshark.org/download.html\n\nReinicie LosaTermVoip." }},

            // ── Impostazioni lingua ───────────────────────────────────────────────
            { "lang.select",      new[]{ "Seleziona lingua:", "Select language:", "Choisir la langue:", "Sprache wählen:", "Seleccionar idioma:" }},
            { "lang.restart",     new[]{ "Lingua cambiata. Riavvia LosaTermVoip per applicare.",
                                         "Language changed. Restart LosaTermVoip to apply.",
                                         "Langue modifiée. Redémarrez LosaTermVoip pour appliquer.",
                                         "Sprache geändert. Starten Sie LosaTermVoip neu.",
                                         "Idioma cambiado. Reinicie LosaTermVoip para aplicar." }},
        };

        // Mappa codice lingua → indice
        static readonly Dictionary<string, int> LangIndex = new Dictionary<string, int>
        {
            { "IT", 0 }, { "EN", 1 }   // solo IT/EN (FR/DE/ES dismessi)
        };

        // ── API pubblica ──────────────────────────────────────────────────────
        public static string T(string key)
        {
            string[] vals;
            if (!Strings.TryGetValue(key, out vals)) return key;
            int idx;
            if (!LangIndex.TryGetValue(CurrentLang, out idx)) idx = 0;
            return idx < vals.Length ? vals[idx] : vals[0];
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        // Helper bilingue per i pannelli nuovi: IT se lingua italiana, altrimenti EN.
        public static string B(string it, string en) { return CurrentLang == "IT" ? it : en; }

        // ── Caricamento e salvataggio ─────────────────────────────────────────
        public static void Load()
        {
            try
            {
                string path = LangFilePath();
                if (File.Exists(path))
                {
                    string saved = File.ReadAllText(path).Trim().ToUpper();
                    if (LangIndex.ContainsKey(saved)) CurrentLang = saved;
                }
            }
            catch { }
        }

        public static void Set(string langCode)
        {
            if (!LangIndex.ContainsKey(langCode.ToUpper())) return;
            CurrentLang = langCode.ToUpper();
            try
            {
                string dir = Path.GetDirectoryName(LangFilePath());
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                File.WriteAllText(LangFilePath(), CurrentLang);
            }
            catch { }
        }

        static string LangFilePath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LosaTermVoip", "language.cfg");
        }

        // ── Dialog selezione lingua ───────────────────────────────────────────
        public static void ShowLanguageDialog(System.Windows.Forms.Form owner)
        {
            var langs = new[]
            {
                new { Code="IT", Label="🇮🇹  Italiano" },
                new { Code="EN", Label="🇬🇧  English" },
                new { Code="FR", Label="🇫🇷  Français" },
                new { Code="DE", Label="🇩🇪  Deutsch" },
                new { Code="ES", Label="🇪🇸  Español" },
            };

            var dlg = new Form
            {
                Text = T("lang.select"),
                Size = new System.Drawing.Size(300, 280),
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterParent,
                BackColor = System.Drawing.Color.FromArgb(28, 28, 40),
                ForeColor = System.Drawing.Color.White,
                Owner = owner
            };

            var lbl = new System.Windows.Forms.Label
            {
                Text = T("lang.select"),
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 32, TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                ForeColor = System.Drawing.Color.LightCyan,
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
            };
            dlg.Controls.Add(lbl);

            var pnl = new System.Windows.Forms.FlowLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.TopDown,
                Padding = new System.Windows.Forms.Padding(20, 8, 8, 8),
                BackColor = System.Drawing.Color.FromArgb(28, 28, 40)
            };
            dlg.Controls.Add(pnl);

            foreach (var lang in langs)
            {
                var code = lang.Code;
                var btn = new System.Windows.Forms.Button
                {
                    Text = lang.Label,
                    Width = 230, Height = 34,
                    FlatStyle = System.Windows.Forms.FlatStyle.Flat,
                    BackColor = code == CurrentLang
                        ? System.Drawing.Color.FromArgb(30, 80, 160)
                        : System.Drawing.Color.FromArgb(45, 45, 65),
                    ForeColor = System.Drawing.Color.White,
                    Font = new System.Drawing.Font("Segoe UI", 10),
                    Margin = new System.Windows.Forms.Padding(0, 4, 0, 4)
                };
                btn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 100, 160);
                btn.Click += (s, e) =>
                {
                    Set(code);
                    dlg.DialogResult = System.Windows.Forms.DialogResult.OK;
                    dlg.Close();
                    System.Windows.Forms.MessageBox.Show(
                        T("lang.restart"), "LosaTermVoip",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Information);
                };
                pnl.Controls.Add(btn);
            }

            dlg.ShowDialog(owner);
        }
    }
}
