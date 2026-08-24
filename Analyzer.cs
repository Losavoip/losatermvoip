using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LosaTermVoip
{
    // ─── Severity ─────────────────────────────────────────────────────────────
    public enum Severity { Info, Warning, Error, Critical }

    // ─── Regola analizzatore ──────────────────────────────────────────────────
    public class AnalyzerRule
    {
        public string   Name        { get; set; }
        public string   Pattern     { get; set; }
        public Severity Severity    { get; set; }
        public string   Explanation { get; set; }
        public string   Device      { get; set; } // CUBE | CUCM | All

        public AnalyzerRule(string name, string pattern, Severity sev, string explanation, string device = "All")
        {
            Name = name; Pattern = pattern; Severity = sev; Explanation = explanation; Device = device;
        }
    }

    // ─── Finding ──────────────────────────────────────────────────────────────
    public class Finding
    {
        public DateTime Timestamp   { get; set; }
        public Severity Severity    { get; set; }
        public string   RuleName    { get; set; }
        public string   LineSnippet { get; set; }
        public string   Explanation { get; set; }
    }

    // ─── Regole Cisco ─────────────────────────────────────────────────────────
    public static class CiscoRules
    {
        public static List<AnalyzerRule> Get(string deviceType)
        {
            var r = new List<AnalyzerRule>();
            bool cube = deviceType == "CUBE"  || deviceType == "Auto";
            bool cucm = deviceType == "CUCM"  || deviceType == "Auto";

            if (cube)
            {
                // ── SIP responses ───────────────────────────────────────────
                r.Add(new AnalyzerRule("SIP 408 Timeout",         @"408 Request Timeout",              Severity.Error,    L.B("Timeout SIP: destinatario non risponde. Verificare connettività IP, firewall, e stato trunk.","SIP timeout: the peer isn't responding. Check IP connectivity, firewall, and trunk status."), "CUBE"));
                r.Add(new AnalyzerRule("SIP 403 Forbidden",       @"403 Forbidden",                    Severity.Error,    L.B("Chiamata rifiutata (403). Trunk non autorizzato, ACL SIP, o IP non nella whitelist CUCM.","Call rejected (403). Unauthorized trunk, SIP ACL, or IP not in the CUCM whitelist."), "CUBE"));
                r.Add(new AnalyzerRule("SIP 404 Not Found",       @"404 Not Found",                    Severity.Error,    L.B("Numero non trovato (404). Verificare dial-plan, translation pattern e route pattern.","Number not found (404). Check dial-plan, translation pattern and route pattern."), "CUBE"));
                r.Add(new AnalyzerRule("SIP 480 Unavailable",     @"480 Temporarily Unavailable",      Severity.Warning,  L.B("Endpoint temporaneamente non disponibile. Probabile nessun agente/device registrato.","Endpoint temporarily unavailable. Likely no agent/device registered."), "CUBE"));
                r.Add(new AnalyzerRule("SIP 486 Busy",            @"486 Busy Here",                    Severity.Warning,  L.B("Utente occupato (486 Busy Here).","User busy (486 Busy Here)."), "CUBE"));
                r.Add(new AnalyzerRule("SIP 487 Cancelled",       @"487 Request Terminated",           Severity.Info,     L.B("Chiamata cancellata prima della risposta (487).","Call cancelled before the answer (487)."), "CUBE"));
                r.Add(new AnalyzerRule("SIP 488 Not Acceptable",  @"488 Not Acceptable",               Severity.Error,    L.B("Codec non accettabile (488). Mismatch SDP/codec tra CUBE e CUCM.","Codec not acceptable (488). SDP/codec mismatch between CUBE and CUCM."), "CUBE"));
                r.Add(new AnalyzerRule("SIP 500 Server Error",    @"500 Internal Server Error",        Severity.Error,    L.B("Errore interno server SIP (500). Verificare log CUCM o gateway.","SIP server internal error (500). Check CUCM or gateway logs."), "CUBE"));
                r.Add(new AnalyzerRule("SIP 503 Unavailable",     @"503 Service Unavailable",          Severity.Error,    L.B("Servizio non disponibile (503). CUCM o trunk giù. Verificare servizi.","Service unavailable (503). CUCM or trunk down. Check services."), "CUBE"));

                // ── Q.850 Cause Codes ────────────────────────────────────────
                r.Add(new AnalyzerRule("Q.850 #1  " + L.B("Numero inesistente","Unallocated number"),  @"[Cc]ause\s*[=:]\s*1\b",  Severity.Error,   L.B("Q.850 Cause 1: Numero non assegnato/inesistente. Verificare destination-pattern e dial-plan.","Q.850 Cause 1: Unallocated/non-existent number. Check destination-pattern and dial-plan."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #16 " + L.B("Chiamata OK","Call OK"),         @"[Cc]ause\s*[=:]\s*16\b", Severity.Info,    L.B("Q.850 Cause 16: Normal call clearing. Terminazione normale.","Q.850 Cause 16: Normal call clearing."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #17 " + L.B("Occupato","Busy"),            @"[Cc]ause\s*[=:]\s*17\b", Severity.Warning, L.B("Q.850 Cause 17: User busy. L'utente è occupato.","Q.850 Cause 17: User busy."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #18 " + L.B("Nessuna risposta","No answer"),    @"[Cc]ause\s*[=:]\s*18\b", Severity.Warning, "Q.850 Cause 18: No user responding. Ring timeout.", "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #19 " + L.B("Nessuna risposta","No answer"),    @"[Cc]ause\s*[=:]\s*19\b", Severity.Warning, "Q.850 Cause 19: No answer from user on time.", "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #21 " + L.B("Chiamata rifiutata","Call rejected"),  @"[Cc]ause\s*[=:]\s*21\b", Severity.Error,   L.B("Q.850 Cause 21: Call rejected. Destinazione rifiuta esplicitamente.","Q.850 Cause 21: Call rejected. The destination explicitly rejects it."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #27 " + L.B("Destinaz. fuori svc","Dest. out of order"), @"[Cc]ause\s*[=:]\s*27\b", Severity.Error,   L.B("Q.850 Cause 27: Destination out of order. Device non raggiungibile.","Q.850 Cause 27: Destination out of order. Device unreachable."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #28 " + L.B("Formato num. errato","Invalid num. format"), @"[Cc]ause\s*[=:]\s*28\b", Severity.Error,   L.B("Q.850 Cause 28: Invalid number format. Verificare E.164 e prefissi.","Q.850 Cause 28: Invalid number format. Check E.164 and prefixes."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #34 " + L.B("No circuito","No circuit"),         @"[Cc]ause\s*[=:]\s*34\b", Severity.Error,   L.B("Q.850 Cause 34: No circuit available. Trunk saturo o linee PRI/BRI esaurite.","Q.850 Cause 34: No circuit available. Trunk saturated or PRI/BRI lines exhausted."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #38 " + L.B("Rete fuori svc","Network out of order"),      @"[Cc]ause\s*[=:]\s*38\b", Severity.Error,   L.B("Q.850 Cause 38: Network out of order. Problema rete/carrier.","Q.850 Cause 38: Network out of order. Network/carrier problem."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #41 " + L.B("Guasto temporaneo","Temporary failure"),   @"[Cc]ause\s*[=:]\s*41\b", Severity.Warning, L.B("Q.850 Cause 41: Temporary failure. Guasto temporaneo, ritentare.","Q.850 Cause 41: Temporary failure. Retry."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #47 " + L.B("Risorse esaurite","Resources exhausted"),    @"[Cc]ause\s*[=:]\s*47\b", Severity.Error,   L.B("Q.850 Cause 47: Resource unavailable. DSP/canali media esauriti.","Q.850 Cause 47: Resource unavailable. DSP/media channels exhausted."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #65 " + L.B("Bearer non support.","Bearer not implemented"), @"[Cc]ause\s*[=:]\s*65\b", Severity.Error,   L.B("Q.850 Cause 65: Bearer capability not implemented. Tipo chiamata non supportato.","Q.850 Cause 65: Bearer capability not implemented. Call type not supported."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #88 " + L.B("Incompatibile","Incompatible"),       @"[Cc]ause\s*[=:]\s*88\b", Severity.Error,   L.B("Q.850 Cause 88: Incompatible destination. Codec/bearer incompatibili.","Q.850 Cause 88: Incompatible destination. Incompatible codec/bearer."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #102 " + L.B("Timer scaduto","Timer expired"),      @"[Cc]ause\s*[=:]\s*102\b",Severity.Warning, L.B("Q.850 Cause 102: Recovery on timer expiry. Loop SIP o ritardo eccessivo.","Q.850 Cause 102: Recovery on timer expiry. SIP loop or excessive delay."), "CUBE"));
                r.Add(new AnalyzerRule("Q.850 #111 " + L.B("Errore protocollo","Protocol error"),  @"[Cc]ause\s*[=:]\s*111\b",Severity.Error,   L.B("Q.850 Cause 111: Protocol error. Messaggio SIP/Q.931 malformato.","Q.850 Cause 111: Protocol error. Malformed SIP/Q.931 message."), "CUBE"));

                // ── CCSIP / IOS VoIP ─────────────────────────────────────────
                r.Add(new AnalyzerRule("CCSIP Error",          @"%CCSIP-\d-\w+",                     Severity.Error,   L.B("Errore CCSIP IOS. Controllare tipo e causa dell'errore.","IOS CCSIP error. Check the error type and cause."), "CUBE"));
                r.Add(new AnalyzerRule("Voice IEC Error",      @"%VOICE_IEC-\d",                     Severity.Warning, L.B("Internal Error Code VoIP. Verificare causa nel log.","VoIP Internal Error Code. Check the cause in the log."), "CUBE"));
                r.Add(new AnalyzerRule("No Dial-Peer",         @"[Nn]o dial.peer|dial.peer not found", Severity.Error, L.B("Nessun dial-peer match. Verificare destination-pattern e incoming chiamante.","No dial-peer match. Check destination-pattern and incoming caller."), "CUBE"));
                r.Add(new AnalyzerRule("Codec Mismatch",       @"[Nn]o common codec|codec.*mismatch|[Ii]ncompatible codec", Severity.Error, L.B("Nessun codec in comune. Allineare codec list in dial-peer e CUCM region.","No common codec. Align the codec list in dial-peer and CUCM region."), "CUBE"));
                r.Add(new AnalyzerRule("RTP Error",            @"[Rr][Tt][Pp].*(error|fail|timeout)", Severity.Error,  L.B("Errore sessione RTP. Possibile problema audio/one-way. Verificare NAT e ACL.","RTP session error. Possible audio/one-way issue. Check NAT and ACL."), "CUBE"));
                r.Add(new AnalyzerRule("DTMF Issue",           @"[Dd][Tt][Mm][Ff].*(fail|error|mismatch)|kpml.*error", Severity.Warning, L.B("Problema DTMF. Verificare modalità (rfc2833/inband/kpml) tra i due leg.","DTMF problem. Check the mode (rfc2833/inband/kpml) between the two legs."), "CUBE"));
                r.Add(new AnalyzerRule("TLS/SSL Error",        @"[Tt][Ll][Ss].*(error|fail)|[Ss][Ss][Ll].*(error|fail)|certificate.*invalid", Severity.Error, L.B("Errore TLS/SSL. Verificare certificati, truststore e configurazione TLS.","TLS/SSL error. Check certificates, truststore and TLS configuration."), "CUBE"));
                r.Add(new AnalyzerRule("Early Media Problem",  @"early.media.*(fail|error)|[Rr][Bb][Pp].*(error|fail)", Severity.Warning, L.B("Problema early media. Possibile audio one-way in fase di ring.","Early media problem. Possible one-way audio during ringing."), "CUBE"));
                r.Add(new AnalyzerRule("T38 Fax Error",        @"[Tt]38.*(fail|error|reject)|fax.*(fail|error)", Severity.Warning, L.B("Problema fax T.38. Verificare configurazione fax su dial-peer.","T.38 fax problem. Check fax configuration on the dial-peer."), "CUBE"));
                r.Add(new AnalyzerRule("SRTP Error",           @"[Ss][Rr][Tt][Pp].*(error|fail)|[Cc]rypto.*mismatch", Severity.Error, L.B("Errore SRTP/crypto. Verificare policy media encryption su CUCM e CUBE.","SRTP/crypto error. Check media encryption policy on CUCM and CUBE."), "CUBE"));

                // ── Call Flow ────────────────────────────────────────────────
                r.Add(new AnalyzerRule(L.B("INVITE entrante","INVITE incoming"),   @"^INVITE sip:",                          Severity.Info, L.B("Chiamata SIP entrante.","Incoming SIP call."), "CUBE"));
                r.Add(new AnalyzerRule(L.B("INVITE uscente","INVITE outgoing"),    @"Sending:\s*\r?\nINVITE sip:",            Severity.Info, L.B("Chiamata SIP uscente.","Outgoing SIP call."), "CUBE"));
                r.Add(new AnalyzerRule("200 OK",            @"SIP/2\.0 200 OK",                       Severity.Info, L.B("Chiamata stabilita (200 OK).","Call established (200 OK)."), "CUBE"));
                r.Add(new AnalyzerRule("BYE",               @"^BYE sip:|Sending.*\nBYE sip:",         Severity.Info, L.B("Terminazione chiamata (BYE).","Call termination (BYE)."), "CUBE"));
                r.Add(new AnalyzerRule("CANCEL",            @"^CANCEL sip:",                          Severity.Warning, L.B("Chiamata annullata (CANCEL).","Call cancelled (CANCEL)."), "CUBE"));
            }

            if (cucm)
            {
                r.Add(new AnalyzerRule("DB Error",             @"[Dd][Bb][_\s][Ee]rror|[Dd]atabase.*[Ff]ail", Severity.Error,   L.B("Errore database CUCM. Verificare stato Publisher DB e replication.","CUCM database error. Check Publisher DB status and replication."), "CUCM"));
                r.Add(new AnalyzerRule(L.B("Registrazione fallita","Registration failed"),@"[Rr]egist.*[Ff]ail|REGISTER.*40[13]|[Rr]egist.*[Rr]eject", Severity.Error, L.B("Registrazione endpoint fallita. Verificare credenziali, pool sicurezza, partition.","Endpoint registration failed. Check credentials, security profile, partition."), "CUCM"));
                r.Add(new AnalyzerRule("CTI/JTAPI Error",      @"CTI.*[Ee]rror|JTAPI.*[Ff]ail",       Severity.Error,   L.B("Errore CTI/JTAPI. Verificare connessione applicazioni CTI e licenze.","CTI/JTAPI error. Check CTI application connectivity and licenses."), "CUCM"));
                r.Add(new AnalyzerRule("Media Resource",       @"[Mm]edia [Rr]esource.*[Uu]navail|MTP.*fail|[Tt]ranscoder.*fail", Severity.Error, L.B("Risorsa media non disponibile. Verificare MTP/Transcoder e MRGL.","Media resource unavailable. Check MTP/Transcoder and MRGL."), "CUCM"));
                r.Add(new AnalyzerRule(L.B("Certificato","Certificate"),          @"[Cc]ertif.*[Ee]xpir|ITLFile.*error|[Tt]omcat.*cert.*fail", Severity.Error, L.B("Problema certificato CUCM. Verificare scadenza, ITL e rigenerazione cert.","CUCM certificate problem. Check expiry, ITL and cert regeneration."), "CUCM"));
                r.Add(new AnalyzerRule("Extension Mobility",   @"EM.*[Ff]ail|[Ee]xtension [Mm]obility.*error", Severity.Warning, L.B("Problema Extension Mobility. Verificare servizio EM e profilo device.","Extension Mobility problem. Check the EM service and device profile."), "CUCM"));
                r.Add(new AnalyzerRule("Trunk SIP Down",       @"[Ss][Ii][Pp].*[Tt]runk.*[Uu]nreg|[Tt]runk.*[Oo]ffline", Severity.Error, L.B("Trunk SIP CUCM non registrato. Verificare configurazione e raggiungibilità CUBE.","CUCM SIP trunk not registered. Check configuration and CUBE reachability."), "CUCM"));
                r.Add(new AnalyzerRule("Route Plan Error",     @"[Rr]oute [Pp]lan.*[Ff]ail|[Nn]o [Rr]oute [Pp]attern", Severity.Error, L.B("Route pattern non trovato. Verificare dial plan e partition/CSS.","Route pattern not found. Check dial plan and partition/CSS."), "CUCM"));
                r.Add(new AnalyzerRule("Service Failure",      @"[Ss]ervice.*[Ff]ailed|[Cc]all[Mm]anager.*[Ss]top", Severity.Critical, L.B("Servizio CUCM in stato Failed o stoppato.","CUCM service in Failed or stopped state."), "CUCM"));
            }

            // ── H.323 (sempre incluse) ────────────────────────────────────────────
            r.Add(new AnalyzerRule("H.323 Setup",           @"\bSetup\b.*(?:H\.225|H323|h225)",                   Severity.Info,    L.B("Chiamata H.323 in ingresso/uscita (Setup).","Incoming/outgoing H.323 call (Setup)."), "All"));
            r.Add(new AnalyzerRule("H.323 ReleaseComplete",  @"[Rr]elease[Cc]omplete|[Hh]323.*[Rr]elease",         Severity.Info,    L.B("H.323 ReleaseComplete — terminazione chiamata.","H.323 ReleaseComplete — call termination."), "All"));
            r.Add(new AnalyzerRule("H.323 FastConnect",      @"[Ff]ast[Cc]onnect|fastStart",                       Severity.Info,    L.B("H.323 FastConnect (fastStart) rilevato.","H.323 FastConnect (fastStart) detected."), "All"));
            r.Add(new AnalyzerRule("H.323 H245 Tunnel",      @"[Hh]245.*[Tt]unnel|[Tt]unnel.*[Hh]245",            Severity.Info,    L.B("H.245 Tunneling attivo nel canale H.225.","H.245 tunneling active in the H.225 channel."), "All"));
            r.Add(new AnalyzerRule("H.323 Reject",           @"[Cc]all[Pp]roceeding.*[Rr]eject|[Cc]onference.*[Rr]eject|[Rr]elease.*[Rr]eason", Severity.Warning, L.B("H.323 Chiamata rifiutata — verificare causa.","H.323 call rejected — check the cause."), "All"));
            r.Add(new AnalyzerRule("H.323 Gatekeeper ARQ",  @"\bARQ\b|\bACF\b|\bARJ\b",                           Severity.Info,    L.B("H.323 Admission Request/Confirm/Reject verso Gatekeeper.","H.323 Admission Request/Confirm/Reject towards the Gatekeeper."), "All"));
            r.Add(new AnalyzerRule("H.323 GK ARJ",          @"\bARJ\b",                                           Severity.Error,   L.B("H.323 Admission Reject — Gatekeeper rifiuta la chiamata.","H.323 Admission Reject — the Gatekeeper rejects the call."), "All"));
            r.Add(new AnalyzerRule("H.323 RAS Error",       @"\bRRJ\b|\bURJ\b|\bDRJ\b",                          Severity.Error,   L.B("H.323 RAS Reject (RRJ/URJ/DRJ) — problema registrazione/deregistrazione.","H.323 RAS Reject (RRJ/URJ/DRJ) — registration/deregistration problem."), "All"));
            r.Add(new AnalyzerRule("H.245 TermCapSet",       @"[Tt]erminal[Cc]apability[Ss]et",                   Severity.Info,    L.B("H.245 Terminal Capability Set — negoziazione codec.","H.245 Terminal Capability Set — codec negotiation."), "All"));
            r.Add(new AnalyzerRule("H.245 OLC",              @"[Oo]pen[Ll]ogical[Cc]hannel",                       Severity.Info,    L.B("H.245 OpenLogicalChannel — apertura canale media.","H.245 OpenLogicalChannel — media channel opening."), "All"));
            r.Add(new AnalyzerRule("H.245 CloseLC",          @"[Cc]lose[Ll]ogical[Cc]hannel",                     Severity.Info,    L.B("H.245 CloseLogicalChannel — chiusura canale media.","H.245 CloseLogicalChannel — media channel closing."), "All"));

            // ── MEGACO / H.248 ───────────────────────────────────────────────────
            r.Add(new AnalyzerRule("MEGACO ServiceChange",   @"[Ss]ervice[Cc]hange",                              Severity.Info,    L.B("MEGACO ServiceChange — registrazione o deregistrazione Media Gateway.","MEGACO ServiceChange — Media Gateway registration or deregistration."), "All"));
            r.Add(new AnalyzerRule("MEGACO Add",             @"\bMEGACO\b.*\bAdd\b|\bAdd\b.*\bMEGACO\b|megaco.*\bAdd\b", Severity.Info, L.B("MEGACO Add — creazione terminazione/context.","MEGACO Add — termination/context creation."), "All"));
            r.Add(new AnalyzerRule("MEGACO Subtract",        @"[Mm]egaco.*[Ss]ubtract|[Ss]ubtract.*[Hh]248",      Severity.Info,    L.B("MEGACO Subtract — rilascio terminazione.","MEGACO Subtract — termination release."), "All"));
            r.Add(new AnalyzerRule("MEGACO Modify",          @"[Mm]egaco.*[Mm]odify",                              Severity.Info,    L.B("MEGACO Modify — modifica parametri terminazione (codec, RTP).","MEGACO Modify — termination parameter change (codec, RTP)."), "All"));
            r.Add(new AnalyzerRule("MEGACO Error",           @"[Mm]egaco.*[Ee]rror|[Hh]248.*[Ee]rror|megaco.*\b5[0-9]{2}\b", Severity.Error, L.B("MEGACO errore — verificare codice causa e stato Media Gateway.","MEGACO error — check the cause code and Media Gateway status."), "All"));
            r.Add(new AnalyzerRule("MEGACO Notify",          @"[Mm]egaco.*[Nn]otify",                              Severity.Info,    L.B("MEGACO Notify — evento segnalato dal Media Gateway (DTMF, fax, on/off hook).","MEGACO Notify — event reported by the Media Gateway (DTMF, fax, on/off hook)."), "All"));

            // ── SKINNY / SCCP ─────────────────────────────────────────────────────
            r.Add(new AnalyzerRule("SCCP Register",         @"[Rr]egister[Mm]essage|SCCP.*[Rr]egist",             Severity.Info,    L.B("SCCP/Skinny RegisterMessage — telefono si registra a CUCM.","SCCP/Skinny RegisterMessage — phone registers to CUCM."), "All"));
            r.Add(new AnalyzerRule("SCCP CallState",        @"[Cc]all[Ss]tate[Mm]essage|[Cc]all[Ss]tate.*SCCP",   Severity.Info,    L.B("SCCP CallState — cambio stato chiamata Skinny.","SCCP CallState — Skinny call state change."), "All"));
            r.Add(new AnalyzerRule("SCCP OpenReceiveChannel",@"[Oo]pen[Rr]eceive[Cc]hannel",                      Severity.Info,    L.B("SCCP OpenReceiveChannel — apertura canale RTP su telefono Skinny.","SCCP OpenReceiveChannel — RTP channel opening on a Skinny phone."), "All"));
            r.Add(new AnalyzerRule("SCCP CloseReceiveChannel",@"[Cc]lose[Rr]eceive[Cc]hannel",                   Severity.Info,    L.B("SCCP CloseReceiveChannel — chiusura canale RTP.","SCCP CloseReceiveChannel — RTP channel closing."), "All"));
            r.Add(new AnalyzerRule("SCCP StartMediaTransmission",@"[Ss]tart[Mm]edia[Tt]ransmission",              Severity.Info,    L.B("SCCP StartMediaTransmission — inizio flusso RTP.","SCCP StartMediaTransmission — RTP stream start."), "All"));
            r.Add(new AnalyzerRule("SCCP StopMediaTransmission",@"[Ss]top[Mm]edia[Tt]ransmission",               Severity.Info,    L.B("SCCP StopMediaTransmission — fine flusso RTP.","SCCP StopMediaTransmission — RTP stream end."), "All"));
            r.Add(new AnalyzerRule("SCCP Reset",            @"[Rr]eset.*SCCP|SCCP.*[Rr]eset|[Kk]eep[Aa]live.*[Ff]ail", Severity.Warning, L.B("SCCP Reset o KeepAlive fallito — possibile perdita registrazione telefono.","SCCP Reset or KeepAlive failed — possible phone registration loss."), "All"));
            r.Add(new AnalyzerRule("SCCP Unregister",       @"[Uu]nregister[Mm]essage|SCCP.*[Uu]nreg",            Severity.Warning, L.B("SCCP UnregisterMessage — telefono si deregistra.","SCCP UnregisterMessage — phone deregisters."), "All"));

            // ── SIP-I (SIP con ISUP incapsulato) ─────────────────────────────────
            r.Add(new AnalyzerRule("SIP-I ISUP Body",        @"[Cc]ontent-[Tt]ype.*application/isup|[Mm]imeType.*isup", Severity.Info, L.B("SIP-I: body ISUP trovato. Chiamata verso/da rete SS7/PSTN.","SIP-I: ISUP body found. Call to/from an SS7/PSTN network."), "All"));
            r.Add(new AnalyzerRule("SIP-I IAM",              @"[Ii][Aa][Mm].*isup|isup.*Initial [Aa]ddress",       Severity.Info,    L.B("SIP-I Initial Address Message (IAM) — setup chiamata SS7.","SIP-I Initial Address Message (IAM) — SS7 call setup."), "All"));
            r.Add(new AnalyzerRule("SIP-I ANM",              @"\bANM\b.*isup|isup.*Answer",                        Severity.Info,    L.B("SIP-I Answer Message (ANM) — risposta chiamata SS7.","SIP-I Answer Message (ANM) — SS7 call answer."), "All"));
            r.Add(new AnalyzerRule("SIP-I REL",              @"\bREL\b.*isup|isup.*[Rr]elease",                    Severity.Info,    L.B("SIP-I Release Message (REL) — terminazione chiamata SS7.","SIP-I Release Message (REL) — SS7 call termination."), "All"));
            r.Add(new AnalyzerRule("SIP-I Cause Mismatch",   @"isup.*cause.*mismatch|[Cc]ause.*isup.*unmatch",     Severity.Warning, L.B("SIP-I: disallineamento cause ISUP/SIP. Verificare mapping cause.","SIP-I: ISUP/SIP cause mismatch. Check cause mapping."), "All"));

            // ── IOS / Piattaforma — anomalie generiche (sempre incluse) ──────────
            // Formato syslog IOS: %FACILITY-SEVERITY-MNEMONIC (severity 0=emerg … 7=debug)
            r.Add(new AnalyzerRule(L.B("IOS severità critica","IOS critical severity"), @"%[A-Z0-9_]+-[0-2]-[A-Z0-9_]+", Severity.Critical, L.B("Messaggio IOS di severità alta (emergency/alert/critical). Da verificare subito.","High-severity IOS message (emergency/alert/critical). Check immediately."), "All"));
            r.Add(new AnalyzerRule(L.B("IOS errore","IOS error"),           @"%[A-Z0-9_]+-3-[A-Z0-9_]+",     Severity.Error,    L.B("Messaggio IOS di errore (severity 3).","IOS error message (severity 3)."), "All"));
            r.Add(new AnalyzerRule(L.B("Interfaccia DOWN","Interface DOWN"),     @"%(LINK|LINEPROTO)-\d-UPDOWN:.*(changed state to (down|administratively down)|Administrative Shutdown)", Severity.Warning, L.B("Interfaccia / line protocol passata a DOWN.","Interface / line protocol went DOWN."), "All"));
            r.Add(new AnalyzerRule("Err-disable",          @"err[_-]?disable", Severity.Error, L.B("Porta in err-disable. Verificare la causa (security violation, BPDU guard, flapping…).","Port in err-disable. Check the cause (security violation, BPDU guard, flapping…)."), "All"));
            r.Add(new AnalyzerRule("Card/SPA offline",     @"OFFLINECARD|CARDREMOVE|OIR-\d-REM(SPA|CARD)", Severity.Warning, L.B("Scheda/SPA rimossa o offline (OIR).","Card/SPA removed or offline (OIR)."), "All"));
            r.Add(new AnalyzerRule("Reload / Crash",       @"%SYS-\d-RELOAD|System restarted|Traceback|crashinfo|forced reload", Severity.Critical, L.B("Reload, crash o traceback del dispositivo.","Device reload, crash or traceback."), "All"));
            r.Add(new AnalyzerRule(L.B("Memoria / CPU","Memory / CPU"),        @"%SYS-2-MALLOCFAIL|%SYS-\d-CPUHOG|[Mm]emory.*low|[Hh]igh CPU", Severity.Error, L.B("Problema di memoria o CPU sul dispositivo.","Memory or CPU problem on the device."), "All"));
            r.Add(new AnalyzerRule("Duplex mismatch",      @"duplex mismatch|%CDP-4-DUPLEX", Severity.Warning, L.B("Possibile duplex mismatch su una porta.","Possible duplex mismatch on a port."), "All"));
            r.Add(new AnalyzerRule("Routing neighbor down",@"%(OSPF|BGP|EIGRP)-\d-(ADJCHG|ADJCHANGE|NBRCHANGE).*(Down|DOWN)|[Nn]eighbor.*Down", Severity.Warning, L.B("Adiacenza/neighbor di routing caduta.","Routing adjacency/neighbor went down."), "All"));
            r.Add(new AnalyzerRule(L.B("Login fallito","Login failed"),        @"%SEC_LOGIN-\d-LOGIN_FAILED|[Ll]ogin failed|authentication failed", Severity.Warning, L.B("Login/autenticazione fallita sul dispositivo.","Login/authentication failed on the device."), "All"));
            // ── Voce/IOS hardware — utili sui gateway/SBC ──
            r.Add(new AnalyzerRule("Controller/PRI down",  @"%(ISDN|CONTROLLER|DSX1)-\d-.*(DOWN|LAYER2_DOWN|loss of frame|LOS)", Severity.Error, L.B("Controller/PRI/E1-T1 in errore o down. Verificare il link verso il carrier.","Controller/PRI/E1-T1 in error or down. Check the link to the carrier."), "All"));
            r.Add(new AnalyzerRule("Voice port / DSP",     @"%FARM_DSPRM|%DSPRM|dsp.*(fail|crash|alarm)|PVDM.*(fail|offline)|%VOICE_HA", Severity.Warning, L.B("Problema DSP/PVDM o voice port: possibile perdita di risorse media.","DSP/PVDM or voice port problem: possible loss of media resources."), "All"));
            r.Add(new AnalyzerRule(L.B("NTP non sincronizzato","NTP not synchronized"),@"%NTP.*(not synchronized|unsynchronized|UNSYNC)|clock is unsynchronized", Severity.Warning, L.B("Orologio non sincronizzato (NTP): può rompere TLS/SRTP e disallineare i CDR. Usa Net Tools → NTP.","Clock not synchronized (NTP): can break TLS/SRTP and misalign CDRs. Use Net Tools → NTP."), "All"));
            r.Add(new AnalyzerRule(L.B("Alimentazione/Ventola","Power/Fan"), @"%ENVMON|%PLATFORM_THERMAL|%CISCO_ENVMON|power.?supply.*(fail|down)|fan.*(fail|fault)|over.?temp", Severity.Critical, L.B("Allarme ambientale: alimentatore, ventola o temperatura. Intervento hardware.","Environmental alarm: power supply, fan or temperature. Hardware action needed."), "All"));
            r.Add(new AnalyzerRule(L.B("Config cambiata","Config changed"),      @"%SYS-5-CONFIG_I:.*Configured", Severity.Info, L.B("Configurazione modificata sul dispositivo (audit).","Configuration changed on the device (audit)."), "All"));

            return r;
        }

        // Auto-detection tipo device
        public static string DetectDevice(string logContent)
        {
            if (Regex.IsMatch(logContent, @"CCSIP|ccsip|dial.peer|%VOICE|IOS.*Software|CUBE", RegexOptions.IgnoreCase)) return "CUBE";
            if (Regex.IsMatch(logContent, @"Cisco Unified CM|CUCM|CallManager|sipcc|ccm\d", RegexOptions.IgnoreCase)) return "CUCM";
            return "Auto";
        }

        // Protocoli PCAP rilevati nel file (per il label "PCAP: SIP+H.323" ecc.)
        public static string DetectPcapProtocols(string tshark, string pcap)
        {
            var protos = new List<string>();
            // Conta pacchetti per protocollo
            var checks = new[] {
                new[] { "sip",    "SIP"    },
                new[] { "h225",   "H.323"  },
                new[] { "h245",   "H.245"  },
                new[] { "megaco", "MEGACO" },
                new[] { "skinny", "SKINNY" },
            };
            foreach (var check in checks)
            {
                try {
                    var psi = new ProcessStartInfo(tshark,
                        string.Format("-r \"{0}\" -Y {1} -T fields -e frame.number -E header=n", pcap, check[0])) {
                        UseShellExecute = false, RedirectStandardOutput = true,
                        RedirectStandardError = true, CreateNoWindow = true
                    };
                    var p = Process.Start(psi);
                    // Leggi max 5 righe — basta sapere se esiste
                    int count = 0;
                    string ln;
                    while ((ln = p.StandardOutput.ReadLine()) != null && count < 5) {
                        if (!string.IsNullOrWhiteSpace(ln)) count++;
                    }
                    p.Kill(); try { p.WaitForExit(3000); } catch { }
                    if (count > 0) protos.Add(check[1]);
                } catch { }
            }
            return protos.Count > 0 ? string.Join("+", protos.ToArray()) : "SIP";
        }
    }

    // ─── Call Flow Entry ──────────────────────────────────────────────────────
    public class CallFlowEntry
    {
        public string Time     { get; set; }
        public string CallId   { get; set; }
        public string Method   { get; set; }
        public string From     { get; set; }
        public string To       { get; set; }
        public string Direction { get; set; } // IN / OUT
    }

    // ─── Log Analyzer Panel ───────────────────────────────────────────────────
    public class LogAnalyzerPanel : UserControl
    {
        string logFilePath;
        long   lastPos = 0;
        string deviceType = "Auto";
        List<AnalyzerRule> rules;
        List<CallFlowEntry> callFlow = new List<CallFlowEntry>();

        ListView lvFindings;
        TextBox  txtDetail;
        TextBox  txtCallFlow;
        SipLadderPanel ladderPanel;
        TabControl rightTabs;
        ComboBox cmbDevice;
        Label    lblStats;
        System.Windows.Forms.Timer pollTimer;

        int cntErr = 0, cntWarn = 0, cntInfo = 0;

        // Stato stateful del parser call flow (persiste tra un poll e il successivo)
        string cfDirection = "?";   // ultima direzione rilevata (IN/OUT)
        string cfCallId    = "?";   // ultimo Call-ID visto
        string cfFrom      = "";
        string cfTo        = "";

        static readonly Color ColError    = Color.FromArgb(255, 210, 210);
        static readonly Color ColWarning  = Color.FromArgb(255, 250, 200);
        static readonly Color ColInfo     = Color.FromArgb(210, 240, 210);
        static readonly Color ColCritical = Color.FromArgb(220, 150, 150);

        public LogAnalyzerPanel(string logPath)
        {
            logFilePath = logPath;
            rules = CiscoRules.Get("Auto");
            BuildUI();

            pollTimer = new System.Windows.Forms.Timer { Interval = 800 };
            pollTimer.Tick += (s, e) => PollLog();
            pollTimer.Start();
        }

        void BuildUI()
        {
            // ── Toolbar ──────────────────────────────────────────────────────
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(40,40,40) };

            var lblDev = new Label { Text = "Device:", ForeColor = Color.White, Location = new Point(4, 6), Width = 50 };
            cmbDevice = new ComboBox { Location = new Point(54, 3), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Height = 22 };
            cmbDevice.Items.AddRange(new object[] { "Auto", "CUBE", "CUCM" });
            cmbDevice.SelectedIndex = 0;
            cmbDevice.SelectedIndexChanged += (s, e) => {
                deviceType = cmbDevice.SelectedItem.ToString();
                rules = CiscoRules.Get(deviceType);
            };

            var btnClear = new Button { Text = L.B("🗑 Pulisci","🗑 Clear"), Location = new Point(162, 2), Width = 80, Height = 24, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(80,80,80) };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => ClearFindings();

            var btnSave = new Button { Text = L.B("💾 Salva report","💾 Save report"), Location = new Point(248, 2), Width = 110, Height = 24, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50,100,50) };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => SaveReport();

            var btnOpenLog = new Button { Text = L.B("📄 Apri log","📄 Open log"), Location = new Point(364, 2), Width = 90, Height = 24, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(50,50,100) };
            btnOpenLog.FlatAppearance.BorderSize = 0;
            btnOpenLog.Click += (s, e) => { if (File.Exists(logFilePath)) Process.Start("notepad.exe", "\"" + logFilePath + "\""); };

            var btnPcap = new Button { Text = L.B("📦 Analizza PCAP","📦 Analyze PCAP"), Location = new Point(460, 2), Width = 130, Height = 24, FlatStyle = FlatStyle.Flat, ForeColor = Color.Yellow, BackColor = Color.FromArgb(60,40,0) };
            btnPcap.FlatAppearance.BorderSize = 0;
            btnPcap.Click += (s, e) => OpenPcap();

            lblStats = new Label { Text = "Errori: 0  Avvisi: 0  Info: 0", ForeColor = Color.LightGray, Location = new Point(596, 7), Width = 350 };

            toolbar.Controls.AddRange(new Control[] { lblDev, cmbDevice, btnClear, btnSave, btnOpenLog, btnPcap, lblStats });

            // ── Split: findings | dettaglio ───────────────────────────────────
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            split.SplitterDistance = 420;

            // Findings list
            lvFindings = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, OwnerDraw = false };
            lvFindings.Columns.Add("Ora",      60);
            lvFindings.Columns.Add("Sev.",     60);
            lvFindings.Columns.Add("Regola",  150);
            lvFindings.Columns.Add("Linea (👤 chiamante → chiamato | messaggio)", 360);
            lvFindings.SelectedIndexChanged += OnSelect;
            split.Panel1.Controls.Add(lvFindings);

            // Dettaglio + call flow
            var rightPanel = new TabControl { Dock = DockStyle.Fill };
            var tabDetail = new TabPage("Dettaglio");
            txtDetail = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(30,30,30), ForeColor = Color.White, Font = new Font("Consolas", 9), WordWrap = true, ScrollBars = ScrollBars.Vertical };
            tabDetail.Controls.Add(txtDetail);

            var tabCF = new TabPage("Call Flow");
            txtCallFlow = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BackColor = Color.FromArgb(20,20,40), ForeColor = Color.LightCyan, Font = new Font("Consolas", 8), ScrollBars = ScrollBars.Both, WordWrap = false };
            tabCF.Controls.Add(txtCallFlow);

            var tabLadder = new TabPage("📊 Ladder");
            ladderPanel = new SipLadderPanel { Dock = DockStyle.Fill };
            tabLadder.Controls.Add(ladderPanel);

            rightTabs = rightPanel;
            rightPanel.TabPages.AddRange(new TabPage[] { tabDetail, tabCF, tabLadder });
            split.Panel2.Controls.Add(rightPanel);

            Controls.Add(split);
            Controls.Add(toolbar);
        }

        void PollLog()
        {
            if (!File.Exists(logFilePath)) return;
            try
            {
                string newContent;
                using (var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length <= lastPos) return;
                    fs.Seek(lastPos, SeekOrigin.Begin);
                    using (var sr = new StreamReader(fs, Encoding.UTF8, true, 4096, true))
                        newContent = sr.ReadToEnd();
                    lastPos = fs.Length;
                }

                newContent = StripAnsi(newContent);

                // Auto-detect device
                if (deviceType == "Auto")
                {
                    string detected = CiscoRules.DetectDevice(newContent);
                    if (detected != "Auto")
                    {
                        deviceType = detected;
                        rules = CiscoRules.Get(deviceType);
                        BeginInvoke((Action)(() => {
                            int idx = cmbDevice.Items.IndexOf(detected);
                            if (idx >= 0) cmbDevice.SelectedIndex = idx;
                        }));
                    }
                }

                // Analizza linea per linea
                var lines = newContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var newFindings = new List<Finding>();

                for (int li = 0; li < lines.Length; li++)
                {
                    string line = lines[li].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // Call flow extraction
                    ExtractCallFlow(line);

                    foreach (var rule in rules)
                    {
                        try
                        {
                            if (Regex.IsMatch(line, rule.Pattern, RegexOptions.IgnoreCase))
                            {
                                string snippet = line.Length > 120 ? line.Substring(0, 120) + "..." : line;
                                // Per i messaggi SIP anteponi chi→chi (From → To): si legge la chiamata al volo
                                if (line.IndexOf("sip:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    line.StartsWith("SIP/2.0", StringComparison.OrdinalIgnoreCase))
                                {
                                    string parties = ExtractParties(lines, li);
                                    if (parties != null) snippet = "👤 " + parties + "   |   " + snippet;
                                }
                                newFindings.Add(new Finding
                                {
                                    Timestamp   = DateTime.Now,
                                    Severity    = rule.Severity,
                                    RuleName    = rule.Name,
                                    LineSnippet = snippet,
                                    Explanation = rule.Explanation
                                });
                                break; // una sola regola per linea
                            }
                        }
                        catch { }
                    }
                }

                if (newFindings.Count > 0)
                    BeginInvoke((Action)(() => AddFindings(newFindings)));
            }
            catch { }
        }

        // Cerca gli header From:/To: nelle righe successive al messaggio SIP e
        // restituisce "chiamante → chiamato" (solo l'utenza/numero), per leggere
        // al volo la chiamata nell'elenco anomalie. Null se non trova nulla.
        static string ExtractParties(string[] lines, int idx)
        {
            string from = null, to = null;
            for (int j = idx; j < lines.Length && j < idx + 20; j++)
            {
                string l = lines[j].Trim();
                if (from == null)
                {
                    var m = Regex.Match(l, @"^From:\s*.*?sip:([^@>;\s]+)", RegexOptions.IgnoreCase);
                    if (m.Success) from = m.Groups[1].Value;
                }
                if (to == null)
                {
                    var m = Regex.Match(l, @"^To:\s*.*?sip:([^@>;\s]+)", RegexOptions.IgnoreCase);
                    if (m.Success) to = m.Groups[1].Value;
                }
                if (from != null && to != null) break;
            }
            if (from == null && to == null) return null;
            return (from ?? "?") + " → " + (to ?? "?");
        }

        void ExtractCallFlow(string line)
        {
            // ── 1. Direzione: cerca segnali "Received"/"Sending" nella riga corrente ──
            // IOS/CUBE debug ccsip messages mostra:
            //   "Received SIP message from x.x.x.x:"  → IN
            //   "Sending SIP message to x.x.x.x:"     → OUT
            if (Regex.IsMatch(line, @"\b(Received|Recv)\b", RegexOptions.IgnoreCase))
                cfDirection = "IN";
            else if (Regex.IsMatch(line, @"\b(Sending|Send)\b", RegexOptions.IgnoreCase))
                cfDirection = "OUT";

            // ── 2. Accumula header SIP (sono su righe separate dal metodo) ──
            var mCallId = Regex.Match(line, @"[Cc]all-[Ii][Dd]\s*:\s*(\S+)");
            var mFrom   = Regex.Match(line, @"(?:^|\s)[Ff]rom\s*:.*?sip:([^@>;\s]+)");
            var mTo     = Regex.Match(line, @"(?:^|\s)[Tt]o\s*:.*?sip:([^@>;\s]+)");
            if (mCallId.Success) cfCallId = mCallId.Groups[1].Value.Substring(0, Math.Min(24, mCallId.Groups[1].Value.Length));
            if (mFrom.Success)   cfFrom   = mFrom.Groups[1].Value;
            if (mTo.Success)     cfTo     = mTo.Groups[1].Value;

            // ── 3. Metodo SIP (senza ^ — può essere preceduto da timestamp/prefissi) ──
            var mMethod   = Regex.Match(line, @"\b(INVITE|BYE|CANCEL|ACK|REGISTER|OPTIONS|REFER|NOTIFY|SUBSCRIBE|PRACK|UPDATE|INFO)\s+sip:", RegexOptions.IgnoreCase);
            var mResponse = Regex.Match(line, @"\bSIP/2\.0\s+(\d{3})\s+(.+)");

            if (mMethod.Success || mResponse.Success)
            {
                // Affina direzione: se nella stessa riga c'è "Sending" o "Received" ha priorità
                string dir = cfDirection;
                if (Regex.IsMatch(line, @"\b(Sending|Send)\b", RegexOptions.IgnoreCase))       dir = "OUT";
                else if (Regex.IsMatch(line, @"\b(Received|Recv)\b", RegexOptions.IgnoreCase)) dir = "IN";
                // Response SIP/2.0 senza contesto → default IN (tipicamente ricevuta)
                if (mResponse.Success && dir == "?") dir = "IN";

                string method = mMethod.Success
                    ? mMethod.Groups[1].Value.ToUpper()
                    : (mResponse.Groups[1].Value + " " + mResponse.Groups[2].Value.Trim());

                // Tronca response description a 30 char
                if (method.Length > 30) method = method.Substring(0, 30) + "…";

                var entry = new CallFlowEntry
                {
                    Time      = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Method    = method,
                    CallId    = cfCallId,
                    From      = cfFrom,
                    To        = cfTo,
                    Direction = dir
                };
                callFlow.Add(entry);
                // Reset From/To dopo aver catturato il messaggio (sono per-messaggio)
                cfFrom = ""; cfTo = "";
                BeginInvoke((Action)(() => RefreshCallFlow()));
            }
        }

        void RefreshCallFlow()
        {
            var sb = new StringBuilder();
            sb.AppendLine("══ Call Flow SIP ════════════════════════════════════════════════");
            sb.AppendLine(L.B("  Ora            DIR   Metodo/Risposta         From → To","  Time           DIR   Method/Response         From → To"));
            sb.AppendLine("────────────────────────────────────────────────────────────────");
            string lastCallId = null;

            foreach (var e in callFlow)
            {
                if (e.CallId != lastCallId && e.CallId != "?")
                {
                    sb.AppendLine();
                    sb.AppendLine("  ┌─ Call-ID: " + e.CallId);
                    if (!string.IsNullOrEmpty(e.From))
                        sb.AppendLine("  │  " + e.From + " → " + e.To);
                    lastCallId = e.CallId;
                }
                // OUT = freccia destra (client→server), IN = freccia sinistra (server→client)
                string arrow = e.Direction == "OUT" ? " ──►  " : " ◄──  ";
                string dir   = e.Direction == "OUT" ? "[OUT]" : "[IN] ";
                sb.AppendLine("  │  " + e.Time + "  " + dir + arrow + e.Method);
            }

            txtCallFlow.Text = sb.ToString();
            txtCallFlow.SelectionStart = txtCallFlow.Text.Length;
            txtCallFlow.ScrollToCaret();
        }

        void AddFindings(List<Finding> findings)
        {
            lvFindings.BeginUpdate();
            foreach (var f in findings)
            {
                string sev = f.Severity == Severity.Critical ? L.B("CRITICO","CRITICAL") :
                             f.Severity == Severity.Error    ? L.B("ERRORE","ERROR")  :
                             f.Severity == Severity.Warning  ? L.B("AVVISO","WARNING")  : "INFO";

                var item = new ListViewItem(f.Timestamp.ToString("HH:mm:ss"));
                item.SubItems.Add(sev);
                item.SubItems.Add(f.RuleName);
                item.SubItems.Add(f.LineSnippet);
                item.Tag = f;

                item.BackColor = f.Severity == Severity.Critical ? ColCritical :
                                 f.Severity == Severity.Error    ? ColError    :
                                 f.Severity == Severity.Warning  ? ColWarning  : ColInfo;

                lvFindings.Items.Add(item);

                if (f.Severity == Severity.Error || f.Severity == Severity.Critical) cntErr++;
                else if (f.Severity == Severity.Warning) cntWarn++;
                else cntInfo++;
            }
            lvFindings.EndUpdate();
            if (lvFindings.Items.Count > 0) lvFindings.Items[lvFindings.Items.Count - 1].EnsureVisible();
            lblStats.Text = string.Format("🔴 Errori: {0}   🟡 Avvisi: {1}   🟢 Info: {2}", cntErr, cntWarn, cntInfo);
        }

        void OnSelect(object s, EventArgs e)
        {
            if (lvFindings.SelectedItems.Count == 0) return;
            var f = (Finding)lvFindings.SelectedItems[0].Tag;
            txtDetail.Text =
                "REGOLA:      " + f.RuleName + "\r\n" +
                "SEVERITA':   " + f.Severity + "\r\n" +
                "ORA:         " + f.Timestamp.ToString("HH:mm:ss.fff") + "\r\n\r\n" +
                "LINEA LOG:\r\n" + f.LineSnippet + "\r\n\r\n" +
                "SPIEGAZIONE:\r\n" + f.Explanation;
        }

        void ClearFindings()
        {
            lvFindings.Items.Clear();
            callFlow.Clear();
            txtDetail.Clear();
            txtCallFlow.Clear();
            cntErr = cntWarn = cntInfo = 0;
            lblStats.Text = "Errori: 0  Avvisi: 0  Info: 0";
        }

        void SaveReport()
        {
            using (var dlg = new SaveFileDialog { Filter = "Text file|*.txt|HTML|*.html", FileName = "NetTerm_Analysis_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") })
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var sb = new StringBuilder();
                bool html = dlg.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
                if (html) sb.AppendLine("<html><body style='font-family:Consolas'><h2>NetTerm Analysis Report</h2><table border=1>");
                else sb.AppendLine("NetTerm Analysis Report — " + DateTime.Now + "\r\n" + new string('=', 60));

                foreach (ListViewItem item in lvFindings.Items)
                {
                    var f = (Finding)item.Tag;
                    if (html)
                    {
                        string bg = f.Severity == Severity.Error ? "#ffd2d2" : f.Severity == Severity.Warning ? "#fffac8" : "#d2f0d2";
                        sb.AppendLine(string.Format("<tr style='background:{0}'><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td></tr>",
                            bg, f.Timestamp.ToString("HH:mm:ss"), f.Severity, f.RuleName, f.LineSnippet, f.Explanation));
                    }
                    else sb.AppendLine(string.Format("[{0}] {1,-8} {2,-35} {3}", f.Timestamp.ToString("HH:mm:ss"), f.Severity, f.RuleName, f.LineSnippet));
                }

                if (html) { sb.AppendLine("</table><h3>Call Flow</h3><pre>" + txtCallFlow.Text + "</pre></body></html>"); }
                else { sb.AppendLine("\r\nCALL FLOW:\r\n" + txtCallFlow.Text); }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                System.Diagnostics.Process.Start(dlg.FileName);
            }
        }

        // Chiamato dall'esterno (OpenPcapTab in MainForm) per analizzare direttamente un file
        public void AnalyzePcap(string tshark, string pcapFile)
        {
            ClearFindings();
            lblStats.Text = L.B("⏳ Analisi PCAP: ","⏳ Analyzing PCAP: ") + Path.GetFileName(pcapFile) + " ...";
            pollTimer.Stop(); // nessun polling live per i tab PCAP standalone

            ThreadPool.QueueUserWorkItem(_ => RunPcapAnalysis(tshark, pcapFile));
        }

        void RunPcapAnalysis(string tshark, string pcapFile)
        {
            // ── Passate tshark eseguite DAVVERO in parallelo ──────────────────────
            // Ogni passata rilegge l'intero pcap: in sequenza erano ~12 letture
            // (lentissimo). In parallelo il tempo totale ≈ la passata più lenta.
            List<SipLadderMessage> ladderMsgs = null, h323Msgs = null, skinnyMsgs = null, isupMsgs = null;
            List<PcapSipEntry> pcapEntries = null;
            string verbose = null, voipStats = null, sipStats = null,
                   h323Verb = null, megacoVerb = null, skinnyVerb = null;

            var tasks = new[] {
                Task.Factory.StartNew(() => { ladderMsgs  = PcapAnalyzer.ExtractLadderMessages(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { pcapEntries = PcapAnalyzer.ExtractCallFlow(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { verbose     = PcapAnalyzer.ExtractVerbose(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { voipStats   = PcapAnalyzer.GetVoipStats(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { sipStats    = PcapAnalyzer.GetSipStats(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { h323Msgs    = PcapAnalyzer.ExtractH323LadderMessages(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { h323Verb    = PcapAnalyzer.ExtractH323Verbose(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { megacoVerb  = PcapAnalyzer.ExtractMegacoVerbose(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { skinnyVerb  = PcapAnalyzer.ExtractSkinnyVerbose(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { skinnyMsgs  = PcapAnalyzer.ExtractSkinnyLadderMessages(tshark, pcapFile); }),
                Task.Factory.StartNew(() => { isupMsgs    = PcapAnalyzer.ExtractIsupLadderMessages(tshark, pcapFile); })
            };
            try { Task.WaitAll(tasks); } catch { }

            // Unisci messaggi ladder SIP + H.323 + Skinny + ISUP ordinati per tempo
            var allLadder = new List<SipLadderMessage>();
            if (ladderMsgs  != null) allLadder.AddRange(ladderMsgs);
            if (h323Msgs    != null) allLadder.AddRange(h323Msgs);
            if (skinnyMsgs  != null) allLadder.AddRange(skinnyMsgs);
            if (isupMsgs    != null) allLadder.AddRange(isupMsgs);
            allLadder.Sort((a, b) => a.RelSec.CompareTo(b.RelSec));

            // Unisci verbose per findings
            string allVerbose = string.Join("\n", new[] { verbose, h323Verb, megacoVerb, skinnyVerb }
                .Where(v => !string.IsNullOrEmpty(v)));

            if (allLadder.Count == 0 && string.IsNullOrEmpty(allVerbose)) {
                BeginInvoke((Action)(() => lblStats.Text = L.B("⚠ Nessun messaggio VoIP (SIP/H.323/MEGACO/Skinny) trovato nel PCAP.","⚠ No VoIP messages (SIP/H.323/MEGACO/Skinny) found in the PCAP.")));
                return;
            }

            // Rileva protocolli per label
            bool hasSip     = ladderMsgs  != null && ladderMsgs.Count  > 0;
            bool hasH323    = h323Msgs    != null && h323Msgs.Count    > 0;
            bool hasMegaco  = !string.IsNullOrEmpty(megacoVerb);
            bool hasSkinny  = !string.IsNullOrEmpty(skinnyVerb);
            var protoLabels = new List<string>();
            if (hasSip)    protoLabels.Add("SIP");
            if (hasH323)   protoLabels.Add("H.323");
            if (hasMegaco) protoLabels.Add("MEGACO");
            if (hasSkinny) protoLabels.Add("SKINNY");
            string protoStr = protoLabels.Count > 0 ? string.Join("+", protoLabels.ToArray()) : "VoIP";

            // Ladder
            if (allLadder.Count > 0)
                BeginInvoke((Action)(() => {
                    ladderPanel.LoadMessages(allLadder, voipStats, sipStats);
                    // Porta in primo piano il tab Ladder
                    if (rightTabs != null)
                        foreach (TabPage tp in rightTabs.TabPages)
                            if (tp.Text == "📊 Ladder") { rightTabs.SelectedTab = tp; break; }
                }));

            // Call flow testuale (tab Call Flow)
            if (pcapEntries != null && pcapEntries.Count > 0)
                BeginInvoke((Action)(() => LoadPcapCallFlow(pcapEntries)));

            // Findings — analisi su tutti i protocolli
            var findings = new List<Finding>();
            if (!string.IsNullOrEmpty(allVerbose))
            {
                DateTime pktTime = DateTime.MinValue;
                foreach (var rawLine in allVerbose.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();
                    var mTime = Regex.Match(line, @"(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+)");
                    if (mTime.Success) DateTime.TryParse(mTime.Groups[1].Value, out pktTime);
                    foreach (var rule in rules) {
                        try {
                            if (Regex.IsMatch(line, rule.Pattern, RegexOptions.IgnoreCase)) {
                                findings.Add(new Finding {
                                    Timestamp   = pktTime != DateTime.MinValue ? pktTime : DateTime.Now,
                                    Severity    = rule.Severity, RuleName = rule.Name,
                                    LineSnippet = line.Length > 120 ? line.Substring(0, 120) + "…" : line,
                                    Explanation = rule.Explanation
                                });
                                break;
                            }
                        } catch { }
                    }
                }
            }
            BeginInvoke((Action)(() => {
                if (findings.Count > 0) AddFindings(findings);
                int mc = allLadder.Count;
                string msg = string.Format("📦 PCAP [{0}]: {1} msg", protoStr, mc);
                if (findings.Count > 0) msg += string.Format("  |  🔴 {0} err  🟡 {1} warn  🟢 {2} info", cntErr, cntWarn, cntInfo);
                else msg += L.B("  ✔ Nessun finding critico","  ✔ No critical findings");
                lblStats.Text = msg;
            }));
        }

        void OpenPcap()
        {
            using (var dlg = new OpenFileDialog {
                Title  = L.B("Seleziona file PCAP / PCAPNG","Select PCAP / PCAPNG file"),
                Filter = L.B("PCAP files|*.pcap;*.pcapng;*.cap|Tutti i file|*.*","PCAP files|*.pcap;*.pcapng;*.cap|All files|*.*")
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                string tshark = PcapAnalyzer.FindTshark();
                if (tshark == null)
                {
                    MessageBox.Show(
                        L.B("tshark.exe non trovato.\n\n","tshark.exe not found.\n\n") +
                        L.B("Installa Wireshark (include tshark):\nhttps://www.wireshark.org/download.html\n\n","Install Wireshark (includes tshark):\nhttps://www.wireshark.org/download.html\n\n") +
                        L.B("Poi riavvia LosaTermVoip.","Then restart LosaTermVoip."),
                        L.B("tshark mancante","tshark missing"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                // Reset findings per la nuova analisi PCAP
                ClearFindings();
                lblStats.Text = L.B("⏳ Analisi PCAP in corso...","⏳ Analyzing PCAP...");
                string pcapFile = dlg.FileName;
                ThreadPool.QueueUserWorkItem(_ => {
                    // ── Passata 1: call flow strutturato con timestamp reali ──────
                    var pcapEntries = PcapAnalyzer.ExtractCallFlow(tshark, pcapFile);
                    // ── Passata 2: testo verbose per le regole findings ───────────
                    string verbose  = PcapAnalyzer.ExtractVerbose(tshark, pcapFile);

                    if ((pcapEntries == null || pcapEntries.Count == 0) && string.IsNullOrEmpty(verbose)) {
                        BeginInvoke((Action)(() => lblStats.Text = L.B("⚠ Nessun messaggio SIP trovato nel PCAP.","⚠ No SIP messages found in the PCAP.")));
                        return;
                    }

                    // Costruisce il call flow direttamente dagli entry strutturati
                    if (pcapEntries != null && pcapEntries.Count > 0)
                        BeginInvoke((Action)(() => LoadPcapCallFlow(pcapEntries)));

                    // Analizza il testo verbose per i findings
                    var findings = new List<Finding>();
                    if (!string.IsNullOrEmpty(verbose))
                    {
                        var lines = verbose.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        // Recupera timestamp dal verbose per associarli ai findings
                        DateTime pktTime = DateTime.MinValue;
                        foreach (var rawLine in lines) {
                            string line = rawLine.Trim();
                            // tshark verbose: riga "Frame N: ... on wire..." contiene il numero frame
                            // usiamo il timestamp dal pcapEntry corrispondente se disponibile
                            var mTime = Regex.Match(line, @"(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d+)");
                            if (mTime.Success) {
                                DateTime.TryParse(mTime.Groups[1].Value, out pktTime);
                            }
                            foreach (var rule in rules) {
                                try {
                                    if (Regex.IsMatch(line, rule.Pattern, RegexOptions.IgnoreCase)) {
                                        findings.Add(new Finding {
                                            Timestamp   = pktTime != DateTime.MinValue ? pktTime : DateTime.Now,
                                            Severity    = rule.Severity,
                                            RuleName    = rule.Name,
                                            LineSnippet = line.Length > 120 ? line.Substring(0, 120) + "…" : line,
                                            Explanation = rule.Explanation
                                        });
                                        break;
                                    }
                                } catch { }
                            }
                        }
                    }
                    BeginInvoke((Action)(() => {
                        if (findings.Count > 0) AddFindings(findings);
                        else if (pcapEntries != null && pcapEntries.Count > 0)
                            lblStats.Text = string.Format(L.B("✔ PCAP: {0} messaggi SIP — nessun finding critico.","✔ PCAP: {0} SIP messages — no critical findings."), pcapEntries.Count);
                        else
                            lblStats.Text = L.B("✔ PCAP analizzato — nessun finding.","✔ PCAP analyzed — no findings.");
                    }));
                });
            }
        }

        // Carica il call flow direttamente dagli entry strutturati del PCAP
        // (con timestamp reali e indirizzi IP src/dst)
        void LoadPcapCallFlow(List<PcapSipEntry> entries)
        {
            callFlow.Clear();
            foreach (var e in entries)
            {
                callFlow.Add(new CallFlowEntry {
                    Time      = e.Time,
                    Method    = e.Method,
                    CallId    = e.CallId ?? "?",
                    From      = e.From   ?? "",
                    To        = e.To     ?? "",
                    Direction = e.Direction
                });
            }
            RefreshCallFlow();
        }

        static string StripAnsi(string s)
        {
            s = Regex.Replace(s, @"\x1B\[[0-9;]*[A-Za-z]", "");
            s = Regex.Replace(s, @"\x1B.", "");
            return s.Replace("\r", "");
        }

        protected override void Dispose(bool disposing) { if (disposing && pollTimer != null) pollTimer.Stop(); base.Dispose(disposing); }
    }

    // ─── SIP Ladder Message ───────────────────────────────────────────────────
    public class SipLadderMessage
    {
        public string Time      { get; set; }  // HH:mm:ss.fff o secondi relativi
        public double RelSec    { get; set; }  // secondi dall'inizio cattura
        public string SrcIp     { get; set; }
        public string SrcPort   { get; set; }
        public string DstIp     { get; set; }
        public string DstPort   { get; set; }
        public string Method    { get; set; }  // "INVITE" / "200 OK" / "BYE" ecc.
        public string CallId    { get; set; }
        public string Group     { get; set; }  // chiamata "logica" = più Call-ID/gambe unite
        public string FromUser  { get; set; }
        public string ToUser    { get; set; }
        public string Codecs    { get; set; }  // da SDP se presente
        public bool   IsRtp     { get; set; }  // true = stream media RTP (non un messaggio SIP)
        public string Detail    { get; set; }  // testo dettaglio (usato per le righe RTP)
        // Header per la validazione RFC 3261
        public string Via       { get; set; }
        public string Branch    { get; set; }
        public string FromHdr   { get; set; }
        public string FromTag   { get; set; }
        public string ToHdr     { get; set; }
        public string ToTag     { get; set; }
        public string CSeq      { get; set; }
        public string MaxFwd    { get; set; }
        public string Contact   { get; set; }
        public string ContentLen{ get; set; }
    }

    // ─── SIP Ladder Panel (GDI+) ──────────────────────────────────────────────
    // Voce del ComboBox chiamate: mostra "Calling → Called [time]" ma contiene il vero CallId
    class CallEntry
    {
        public string CallId   { get; set; }
        public string Display  { get; set; }
        public override string ToString() { return Display; }
    }

    public class SipLadderPanel : UserControl
    {
        List<SipLadderMessage> allMsgs  = new List<SipLadderMessage>();
        List<SipLadderMessage> msgs     = new List<SipLadderMessage>();
        List<string>           endpts   = new List<string>();
        SipLadderMessage       selMsg   = null;
        Panel                  drawPanel;
        TextBox                txtInfo;
        Label                  lblVoip;
        ComboBox               cmbCallId;
        ComboBox               cmbProto;
        CheckBox               chkCorr;
        ComboBox               cmbGroupMode;
        Label                  lblCallSel;
        string                 lastVoip, lastSip;

        const int ROW_H   = 28;
        const int HDR_H   = 65;
        const int TIME_W  = 88;
        const int EP_W    = 160;

        public SipLadderPanel() { BuildUI(); }

        void BuildUI()
        {
            var outer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
            outer.SplitterDistance = 620;

            // ── Sinistra: filtri + stats + ladder ─────────────────────────────
            var leftStack = new Panel { Dock = DockStyle.Fill };

            // Barra filtri
            var filterBar = new Panel { Dock = DockStyle.Top, Height = 30,
                BackColor = Color.FromArgb(28, 35, 55), Padding = new Padding(4, 3, 4, 2) };

            var lblF = new Label { Text = L.T("lad.filter_call"),
                ForeColor = Color.LightCyan, Location = new Point(4, 6), Width = 120, AutoSize = false };
            filterBar.Controls.Add(lblF);

            cmbCallId = new ComboBox { Location = new Point(124, 3), Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 55, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbCallId.SelectedIndexChanged += (s, e) => ApplyFilter();
            filterBar.Controls.Add(cmbCallId);

            var lblP = new Label { Text = L.T("lad.filter_proto"),
                ForeColor = Color.LightCyan, Location = new Point(354, 6), Width = 80, AutoSize = false };
            filterBar.Controls.Add(lblP);

            cmbProto = new ComboBox { Location = new Point(432, 3), Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 55, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbProto.SelectedIndexChanged += (s, e) => {
                // Cambiando protocollo, se è selezionata una chiamata specifica la
                // combinazione call+proto potrebbe dare 0 messaggi: riparti da "Tutte".
                if (cmbCallId.SelectedIndex > 0)
                {
                    var ce = cmbCallId.SelectedItem as CallEntry;
                    if (ce != null && !string.IsNullOrEmpty(ce.CallId))
                    {
                        cmbCallId.SelectedIndex = 0;   // → "Tutte le chiamate" (richiama ApplyFilter)
                        return;
                    }
                }
                ApplyFilter();
            };
            filterBar.Controls.Add(cmbProto);

            // Pulsante "Evidenzia chiamata selezionata" — seleziona il Call-ID del messaggio cliccato
            var btnFollow = new Button { Text = "🔍 Segui call", Location = new Point(552, 2),
                Width = 95, Height = 24, FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White, BackColor = Color.FromArgb(40, 80, 40) };
            btnFollow.FlatAppearance.BorderSize = 0;
            btnFollow.Click += (s, e) => FollowSelected();
            filterBar.Controls.Add(btnFollow);

            var btnReset = new Button { Text = "✕", Location = new Point(654, 2),
                Width = 28, Height = 24, FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White, BackColor = Color.FromArgb(80, 40, 40) };
            btnReset.FlatAppearance.BorderSize = 0;
            new ToolTip().SetToolTip(btnReset, L.B("Rimuovi filtro Call-ID","Clear Call-ID filter"));
            btnReset.Click += (s, e) => { if (cmbCallId.Items.Count > 0) cmbCallId.SelectedIndex = 0; };
            filterBar.Controls.Add(btnReset);

            // Spunta: includi le gambe correlate della stessa telefonata (opt-in)
            chkCorr = new CheckBox { Text = "🔗 Gambe", Location = new Point(690, 6), AutoSize = true, ForeColor = Color.LightCyan };
            new ToolTip().SetToolTip(chkCorr, L.B("Mostra anche le gambe correlate della stessa telefonata (come selezionare più chiamate in Wireshark)","Also show correlated legs of the same call (like selecting multiple calls in Wireshark)"));
            chkCorr.CheckedChanged += (s, e) => ApplyFilter();
            filterBar.Controls.Add(chkCorr);

            // Modalità di raggruppamento: per Call-ID (come Wireshark) oppure chiamata intera (gambe unite)
            cmbGroupMode = new ComboBox { Location = new Point(772, 3), Width = 165,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 55, 80), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cmbGroupMode.Items.Add(L.B("🔗 Per Call-ID","🔗 By Call-ID"));
            cmbGroupMode.Items.Add(L.B("📞 Chiamata intera","📞 Whole call"));
            cmbGroupMode.SelectedIndex = 0;
            new ToolTip().SetToolTip(cmbGroupMode, L.B("Per Call-ID = come Wireshark (una gamba per voce). Chiamata intera = unisce le gambe della stessa telefonata via numeri+tempo (utile attraverso gli SBC).","By Call-ID = like Wireshark (one leg each). Whole call = merges the legs of the same call by numbers+time (useful across SBCs)."));
            cmbGroupMode.SelectedIndexChanged += (s, e) => {
                bool logical = cmbGroupMode.SelectedIndex == 1;
                if (chkCorr != null) { chkCorr.Enabled = !logical; if (logical) chkCorr.Checked = false; }
                LoadMessages(allMsgs, lastVoip, lastSip);   // ricostruisce gruppi + tendina
            };
            filterBar.Controls.Add(cmbGroupMode);

            // Stats bar
            lblVoip = new Label {
                Dock = DockStyle.Top, Height = 36,
                BackColor = Color.FromArgb(20, 30, 50), ForeColor = Color.LightCyan,
                Font = new Font("Consolas", 8), Padding = new Padding(4),
                Text = L.T("ana.loading")
            };

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            drawPanel = new Panel { BackColor = Color.White };
            drawPanel.Paint      += Draw;
            drawPanel.MouseClick += OnClick;
            scroll.Controls.Add(drawPanel);

            leftStack.Controls.Add(scroll);     // Fill
            leftStack.Controls.Add(lblVoip);    // Top (dopo scroll → sopra)
            leftStack.Controls.Add(filterBar);  // Top (sopra lblVoip)
            outer.Panel1.Controls.Add(leftStack);

            // ── Destra: dettaglio messaggio ───────────────────────────────────
            var rightPanel = new Panel { Dock = DockStyle.Fill };

            lblCallSel = new Label { Dock = DockStyle.Top, Height = 22,
                BackColor = Color.FromArgb(15, 40, 80), ForeColor = Color.Yellow,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Padding = new Padding(4, 3, 0, 0), Text = "" };
            txtInfo = new TextBox {
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                BackColor = Color.FromArgb(22, 22, 35), ForeColor = Color.LightCyan,
                Font = new Font("Consolas", 8), ScrollBars = ScrollBars.Both, WordWrap = false
            };
            rightPanel.Controls.Add(txtInfo);
            rightPanel.Controls.Add(lblCallSel);
            outer.Panel2.Controls.Add(rightPanel);

            Controls.Add(outer);
        }

        // Chiave di raggruppamento: la "chiamata logica" (gambe unite) se presente
        static string GKey(SipLadderMessage m)
        {
            if (m == null) return "?";
            return string.IsNullOrEmpty(m.Group) ? m.CallId : m.Group;
        }

        // ── Applica filtri Call-ID e Protocollo ───────────────────────────────
        void ApplyFilter()
        {
            var   entry    = cmbCallId.SelectedItem as CallEntry;
            string protoSel = cmbProto.SelectedItem as string;
            bool allCalls  = entry == null || string.IsNullOrEmpty(entry.CallId);
            bool allProtos = string.IsNullOrEmpty(protoSel) || protoSel == L.T("lad.all_protos");

            // Gruppi da mostrare: la chiamata selezionata + eventuali gambe correlate
            HashSet<string> target = null;
            if (!allCalls)
            {
                target = new HashSet<string>();
                target.Add(entry.CallId);
                if (chkCorr != null && chkCorr.Checked) AddRelatedGroups(entry.CallId, target);
            }

            var filtered = new List<SipLadderMessage>();
            foreach (var m in allMsgs)
            {
                if (!allCalls  && !target.Contains(GKey(m))) continue;
                if (!allProtos && !MatchProto(m, protoSel)) continue;
                filtered.Add(m);
            }
            msgs = filtered;
            selMsg = null;
            RebuildEndpoints();
            ResizeDrawPanel();
            drawPanel.Invalidate();

            if (!allCalls && entry != null)
            {
                lblCallSel.Text = "🔍 " + entry.Display + "  |  Call-ID: " + entry.CallId;
                txtInfo.Text = DiagnoseMessages(msgs);
            }
            else lblCallSel.Text = "";
        }

        bool MatchProto(SipLadderMessage m, string proto)
        {
            if (proto == "SIP")     return !m.Method.StartsWith("[");
            if (proto == "H.323")   return m.Method.StartsWith("[H.323]");
            if (proto == "Skinny")  return m.Method.StartsWith("[SCCP]");
            if (proto == "SIP-I")   return m.Method.StartsWith("[ISUP]");
            return true;
        }

        // Aggiunge a 'target' le gambe correlate alla chiamata 'sel': altre telefonate
        // (con INVITE) vicine nel tempo (≤8s) e che condividono un numero chiamante/chiamato.
        void AddRelatedGroups(string sel, HashSet<string> target)
        {
            const double WIN = 20.0;   // finestra ampia: il ring/setup tra le gambe può durare
            var users = new Dictionary<string, HashSet<string>>();
            var t0 = new Dictionary<string, double>();
            var t1 = new Dictionary<string, double>();
            var hasInv = new HashSet<string>();
            foreach (var m in allMsgs)
            {
                string g = GKey(m);
                if (string.IsNullOrEmpty(g) || g == "?") continue;
                if (!users.ContainsKey(g)) { users[g] = new HashSet<string>(); t0[g] = m.RelSec; t1[g] = m.RelSec; }
                string a = NormNum(m.FromUser), b = NormNum(m.ToUser);
                if (a != null) users[g].Add(a);
                if (b != null) users[g].Add(b);
                if (m.RelSec < t0[g]) t0[g] = m.RelSec;
                if (m.RelSec > t1[g]) t1[g] = m.RelSec;
                if (m.Method != null && m.Method.StartsWith("INVITE")) hasInv.Add(g);
            }
            if (!users.ContainsKey(sel)) return;
            foreach (var g in users.Keys)
            {
                if (g == sel || target.Contains(g) || !hasInv.Contains(g)) continue;
                bool timeClose = !(t1[sel] < t0[g] - WIN || t1[g] < t0[sel] - WIN);
                if (timeClose && users[sel].Overlaps(users[g])) target.Add(g);
            }
        }

        // true se la modalità "Chiamata intera" (gambe unite) è attiva
        bool IsLogicalMode()
        {
            return cmbGroupMode != null && cmbGroupMode.SelectedIndex == 1;
        }

        // Normalizza un numero per il confronto fra gambe: tiene solo le cifre e
        // assorbe i prefissi internazionali/E.164 confrontando le ultime cifre
        // (così 03382000993, +393382000993, 00393382000993… combaciano).
        static string NormNum(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var sb = new StringBuilder();
            foreach (char ch in s) if (ch >= '0' && ch <= '9') sb.Append(ch);
            string d = sb.ToString();
            if (d.Length < 3) return null;                       // troppo corto / inaffidabile
            if (d.StartsWith("00")) d = d.Substring(2);          // 0039… → 39…
            if (d.Length > 9) d = d.Substring(d.Length - 9);     // confronta sulle ultime 9 cifre
            return d;
        }

        // Imposta m.Group per tutti i messaggi secondo la modalità di raggruppamento:
        //  - non-logica → Group = Call-ID (una gamba per voce, come Wireshark);
        //  - logica     → unisce i Call-ID della stessa telefonata (numeri normalizzati
        //                 in comune + vicinanza temporale ≤20s) con union-find, e usa
        //                 l'id del cluster come Group → tutta la chiamata end-to-end.
        void RecomputeGroups(bool logical)
        {
            if (allMsgs == null) return;
            if (!logical)
            {
                foreach (var m in allMsgs) m.Group = m.CallId;
                return;
            }
            const double WIN = 20.0;
            var nums = new Dictionary<string, HashSet<string>>();
            var t0 = new Dictionary<string, double>();
            var t1 = new Dictionary<string, double>();
            foreach (var m in allMsgs)
            {
                string c = m.CallId;
                if (string.IsNullOrEmpty(c) || c == "?") continue;
                if (!nums.ContainsKey(c)) { nums[c] = new HashSet<string>(); t0[c] = m.RelSec; t1[c] = m.RelSec; }
                string a = NormNum(m.FromUser), b = NormNum(m.ToUser);
                if (a != null) nums[c].Add(a);
                if (b != null) nums[c].Add(b);
                if (m.RelSec < t0[c]) t0[c] = m.RelSec;
                if (m.RelSec > t1[c]) t1[c] = m.RelSec;
            }
            var ids = new List<string>(nums.Keys);
            var parent = new Dictionary<string, string>();
            foreach (var id in ids) parent[id] = id;
            Func<string, string> find = null;
            find = x => { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; };
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                {
                    string A = ids[i], B = ids[j];
                    bool timeClose = !(t1[A] < t0[B] - WIN || t1[B] < t0[A] - WIN);
                    if (timeClose && nums[A].Overlaps(nums[B]))
                    {
                        string ra = find(A), rb = find(B);
                        if (ra != rb) parent[ra] = rb;
                    }
                }
            foreach (var m in allMsgs)
            {
                if (string.IsNullOrEmpty(m.CallId) || m.CallId == "?") { m.Group = m.CallId; continue; }
                m.Group = find(m.CallId);
            }
        }

        // ── Auto-diagnosi: analizza i messaggi mostrati e spiega l'esito ──────
        string DiagnoseMessages(List<SipLadderMessage> list)
        {
            if (list == null || list.Count == 0) return "";
            var sb = new StringBuilder();
            sb.AppendLine(L.B("══ Diagnosi automatica ══════════════════════","══ Automatic diagnosis ══════════════════════"));

            var groups = new List<string>();
            foreach (var m in list) { string g = GKey(m); if (!groups.Contains(g)) groups.Add(g); }

            foreach (var grp in groups)
            {
                var cm = new List<SipLadderMessage>();
                foreach (var m in list) if (GKey(m) == grp && !m.IsRtp) cm.Add(m);
                if (cm.Count == 0) continue;

                string calling = "", called = "";
                foreach (var m in cm)
                {
                    if (calling == "" && !string.IsNullOrEmpty(m.FromUser)) calling = m.FromUser;
                    if (called  == "" && !string.IsNullOrEmpty(m.ToUser))   called  = m.ToUser;
                }

                bool hasInvite = false, has180 = false, has183 = false, has200 = false,
                     hasBye = false, hasCancel = false, has487 = false;
                int auth = 0;
                var failCodes = new List<int>();
                foreach (var m in cm)
                {
                    string bare = StripProtoTag(m.Method ?? "");
                    if (bare.StartsWith("INVITE")) hasInvite = true;
                    else if (bare.StartsWith("BYE")) hasBye = true;
                    else if (bare.StartsWith("CANCEL")) hasCancel = true;
                    else if (bare.Length > 0 && char.IsDigit(bare[0]))
                    {
                        int code; int.TryParse(bare.Split(' ')[0], out code);
                        if (code == 180) has180 = true;
                        else if (code == 183) has183 = true;
                        else if (code >= 200 && code < 300) has200 = true;
                        else if (code == 401 || code == 407) auth++;
                        else if (code >= 400) failCodes.Add(code);
                        if (code == 487) has487 = true;
                    }
                }

                sb.AppendLine();
                sb.AppendLine("▶ " + (calling == "" ? "?" : calling) + " → " + (called == "" ? "?" : called) + "   (" + cm.Count + " msg)");

                if (has200) sb.AppendLine(L.B("  ✓ Connessa (200 OK)","  ✓ Connected (200 OK)") + (hasBye ? L.B(" e terminata (BYE)"," and ended (BYE)") : ""));
                else if (has487 || hasCancel) sb.AppendLine(L.B("  • Annullata dal chiamante (CANCEL/487) prima della risposta.","  • Cancelled by the caller (CANCEL/487) before the answer."));
                else if (failCodes.Count > 0)
                {
                    int fc = failCodes[failCodes.Count - 1];
                    sb.AppendLine(L.B("  ✗ FALLITA: ","  ✗ FAILED: ") + VoipCodes.ShortSip(fc));
                    string[] sv;
                    if (VoipCodes.Sip.TryGetValue(fc, out sv)) sb.AppendLine(L.B("     → Da controllare: ","     → What to check: ") + sv[2]);
                }
                else if (hasInvite) sb.AppendLine(L.B("  ⚠ Nessuna risposta finale (solo provvisorie) → timeout / peer non risponde / firewall.","  ⚠ No final response (provisional only) → timeout / peer not answering / firewall."));

                if (auth >= 2) sb.AppendLine(L.B("  ⚠ Challenge autenticazione ripetuti (","  ⚠ Repeated auth challenges (") + auth + L.B("×) → verifica credenziali/registrar.","×) → check credentials/registrar."));

                if (has183) sb.AppendLine(L.B("  ♪ Early media (183 Session Progress): ringback/annuncio in-band dal remoto. Se l'utente non sente nulla → verifica taglio early media (P-Early-Media), percorso RTP e direzione media.","  ♪ Early media (183 Session Progress): in-band ringback/announcement from the remote. If the user hears nothing → check early-media cut (P-Early-Media), RTP path and media direction."));
                else if (has180) sb.AppendLine(L.B("  ♪ Ringback locale (180 Ringing): generato dal lato chiamante.","  ♪ Local ringback (180 Ringing): generated by the calling side."));
            }
            return sb.ToString();
        }

        // Segui la chiamata del messaggio attualmente selezionato
        void FollowSelected()
        {
            string gk = GKey(selMsg);
            if (selMsg == null || string.IsNullOrEmpty(gk) || gk == "?") return;
            for (int i = 0; i < cmbCallId.Items.Count; i++)
            {
                var e = cmbCallId.Items[i] as CallEntry;
                if (e != null && e.CallId == gk)
                {
                    cmbCallId.SelectedIndex = i;
                    break;
                }
            }
        }

        void RebuildEndpoints()
        {
            endpts.Clear();
            foreach (var m in msgs)
            {
                string s = m.SrcIp + ":" + m.SrcPort;
                string d = m.DstIp + ":" + m.DstPort;
                if (!endpts.Contains(s)) endpts.Add(s);
                if (!endpts.Contains(d)) endpts.Add(d);
            }
        }

        void ResizeDrawPanel()
        {
            int pw = TIME_W + endpts.Count * EP_W + 40;
            int ph = HDR_H  + msgs.Count   * ROW_H + 30;
            if (pw < 200) pw = 200;
            if (ph < 100) ph = 100;
            drawPanel.Size = new Size(pw, ph);
        }

        // ── Helper per classificare il tipo di dialogo nel menu chiamate ──────────
        // Una "risposta" inizia con una cifra (es. "200 OK", "180 Ringing");
        // tutto il resto (INVITE, SUBSCRIBE, NOTIFY, OPTIONS, REGISTER, BYE, Setup…)
        // è una richiesta e qualifica il dialogo.
        static bool IsRequestMethod(string method)
        {
            if (string.IsNullOrEmpty(method)) return false;
            string m = StripProtoTag(method);
            if (m.Length == 0) return false;
            return !char.IsDigit(m[0]);   // le risposte iniziano con il codice numerico
        }

        static string StripProtoTag(string method)
        {
            if (string.IsNullOrEmpty(method)) return "";
            int i = method.IndexOf(']');
            if (method.StartsWith("[") && i > 0 && i < method.Length - 1)
                return method.Substring(i + 1).Trim();   // "[H.323] Setup" → "Setup"
            return method.Trim();
        }

        static bool IsCallSetup(string method)
        {
            string m = StripProtoTag(method);
            return m == "INVITE" || m == "Setup";
        }

        static bool LooksLikeIp(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int dots = 0;
            foreach (char c in s) if (c == '.') dots++;
            return dots == 3;   // euristica: "10.20.30.40"
        }

        // Icona in base al tipo di dialogo: 📞 telefonata vera (INVITE/Setup),
        // 📋 sottoscrizione/presence (SUBSCRIBE/NOTIFY), ❓ keepalive (OPTIONS),
        // 🔑 registrazione (REGISTER), 🔷 H.323, ☎️ Skinny/altro.
        static string DialogIcon(string proto, string method)
        {
            if (proto == "H323") return "🔷";
            string m = (method ?? "").ToUpperInvariant();
            if (m == "INVITE" || m == "SETUP")          return "📞";
            if (m == "SUBSCRIBE" || m == "NOTIFY")      return "📋";
            if (m == "OPTIONS")                          return "❓";
            if (m == "REGISTER")                         return "🔑";
            if (m == "MESSAGE" || m == "PUBLISH" || m == "INFO") return "✉️";
            if (proto == "OTHER")                        return "☎️";
            return "📞";   // SIP generico senza richiesta identificata
        }

        public void LoadMessages(List<SipLadderMessage> messages, string voipStats, string sipStats)
        {
            allMsgs = messages ?? new List<SipLadderMessage>();
            lastVoip = voipStats; lastSip = sipStats;
            RecomputeGroups(IsLogicalMode());

            // ── Popola combo chiamate: "Calling → Called [HH:mm:ss]" ──────────
            // Per ogni Call-ID unico, cerchiamo il primo messaggio INVITE/Setup
            // per estrarre chiamante e chiamato leggibili.
            var seenCallIds = new List<string>();
            // Mappa callId → { calling, called, time, proto, msgCount }
            var callMap = new Dictionary<string, string[]>();

            foreach (var m in allMsgs)
            {
                string gk = GKey(m);
                if (string.IsNullOrEmpty(gk) || gk == "?") continue;
                if (!seenCallIds.Contains(gk)) seenCallIds.Add(gk);

                if (!callMap.ContainsKey(gk))
                {
                    // Prima occorrenza: inizializza con le info disponibili
                    string calling = !string.IsNullOrEmpty(m.FromUser) ? m.FromUser : m.SrcIp;
                    string called  = !string.IsNullOrEmpty(m.ToUser)   ? m.ToUser   : m.DstIp;
                    string proto   = m.Method.StartsWith("[H.323]") ? "H323" :
                                     m.Method.StartsWith("[")        ? "OTHER" : "SIP";
                    // metodo primario del dialogo (richiesta, non risposta)
                    string pm = IsRequestMethod(m.Method) ? StripProtoTag(m.Method) : "";
                    callMap[gk] = new[] { calling, called, m.Time, proto, "1", pm };
                }
                else
                {
                    // Aggiorna chiamante/chiamato quando arriva un'info migliore:
                    // su INVITE/Setup, oppure quando un messaggio porta i numeri delle parti
                    // (es. SCCP CallInfo) e finora avevamo solo l'IP.
                    bool isSetup = IsCallSetup(m.Method);
                    if (isSetup || LooksLikeIp(callMap[gk][0]) || string.IsNullOrEmpty(callMap[gk][0]))
                        if (!string.IsNullOrEmpty(m.FromUser)) callMap[gk][0] = m.FromUser;
                    if (isSetup || LooksLikeIp(callMap[gk][1]) || string.IsNullOrEmpty(callMap[gk][1]))
                        if (!string.IsNullOrEmpty(m.ToUser))   callMap[gk][1] = m.ToUser;
                    // Determina il metodo primario: INVITE/Setup ha priorità massima;
                    // altrimenti la prima richiesta vista (SUBSCRIBE, NOTIFY, OPTIONS, REGISTER…)
                    if (isSetup) callMap[gk][5] = "INVITE";
                    else if (string.IsNullOrEmpty(callMap[gk][5]) && IsRequestMethod(m.Method))
                        callMap[gk][5] = StripProtoTag(m.Method);
                    // Conta messaggi
                    int cnt = int.Parse(callMap[gk][4]) + 1;
                    callMap[gk][4] = cnt.ToString();
                }
            }

            cmbCallId.Items.Clear();
            // Voce "Tutte"
            cmbCallId.Items.Add(new CallEntry {
                CallId  = "",
                Display = L.T("lad.all_calls") + "  (" + allMsgs.Count + " msg)"
            });
            // Una voce per ogni chiamata
            foreach (var cid in seenCallIds)
            {
                string[] info    = callMap.ContainsKey(cid) ? callMap[cid] : new[]{"?","?","","SIP","0",""};
                string calling   = info[0]; if (string.IsNullOrEmpty(calling)) calling = "?";
                string called    = info[1]; if (string.IsNullOrEmpty(called))  called  = "?";
                string time      = info[2]; if (string.IsNullOrEmpty(time))    time    = "";
                string proto     = info[3];
                string count     = info[4];
                string method    = info.Length > 5 ? info[5] : "";
                string icon      = DialogIcon(proto, method);
                string shortTime = time.Length >= 8 ? time.Substring(0, 8) : time;
                // tag del tipo di dialogo (es. SUBSCRIBE, NOTIFY, OPTIONS) per non confondere
                // una sottoscrizione/presence con una telefonata vera
                string tag       = string.IsNullOrEmpty(method) ? "" : method + " ";
                string display   = string.Format("{0} {1}{2} → {3}  [{4}]  {5}msg",
                    icon, tag, calling, called, shortTime, count);
                cmbCallId.Items.Add(new CallEntry { CallId = cid, Display = display });
            }
            if (cmbCallId.Items.Count > 0) cmbCallId.SelectedIndex = 0;

            // ── Popola combo Protocollo ──────────────────────────────────────
            bool hasSip = false, hasH323 = false, hasSkinny = false, hasIsup = false;
            foreach (var m in allMsgs)
            {
                if (m.Method.StartsWith("[H.323]"))      hasH323   = true;
                else if (m.Method.StartsWith("[SCCP]"))  hasSkinny = true;
                else if (m.Method.StartsWith("[ISUP]"))  hasIsup   = true;
                else if (!m.Method.StartsWith("["))      hasSip    = true;
            }

            cmbProto.Items.Clear();
            cmbProto.Items.Add(L.T("lad.all_protos"));
            if (hasSip)    cmbProto.Items.Add("SIP");
            if (hasH323)   cmbProto.Items.Add("H.323");
            if (hasSkinny) cmbProto.Items.Add("Skinny");
            if (hasIsup)   cmbProto.Items.Add("SIP-I");
            cmbProto.SelectedIndex = 0;

            // msgs viene popolato da ApplyFilter (già triggerato da SelectedIndex=0 sopra)
            // ma SelectedIndex=0 potrebbe non triggerare l'evento se era già 0
            msgs = new List<SipLadderMessage>(allMsgs);
            RebuildEndpoints();
            ResizeDrawPanel();

            // Stats in alto
            lblVoip.Text = BuildStatsText(voipStats, sipStats);
            drawPanel.Invalidate();
        }

        string BuildStatsText(string voip, string sip)
        {
            var sb = new StringBuilder();
            sb.Append("  📦 Messaggi: " + msgs.Count + "   Endpoint: " + endpts.Count);

            // Estrai contatori da sip,stat
            if (!string.IsNullOrEmpty(sip))
            {
                var methods = new List<string>();
                foreach (Match m in Regex.Matches(sip, @"\|\s*(\w+)\s*\|\s*(\d+)\s*\|"))
                    methods.Add(m.Groups[1].Value + "×" + m.Groups[2].Value);
                if (methods.Count > 0) sb.Append("   SIP: " + string.Join("  ", methods));
            }

            // Voip calls summary
            if (!string.IsNullOrEmpty(voip))
            {
                var states = Regex.Matches(voip, @"(CALL SETUP|ESTABLISHED|CANCELLED|REJECTED|UNKNOWN)");
                int setup=0, ok=0, cancel=0, rej=0;
                foreach (Match m in states)
                {
                    string s = m.Groups[1].Value;
                    if (s == "CALL SETUP")   setup++;
                    else if (s == "ESTABLISHED") ok++;
                    else if (s == "CANCELLED")   cancel++;
                    else if (s == "REJECTED")    rej++;
                }
                sb.Append(string.Format("   📞 Setup:{0} OK:{1} Cancel:{2} Rej:{3}", setup, ok, cancel, rej));
            }
            return sb.ToString();
        }

        // ── Disegno GDI+ ─────────────────────────────────────────────────────
        void Draw(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (endpts.Count == 0) { g.DrawString(L.T("ana.no_sip"), new Font("Segoe UI",10), Brushes.Gray, 10, 10); return; }

            // Calcola X di ogni endpoint (centrato nella sua colonna)
            var epX = new int[endpts.Count];
            for (int i = 0; i < endpts.Count; i++)
                epX[i] = TIME_W + i * EP_W + EP_W / 2;

            using (var epBg   = new SolidBrush(Color.FromArgb(210, 228, 255)))
            using (var epFg   = new SolidBrush(Color.FromArgb(20, 50, 120)))
            using (var epPen  = new Pen(Color.FromArgb(90, 130, 200)))
            using (var vPen   = new Pen(Color.FromArgb(180, 200, 230), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            using (var epFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var tFont  = new Font("Consolas", 7))
            using (var mFont  = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var pFont  = new Font("Consolas", 6.5f))
            using (var portBr = new SolidBrush(Color.FromArgb(120, 120, 130)))
            {
                var sfC    = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                var sfNear = new StringFormat { Alignment = StringAlignment.Near };
                var sfFar  = new StringFormat { Alignment = StringAlignment.Far };

                // Intestazioni endpoint
                for (int i = 0; i < endpts.Count; i++)
                {
                    var r = new Rectangle(epX[i] - EP_W/2 + 4, 4, EP_W - 8, 34);
                    g.FillRectangle(epBg, r);
                    g.DrawRectangle(epPen, r);
                    g.DrawString(endpts[i], epFont, epFg, r, sfC);
                    // Linea verticale tratteggiata
                    g.DrawLine(vPen, epX[i], 40, epX[i], HDR_H + msgs.Count * ROW_H + 10);
                }

                // Righe messaggi — disegna SOLO quelle nel rettangolo visibile (clip)
                // così con pcap grandi (migliaia di messaggi) il repaint resta veloce.
                int iStart = (e.ClipRectangle.Top - HDR_H) / ROW_H;
                if (iStart < 0) iStart = 0;
                int iEnd = (e.ClipRectangle.Bottom - HDR_H) / ROW_H + 1;
                if (iEnd > msgs.Count) iEnd = msgs.Count;
                for (int i = iStart; i < iEnd; i++)
                {
                    var m = msgs[i];
                    int rowY = HDR_H + i * ROW_H;
                    int midY = rowY + ROW_H / 2;

                    int si = endpts.IndexOf(m.SrcIp + ":" + m.SrcPort);
                    int di = endpts.IndexOf(m.DstIp + ":" + m.DstPort);
                    if (si < 0 || di < 0) continue;

                    // Sfondo: messaggi stessa chiamata (gruppo) del selezionato → azzurro tenue
                    bool sameCall = selMsg != null && !string.IsNullOrEmpty(GKey(selMsg))
                                    && GKey(selMsg) != "?" && GKey(m) == GKey(selMsg) && m != selMsg;
                    if (m == selMsg)
                        g.FillRectangle(new SolidBrush(Color.FromArgb(255, 252, 180)),
                            new Rectangle(0, rowY, drawPanel.Width, ROW_H));
                    else if (sameCall)
                        g.FillRectangle(new SolidBrush(Color.FromArgb(220, 235, 255)),
                            new Rectangle(0, rowY, drawPanel.Width, ROW_H));
                    // Linea separatore leggera sulle righe pari non evidenziate
                    else if (i % 2 == 0)
                        g.FillRectangle(new SolidBrush(Color.FromArgb(248, 248, 252)),
                            new Rectangle(0, rowY, drawPanel.Width, ROW_H));

                    // Timestamp
                    g.DrawString(m.Time, tFont, Brushes.DimGray,
                        new RectangleF(2, rowY + 6, TIME_W - 4, ROW_H - 8));

                    // Freccia
                    Color ac = MethodColor(m.Method);
                    int x1 = epX[si], x2 = epX[di];
                    bool same = si == di;

                    using (var ap = new Pen(ac, 1.8f))
                    {
                        if (same)
                        {
                            // Self-loop
                            g.DrawArc(ap, x1 - 18, midY - 12, 36, 24, 0, 270);
                        }
                        else
                        {
                            g.DrawLine(ap, x1, midY, x2, midY);
                            DrawHead(g, ap, x2, midY, x2 > x1);
                        }
                    }

                    // Porte vicino alle lifeline (stile Wireshark)
                    if (!same)
                    {
                        bool toRight = x2 > x1;
                        g.DrawString(m.SrcPort, pFont, portBr,
                            new PointF(toRight ? x1 + 3 : x1 - 3, midY - 14),
                            toRight ? sfNear : sfFar);
                        g.DrawString(m.DstPort, pFont, portBr,
                            new PointF(toRight ? x2 - 3 : x2 + 3, midY - 14),
                            toRight ? sfFar : sfNear);
                    }

                    // Etichetta metodo (sopra la freccia, centrata)
                    if (!same)
                    {
                        int lx = (x1 + x2) / 2;
                        string label = m.Method;
                        if (!string.IsNullOrEmpty(m.Codecs)) label += " [" + m.Codecs + "]";
                        var lr = new RectangleF(lx - 100, rowY + 1, 200, 14);
                        g.FillRectangle(Brushes.White, lr);
                        g.DrawString(label, mFont, new SolidBrush(ac), lr, sfC);
                    }
                }
            }
        }

        void DrawHead(Graphics g, Pen p, int x, int y, bool right)
        {
            int d = right ? -7 : 7;
            g.DrawLine(p, x, y, x + d, y - 4);
            g.DrawLine(p, x, y, x + d, y + 4);
        }

        Color MethodColor(string m)
        {
            if (m == null) return Color.Gray;
            if (m.StartsWith("RTP"))       return Color.FromArgb(0, 150, 70);    // verde (media RTP)
            // protocolli non-SIP: colore dedicato così si distinguono nel ladder
            if (m.StartsWith("[H.323]"))   return Color.FromArgb(140, 0, 180);   // viola
            if (m.StartsWith("[SCCP]"))    return Color.FromArgb(180, 110, 0);   // ambra (Skinny)
            if (m.StartsWith("[ISUP]"))    return Color.FromArgb(0, 130, 160);   // ciano (SIP-I/ISUP)
            if (m.StartsWith("["))         return Color.FromArgb(90, 90, 90);
            if (m.StartsWith("INVITE"))    return Color.FromArgb(0, 140, 0);
            if (m.StartsWith("200"))       return Color.FromArgb(0, 100, 200);
            if (m.StartsWith("1"))         return Color.FromArgb(130, 130, 130);
            if (m.StartsWith("BYE"))       return Color.FromArgb(140, 0, 180);
            if (m.StartsWith("CANCEL"))    return Color.FromArgb(210, 100, 0);
            if (m == "ACK")                return Color.FromArgb(0, 150, 150);
            if (m.StartsWith("REGISTER"))  return Color.FromArgb(100, 0, 200);
            if (m.StartsWith("SUBSCRIBE")) return Color.FromArgb(0, 120, 120);
            if (m.StartsWith("NOTIFY"))    return Color.FromArgb(60, 100, 60);
            if (m.StartsWith("UPDATE"))    return Color.FromArgb(160, 120, 0);
            if (Regex.IsMatch(m, @"^[45]")) return Color.Red;
            return Color.FromArgb(80, 80, 80);
        }

        void OnClick(object sender, MouseEventArgs e)
        {
            int idx = (e.Y - HDR_H) / ROW_H;
            if (idx >= 0 && idx < msgs.Count)
            {
                selMsg = msgs[idx];
                drawPanel.Invalidate();
                ShowDetail(selMsg);
                // Mostra info chiamata nel label in basso
                if (GKey(selMsg) != null && GKey(selMsg) != "?")
                {
                    string gk = GKey(selMsg);
                    int cnt = 0;
                    foreach (var m in msgs) if (GKey(m) == gk) cnt++;
                    // Cerca la CallEntry corrispondente per il display leggibile
                    string callDisplay = gk;
                    foreach (var item in cmbCallId.Items)
                    {
                        var ce = item as CallEntry;
                        if (ce != null && ce.CallId == gk)
                        { callDisplay = ce.Display; break; }
                    }
                    lblCallSel.Text = "  " + callDisplay
                                    + "   (" + cnt + " msg — doppio-click o 🔍 per isolare)";
                }
            }
        }

        protected override void OnDoubleClick(EventArgs e) { FollowSelected(); base.OnDoubleClick(e); }

        void ShowDetail(SipLadderMessage m)
        {
            if (m != null && m.IsRtp)
            {
                txtInfo.Text = m.Detail != null ? m.Detail : ("RTP " + m.Codecs);
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("══ SIP Message ══════════════════════════════");
            sb.AppendLine(" Time    : " + m.Time + (m.RelSec > 0 ? "  (+" + m.RelSec.ToString("F3") + "s)" : ""));
            sb.AppendLine(" From    : " + m.SrcIp + ":" + m.SrcPort);
            sb.AppendLine(" To      : " + m.DstIp + ":" + m.DstPort);
            sb.AppendLine(" Method  : " + m.Method);
            sb.AppendLine(" Call-ID : " + m.CallId);
            sb.AppendLine(" From URI: " + m.FromUser);
            sb.AppendLine(" To URI  : " + m.ToUser);
            if (!string.IsNullOrEmpty(m.Codecs))
            { sb.AppendLine(); sb.AppendLine(" Codecs SDP: " + m.Codecs); }

            // Analisi automatica del messaggio
            sb.AppendLine();
            sb.AppendLine(L.B("══ Analisi ══════════════════════════════════","══ Analysis ══════════════════════════════════"));
            if (m.Method.StartsWith("INVITE"))
                sb.AppendLine(L.B(" ● Apertura sessione SIP.\n   Codec offerti: "," ● SIP session setup.\n   Offered codecs: ") + (m.Codecs ?? "n/a"));
            else if (m.Method.StartsWith("200"))
                sb.AppendLine(L.B(" ● Risposta positiva — sessione stabilita o richiesta accettata."," ● Positive response — session established or request accepted."));
            else if (m.Method.StartsWith("100"))
                sb.AppendLine(L.B(" ● Provisional: il server ha ricevuto l'INVITE, sta elaborando."," ● Provisional: the server received the INVITE and is processing."));
            else if (m.Method.StartsWith("183"))
                sb.AppendLine(L.B(" ● Session Progress: early media disponibile (ring-back / IVR)."," ● Session Progress: early media available (ring-back / IVR)."));
            else if (m.Method.StartsWith("CANCEL"))
                sb.AppendLine(L.B(" ⚠ Chiamata annullata prima della risposta.\n   Causa tipica: timeout ring, utente ha riagganciato."," ⚠ Call cancelled before the answer.\n   Typical cause: ring timeout, user hung up."));
            else if (m.Method.StartsWith("487"))
                sb.AppendLine(L.B(" ⚠ Request Terminated: risposta al CANCEL.\n   Chiamata terminata normalmente lato server."," ⚠ Request Terminated: response to CANCEL.\n   Call ended normally on the server side."));
            else if (m.Method.StartsWith("BYE"))
                sb.AppendLine(L.B(" ● Fine sessione SIP (BYE)."," ● SIP session end (BYE)."));
            else if (m.Method.StartsWith("SUBSCRIBE"))
                sb.AppendLine(L.B(" ● Sottoscrizione eventi (dialog, presence, MWI...)."," ● Event subscription (dialog, presence, MWI...)."));
            else if (m.Method.StartsWith("NOTIFY"))
                sb.AppendLine(L.B(" ● Notifica evento al sottoscrittore."," ● Event notification to the subscriber."));
            else if (m.Method.StartsWith("UPDATE"))
                sb.AppendLine(L.B(" ● Aggiornamento parametri sessione (codec, hold...)."," ● Session parameter update (codec, hold...)."));
            else if (Regex.IsMatch(m.Method, @"^[45]"))
                sb.AppendLine(L.B(" 🔴 Errore SIP "," 🔴 SIP error ") + m.Method + L.B("\n   Verificare configurazione e log CUCM/CUBE.","\n   Check configuration and CUCM/CUBE logs."));

            // ── Conformità RFC 3261 (solo se tshark ha popolato gli header) ──
            bool haveHdr = !string.IsNullOrEmpty(m.Via) || !string.IsNullOrEmpty(m.CSeq)
                        || !string.IsNullOrEmpty(m.FromHdr) || !string.IsNullOrEmpty(m.Branch)
                        || !string.IsNullOrEmpty(m.MaxFwd);
            if (haveHdr)
            {
                var pt = new SipParts();
                string meth = m.Method ?? "";
                pt.IsResponse = Regex.IsMatch(meth, @"^\d{3}");
                if (pt.IsResponse) { int st; int.TryParse(meth.Substring(0, 3), out st); pt.Status = st; }
                else pt.Method = meth.Split(' ')[0].ToUpperInvariant();
                pt.StartLineOk = true;   // tshark ha dissezionato il messaggio
                pt.Via    = !string.IsNullOrEmpty(m.Via) ? m.Via : (!string.IsNullOrEmpty(m.Branch) ? "Via" : "");
                pt.Branch = m.Branch ?? "";
                pt.From   = !string.IsNullOrEmpty(m.FromHdr) ? m.FromHdr : ((!string.IsNullOrEmpty(m.FromUser) || !string.IsNullOrEmpty(m.FromTag)) ? "From" : "");
                pt.FromTag= m.FromTag ?? "";
                pt.To     = !string.IsNullOrEmpty(m.ToHdr) ? m.ToHdr : ((!string.IsNullOrEmpty(m.ToUser) || !string.IsNullOrEmpty(m.ToTag)) ? "To" : "");
                pt.ToTag  = m.ToTag ?? "";
                pt.CallId = (m.CallId == "?" || m.CallId == null) ? "" : m.CallId;
                pt.CSeq   = m.CSeq ?? "";
                pt.MaxFwd = m.MaxFwd ?? "";
                pt.Contact= m.Contact ?? "";
                pt.ContentLength = m.ContentLen ?? "";
                pt.BodyLen = -1;   // il body grezzo non è disponibile in questa vista
                sb.AppendLine();
                sb.Append(SipValidator.Report(pt));
            }

            txtInfo.Text = sb.ToString();
        }
    }

    // ─── PCAP SIP Entry (strutturato) ────────────────────────────────────────
    public class PcapSipEntry
    {
        public string Time      { get; set; }   // timestamp reale dal pcap
        public double RelSec    { get; set; }   // secondi dall'inizio cattura (frame.time_relative)
        public string Method    { get; set; }   // INVITE / 200 OK / BYE ecc.
        public string CallId    { get; set; }
        public string From      { get; set; }
        public string To        { get; set; }
        public string SrcIp     { get; set; }
        public string DstIp     { get; set; }
        public string Direction { get; set; }   // IN / OUT / ?
        // Header per la validazione RFC 3261 (Fasi 1+2)
        public string Via       { get; set; }
        public string Branch    { get; set; }
        public string FromHdr   { get; set; }
        public string FromTag   { get; set; }
        public string ToHdr     { get; set; }
        public string ToTag     { get; set; }
        public string CSeq      { get; set; }
        public string MaxFwd    { get; set; }
        public string Contact   { get; set; }
        public string ContentLen{ get; set; }
    }

    // ─── PCAP Analyzer (via tshark) ───────────────────────────────────────────
    public static class PcapAnalyzer
    {
        static readonly string[] TsharkPaths = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Wireshark", "tshark.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Wireshark", "tshark.exe"),
            "tshark.exe",
        };

        public static string FindTshark()
        {
            // 1. Percorsi fissi comuni
            foreach (var p in TsharkPaths)
                if (File.Exists(p)) return p;

            // 2. Registry: HKLM\SOFTWARE\Wireshark → InstallDir  (64-bit e 32-bit)
            string[] regKeys = {
                @"SOFTWARE\Wireshark",
                @"SOFTWARE\WOW6432Node\Wireshark",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Wireshark",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Wireshark",
            };
            foreach (var rk in regKeys)
            {
                try {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(rk))
                    {
                        if (key == null) continue;
                        // prova InstallDir e InstallLocation
                        foreach (var valName in new[] { "InstallDir", "InstallLocation", "UninstallString" })
                        {
                            string val = key.GetValue(valName) as string;
                            if (string.IsNullOrEmpty(val)) continue;
                            // UninstallString può essere "C:\Program Files\Wireshark\uninstall.exe"
                            string dir = val.Contains("uninstall") || val.EndsWith(".exe")
                                ? Path.GetDirectoryName(val.Trim('"'))
                                : val.Trim('"').TrimEnd('\\');
                            string candidate = Path.Combine(dir, "tshark.exe");
                            if (File.Exists(candidate)) return candidate;
                        }
                    }
                } catch { }
            }

            // 3. Cerca in tutte le cartelle Program Files
            foreach (var root in new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"C:\Program Files",
                @"C:\Program Files (x86)",
            })
            {
                try {
                    if (!Directory.Exists(root)) continue;
                    foreach (var subdir in Directory.GetDirectories(root, "*wireshark*", SearchOption.TopDirectoryOnly))
                    {
                        string candidate = Path.Combine(subdir, "tshark.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                } catch { }
            }

            // 4. Comando WHERE (richiede che tshark sia nel PATH di sistema)
            try {
                var pi = new ProcessStartInfo("where", "tshark.exe") {
                    UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
                };
                var pr = Process.Start(pi);
                string found = pr.StandardOutput.ReadLine();
                pr.WaitForExit(3000);
                if (!string.IsNullOrEmpty(found) && File.Exists(found.Trim())) return found.Trim();
            } catch { }

            return null;
        }

        // ── Passata 1: fields strutturati — timestamp reali + IP + metodo ──────
        // tshark -r file -Y sip -T fields
        //   -e frame.time_relative   (secondi dall'inizio cattura)
        //   -e frame.time            (data/ora assoluta)
        //   -e ip.src  -e ip.dst
        //   -e sip.Request-Line      (es. "INVITE sip:...")
        //   -e sip.Status-Line       (es. "SIP/2.0 200 OK")
        //   -e sip.Call-ID
        //   -e sip.from.user  -e sip.to.user
        //   -E separator=|  -E quote=n
        // Accesso difensivo ai campi tshark (tshark taglia gli empty finali)
        static string Fld(string[] a, int i) { return (a != null && i < a.Length) ? a[i].Trim() : ""; }

        public static List<PcapSipEntry> ExtractCallFlow(string tshark, string pcap)
        {
            var result = new List<PcapSipEntry>();
            try
            {
                string args = string.Format(
                    "-r \"{0}\" -Y sip -T fields " +
                    "-e frame.time_relative -e frame.time -e ip.src -e ip.dst " +
                    "-e sip.Request-Line -e sip.Status-Line " +
                    "-e sip.Call-ID -e sip.from.user -e sip.to.user " +
                    "-e sip.Via -e sip.Via.branch -e sip.From -e sip.from.tag -e sip.To -e sip.to.tag " +
                    "-e sip.CSeq -e sip.Max-Forwards -e sip.Contact -e sip.Content-Length " +
                    "-E separator=| -E quote=n -E occurrence=f",
                    pcap);

                var psi = new ProcessStartInfo(tshark, args) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    var f = line.Split('|');
                    if (f.Length < 9) continue;
                    string reqLine    = f[4].Trim();
                    string statusLine = f[5].Trim();
                    string method = "";
                    if (!string.IsNullOrEmpty(reqLine))
                    {
                        // "INVITE sip:..." → prende solo il metodo
                        int sp = reqLine.IndexOf(' ');
                        method = sp > 0 ? reqLine.Substring(0, sp) : reqLine;
                    }
                    else if (!string.IsNullOrEmpty(statusLine))
                    {
                        // "SIP/2.0 200 OK" → "200 OK"
                        var m = Regex.Match(statusLine, @"SIP/2\.0\s+(\d{3}\s+.+)");
                        method = m.Success ? m.Groups[1].Value.Trim() : statusLine;
                        if (method.Length > 30) method = method.Substring(0, 30) + "…";
                    }
                    else continue;

                    // Tempo: frame.time_relative (secondi) + frame.time (assoluto → HH:mm:ss.fff)
                    double relSec; double.TryParse(f[0].Trim(),
                        System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out relSec);
                    string time = ParseTsharkTime(f[1].Trim());

                    string cid = f[6].Trim();
                    result.Add(new PcapSipEntry {
                        Time      = time,
                        RelSec    = relSec,
                        Method    = method,
                        SrcIp     = f[2].Trim(),
                        DstIp     = f[3].Trim(),
                        CallId    = cid.Length > 0 ? cid : "?",
                        From      = f[7].Trim(),
                        To        = f[8].Trim(),
                        Direction = "?",  // senza riferimento IP locale non possiamo sapere IN/OUT
                        Via        = Fld(f, 9),
                        Branch     = Fld(f, 10),
                        FromHdr    = Fld(f, 11),
                        FromTag    = Fld(f, 12),
                        ToHdr      = Fld(f, 13),
                        ToTag      = Fld(f, 14),
                        CSeq       = Fld(f, 15),
                        MaxFwd     = Fld(f, 16),
                        Contact    = Fld(f, 17),
                        ContentLen = Fld(f, 18)
                    });
                }
                proc.WaitForExit(60000);
            }
            catch { }
            return result;
        }

        // ── Passata per il Ladder diagram ─────────────────────────────────────
        // STRATEGIA: usa ExtractCallFlow (separator=| già funzionante) per i
        // dati SIP, poi un pass veloce con separator=| per aggiungere le porte.
        // Evita completamente il separatore \t che ProcessStartInfo non passa bene.
        public static List<SipLadderMessage> ExtractLadderMessages(string tshark, string pcap)
        {
            // 1. Prendi i dati SIP dalla stessa command che già funziona per il Call Flow
            var sipEntries = ExtractCallFlow(tshark, pcap);
            if (sipEntries == null || sipEntries.Count == 0)
                return new List<SipLadderMessage>();

            // 2. Estrai porte (UDP+TCP) con separator=| che sappiamo funziona
            //    Campi: frame.number | udp.srcport | udp.dstport | tcp.srcport | tcp.dstport
            var portMap = new Dictionary<int, string[]>(); // frameNum → [srcPort, dstPort]
            try
            {
                string portArgs = string.Format(
                    "-r \"{0}\" -Y sip -T fields " +
                    "-e frame.number -e udp.srcport -e udp.dstport " +
                    "-e tcp.srcport -e tcp.dstport " +
                    "-E separator=| -E quote=n -E occurrence=f",
                    pcap);
                var psi2 = new ProcessStartInfo(tshark, portArgs) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var p2 = Process.Start(psi2);
                string ln;
                int frameIdx = 0;
                while ((ln = p2.StandardOutput.ReadLine()) != null)
                {
                    var f2 = ln.Split('|');
                    if (f2.Length < 3) { frameIdx++; continue; }
                    string sp = f2.Length > 1 ? f2[1].Trim() : "";
                    string dp = f2.Length > 2 ? f2[2].Trim() : "";
                    if (string.IsNullOrEmpty(sp) && f2.Length > 3) sp = f2[3].Trim();
                    if (string.IsNullOrEmpty(dp) && f2.Length > 4) dp = f2[4].Trim();
                    portMap[frameIdx] = new[] {
                        string.IsNullOrEmpty(sp) ? "5060" : sp,
                        string.IsNullOrEmpty(dp) ? "5060" : dp
                    };
                    frameIdx++;
                }
                p2.WaitForExit(30000);
            }
            catch { }

            // 3. Converti PcapSipEntry → SipLadderMessage (RelSec dal frame.time_relative)
            var result = new List<SipLadderMessage>();
            for (int i = 0; i < sipEntries.Count; i++)
            {
                var e   = sipEntries[i];
                var ports = portMap.ContainsKey(i) ? portMap[i] : new[] { "5060", "5060" };
                result.Add(new SipLadderMessage {
                    Time     = e.Time,
                    RelSec   = e.RelSec,
                    SrcIp    = e.SrcIp,
                    DstIp    = e.DstIp,
                    SrcPort  = ports[0],
                    DstPort  = ports[1],
                    Method   = e.Method,
                    CallId   = e.CallId,
                    Group    = e.CallId,   // default: ogni Call-ID è la sua chiamata
                    FromUser = e.From,
                    ToUser   = e.To,
                    Codecs   = "",
                    Via        = e.Via,
                    Branch     = e.Branch,
                    FromHdr    = e.FromHdr,
                    FromTag    = e.FromTag,
                    ToHdr      = e.ToHdr,
                    ToTag      = e.ToTag,
                    CSeq       = e.CSeq,
                    MaxFwd     = e.MaxFwd,
                    Contact    = e.Contact,
                    ContentLen = e.ContentLen
                });
            }

            // 4. Raggruppamento per Call-ID PURO — identico a Wireshark "VoIP Calls":
            //    ogni Call-ID è una chiamata. Le re-INVITE restano insieme (stesso
            //    Call-ID); SUBSCRIBE/NOTIFY/REGISTER sono Call-ID distinti → fuori.
            //    Nessuna fusione euristica delle gambe.
            foreach (var m in result) m.Group = m.CallId;

            // 5. Mappa endpoint media (da SDP c=/m=) per ogni Call-ID — query tshark
            //    SEPARATA: se i nomi-campo SDP non fossero validi su questa versione,
            //    fallisce solo l'aggancio RTP-via-SDP, non l'estrazione SIP.
            var callMedia   = new Dictionary<string, HashSet<string>>(); // callId → "ip:port"
            var callMediaIp = new Dictionary<string, HashSet<string>>(); // callId → ip
            ExtractSdpMedia(tshark, pcap, callMedia, callMediaIp);

            // 6. Estrai gli stream RTP e agganciali alla chiamata via SDP (fallback su IP)
            try
            {
                var rtp = ExtractRtpStreams(tshark, pcap);
                foreach (var st in rtp)
                {
                    string e1 = st.SrcIp + ":" + st.SrcPort;
                    string e2 = st.DstIp + ":" + st.DstPort;
                    string cid = FindCallByEndpoint(callMedia, e1);
                    if (cid == null) cid = FindCallByEndpoint(callMedia, e2);
                    if (cid == null) cid = FindCallByIp(callMediaIp, st.SrcIp);
                    if (cid == null) cid = FindCallByIp(callMediaIp, st.DstIp);

                    string grp = (cid != null) ? cid : "?";
                    string codec = CodecName(st.Ptype);
                    double dur = st.LastRel - st.FirstRel; if (dur < 0) dur = 0;

                    var detail = new StringBuilder();
                    detail.AppendLine("══ RTP Stream ══════════════════════════════");
                    detail.AppendLine(" Codec   : " + codec + "  (PT " + st.Ptype + ")");
                    detail.AppendLine(" SSRC    : " + st.Ssrc);
                    detail.AppendLine(" From    : " + e1);
                    detail.AppendLine(" To      : " + e2);
                    detail.AppendLine(L.B(" Pacchetti: "," Packets: ") + st.Pkts);
                    detail.AppendLine(" Durata  : " + dur.ToString("F1") + " s");

                    // Qualità voce: perdita, jitter, MOS stimato
                    int received = st.Pkts;
                    int expected = (st.MaxSeq >= 0 && st.MinSeq >= 0 && st.MaxSeq >= st.MinSeq) ? (st.MaxSeq - st.MinSeq + 1) : received;
                    int lost = expected - received; if (lost < 0) lost = 0;
                    double lossPct = expected > 0 ? 100.0 * lost / expected : 0;
                    double jitterMs = st.Jitter / ClockHz(st.Ptype) * 1000.0;
                    double mos = EstimateMos(lossPct, jitterMs);
                    detail.AppendLine(L.B("── Qualità voce ──","── Voice quality ──"));
                    detail.AppendLine(" Persi    : " + lost + " / " + expected + "  (" + lossPct.ToString("F1") + "%)");
                    detail.AppendLine(" Jitter   : " + jitterMs.ToString("F1") + " ms");
                    detail.AppendLine(" MOS stim.: " + mos.ToString("F1") + "  (" + MosVerdict(mos) + ")");

                    result.Add(new SipLadderMessage {
                        Time     = "▶ +" + st.FirstRel.ToString("F1"),
                        RelSec   = st.FirstRel,
                        SrcIp    = st.SrcIp, SrcPort = st.SrcPort,
                        DstIp    = st.DstIp, DstPort = st.DstPort,
                        Method   = "RTP ▶ " + st.Pkts + " pkt",
                        CallId   = cid ?? "?",
                        Group    = grp,
                        Codecs   = codec,
                        IsRtp    = true,
                        Detail   = detail.ToString()
                    });
                }
            }
            catch { }

            // 7. Ordina tutto per tempo relativo (SIP e RTP interlacciati)
            result.Sort(delegate (SipLadderMessage a, SipLadderMessage b) { return a.RelSec.CompareTo(b.RelSec); });
            return result;
        }

        // ── Union-find: unisce i Call-ID che sono gambe della stessa telefonata ──
        static Dictionary<string, string> BuildCallGroups(List<SipLadderMessage> msgs)
        {
            var users = new Dictionary<string, string[]>();    // callId → [calling, called]
            var t0    = new Dictionary<string, double>();
            var t1    = new Dictionary<string, double>();
            var order = new List<string>();
            var isCall = new HashSet<string>();                 // Call-ID con un INVITE/Setup = vera telefonata
            foreach (var m in msgs)
            {
                string c = m.CallId;
                if (string.IsNullOrEmpty(c) || c == "?") continue;
                if (!t0.ContainsKey(c)) { t0[c] = m.RelSec; t1[c] = m.RelSec; users[c] = new[] { "", "" }; order.Add(c); }
                if (m.RelSec < t0[c]) t0[c] = m.RelSec;
                if (m.RelSec > t1[c]) t1[c] = m.RelSec;
                if (!string.IsNullOrEmpty(m.FromUser) && users[c][0] == "") users[c][0] = m.FromUser;
                if (!string.IsNullOrEmpty(m.ToUser)   && users[c][1] == "") users[c][1] = m.ToUser;
                // INVITE (o Setup H.323) qualifica il dialogo come telefonata
                if (m.Method != null && (m.Method.StartsWith("INVITE") || m.Method.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0))
                    isCall.Add(c);
            }

            var parent = new Dictionary<string, string>();
            foreach (var c in order) parent[c] = c;

            for (int i = 0; i < order.Count; i++)
                for (int j = i + 1; j < order.Count; j++)
                {
                    string a = order[i], b = order[j];
                    // Unisci SOLO vere telefonate (con INVITE): REGISTER/SUBSCRIBE/
                    // NOTIFY/OPTIONS restano dialoghi separati, come in Wireshark.
                    if (!isCall.Contains(a) || !isCall.Contains(b)) continue;
                    bool timeClose = !(t1[a] < t0[b] - 8 || t1[b] < t0[a] - 8); // finestre entro 8s
                    if (timeClose && ShareUser(users[a], users[b])) Union(parent, a, b);
                }

            var grp = new Dictionary<string, string>();
            foreach (var c in order) grp[c] = Find(parent, c);
            return grp;
        }

        static bool ShareUser(string[] a, string[] b)
        {
            foreach (var x in a)
                if (!string.IsNullOrEmpty(x))
                    foreach (var y in b)
                        if (x == y) return true;
            return false;
        }

        static string Find(Dictionary<string, string> p, string x)
        {
            while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; }
            return x;
        }

        static void Union(Dictionary<string, string> p, string a, string b)
        {
            string ra = Find(p, a), rb = Find(p, b);
            if (ra != rb) p[ra] = rb;
        }

        static string FindCallByEndpoint(Dictionary<string, HashSet<string>> map, string ipPort)
        {
            foreach (var kv in map) if (kv.Value.Contains(ipPort)) return kv.Key;
            return null;
        }

        static string FindCallByIp(Dictionary<string, HashSet<string>> map, string ip)
        {
            foreach (var kv in map) if (kv.Value.Contains(ip)) return kv.Key;
            return null;
        }

        public class RtpStream
        {
            public string SrcIp, SrcPort, DstIp, DstPort, Ssrc, Ptype;
            public int    Pkts;
            public double FirstRel, LastRel;
            // Qualità (RFC 3550)
            public int    MinSeq = -1, MaxSeq = -1;
            public double Jitter;        // in unità di timestamp RTP
            public double LastTransit;   // stato per il calcolo jitter
            public bool   HaveLast;
        }

        // ── Stream RTP aggregati per (src,sport,dst,dport,ssrc) ───────────────
        // rtp.heuristic_rtp:TRUE → riconosce l'RTP anche senza il setup SDP nello
        // stesso file (es. cattura parziale), come fa Wireshark con l'euristica.
        public static List<RtpStream> ExtractRtpStreams(string tshark, string pcap)
        {
            var map = new Dictionary<string, RtpStream>();
            try
            {
                string args = string.Format(
                    "-r \"{0}\" -o rtp.heuristic_rtp:TRUE -Y rtp -T fields " +
                    "-e frame.time_relative -e ip.src -e udp.srcport -e ip.dst -e udp.dstport " +
                    "-e rtp.ssrc -e rtp.p_type -e rtp.seq -e rtp.timestamp " +
                    "-E separator=| -E quote=n -E occurrence=f", pcap);
                var psi = new ProcessStartInfo(tshark, args) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    var f = line.Split('|');
                    if (f.Length < 7) continue;
                    double rel; double.TryParse(f[0].Trim(), System.Globalization.NumberStyles.Any, inv, out rel);
                    string src = f[1].Trim(), sp = f[2].Trim(), dst = f[3].Trim(), dp = f[4].Trim();
                    string ssrc = f[5].Trim(), pt = f[6].Trim();
                    if (src.Length == 0 || dst.Length == 0) continue;
                    string key = src + "|" + sp + "|" + dst + "|" + dp + "|" + ssrc;
                    RtpStream st;
                    if (!map.TryGetValue(key, out st))
                    {
                        st = new RtpStream { SrcIp = src, SrcPort = sp, DstIp = dst, DstPort = dp,
                            Ssrc = ssrc, Ptype = pt, Pkts = 0, FirstRel = rel, LastRel = rel };
                        map[key] = st;
                    }
                    st.Pkts++;
                    if (rel < st.FirstRel) st.FirstRel = rel;
                    if (rel > st.LastRel)  st.LastRel  = rel;

                    // Qualità: sequenza (perdita) + jitter (RFC 3550)
                    int seq;
                    if (f.Length > 7 && int.TryParse(f[7].Trim(), out seq))
                    {
                        if (st.MinSeq < 0 || seq < st.MinSeq) st.MinSeq = seq;
                        if (seq > st.MaxSeq) st.MaxSeq = seq;
                    }
                    long rtpTs;
                    if (f.Length > 8 && long.TryParse(f[8].Trim(), out rtpTs))
                    {
                        double clock = ClockHz(pt);
                        double transit = rel * clock - rtpTs;
                        if (st.HaveLast)
                        {
                            double d = transit - st.LastTransit; if (d < 0) d = -d;
                            st.Jitter += (d - st.Jitter) / 16.0;
                        }
                        st.LastTransit = transit; st.HaveLast = true;
                    }
                }
                proc.WaitForExit(120000);
            }
            catch { }
            return new List<RtpStream>(map.Values);
        }

        // ── Endpoint media SDP per Call-ID (query tshark separata e isolata) ──
        static void ExtractSdpMedia(string tshark, string pcap,
            Dictionary<string, HashSet<string>> callMedia,
            Dictionary<string, HashSet<string>> callMediaIp)
        {
            try
            {
                string args = string.Format(
                    "-r \"{0}\" -Y \"sip and sdp\" -T fields " +
                    "-e sip.Call-ID -e sdp.connection_info.connection_address -e sdp.media.port " +
                    "-E separator=| -E quote=n -E occurrence=f", pcap);
                var psi = new ProcessStartInfo(tshark, args) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    var f = line.Split('|');
                    if (f.Length < 3) continue;
                    string cid = f[0].Trim(), ip = f[1].Trim(), port = f[2].Trim();
                    if (cid.Length == 0 || ip.Length == 0 || port.Length == 0 || port == "0") continue;
                    if (!callMedia.ContainsKey(cid)) { callMedia[cid] = new HashSet<string>(); callMediaIp[cid] = new HashSet<string>(); }
                    callMedia[cid].Add(ip + ":" + port);
                    callMediaIp[cid].Add(ip);
                }
                proc.WaitForExit(30000);
            }
            catch { }
        }

        static string CodecName(string pt)
        {
            switch (pt)
            {
                case "0":   return "g711U";
                case "3":   return "GSM";
                case "4":   return "G723";
                case "8":   return "g711A";
                case "9":   return "G722";
                case "18":  return "G729";
                case "101": return "telephone-event";
                default:    return string.IsNullOrEmpty(pt) ? "RTP" : "PT" + pt;
            }
        }

        // Clock RTP (Hz) per payload type — i codec voce comuni usano 8000 Hz.
        static double ClockHz(string pt) { return 8000.0; }

        // MOS stimato (E-model semplificato) da perdita% e jitter(ms). Approssimato.
        static double EstimateMos(double lossPct, double jitterMs)
        {
            double delay = 30 + jitterMs * 2;                 // delay one-way stimato (ms)
            double Id = delay < 160 ? delay * 0.024 : delay * 0.11 - 13.6;
            double Ipl = 30 * System.Math.Log(1 + 15 * (lossPct / 100.0));
            double R = 93.2 - Id - Ipl;
            if (R < 0) R = 0; if (R > 100) R = 100;
            double mos = 1 + 0.035 * R + R * (R - 60) * (100 - R) * 7e-6;
            if (mos < 1) mos = 1; if (mos > 4.5) mos = 4.5;
            return mos;
        }

        static string MosVerdict(double mos)
        {
            if (mos >= 4.0) return "buono";
            if (mos >= 3.6) return "discreto";
            if (mos >= 3.1) return "mediocre";
            return "scarso";
        }

        // ── tshark -z voip,calls ──────────────────────────────────────────────
        public static string GetVoipStats(string tshark, string pcap)
        {
            return RunTsharkZ(tshark, pcap, "voip,calls");
        }

        // ── tshark -z sip,stat ────────────────────────────────────────────────
        public static string GetSipStats(string tshark, string pcap)
        {
            return RunTsharkZ(tshark, pcap, "sip,stat");
        }

        static string RunTsharkZ(string tshark, string pcap, string zOption)
        {
            try {
                var psi = new ProcessStartInfo(tshark,
                    string.Format("-r \"{0}\" -q -z {1}", pcap, zOption)) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(30000);
                return output;
            } catch { return null; }
        }

        // ── Passata 2: output verbose per analisi regole ──────────────────────
        public static string ExtractVerbose(string tshark, string pcap)
        {
            return ExtractVerboseFilter(tshark, pcap, "sip");
        }

        // H.323 verbose (H.225 + H.245)
        public static string ExtractH323Verbose(string tshark, string pcap)
        {
            return ExtractVerboseFilter(tshark, pcap, "h225 or h245");
        }

        // MEGACO/H.248 verbose
        public static string ExtractMegacoVerbose(string tshark, string pcap)
        {
            return ExtractVerboseFilter(tshark, pcap, "megaco");
        }

        // SCCP/Skinny verbose
        public static string ExtractSkinnyVerbose(string tshark, string pcap)
        {
            return ExtractVerboseFilter(tshark, pcap, "skinny");
        }

        // SIP-I verbose (SIP con ISUP body)
        public static string ExtractSipIVerbose(string tshark, string pcap)
        {
            // SIP packets that contain ISUP MIME body
            return ExtractVerboseFilter(tshark, pcap, "sip && media.type == \"application/isup\"");
        }

        static string ExtractVerboseFilter(string tshark, string pcap, string filter)
        {
            try {
                var psi = new ProcessStartInfo(tshark,
                    string.Format("-r \"{0}\" -Y \"{1}\" -T text -V", pcap, filter)) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(60000);
                return output;
            } catch { return null; }
        }

        // ── H.323 Ladder messages (H.225 Setup/Connect/Release) ──────────────
        public static List<SipLadderMessage> ExtractH323LadderMessages(string tshark, string pcap)
        {
            var result = new List<SipLadderMessage>();
            try
            {
                // h225.messageType: 0=Setup, 1=CallProceeding, 2=Connect, 3=Alerting, 5=ReleaseComplete, 6=Facility, 7=Progress
                string args = string.Format(
                    "-r \"{0}\" -Y h225 -T fields " +
                    "-e frame.time -e frame.time_relative " +
                    "-e ip.src -e ip.dst " +
                    "-e tcp.srcport -e tcp.dstport " +
                    "-e h225.messageType " +
                    "-e h225.callIdentifier.guid " +
                    "-e h225.terminalAlias.e164 " +
                    "-E separator=| -E quote=n -E occurrence=f",
                    pcap);

                var psi = new ProcessStartInfo(tshark, args) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    var f = line.Split('|');
                    if (f.Length < 7) continue;
                    string srcPort = f.Length > 4 ? f[4].Trim() : "1720";
                    string dstPort = f.Length > 5 ? f[5].Trim() : "1720";
                    if (string.IsNullOrEmpty(srcPort)) srcPort = "1720";
                    if (string.IsNullOrEmpty(dstPort)) dstPort = "1720";

                    string msgType = f[6].Trim();
                    // Mappa il valore numerico
                    string method = MapH225MessageType(msgType);
                    if (string.IsNullOrEmpty(method)) method = "H.225/" + msgType;

                    double relSec = 0;
                    if (f.Length > 1) double.TryParse(
                        f[1].Trim().Replace(",", "."),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out relSec);

                    result.Add(new SipLadderMessage {
                        Time     = ParseTsharkTime(f[0].Trim()),
                        RelSec   = relSec,
                        SrcIp    = f.Length > 2 ? f[2].Trim() : "?",
                        DstIp    = f.Length > 3 ? f[3].Trim() : "?",
                        SrcPort  = srcPort,
                        DstPort  = dstPort,
                        Method   = "[H.323] " + method,
                        CallId   = f.Length > 7 ? f[7].Trim().Substring(0, Math.Min(20, (f.Length > 7 ? f[7].Trim().Length : 0))) : "?",
                        FromUser = f.Length > 8 ? f[8].Trim() : "",
                        ToUser   = "",
                        Codecs   = ""
                    });
                }
                proc.WaitForExit(30000);
            }
            catch { }
            return result;
        }

        // ── Estrazione Skinny/SCCP per il Ladder (telefono ↔ CUCM) ───────────
        public static List<SipLadderMessage> ExtractSkinnyLadderMessages(string tshark, string pcap)
        {
            var result = new List<SipLadderMessage>();
            try
            {
                // _ws.col.info dà il nome del messaggio leggibile (OpenReceiveChannel, ecc.)
                // skinny.callId raggruppa per chiamata; callingParty/calledParty quando presenti.
                string args = string.Format(
                    "-r \"{0}\" -Y skinny -T fields " +
                    "-e frame.time -e frame.time_relative " +
                    "-e ip.src -e ip.dst " +
                    "-e tcp.srcport -e tcp.dstport " +
                    "-e _ws.col.info " +
                    "-e skinny.callReference " +
                    "-e skinny.callingParty -e skinny.calledParty " +
                    "-E separator=| -E quote=n -E occurrence=f",
                    pcap);

                var psi = new ProcessStartInfo(tshark, args) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    var f = line.Split('|');
                    if (f.Length < 7) continue;

                    double relSec = 0;
                    double.TryParse(f[1].Trim().Replace(",", "."),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out relSec);

                    string info = f.Length > 6 ? f[6].Trim() : "";
                    // la Info column può contenere più messaggi separati da virgola → prendi il primo
                    int comma = info.IndexOf(',');
                    if (comma > 0) info = info.Substring(0, comma).Trim();
                    if (string.IsNullOrEmpty(info)) info = "SCCP";

                    // Scarta il rumore di manutenzione/registrazione (non sono chiamate):
                    // tiene il ladder pulito e veloce, mostra solo i messaggi di chiamata/media.
                    if (IsSkinnyNoise(info)) continue;

                    string callId = f.Length > 7 ? f[7].Trim() : "";
                    if (callId == "0") callId = "";   // 0 = nessuna chiamata (KeepAlive, Register…)

                    result.Add(new SipLadderMessage {
                        Time     = ParseTsharkTime(f[0].Trim()),
                        RelSec   = relSec,
                        SrcIp    = f.Length > 2 ? f[2].Trim() : "?",
                        DstIp    = f.Length > 3 ? f[3].Trim() : "?",
                        SrcPort  = (f.Length > 4 && f[4].Trim().Length > 0) ? f[4].Trim() : "2000",
                        DstPort  = (f.Length > 5 && f[5].Trim().Length > 0) ? f[5].Trim() : "2000",
                        Method   = "[SCCP] " + info,
                        CallId   = string.IsNullOrEmpty(callId) ? "" : "SCCP-" + callId,
                        FromUser = f.Length > 8 ? f[8].Trim() : "",
                        ToUser   = f.Length > 9 ? f[9].Trim() : "",
                        Codecs   = ""
                    });
                }
                proc.WaitForExit(30000);
            }
            catch { }
            return result;
        }

        // ── Estrazione ISUP per il Ladder (SIP-I / SS7) ───────────────────────
        public static List<SipLadderMessage> ExtractIsupLadderMessages(string tshark, string pcap)
        {
            var result = new List<SipLadderMessage>();
            try
            {
                string args = string.Format(
                    "-r \"{0}\" -Y isup -T fields " +
                    "-e frame.time -e frame.time_relative " +
                    "-e ip.src -e ip.dst " +
                    "-e udp.srcport -e udp.dstport " +
                    "-e isup.message_type " +
                    "-e isup.cic " +
                    "-E separator=| -E quote=n -E occurrence=f",
                    pcap);

                var psi = new ProcessStartInfo(tshark, args) {
                    UseShellExecute = false, RedirectStandardOutput = true,
                    RedirectStandardError = true, CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    var f = line.Split('|');
                    if (f.Length < 7) continue;

                    double relSec = 0;
                    double.TryParse(f[1].Trim().Replace(",", "."),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out relSec);

                    string mt   = f.Length > 6 ? f[6].Trim() : "";
                    // se ci sono più messaggi ISUP nel pacchetto, prendi il primo
                    int comma = mt.IndexOf(',');
                    if (comma > 0) mt = mt.Substring(0, comma).Trim();
                    string name = MapIsupType(mt);

                    string cic = f.Length > 7 ? f[7].Trim() : "";
                    if (cic.IndexOf(',') > 0) cic = cic.Substring(0, cic.IndexOf(',')).Trim();

                    result.Add(new SipLadderMessage {
                        Time     = ParseTsharkTime(f[0].Trim()),
                        RelSec   = relSec,
                        SrcIp    = f.Length > 2 ? f[2].Trim() : "?",
                        DstIp    = f.Length > 3 ? f[3].Trim() : "?",
                        SrcPort  = (f.Length > 4 && f[4].Trim().Length > 0) ? f[4].Trim() : "5060",
                        DstPort  = (f.Length > 5 && f[5].Trim().Length > 0) ? f[5].Trim() : "5060",
                        Method   = "[ISUP] " + name,
                        CallId   = string.IsNullOrEmpty(cic) ? "" : "ISUP-CIC-" + cic,
                        FromUser = "",
                        ToUser   = "",
                        Codecs   = ""
                    });
                }
                proc.WaitForExit(30000);
            }
            catch { }
            return result;
        }

        // Messaggi Skinny di manutenzione/registrazione da escludere dal ladder
        static bool IsSkinnyNoise(string info)
        {
            if (string.IsNullOrEmpty(info)) return false;
            string u = info.ToLowerInvariant();
            return u.Contains("keepalive") || u.Contains("register") || u.Contains("unregister") ||
                   u.Contains("capabilities") || u.Contains("buttontemplate") || u.Contains("linestat") ||
                   u.Contains("speeddial") || u.Contains("configstat") || u.Contains("timedate") ||
                   u.Contains("version") || u.Contains("alarm") || u.Contains("server") ||
                   u.Contains("forwardstat") || u.Contains("displaytext") || u.Contains("clearprompt") ||
                   u.Contains("setlamp") || u.Contains("setringer") || u.Contains("setspeaker");
        }

        // Mappa il tipo di messaggio ISUP (ITU-T Q.763)
        static string MapIsupType(string t)
        {
            switch (t.Trim().ToLowerInvariant())
            {
                case "1":  case "iam": return "IAM";   // Initial Address
                case "2":  return "SAM";               // Subsequent Address
                case "5":  return "COT";               // Continuity
                case "6":  case "acm": return "ACM";   // Address Complete
                case "7":  case "con": return "CON";   // Connect
                case "9":  case "anm": return "ANM";   // Answer
                case "12": case "rel": return "REL";   // Release
                case "13": return "RSC";               // Reset Circuit
                case "16": case "rlc": return "RLC";   // Release Complete
                case "44": case "cpg": return "CPG";   // Call Progress
                case "":   return "ISUP";
                default:   return string.IsNullOrEmpty(t) ? "ISUP" : "ISUP/" + t;
            }
        }

        static string MapH225MessageType(string t)
        {
            // tshark restituisce la stringa del valore enum direttamente
            switch (t.ToLower())
            {
                case "setup":          return "Setup";
                case "callproceeding": return "CallProceeding";
                case "connect":        return "Connect";
                case "alerting":       return "Alerting";
                case "releasecomplete":return "ReleaseComplete";
                case "facility":       return "Facility";
                case "progress":       return "Progress";
                case "information":    return "Information";
                case "status":         return "Status";
                case "0": return "Setup";
                case "1": return "CallProceeding";
                case "2": return "Connect";
                case "3": return "Alerting";
                case "5": return "ReleaseComplete";
                case "6": return "Facility";
                case "7": return "Progress";
                default: return t;
            }
        }

        // Converte timestamp tshark ("Jan  1, 2025 14:32:01.123456789 CET") → "HH:mm:ss.fff"
        static string ParseTsharkTime(string raw)
        {
            // Prova parse diretto
            DateTime dt;
            if (DateTime.TryParse(raw, out dt))
                return dt.ToString("HH:mm:ss.fff");
            // Regex fallback: cerca HH:MM:SS.
            var m = Regex.Match(raw, @"(\d{2}:\d{2}:\d{2}\.\d+)");
            return m.Success ? m.Groups[1].Value.Substring(0, Math.Min(12, m.Groups[1].Value.Length)) : raw;
        }
    }
}
