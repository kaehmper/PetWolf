VOLLSTÄNDIGE DOKUMENTATION: C#-CARBON PLUGINS ENTWICKELN

Eine detaillierte und genaue Anleitung zur Entwicklung von Carbon-Plugins mit C#, basierend auf der offiziellen Carbon-Dokumentation.

================================================================================
INHALTSVERZEICHNIS
================================================================================

1. GRUNDLAGEN UND PROJEKT-SETUP
2. PLUGIN-STRUKTUR UND LIFECYCLE
3. HOOKS UND EVENT-HANDLING
4. COMMAND-SYSTEM DETAILLIERT
5. COMMAND-AUTHENTIFIZIERUNG UND SICHERHEIT
6. PERMISSIONS UND GRUPPEN-VERWALTUNG
7. TIMER UND FRAME-BASIERTE PLANUNG
8. LIGHTWEIGHT UI (LUI) - KOMPLETTES SYSTEM
9. CLIENT-ENTITIES FÜR NETZWERK-OBJEKTE
10. EXTENSIONS UND WIEDERVERWENDBARER CODE
11. ZIP-SCRIPT-PACKAGES FÜR MODULARISIERUNG
12. BEST PRACTICES UND ERWEITERTE KONZEPTE

================================================================================
1. GRUNDLAGEN UND PROJEKT-SETUP
================================================================================

WAS IST CARBON?

Carbon ist ein modernes Plugin-Framework für Rust-Server, das auf der Oxide-Basis aufbaut und erweiterte Funktionalität bietet. Carbon ermöglicht zwei Hauptentwicklungsansätze:

1. Oxide-kompatible Plugins (RustPlugin-Basis) - traditioneller Ansatz mit vollständiger Kompatibilität zu bestehenden Oxide-Plugins
2. Carbon-spezifische Plugins (CarbonPlugin-Basis) - moderner Ansatz mit zusätzlichen Carbon-Tools und Features

Die Wahl hängt davon ab, ob Sie bestehende Oxide-Plugins portieren möchten oder von Anfang an Carbon-Features nutzen wollen.

SYSTEMANFORDERUNGEN

Um Carbon-Plugins zu entwickeln, benötigen Sie:
- Programmiersprache C# (.NET Framework kompatibel mit Rust)
- IDE wie Visual Studio 2022+, Visual Studio Code mit C#-Erweiterung oder Rider
- Rust Dedicated Server mit Carbon installiert
- Grundkenntnisse in C# und objektorientierter Programmierung
- Verständnis für Rust-Server-Mechaniken (optional, aber hilfreich)

PROJEKT-VERZEICHNISSTRUKTUR

Die Standard-Verzeichnisstruktur auf einem Rust-Server mit Carbon sieht folgendermaßen aus:

RustDedicated/
├── carbon/
│   ├── plugins/                           # Alle aktivierten Plugins (.cs oder .cszip Dateien)
│   │   ├── MeinErstesPlugin.cs           # Einzelne Plugin-Datei
│   │   ├── KomplexesPlugin.cszip         # Gepacktes Plugin mit mehreren Dateien
│   │   └── cszip_dev/                    # Entwicklungsordner für DEBUG-Builds
│   │       └── MeinPlugin/               # Ordner mit Plugin-Name
│   │           ├── MeinPlugin.cs         # Hauptdatei
│   │           ├── Commands.cs           # Commands-Modul
│   │           └── Hooks.cs              # Event-Handler Modul
│   ├── extensions/                        # Wiederverwendbare DLL-Libraries
│   │   ├── MeineExtension.dll
│   │   └── HilfsExtension.dll
│   ├── config/                            # Plugin-Konfigurationsdateien
│   │   └── meinplugin.json
│   └── data/                              # Plugin-Datendateien
│       └── meinplugin_daten.json
├── RustDedicated_Data/
│   └── Managed/                          # Rust Engine DLLs (nicht editieren!)
└── server.cfg                            # Server-Konfiguration

Für die lokale Entwicklung können Sie eine ähnliche Struktur auf Ihrem Entwicklungs-PC nachbilden.

NAMENSBEREICHE (NAMESPACES)

Carbon nutzt zwei Standard-Namensbereiche:

Oxide.Plugins - wird für Oxide-kompatible Plugins verwendet
namespace Oxide.Plugins;

Dies ist der traditionelle Namespace, der auch mit älteren Oxide-Systemen kompatibel ist.

Carbon.Plugins - wird für Carbon-native Plugins verwendet
namespace Carbon.Plugins;

Dies ist der moderere Namespace, der alle Carbon-Features bereitstellt.

In beiden Fällen wird der Plugin-Name als Klassenname verwendet, und alle Hooks sowie Commands werden als Methoden dieser Klasse definiert.

================================================================================
2. PLUGIN-STRUKTUR UND LIFECYCLE
================================================================================

DIE ZWEI PLUGIN-TYPEN

Typ 1: Oxide-kompatibles Plugin

Ein Oxide-kompatibles Plugin erbt von der Klasse RustPlugin und befindet sich im Namespace Oxide.Plugins. Dieses Format ist abwärtskompatibel mit älteren Oxide-Servern und wird von vielen etablierten Plugins verwendet.

Die minimale Struktur eines Oxide-Plugins:

namespace Oxide.Plugins;

[Info("MeinPlugin", "AutorName", "1.0.0")]
[Description("Eine kurze Beschreibung des Plugins")]
public class MeinPlugin : RustPlugin
{
    private void OnServerInitialized()
    {
        Puts("Plugin wurde geladen!");
    }
}

Die Attribute [Info] und [Description] sind optional für die Grundfunktionalität, aber empfohlen zur Dokumentation.

Typ 2: Carbon-Plugin (empfohlen)

Ein Carbon-Plugin erbt von der Klasse CarbonPlugin und befindet sich im Namespace Carbon.Plugins. Dieses Format bietet Zugang zu allen modernen Carbon-Features wie LUI, ClientEntity und erweiterten Command-Features.

Die minimale Struktur eines Carbon-Plugins:

namespace Carbon.Plugins;

[Info("MeinPlugin", "AutorName", "1.0.0")]
[Description("Eine kurze Beschreibung des Plugins")]
public class MeinPlugin : CarbonPlugin
{
    private void OnServerInitialized()
    {
        Puts("Carbon Plugin wurde geladen!");
    }
}

Der Unterschied ist nur die Basisklasse, aber Carbon-Plugins haben Zugriff auf zusätzliche APIs wie CuiHandler für UI-Erstellung und erweiterte Command-Authentifizierung.

PLUGIN-ATTRIBUTE

Das [Info]-Attribut ist das wichtigste Attribut und enthält drei Parameter:

[Info("PluginName", "AutorName", "Versionsnummer")]

- PluginName: Der eindeutige Name des Plugins (keine Leerzeichen)
- AutorName: Der Name des Autors/Entwicklers
- Versionsnummer: Semantische Versionierung (Major.Minor.Patch), z.B. "1.0.0", "2.1.5"

Das [Description]-Attribut ist optional und enthält eine kurze Beschreibung:

[Description("Beschreibung, was das Plugin tut")]

DER PLUGIN-LIFECYCLE

Der Plugin-Lifecycle beschreibt die Abfolge von Ereignissen vom Laden bis zum Entladen des Plugins.

1. Init() - wird aufgerufen, wenn das Plugin erstmals initialisiert wird
   Typischerweise wird hier folgendes gemacht:
   - Permissions registrieren
   - Konfigurationsdatei laden/erstellen
   - Datenbankverbindungen aufbauen
   - Globale Variablen initialisieren

   private void Init()
   {
       Puts("Init wird aufgerufen!");
       permission.RegisterPermission("meinplugin.admin", this);
   }

2. OnServerInitialized() - wird aufgerufen, sobald der Server vollständig geladen ist
   Dies ist der sicherste Punkt, um auf Server-Funktionen zuzugreifen:
   - UI-Elemente initializieren
   - Timer starten
   - Spielerlisten auslesen
   - Server-Wide-Funktionalität aktivieren

   private void OnServerInitialized()
   {
       Puts("Server ist bereit!");
       BasePlayer[] allPlayers = BasePlayer.activePlayerList.ToArray();
   }

3. Hooks werden während der Laufzeit aufgerufen
   Bei jedem Spieler-Event, Server-Event oder Entity-Event werden entsprechende Hooks aufgerufen:
   - OnPlayerConnected
   - OnPlayerDisconnected
   - OnEntitySpawned
   - Und viele weitere

4. Unload() - wird aufgerufen, wenn das Plugin entladen wird
   Dies sollte zum Aufräumen genutzt werden:
   - UI-Elemente entfernen
   - Timer abbrechen
   - Datenbankverbindungen schließen
   - Ressourcen freigeben

   private void Unload()
   {
       Puts("Plugin wird entladen!");
       // Aufräum-Code hier
   }

Der Lifecycle von mehreren Plugins wird sequenziell abgearbeitet. Wenn Sie mehrere Plugins mit zeitintensiven Init-Routinen haben, kann dies den Server-Start verlangsamen.

ZUGRIFF AUF CARBON-INSTANZEN

Innerhalb eines Plugins haben Sie Zugriff auf mehrere wichtige Singletons und APIs:

timer - Das Timer-System von Carbon für zeitgesteuerte Aktionen
permission - Das Permission-System zur Verwaltung von Berechtigungen
logger - Das Logging-System zum Ausgeben von Meldungen
CuiHandler - Zugang zum UI-System (nur in CarbonPlugin)
Community - Globale Carbon-Community-APIs

Diese sind direkt in der Plugin-Klasse verfügbar und benötigen keine Initialisierung.

================================================================================
3. HOOKS UND EVENT-HANDLING
================================================================================

HOOK-KONZEPT

Ein Hook ist eine spezielle Methode, die automatisch von Carbon aufgerufen wird, wenn ein bestimmtes Ereignis eintritt. Hooks werden durch ihre exakte Signatur erkannt - Carbon nutzt Reflection, um Hooks zu entdecken.

Wichtig: Der Hook-Name, die Parameter und der Rückgabetyp müssen exakt mit der Carbon-Definition übereinstimmen.

SERVER-LIFECYCLE-HOOKS

OnServerInitialized()
Wird aufgerufen, sobald der Server vollständig gestartet ist und alle Systeme bereit sind. Dies ist der sicherste Einstiegspunkt für komplexe Initialisierungen.

private void OnServerInitialized()
{
    Puts("Server ist bereit zum Betrieb");
    var playerCount = BasePlayer.activePlayerList.Count;
    Puts($"Aktive Spieler: {playerCount}");
}

OnServerSave()
Wird aufgerufen, wenn der Server eine Speicherung durchführt (standardmäßig alle 10 Minuten, konfigurierbar).

private void OnServerSave()
{
    Puts("Server führt Speicherung durch");
    // Ihre Plugin-Daten speichern
}

OnServerShutdown()
Wird aufgerufen, bevor der Server beendet wird.

private void OnServerShutdown()
{
    Puts("Server wird heruntergefahren");
    // Finale Aufräumarbeiten
}

SPIELER-LIFECYCLE-HOOKS

OnPlayerConnected(BasePlayer player)
Wird aufgerufen, sobald ein Spieler den Server betreten hat. Dies ist früh genug, um Willkommensnachrichten zu senden, aber möglicherweise nicht, um auf alle Entity-Objekte des Spielers zuzugreifen.

private void OnPlayerConnected(BasePlayer player)
{
    if (player == null || !player.IsConnected)
        return;
    
    player.ChatMessage("Willkommen auf unserem Server!");
    Puts($"Spieler verbunden: {player.displayName}");
}

OnPlayerDisconnected(BasePlayer player, string reason)
Wird aufgerufen, wenn ein Spieler den Server verlässt. Der Spieler ist noch teilweise vorhanden, Sie sollten aber schnell operieren, da der Spieler-Datensatz bald gelöscht wird.

private void OnPlayerDisconnected(BasePlayer player, string reason)
{
    if (player == null)
        return;
    
    Puts($"{player.displayName} hat den Server verlassen ({reason})");
}

OnPlayerChat(BasePlayer player, string message)
Wird aufgerufen, wenn ein Spieler im globalen Chat etwas schreibt. Dieser Hook wird auch für Commands aufgerufen, die mit / beginnen.

Rückgabewert: null zum Erlauben, oder eine beliebige andere Nachricht zum Ersetzen der Nachricht. Sie können auch true zurückgeben, um die Nachricht zu blockieren.

private object OnPlayerChat(BasePlayer player, string message)
{
    if (message.Contains("schlechtes Wort"))
    {
        return "Diese Nachricht wurde gefiltert";
    }
    return null;
}

OnPlayerInput(BasePlayer player, InputState input)
Wird aufgerufen, wenn ein Spieler Input-Tasten drückt (WASD, Springen, Schnellschuss, etc.). Dies wird sehr häufig aufgerufen und sollte performant sein.

private void OnPlayerInput(BasePlayer player, InputState input)
{
    if ((input.buttons & IN.FORWARD) != 0)
    {
        // Spieler drückt W
    }
}

OnPlayerDeath(BasePlayer player, HitInfo info)
Wird aufgerufen, wenn ein Spieler stirbt. HitInfo enthält Informationen über den Verursacher und die Waffe.

private void OnPlayerDeath(BasePlayer player, HitInfo info)
{
    if (player == null)
        return;
    
    var attacker = info?.Initiator as BasePlayer;
    if (attacker != null)
    {
        Puts($"{player.displayName} wurde von {attacker.displayName} getötet");
    }
    else
    {
        Puts($"{player.displayName} ist gestorben");
    }
}

ENTITY-LIFECYCLE-HOOKS

OnEntitySpawned(BaseEntity entity)
Wird aufgerufen, wenn eine neue Entity (Spieler, Tier, Building, Item) spawnt. Dieser Hook wird sehr häufig aufgerufen.

private void OnEntitySpawned(BaseEntity entity)
{
    if (entity is BasePlayer)
        return; // Wir behandeln Spieler separat
    
    if (entity is BuildingBlock buildingBlock)
    {
        Puts($"Building Block wurde gespawnt: {buildingBlock.ShortPrefabName}");
    }
}

OnEntityBuilt(Planner planner, GameObject gameObject)
Wird aufgerufen, wenn ein Spieler etwas baut (Wand, Tür, Dach, etc.).

private void OnEntityBuilt(Planner planner, GameObject gameObject)
{
    var player = planner.GetOwnerPlayer();
    if (player != null)
    {
        Puts($"{player.displayName} hat etwas gebaut");
    }
}

OnEntityDeath(BaseCombatEntity entity, HitInfo info)
Wird aufgerufen, wenn eine Entität stirbt (NPC, Tier, Building-Block mit Health).

private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
{
    if (entity is BuildingBlock buildingBlock)
    {
        Puts($"Building Block wurde zerstört: {buildingBlock.ShortPrefabName}");
    }
    else if (entity is BaseNpc npc)
    {
        Puts($"NPC wurde getötet");
    }
}

HOOK-RÜCKGABEWERTE

Manche Hooks haben Rückgabewerte, die das Verhalten beeinflussen:

- null oder void: Das Ereignis wird normal fortgesetzt
- true/false: Das Ereignis wird erlaubt oder blockiert
- Eine andere Nachricht: Das Original-Ergebnis wird durch Ihren Wert ersetzt

Beispiel für Blockieren:

private object OnPlayerChat(BasePlayer player, string message)
{
    if (message.Contains("SPAM"))
    {
        return true; // Blockiert die Nachricht
    }
    return null; // Erlaubt die Nachricht
}

================================================================================
4. COMMAND-SYSTEM DETAILLIERT
================================================================================

ÜBERSICHT DER COMMAND-TYPEN

Carbon bietet vier Haupttypen von Commands, die unterschiedliche Eingabe-Kontexte abdecken:

1. ChatCommand - nur im Spiel-Chat (mit /)
2. ConsoleCommand - F1-Konsole, Server-Terminal, RCon
3. Command (Universal) - überall (Chat, F1, Server, RCon)
4. ProtectedCommand - sicher für UI-Calls mit randomisiertem Namen

CHATCOMMAND - CHAT-BEFEHLE

ChatCommands sind Befehle, die nur im Spiel-Chat eingegeben werden können und mit / beginnen.

Syntax:

[ChatCommand("befehlsname")]
private void BefehslsMethode(BasePlayer player, string command, string[] args)
{
    // Code hier
}

Erklärung der Parameter:
- player: Der Spieler, der den Befehl eingegeben hat (BasePlayer)
- command: Der Name des Befehls (string) - z.B. "befehlsname"
- args: Die Argumente, die der Spieler eingegeben hat (string[])

Wenn ein Spieler /befehlsname arg1 arg2 arg3 eingeben würde, wäre:
- command = "befehlsname"
- args = ["arg1", "arg2", "arg3"]
- args.Length = 3

Beispiel: Ein einfacher Welcome-Befehl

[ChatCommand("welcome")]
private void WelcomeCommand(BasePlayer player, string command, string[] args)
{
    if (player == null || !player.IsConnected)
        return;
    
    player.ChatMessage("Willkommen auf unserem Server!");
    player.ChatMessage("Hier sind die Regeln: ");
    player.ChatMessage("1. Kein Spamming");
    player.ChatMessage("2. Sei respektvoll");
}

Nutzung im Spiel: /welcome

Beispiel: Ein Befehl mit Argument-Verarbeitung

[ChatCommand("kick")]
private void KickCommand(BasePlayer player, string command, string[] args)
{
    if (player == null)
        return;
    
    if (args.Length == 0)
    {
        player.ChatMessage("Verwendung: /kick <spielername>");
        return;
    }
    
    var targetName = string.Join(" ", args);
    var target = BasePlayer.Find(targetName);
    
    if (target == null)
    {
        player.ChatMessage("Spieler nicht gefunden!");
        return;
    }
    
    target.Kick("Du wurdest vom Server gekickt");
}

Nutzung im Spiel: /kick DeinSpielername

CONSOLECOMMAND - KONSOLEN-BEFEHLE

ConsoleCommands sind Befehle, die über mehrere Kontexte aufgerufen werden können:
- F1-Konsole (im Spiel)
- Server-Terminal (außerhalb des Spiels)
- RCon (Remote Console)

Syntax:

[ConsoleCommand("befehlsname")]
private void BefehslsMethode(ConsoleSystem.Arg arg)
{
    // Code hier
}

Der Parameter ist ConsoleSystem.Arg arg, nicht BasePlayer. Dies ist das Befehls-Argument-System und funktioniert anders als ChatCommand.

Die wichtigsten Eigenschaften von ConsoleSystem.Arg:

arg.Player - Der Spieler, falls der Befehl aus F1-Konsole aufgerufen wurde (kann null sein)
arg.Args - Die Argumente (string[])
arg.Args.Length - Die Anzahl der Argumente
arg.ReplyWith(string message) - Antwortet in dem Kontext, aus dem der Befehl kam

Beispiel: Befehl, der angerufen werden kann von überall

[ConsoleCommand("test")]
private void TestCommand(ConsoleSystem.Arg arg)
{
    if (arg.Player != null)
    {
        arg.ReplyWith("Du hast den Befehl aus der F1-Konsole aufgerufen");
    }
    else
    {
        arg.ReplyWith("Du hast den Befehl vom Server-Terminal aufgerufen");
    }
}

Beispiel: Mit Argumenten

[ConsoleCommand("teleport")]
private void TeleportCommand(ConsoleSystem.Arg arg)
{
    if (arg.Args.Length < 3)
    {
        arg.ReplyWith("Verwendung: teleport <x> <y> <z>");
        return;
    }
    
    if (!float.TryParse(arg.Args[0], out float x) ||
        !float.TryParse(arg.Args[1], out float y) ||
        !float.TryParse(arg.Args[2], out float z))
    {
        arg.ReplyWith("Koordinaten müssen Zahlen sein!");
        return;
    }
    
    if (arg.Player != null)
    {
        arg.Player.Teleport(new Vector3(x, y, z));
        arg.ReplyWith("Teleportiert!");
    }
    else
    {
        arg.ReplyWith("Nur Spieler können sich teleportieren");
    }
}

Nutzung:
- Im Spiel (F1): teleport 100 50 200
- Server-Terminal: teleport 100 50 200
- RCon: teleport 100 50 200

COMMAND - UNIVERSELLE BEFEHLE

Universal Commands funktionieren überall (Chat, F1-Konsole, Server, RCon) und kombinieren die Funktionalität von ChatCommand und ConsoleCommand.

Syntax:

[Command("befehlsname")]
private void BefehslsMethode(BasePlayer player, string command, string[] args)
{
    if (player == null)
    {
        // Aufgerufen aus Konsole/Server/RCon
    }
    else
    {
        // Aufgerufen aus Chat/F1-Konsole
    }
}

Der Unterschied ist entscheidend: Der player-Parameter kann null sein. Sie müssen immer prüfen:

if (player == null)
{
    // Von Konsole/RCon aufgerufen
}
else
{
    // Von Spieler aufgerufen
}

Beispiel: Ein Universal Command mit Kontexterkennung

[Command("info")]
private void InfoCommand(BasePlayer player, string command, string[] args)
{
    var info = "Server-Informationen: \n";
    info += $"Spieler Online: {BasePlayer.activePlayerList.Count}\n";
    info += $"Zeit: {DateTime.Now}\n";
    
    if (player == null)
    {
        Puts(info);
    }
    else
    {
        player.ChatMessage(info);
    }
}

Nutzung:
- Im Chat: /info
- In F1-Konsole: info
- Server-Terminal: info
- RCon: info

PROTECTEDCOMMAND - SICHERE BEFEHLE FÜR UI

ProtectedCommands sind Befehle, die eine Server-seitig randomisierte ID haben. Dies verhindert, dass Spieler UI-interne Befehle erraten können, selbst wenn sie den Namen kennen.

Dies ist wichtig für Befehle, die nur über UI aufgerufen werden sollen, nicht manuell vom Spieler.

Syntax:

[ProtectedCommand("befehlsname")]
private void ProtectedMethode(ConsoleSystem.Arg arg)
{
    // Code hier
}

Beispiel: Ein geschützter Befehl

[ProtectedCommand("privatekaufen")]
private void KaufePrivat(ConsoleSystem.Arg arg)
{
    var player = arg.Player;
    if (player == null)
        return;
    
    var itemId = arg.Args.Length > 0 ? arg.Args[0] : "0";
    player.ChatMessage($"Du hast {itemId} gekauft (Protected)!");
}

Um einen ProtectedCommand aufzurufen, müssen Sie die randomisierte ID abrufen:

private void OnServerInitialized()
{
    // ProtectedCommand zu String umwandeln
    var protectedId = Community.Protect("privatekaufen");
    
    // Später: Befehl mit der schützenden ID aufrufen
    ConsoleSystem.Run(ConsoleSystem.Option.Server, protectedId, "5");
}

BEFEHLS-ARGUMENT-VERARBEITUNG DETAILLIERT

Null-Checks

Immer prüfen, ob player oder args null sind:

[ChatCommand("test")]
private void TestCommand(BasePlayer player, string command, string[] args)
{
    if (player == null || !player.IsConnected)
    {
        return;
    }
    
    if (args == null || args.Length == 0)
    {
        player.ChatMessage("Keine Argumente angegeben");
        return;
    }
}

Type-Konvertierung mit TryParse

Verwenden Sie immer TryParse statt Parse:

[ChatCommand("gib")]
private void GibCommand(BasePlayer player, string command, string[] args)
{
    if (args.Length < 2)
    {
        player.ChatMessage("Verwendung: /gib <item> <anzahl>");
        return;
    }
    
    string itemName = args[0];
    string anzahlStr = args[1];
    
    // TRY-PARSE verwenden
    if (!int.TryParse(anzahlStr, out int anzahl))
    {
        player.ChatMessage("Anzahl muss eine Zahl sein!");
        return;
    }
    
    if (anzahl <= 0)
    {
        player.ChatMessage("Anzahl muss > 0 sein!");
        return;
    }
    
    player.ChatMessage($"Du erhältst {anzahl}x {itemName}");
}

Zusammengefügte Argumente

Manchmal benötigen Sie alles nach einem bestimmten Index als eine Nachricht:

[ChatCommand("sagen")]
private void SayCommand(BasePlayer player, string command, string[] args)
{
    if (args.Length == 0)
    {
        return;
    }
    
    // Alle Argumente zusammenführen
    string message = string.Join(" ", args);
    
    Puts($"{player.displayName} sagt: {message}");
}

Nutzung: /sagen Hallo ich bin ein Spieler
Ergebnis: args = ["Hallo", "ich", "bin", "ein", "Spieler"], message = "Hallo ich bin ein Spieler"

================================================================================
5. COMMAND-AUTHENTIFIZIERUNG UND SICHERHEIT
================================================================================

ÜBERBLICK

Carbon bietet mehrere Authentifizierungs-Attribute, die Sie auf Commands anwenden können. Sie können alle Attribute kombinieren, um mehrschichtige Sicherheit zu erreichen.

Die Attribute sind:
- Permission: Erfordert eine bestimmte Permission
- Group: Erfordert Zugehörigkeit zu einer Gruppe
- AuthLevel: Erfordert einen bestimmten AuthLevel (Owner, Moderator, etc.)
- Cooldown: Begrenzt wie oft ein Spieler einen Command nutzen kann

PERMISSION-ATTRIBUT

Das Permission-Attribut erfordert, dass der Spieler (oder eine seiner Gruppen) eine bestimmte Permission besitzt.

Syntax:

[ChatCommand("admin"), Permission("meinplugin.admin")]
private void AdminCommand(BasePlayer player, string command, string[] args)
{
    player.ChatMessage("Du bist Admin!");
}

Wichtig: Sie müssen die Permission zuvor registrieren (in Init() oder OnServerInitialized()):

private void Init()
{
    permission.RegisterPermission("meinplugin.admin", this);
}

Beispiel mit Beschreibung:

[ChatCommand("vip"), Permission("server.vip")]
private void VipCommand(BasePlayer player, string command, string[] args)
{
    player.ChatMessage("Du hast VIP-Status!");
    player.ChatMessage("Hier sind deine VIP-Vorteile...");
}

Der Spieler benötigt entweder die Permission direkt oder eine Gruppe, die diese Permission hat.

GROUP-ATTRIBUT

Das Group-Attribut erfordert, dass der Spieler in einer der angegebenen Gruppen ist. Sie können mehrere Group-Attribute verwenden:

[ChatCommand("staff"), Group("admin"), Group("moderator")]
private void StaffCommand(BasePlayer player, string command, string[] args)
{
    player.ChatMessage("Du bist in einer Staff-Gruppe!");
}

Die Gruppen-Namen müssen mit der Gruppen-Verwaltung erstellt werden (siehe Abschnitt Permissions und Gruppen).

AUTHLEVEL-ATTRIBUT

AuthLevel ist ein numerischer Wert, der die Rolle des Spielers angibt. Es gibt vier Standard-Levels:

0 = Normaler Spieler
1 = Moderator
2 = Owner
3 = Developer

Das Attribut erfordert mindestens diesen Level. Ein Owner kann z.B. alle Commands ausführen, die Level 0 erfordern.

[ConsoleCommand("kick"), AuthLevel(1)]
private void KickCommand(ConsoleSystem.Arg arg)
{
    if (arg.Player == null)
    {
        arg.ReplyWith("Nur Spieler können mit AuthLevel arbeiten");
        return;
    }
    
    // Spieler mit AuthLevel >= 1 können hier rein
    arg.ReplyWith("Du hast die Berechtigung zum Kicken!");
}

Beispiel: Developer-Only Command

[ConsoleCommand("dev"), AuthLevel(3)]
private void DevCommand(ConsoleSystem.Arg arg)
{
    arg.ReplyWith("Du bist ein Developer!");
}

COOLDOWN-ATTRIBUT

Das Cooldown-Attribut begrenzt, wie oft ein Spieler einen Command verwenden kann. Der Cooldown wird in Millisekunden angegeben und ist pro Spieler.

[ChatCommand("teuer"), Cooldown(10000)]
private void TeuerCommand(BasePlayer player, string command, string[] args)
{
    player.ChatMessage("Dieser Befehl verbraucht Ressourcen!");
}

Der Cooldown ist 10000 Millisekunden = 10 Sekunden. Jeder Spieler hat seinen eigenen Cooldown-Timer.

Beispiel: Kürzerer Cooldown für häufig genutzte Commands

[ChatCommand("hilfe"), Cooldown(2000)]
private void HilfeCommand(BasePlayer player, string command, string[] args)
{
    // Relativ häufig nutzbar
}

Beispiel: Längerer Cooldown für teure Operations

[ChatCommand("teleport"), Cooldown(60000)]
private void TeleportCommand(BasePlayer player, string command, string[] args)
{
    // Nur einmal pro Minute nutzbar
}

KOMBINIERTE AUTHENTIFIZIERUNG

Sie können alle Attribute kombinieren, um mehrschichtige Sicherheit zu erreichen:

[ChatCommand("powerful"),
 Permission("meinplugin.powerful"),
 AuthLevel(2),
 Cooldown(30000)]
private void PowerfulCommand(BasePlayer player, string command, string[] args)
{
    player.ChatMessage("Du erfüllst alle Sicherheitsanforderungen!");
}

Dies erfordert:
1. Die Permission "meinplugin.powerful" zu haben ODER
2. In einer Gruppe zu sein, die diese Permission hat UND
3. AuthLevel >= 2 zu haben (Owner oder höher) UND
4. Den Befehl nicht öfter als alle 30 Sekunden zu nutzen

FEHLERBEHANDLUNG BEI AUTHENTIFIZIERUNG

Carbon behandelt Authentifizierungs-Fehler automatisch. Wenn ein Spieler die Anforderungen nicht erfüllt, erhält er automatisch eine Fehlermeldung und der Befehl wird nicht ausgeführt.

Sie müssen sich nicht selbst um die Fehlerbehandlung kümmern. Carbon prüft die Attribute BEVOR ihre Methode aufgerufen wird.

Wenn Sie aber zusätzliche manuelle Checks möchten:

[ChatCommand("admin")]
private void AdminCommand(BasePlayer player, string command, string[] args)
{
    if (!permission.UserHasPermission(player.UserIDString, "admin.access"))
    {
        player.ChatMessage("Du hast keine Berechtigung!");
        return;
    }
    
    // Zusätzliche Checks...
}

================================================================================
6. PERMISSIONS UND GRUPPEN-VERWALTUNG
================================================================================

PERMISSIONS-KONZEPT

Das Permission-System von Carbon (geerbt von Oxide) ist ein hierarchisches System zur Verwaltung von Benutzer-Berechtigungen. Permissions sind eindeutige Zeichenketten mit Punkt-Notation.

Naming Convention für Permissions:

meinplugin.admin
meinplugin.use
meinplugin.vip
meinplugin.advanced

Format: [pluginname].[feature].[level]

PERMISSIONS REGISTRIEREN

Permissions müssen in der Init() oder OnServerInitialized() Methode registriert werden. Das Plugin wird als Besitzer registriert (das 'this' Argument).

private void Init()
{
    permission.RegisterPermission("meinplugin.admin", this);
    permission.RegisterPermission("meinplugin.use", this);
    permission.RegisterPermission("meinplugin.vip", this);
    permission.RegisterPermission("meinplugin.advanced", this);
    
    Puts("Permissions registriert!");
}

Nach der Registrierung können diese Permissions in Commands verwendet werden:

[ChatCommand("admin"), Permission("meinplugin.admin")]
private void AdminCommand(BasePlayer player, string command, string[] args)
{
    // Code...
}

PERMISSIONS PRÜFEN

Sie können Permissions manuell prüfen:

private void CheckPermission(BasePlayer player, string permission)
{
    if (player == null)
        return;
    
    bool hasPermission = permission.UserHasPermission(player.UserIDString, permission);
    
    if (hasPermission)
    {
        player.ChatMessage("Du hast diese Permission!");
    }
    else
    {
        player.ChatMessage("Du hast diese Permission nicht!");
    }
}

Beispiel in einem Hook:

private void OnPlayerConnected(BasePlayer player)
{
    if (permission.UserHasPermission(player.UserIDString, "vip.status"))
    {
        player.ChatMessage("Willkommen, VIP!");
    }
    else
    {
        player.ChatMessage("Willkommen, Spieler!");
    }
}

GRUPPEN-VERWALTUNG

Gruppen sind Sammlungen von Permissions, die an Spieler vergeben werden können. Statt jedem Spieler einzelne Permissions zu geben, können Sie sie einer Gruppe hinzufügen.

Gruppe erstellen

permission.CreateGroup(groupName, displayName, rank) gibt true zurück, wenn erfolgreich, false wenn Gruppe bereits existiert.

private void CreateGroups()
{
    // Admin-Gruppe
    if (permission.CreateGroup("admin", "Administrator", 2))
    {
        Puts("Admin-Gruppe erstellt");
        permission.GrantGroupPermission("admin", "meinplugin.admin", this);
        permission.GrantGroupPermission("admin", "meinplugin.use", this);
    }
    
    // VIP-Gruppe
    if (permission.CreateGroup("vip", "VIP-Spieler", 1))
    {
        Puts("VIP-Gruppe erstellt");
        permission.GrantGroupPermission("vip", "meinplugin.vip", this);
        permission.GrantGroupPermission("vip", "meinplugin.use", this);
    }
    
    // Standard-Spieler-Gruppe
    if (permission.CreateGroup("player", "Standard-Spieler", 0))
    {
        Puts("Spieler-Gruppe erstellt");
        permission.GrantGroupPermission("player", "meinplugin.use", this);
    }
}

Gruppe löschen

permission.RemoveGroup(groupName) gibt true zurück, wenn erfolgreich, false wenn Gruppe nicht existiert.

private void DeleteGroup()
{
    if (permission.RemoveGroup("admin"))
    {
        Puts("Admin-Gruppe gelöscht");
    }
    else
    {
        Puts("Admin-Gruppe existiert nicht");
    }
}

Alle Gruppen abrufen

permission.GetGroups() gibt ein string[] aller Gruppennamen zurück.

private void ListAllGroups()
{
    var groups = permission.GetGroups();
    
    Puts("Alle Gruppen:");
    foreach (var group in groups)
    {
        Puts($"  - {group}");
    }
}

GRUPPE-PERMISSIONS VERWALTEN

Berechtigung zur Gruppe hinzufügen

permission.GrantGroupPermission(groupName, permissionName, plugin) gibt true zurück, wenn erfolgreich, false wenn Gruppe/Permission nicht existiert.

private void GrantPermission()
{
    permission.GrantGroupPermission("admin", "meinplugin.advanced", this);
    Puts("Permission 'meinplugin.advanced' zur Admin-Gruppe hinzugefügt");
}

Berechtigung aus Gruppe entfernen

permission.RevokeGroupPermission(groupName, permissionName) gibt true zurück, wenn erfolgreich, false wenn nicht.

private void RevokePermission()
{
    permission.RevokeGroupPermission("vip", "meinplugin.admin");
    Puts("Permission 'meinplugin.admin' aus VIP-Gruppe entfernt");
}

Alle Permissions einer Gruppe abrufen

permission.GetGroupPermissions(groupName, parents) gibt ein string[] aller Permissions zurück. Der parents-Parameter steuert, ob Eltern-Gruppen-Permissions auch mitgezählt werden.

private void ListGroupPermissions()
{
    var adminPerms = permission.GetGroupPermissions("admin", includeParents: false);
    
    Puts("Admin-Permissions:");
    foreach (var perm in adminPerms)
    {
        Puts($"  - {perm}");
    }
}

GRUPPE-INFORMATIONEN

Rang abrufen

permission.GetGroupRank(groupName) gibt den numerischen Rang der Gruppe zurück.

var rank = permission.GetGroupRank("admin");
Puts($"Admin-Rang: {rank}");

Anzeigename abrufen

permission.GetGroupTitle(groupName) gibt den Anzeigenamen zurück.

var title = permission.GetGroupTitle("vip");
Puts($"VIP-Anzeigename: {title}");

Parent-Gruppe abrufen

permission.GetGroupParent(groupName) gibt den Namen der Eltern-Gruppe zurück (oder leer, wenn keine).

var parent = permission.GetGroupParent("vip");
Puts($"Parent-Gruppe: {parent}");

SPIELER ZU GRUPPE HINZUFÜGEN/ENTFERNEN

Die meisten Systeme verwenden externe Tools oder Datenbanken, um Spieler zu Gruppen zuzuordnen. Dies ist nicht direkt über Carbon möglich, aber Sie können es über Ihre eigenen Datenstrukturen tun.

Beispiel mit Wörterbuch:

private Dictionary<ulong, string> playerGroups = new Dictionary<ulong, string>();

private void AddPlayerToGroup(BasePlayer player, string groupName)
{
    playerGroups[player.userID] = groupName;
    Puts($"{player.displayName} wurde zur {groupName} Gruppe hinzugefügt");
}

private bool IsPlayerInGroup(BasePlayer player, string groupName)
{
    return playerGroups.ContainsKey(player.userID) && 
           playerGroups[player.userID] == groupName;
}

PRAKTISCHES BEISPIEL: KOMPLETTES PERMISSIONS-SYSTEM

private void Init()
{
    // Permissions registrieren
    permission.RegisterPermission("mystuff.admin", this);
    permission.RegisterPermission("mystuff.use", this);
    permission.RegisterPermission("mystuff.vip", this);
}

private void OnServerInitialized()
{
    // Gruppen erstellen
    if (permission.CreateGroup("admin", "Server Admin", 2))
    {
        permission.GrantGroupPermission("admin", "mystuff.admin", this);
        permission.GrantGroupPermission("admin", "mystuff.use", this);
    }
    
    if (permission.CreateGroup("vip", "VIP Spieler", 1))
    {
        permission.GrantGroupPermission("vip", "mystuff.vip", this);
        permission.GrantGroupPermission("vip", "mystuff.use", this);
    }
}

[ChatCommand("status"), Permission("mystuff.use")]
private void StatusCommand(BasePlayer player, string command, string[] args)
{
    player.ChatMessage("Du hast die 'use' Permission!");
    
    if (permission.UserHasPermission(player.UserIDString, "mystuff.admin"))
    {
        player.ChatMessage("Du bist auch Admin!");
    }
}

================================================================================
7. TIMER UND FRAME-BASIERTE PLANUNG
================================================================================

TIMER-SYSTEM OVERVIEW

Das Timer-System in Carbon ermöglicht zeitgesteuerte Code-Ausführung. Anstatt Threads zu verwenden (was problematisch ist), nutzen Sie Timers, die von Carbon verwaltet werden und im Main Game Loop laufen.

Es gibt mehrere Timer-Varianten für unterschiedliche Anforderungen.

ONE-SHOT TIMER - EINMALIGE AUSFÜHRUNG

timer.In(float seconds, Action callback) oder timer.Once(float seconds, Action callback)

In und Once sind identisch. Beide führen den Code nach der angegebenen Zeit einmal aus.

timer.In(5f, () =>
{
    Puts("Das wird nach 5 Sekunden ausgeführt");
});

timer.Once(10f, () =>
{
    Puts("Das wird nach 10 Sekunden ausgeführt");
});

Praktisches Beispiel: Verzögerte Willkommensnachricht

[ChatCommand("willkommen")]
private void WillkommenCommand(BasePlayer player, string command, string[] args)
{
    player.ChatMessage("Du erhältst in 3 Sekunden weitere Informationen...");
    
    timer.In(3f, () =>
    {
        if (player != null && player.IsConnected)
        {
            player.ChatMessage("Hier sind die Server-Regeln!");
            player.ChatMessage("1. Kein Spamming");
            player.ChatMessage("2. Sei respektvoll");
        }
    });
}

EVERY - ENDLOSE WIEDERHOLUNG

timer.Every(float seconds, Action callback) führt den Code alle X Sekunden für immer aus. Dies wird typischerweise in OnServerInitialized() gestartet.

timer.Every(300f, () =>
{
    Puts("Das wird alle 5 Minuten (300 Sekunden) ausgeführt");
});

Praktisches Beispiel: Periodische Server-Ankündigung

private void OnServerInitialized()
{
    // Alle 10 Minuten eine Ankündigung
    timer.Every(600f, () =>
    {
        foreach (var player in BasePlayer.activePlayerList)
        {
            player.ChatMessage("[Server] Erinnere dich an die Regeln!");
        }
    });
}

REPEAT - BEGRENZTE WIEDERHOLUNG

timer.Repeat(float seconds, int count, Action callback) führt den Code count-mal alle seconds Sekunden aus.

timer.Repeat(10f, 5, () =>
{
    Puts("Das wird 5 Mal alle 10 Sekunden ausgeführt");
});

Nach 5 Ausführungen stoppt der Timer automatisch.

Praktisches Beispiel: Countdown

[ChatCommand("countdown")]
private void CountdownCommand(BasePlayer player, string command, string[] args)
{
    var remaining = 5;
    
    var countdownTimer = timer.Repeat(1f, remaining, () =>
    {
        if (player != null && player.IsConnected)
        {
            player.ChatMessage($"Countdown: {remaining}");
            remaining--;
        }
    });
}

TIMER ABBRECHEN

Alle Timer-Varianten geben ein Timer-Objekt zurück, das Sie speichern können. Mit .Destroy() brechen Sie den Timer ab.

var myTimer = timer.Every(10f, () =>
{
    Puts("Tickt...");
});

// Später: Timer stoppen
myTimer.Destroy();

Beispiel: Timer mit Bedingung abbrechen

private Timer versionCheckTimer;

private void OnServerInitialized()
{
    var checkCount = 0;
    
    versionCheckTimer = timer.Every(30f, () =>
    {
        checkCount++;
        Puts($"Version Check {checkCount}");
        
        if (checkCount >= 10)
        {
            Puts("Maximale Checks erreicht, Timer stoppt");
            versionCheckTimer.Destroy();
        }
    });
}

Auch im Unload sollten Sie Timers aufräumen:

private void Unload()
{
    if (versionCheckTimer != null)
    {
        versionCheckTimer.Destroy();
    }
}

NEXTFRAME / NEXTTICK - FRAME-BASIERTE PLANUNG

NextFrame() und NextTick() sind identisch (für Oxide-Kompatibilität) und führen Code im nächsten Game Frame aus.

Puts("Frame 1");

NextFrame(() =>
{
    Puts("Frame 2");
});

Puts("Frame 3");

Die Ausgabe wird sein:
Frame 1
Frame 3
Frame 2

Dies ist nützlich, wenn Sie Code in der korrekten Reihenfolge ausführen müssen, nachdem Entity-Spawning oder ähnliches abgeschlossen ist.

Beispiel: Entity nach korrektem Spawn zugreifen

private void OnEntitySpawned(BaseEntity entity)
{
    if (entity.ShortPrefabName == "loot-barrel-2")
    {
        NextFrame(() =>
        {
            // Entity ist jetzt vollständig gespawnt und initialisiert
            if (entity != null && !entity.IsDestroyed)
            {
                Puts($"Entity ist bereit: {entity.transform.position}");
            }
        });
    }
}

PRAKTISCHE TIMER-BEISPIELE

Beispiel 1: Spieler-Aktivitäts-Tracker

private Dictionary<ulong, float> lastActivity = new Dictionary<ulong, float>();

private void OnPlayerConnected(BasePlayer player)
{
    lastActivity[player.userID] = Time.realtimeSinceStartup;
}

private void OnServerInitialized()
{
    // Alle 60 Sekunden inaktive Spieler prüfen
    timer.Every(60f, () =>
    {
        var currentTime = Time.realtimeSinceStartup;
        var inactiveDuration = 300f; // 5 Minuten
        
        var toRemove = new List<ulong>();
        
        foreach (var kvp in lastActivity)
        {
            if (currentTime - kvp.Value > inactiveDuration)
            {
                var player = BasePlayer.FindByID(kvp.Key);
                if (player != null)
                {
                    Puts($"{player.displayName} ist länger als 5 Minuten inaktiv");
                }
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var userId in toRemove)
        {
            lastActivity.Remove(userId);
        }
    });
}

Beispiel 2: Selbstzerstörendes Item

[ChatCommand("bombe")]
private void BombeCommand(BasePlayer player, string command, string[] args)
{
    var bombItem = ItemManager.Create(ItemManager.FindItemDefinition("c4"), 1);
    
    player.ChatMessage("Bombe gesetzt! Sie explodiert in 30 Sekunden");
    
    timer.In(30f, () =>
    {
        if (bombItem != null)
        {
            Puts("BOOM!");
            bombItem.Remove();
        }
    });
}

Beispiel 3: Fortschritts-Anzeige mit Repeat

[ChatCommand("laden")]
private void LadenCommand(BasePlayer player, string command, string[] args)
{
    var progress = 0;
    var maxProgress = 10;
    
    player.ChatMessage("Lädt...");
    
    timer.Repeat(1f, maxProgress, () =>
    {
        progress++;
        var percent = (progress / (float)maxProgress) * 100;
        player.ChatMessage($"Fortschritt: {percent:F0}%");
        
        if (progress >= maxProgress)
        {
            player.ChatMessage("Fertig!");
        }
    });
}

================================================================================
8. LIGHTWEIGHT UI (LUI) - KOMPLETTES SYSTEM
================================================================================

WAS IST LUI?

LUI (Lightweight UI) ist Carbons modernes UI-System zur Erstellung von CUI-Elementen (Client User Interface). Es bietet eine flüssige, objekt-orientierte API zum Konstruieren und Verwalten von UI-Elementen.

LUI ist einfacher als rohes CUI zu schreiben und hat bessere Performance als viele alternative UI-Systeme.

GRUNDKONZEPT

Die Verwendung von LUI erfolgt in drei Schritten:
1. Instanz erstellen (using CUI)
2. Elemente hinzufügen und konfigurieren
3. An Spieler senden (SendUi)

[ChatCommand("ui")]
private void UICommand(BasePlayer player, string command, string[] args)
{
    using CUI cui = new CUI(CuiHandler);
    
    // Elemente erstellen
    var mainPanel = cui.v2.CreatePanel(
        "Hud.Root",
        LuiPosition.Full,
        new LuiOffset(50, 50, 450, 300),
        "0.1 0.1 0.1 0.8"
    );
    
    // Text hinzufügen
    cui.v2.CreateText(
        mainPanel,
        LuiPosition.Full,
        LuiOffset.Zero,
        16,
        "1 1 1 1",
        "Hallo!"
    );
    
    // An Spieler senden
    cui.v2.SendUi(player);
}

PARENT PANELS

Parent Panels sind vordefinierte Root-Elemente, die im Spiel existieren. Sie hängen verschiedene UI-Elemente an:

CUI.ClientPanels.HudRoot - Hauptbereich (Standard für meiste UIs)
CUI.ClientPanels.Overlay - Oberste Schicht
CUI.ClientPanels.Under - Unter der Standard-UI
CUI.ClientPanels.CenterGuide - Zentriert

CreateParent erstellt einen Container, der an einen dieser Roots gehängt wird:

var mainPanel = cui.v2.CreateParent(
    CUI.ClientPanels.HudRoot,
    LuiPosition.Full,
    "mein_hauptpanel"
);

POSITIONEN UND OFFSETS

Positionen bestimmen, wie ein Element angeankert wird:

LuiPosition.Full - Vollständig füllen
LuiPosition.None - Nicht positioniert (Sie müssen Offset setzen)
LuiPosition.TopLeft - Oben links
LuiPosition.TopCenter - Oben Mitte
LuiPosition.TopRight - Oben rechts
LuiPosition.MiddleLeft - Mitte links
LuiPosition.Center - Zentriert
LuiPosition.MiddleRight - Mitte rechts
LuiPosition.BottomLeft - Unten links
LuiPosition.BottomCenter - Unten Mitte
LuiPosition.BottomRight - Unten rechts

Offsets definieren exakte Positionen und Größen in Pixel:

new LuiOffset(left, bottom, right, top)

Beispiel: Element bei Position (100, 200) mit Größe (300x400):

new LuiOffset(100, 200, 400, 600)

Dies platziert das Element 100 Pixel von links, 200 Pixel von unten, und dehnt es bis 400 von links und 600 von unten.

FARBEN UND ALPHA

Farben in LUI sind im Format "R G B A", wobei jeder Wert von 0 bis 1 liegt:

"1 1 1 1" = Weiß (undurchsichtig)
"0 0 0 0" = Schwarz (transparent)
"1 0 0 1" = Rot (undurchsichtig)
"0 1 0 0.5" = Grün (halbdurchsichtig)
"0 0 1 0" = Blau (unsichtbar, aber noch da)

PANELS ERSTELLEN

Ein Panel ist ein einfarbiger rechteckiger Container:

cui.v2.CreatePanel(
    "parent_name",
    LuiPosition.Full,
    new LuiOffset(50, 50, 450, 300),
    "0.2 0.2 0.2 0.9"
);

Parameter:
- parent_name: Der Parent-Container (string)
- Position: LuiPosition enum
- Offset: Exakte Position und Größe
- Farbe: "R G B A"

Helfer-Methoden für Panels:

.SetColor("1 0 0 1") // Farbe ändern
.SetMaterial("mat") // Material ändern
.SetOutline("1 1 1 1", new Vector2(1, 1)) // Outline hinzufügen

TEXT ERSTELLEN

Text-Elemente zeigen Text an:

cui.v2.CreateText(
    "parent_panel",
    LuiPosition.Full,
    LuiOffset.Zero,
    14,
    "1 1 1 1",
    "Hallo Welt!",
    TextAnchor.MiddleCenter
);

Parameter:
- Parent: Der Container
- Position: LuiPosition
- Offset: Exakte Position
- FontSize: Schriftgröße (14 ist Standard)
- Color: "R G B A"
- Text: Der anzuzeigende Text
- Alignment: TextAnchor (MiddleCenter ist Standard)

Text-Aliase für verschiedene Position/Offset-Kombinationen:

// Standard
CreateText(string parent, LuiPosition position, LuiOffset offset, ...)

// Ohne Position (wenn Position None ist)
CreateText(string parent, LuiOffset offset, ...)

// Mit LuiContainer statt Parent-Name
CreateText(LuiContainer container, LuiPosition position, LuiOffset offset, ...)

// Mit LuiContainer ohne Position
CreateText(LuiContainer container, LuiOffset offset, ...)

Text-Helfer-Methoden:

.SetTextFont(CUI.Handler.FontTypes.RobotoCondensedRegular)
.SetTextColor("1 0 0 1")
.SetTextAlign(TextAnchor.TopLeft)
.SetTextOverflow(VerticalWrapMode.Overflow)

Beispiel: Dynamischer Text mit Helfer

var textElement = cui.v2.CreateText(
    mainPanel,
    LuiPosition.Full,
    new LuiOffset(10, 10, 290, 60),
    12,
    "1 1 1 1",
    "Standard Text"
);

textElement.SetTextFont(CUI.Handler.FontTypes.RobotoCondensedBold);
textElement.SetTextAlign(TextAnchor.TopLeft);

BUTTONS ERSTELLEN

Buttons sind anklickbar und führen Commands aus:

cui.v2.CreateButton(
    "parent_panel",
    LuiPosition.Full,
    new LuiOffset(10, 10, 190, 50),
    "mycommand",
    "0.2 0.8 0.2 1",
    true
);

Parameter:
- Parent: Der Container
- Position: LuiPosition
- Offset: Exakte Position
- Command: Der Command, der ausgeführt wird
- Farbe: "R G B A"
- isProtected: true für Protected Commands, false für normale

Button-Helfer-Methoden:

.SetButton("neuer_command", "1 0 0 1") // Command und Farbe ändern
.SetButtonColors(...) // Multiple Farben für verschiedene Zustände
.SetButtonSprite("sprite_name") // Sprite für Button setzen

Wichtig: Protected Commands sind sicherer und sollten für UI-Buttons immer verwendet werden.

Praktisches Button-Beispiel:

[ChatCommand("butmenu")]
private void ButtonMenuCommand(BasePlayer player, string command, string[] args)
{
    using CUI cui = new CUI(CuiHandler);
    
    var mainPanel = cui.v2.CreatePanel(
        "Hud.Root",
        LuiPosition.Full,
        new LuiOffset(100, 100, 400, 250),
        "0.1 0.1 0.1 0.9"
    );
    
    // Button 1
    cui.v2.CreateButton(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(10, 100, 190, 140),
        "ui_action_1",
        "0.2 0.8 0.2 1",
        true
    );
    
    cui.v2.CreateText(
        "ui_action_1_button",
        LuiPosition.Full,
        LuiOffset.Zero,
        14,
        "1 1 1 1",
        "Aktion 1"
    );
    
    // Button 2
    cui.v2.CreateButton(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(210, 100, 390, 140),
        "ui_action_2",
        "0.8 0.2 0.2 1",
        true
    );
    
    cui.v2.CreateText(
        "ui_action_2_button",
        LuiPosition.Full,
        LuiOffset.Zero,
        14,
        "1 1 1 1",
        "Aktion 2"
    );
    
    cui.v2.SendUi(player);
}

[ProtectedCommand("ui_action_1")]
private void OnAction1(ConsoleSystem.Arg arg)
{
    if (arg.Player != null)
    {
        arg.Player.ChatMessage("Aktion 1 ausgeführt!");
    }
}

[ProtectedCommand("ui_action_2")]
private void OnAction2(ConsoleSystem.Arg arg)
{
    if (arg.Player != null)
    {
        arg.Player.ChatMessage("Aktion 2 ausgeführt!");
    }
}

INPUT-FELDER ERSTELLEN

Input-Felder erlauben Spielern, Text einzugeben:

cui.v2.CreateInput(
    "parent_panel",
    LuiPosition.Full,
    new LuiOffset(10, 10, 290, 50),
    "1 1 1 0.1",
    "Gib etwas ein...",
    14,
    "input_submitted",
    100,
    true,
    CUI.Handler.FontTypes.RobotoCondensedRegular,
    TextAnchor.MiddleLeft
);

Parameter:
- Parent: Der Container
- Position: LuiPosition
- Offset: Exakte Position
- Color: "R G B A" (Hintergrund)
- Text: Placeholder-Text
- FontSize: Schriftgröße
- Command: Wird aufgerufen, wenn Spieler mit Enter bestätigt
- CharLimit: Max Zeichen (0 = unbegrenzt)
- isProtected: true für Protected Commands
- Font: Schriftart
- Alignment: TextAnchor

Input-Helfer-Methoden:

.SetInputPassword(true) // Text verstecken (wie Passwort)
.SetInputReadOnly(true) // Nur Lesen
.SetInputAutoFocus(true) // Fokus automatisch aktivieren
.SetInputKeyboard(true) // Tastatur anzeigen
.SetInputLineType(InputField.LineType.SingleLine) // Nur eine Zeile

Beispiel: Namens-Input

[ChatCommand("name")]
private void NameCommand(BasePlayer player, string command, string[] args)
{
    using CUI cui = new CUI(CuiHandler);
    
    var mainPanel = cui.v2.CreatePanel(
        "Hud.Root",
        LuiPosition.Full,
        new LuiOffset(200, 150, 600, 250),
        "0.1 0.1 0.1 0.9"
    );
    
    cui.v2.CreateInput(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(10, 10, 390, 50),
        "1 1 1 0.2",
        "Gib deinen Namen ein",
        14,
        "name_submitted",
        50,
        true
    );
    
    cui.v2.CreateText(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(10, 60, 390, 90),
        12,
        "0.8 0.8 0.8 1",
        "Gib einen Namen ein und drücke Enter"
    );
    
    cui.v2.SendUi(player);
}

[ProtectedCommand("name_submitted")]
private void OnNameSubmitted(ConsoleSystem.Arg arg)
{
    if (arg.Player != null && arg.Args.Length > 0)
    {
        var name = arg.Args[0];
        arg.Player.ChatMessage($"Dein Name ist jetzt: {name}");
    }
}

BILDER UND SPRITES

Sprites (von Rust):

cui.v2.CreateSprite(
    "parent_panel",
    LuiPosition.Full,
    new LuiOffset(10, 10, 100, 100),
    "assets/content/ui/ui.background.tile.psd",
    "1 1 1 1"
);

Item-Icons:

cui.v2.CreateItemIcon(
    "parent_panel",
    LuiPosition.Full,
    new LuiOffset(10, 10, 60, 60),
    "rifle.ak",
    0
);

URL-Bilder (von Webserver):

cui.v2.CreateUrlImage(
    "parent_panel",
    LuiPosition.Full,
    new LuiOffset(10, 10, 100, 100),
    "https://example.com/image.png"
);

Steam-Avatar:

cui.v2.CreateEmptyContainer(mainPanel)
    .SetOffset(new LuiOffset(10, 10, 100, 100))
    .SetSteamIcon(76561198000000000);

COUNTDOWNS

Countdowns zeigen die verbleibende Zeit bis zu einem Event:

var startTime = Time.realtimeSinceStartup;
var endTime = startTime + 60f;

cui.v2.CreateCountdown(
    mainPanel,
    LuiPosition.Full,
    new LuiOffset(10, 10, 290, 60),
    16,
    "1 1 1 1",
    "Verbleibende Zeit: %TIME_LEFT%",
    TextAnchor.MiddleCenter,
    startTime,
    endTime,
    1f,
    1f
);

Das Countdown-Element ersetzt %TIME_LEFT% automatisch mit der formatierten Zeit.

SCROLL-VIEWS

Scroll-Views für lange Listen:

var scroll = cui.v2.CreateScrollView(
    mainPanel,
    LuiPosition.Full,
    new LuiOffset(5, 5, 295, 295),
    true,
    false
);

scroll.SetScrollContent(
    LuiPosition.Full,
    new LuiOffset(0, 0, 0, 2000)
);

// Inhalte hinzufügen
for (int i = 0; i < 50; i++)
{
    cui.v2.CreateText(
        scroll,
        LuiPosition.Full,
        new LuiOffset(10, 40 * i, 280, 40 * (i + 1)),
        12,
        "1 1 1 1",
        $"Zeile {i + 1}"
    );
}

UI AKTUALISIEREN

Sie können Teile der UI nach dem Senden aktualisieren:

Einfache Updates:

cui.v2.UpdatePosition("element_name", new LuiPosition(0, 0), new LuiOffset(100, 100, 300, 200));
cui.v2.UpdateColor("element_name", "1 0 0 1");
cui.v2.UpdateText("element_name", "Neuer Text", 16, "1 1 1 1");
cui.v2.UpdateButtonCommand("element_name", "new_command");

Komplexere Updates:

cui.v2.Update("element_name")
    .SetTextFont(CUI.Handler.FontTypes.RobotoBold)
    .SetColor("0 0 1 1");

UI LÖSCHEN

Um UI vom Spieler zu entfernen:

cui.v2.SetDestroy("element_name");

LEERE CONTAINER

Leere Container für Gruppierung:

var container = cui.v2.CreateEmptyContainer(
    mainPanel,
    "mein_container"
);

container.SetOffset(new LuiOffset(10, 10, 290, 290));

GENERIERTE NAMEN

Carbon kann automatisch eindeutige Namen für Elemente generieren, wenn Sie keinen Namen angeben. Dies ist standardmäßig aktiviert.

Um es zu deaktivieren (und alle Namen manuell zu setzen):

cui.v2.generateNames = false;

PRAKTISCHES KOMPLETTES BEISPIEL

[ChatCommand("shop")]
private void ShopCommand(BasePlayer player, string command, string[] args)
{
    using CUI cui = new CUI(CuiHandler);
    
    // Hauptpanel
    var mainPanel = cui.v2.CreatePanel(
        "Hud.Root",
        LuiPosition.Full,
        new LuiOffset(100, 100, 600, 500),
        "0.1 0.1 0.1 0.95"
    );
    
    // Titel
    cui.v2.CreateText(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(10, 350, 490, 380),
        18,
        "1 1 1 1",
        "SHOP",
        TextAnchor.MiddleCenter
    ).SetTextFont(CUI.Handler.FontTypes.RobotoBold);
    
    // Item 1
    cui.v2.CreateButton(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(10, 280, 230, 330),
        "buy_item_1",
        "0.2 0.8 0.2 1",
        true
    );
    
    cui.v2.CreateText(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(10, 280, 230, 330),
        14,
        "1 1 1 1",
        "AK-47 - 1000$"
    );
    
    // Item 2
    cui.v2.CreateButton(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(250, 280, 470, 330),
        "buy_item_2",
        "0.2 0.8 0.2 1",
        true
    );
    
    cui.v2.CreateText(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(250, 280, 470, 330),
        14,
        "1 1 1 1",
        "MP5 - 800$"
    );
    
    // Close Button
    cui.v2.CreateButton(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(10, 10, 490, 50),
        "close_shop",
        "0.8 0.2 0.2 1",
        true
    );
    
    cui.v2.CreateText(
        mainPanel,
        LuiPosition.Full,
        new LuiOffset(10, 10, 490, 50),
        14,
        "1 1 1 1",
        "SCHLIESSEN"
    );
    
    cui.v2.SendUi(player);
}

[ProtectedCommand("buy_item_1")]
private void BuyItem1(ConsoleSystem.Arg arg)
{
    if (arg.Player != null)
    {
        arg.Player.ChatMessage("Du hast AK-47 gekauft!");
    }
}

[ProtectedCommand("buy_item_2")]
private void BuyItem2(ConsoleSystem.Arg arg)
{
    if (arg.Player != null)
    {
        arg.Player.ChatMessage("Du hast MP5 gekauft!");
    }
}

[ProtectedCommand("close_shop")]
private void CloseShop(ConsoleSystem.Arg arg)
{
    if (arg.Player != null)
    {
        arg.Player.ChatMessage("Shop geschlossen");
    }
}

================================================================================
9. CLIENT-ENTITIES FÜR NETZWERK-OBJEKTE
================================================================================

WAS SIND CLIENT-ENTITIES?

ClientEntities sind Netzwerk-Entitäten, die nur für bestimmte Clients (Spieler) sichtbar sind. Sie existieren ausschließlich auf dem Client-Display und erzeugen keinen Server-Overhead wie echte Server-Entitäten.

ClientEntities sind ideal für:
- Visuelle Effekte nur für spezifische Spieler
- Temporäre UI-Objekte in der Spielwelt
- Dekorative Elemente, die nicht synchronisiert werden müssen

CLIENT-ENTITY ERSTELLEN

var entity = ClientEntity.Create(
    "assets/prefabs/deployable/chair/chair.deployed.prefab",
    new Vector3(0, 2, 0),
    Quaternion.identity
);

Parameter:
- Prefab-Pfad: Der volle Pfad zur Rust-Prefab
- Position: Vector3 Welt-Koordinaten
- Rotation: Quaternion Rotation

ENTITY FÜR SPIELER SPAWNEN

Eine Entity nur einem Spieler zeigen:

entity.SpawnFor(player);

Mehrere Spieler:

var playerList = new List<BasePlayer> { player1, player2, player3 };
entity.SpawnAll(playerList);

Oder alle aktiven Spieler:

entity.SpawnAll(BasePlayer.activePlayerList);

ENTITY-EIGENSCHAFTEN ÄNDERN

Nachdem erstellen können Sie Properties ändern:

entity.Position = new Vector3(10, 5, 20);
entity.Rotation = Quaternion.Euler(0, 45, 0);
entity.Flags = BaseEntity.Flags.OnFire;
entity.ParentID = parentEntity.net.ID;

NETZWERK-UPDATE SENDEN

Nach dem Ändern von Eigenschaften müssen Sie Updates senden:

entity.SendNetworkUpdate(); // Alles aktualisieren
entity.SendNetworkUpdate_Position(); // Nur Position
entity.SendNetworkUpdate_Flags(); // Nur Flags

FLAGS SETZEN UND PRÜFEN

Flags sind Boolean-Eigenschaften:

entity.SetFlag(BaseEntity.Flags.OnFire, true);
entity.SetFlag(BaseEntity.Flags.Locked, false);

if (entity.HasFlag(BaseEntity.Flags.OnFire))
{
    Puts("Entity brennt!");
}

Häufige Flags:
BaseEntity.Flags.OnFire - Brennt
BaseEntity.Flags.Locked - Gesperrt
BaseEntity.Flags.Reserved1 - Custom Flag 1
BaseEntity.Flags.Reserved2 - Custom Flag 2

RPC-HANDLING

Sie können RPC-Nachrichten vom Client verarbeiten:

public override void OnRpc(string rpc, Message message)
{
    if (rpc == "click")
    {
        Puts("Entity wurde angeklickt!");
    }
}

ENTITY ZERSTÖREN

Entity für einen Spieler entfernen:

entity.KillFor(player);

Für alle Spieler entfernen:

entity.KillAll();

Komplett freigeben:

entity.Dispose();

PRAKTISCHES BEISPIEL: SAMMELBARER GEGENSTAND

[ChatCommand("spawn")]
private void SpawnCommand(BasePlayer player, string command, string[] args)
{
    if (player == null)
        return;
    
    // Entity vor Spieler spawnen
    var positionAhead = player.transform.position + 
                       player.eyes.HeadRay().direction * 5f;
    
    var entity = ClientEntity.Create(
        "assets/prefabs/misc/xmas/xmas_loot_sack/xmas_loot_sack.prefab",
        positionAhead,
        Quaternion.identity
    );
    
    entity.SpawnFor(player);
    
    player.ChatMessage("Ein Geschenk wurde vor dir materialisiert!");
    
    // Nach 30 Sekunden verschwindet es
    timer.In(30f, () =>
    {
        if (entity != null)
        {
            entity.KillFor(player);
            player.ChatMessage("Das Geschenk ist verschwunden!");
        }
    });
}

================================================================================
10. EXTENSIONS UND WIEDERVERWENDBARER CODE
================================================================================

WAS SIND EXTENSIONS?

Extensions sind kompilierte DLL-Bibliotheken, die wiederverwendbarer Code enthalten. Sie implementieren das ICarbonExtension Interface und können von mehreren Plugins genutzt werden.

Extensions sind nützlich, wenn Sie:
- Code zwischen mehreren Plugins teilen möchten
- Eine Bibliothek für andere Entwickler bereitstellen möchten
- Umfangreiche Funktionen kapseln möchten

EXTENSION-STRUKTUR

Jede Extension benötigt eine Klasse, die ICarbonExtension implementiert:

#if CARBON
using System;
using API.Assembly;
using Carbon;

namespace MyExtension
{
    public class MyExtensionEntry : ICarbonExtension
    {
        public void OnLoaded(EventArgs args)
        {
            // Wird aufgerufen, wenn Extension geladen wird
            Community.Runtime.Events.Subscribe(
                API.Events.CarbonEvent.OnServerInitialized,
                arg =>
                {
                    try
                    {
                        Logger.Log("Extension ist geladen!");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Fehler", ex);
                    }
                }
            );
        }
        
        public void Awake(EventArgs args)
        {
            // Extension wird aktiviert (Framework ist bereit)
        }
        
        public void OnUnloaded(EventArgs args)
        {
            // Extension wird entladen
        }
    }
}
#endif

EXTENSION ALS LIBRARY NUTZEN

Sie können öffentliche statische Methoden und Klassen in Ihrer Extension definieren, die dann von Plugins genutzt werden:

Extension-Code (Shared.cs):

namespace MyExtension
{
    public static class UtilityHelper
    {
        public static bool IsPlayerVIP(BasePlayer player, PermissionSystem permission)
        {
            return permission.UserHasPermission(player.UserIDString, "vip.access");
        }
        
        public static void NotifyPlayer(BasePlayer player, string message)
        {
            player.ChatMessage($"[VIP System] {message}");
        }
        
        public static float GetDistance(BasePlayer player1, BasePlayer player2)
        {
            return Vector3.Distance(player1.transform.position, player2.transform.position);
        }
    }
}

Plugin-Code, das die Extension nutzt:

[ChatCommand("vipcheck")]
private void VipCheckCommand(BasePlayer player, string command, string[] args)
{
    if (MyExtension.UtilityHelper.IsPlayerVIP(player, permission))
    {
        MyExtension.UtilityHelper.NotifyPlayer(player, "Du bist VIP!");
    }
}

EXTENSION KOMPILIEREN

1. Erstellen Sie ein neues C# Class Library Projekt
2. Implementieren Sie ICarbonExtension
3. Kompilieren Sie zu DLL
4. Legen Sie DLL in /carbon/extensions/ ab
5. Server neu starten

EXTENSION-BEST-PRACTICES

- Immer Fehlerbehandlung nutzen (try-catch)
- Logging für Debugging verwenden
- Logger.Log() statt Puts() verwenden
- Statische Methoden für Utilities bevorzugen
- Große Extensions in Dateien aufteilen

================================================================================
11. ZIP-SCRIPT-PACKAGES FÜR MODULARISIERUNG
================================================================================

WAS SIND ZIP-PACKAGES?

ZIP-Packages (.cszip Dateien) erlauben es, große Plugins in mehrere C#-Dateien aufzuteilen. Dies verbessert die Code-Organisation und Wartbarkeit.

ZIP-PACKAGES STRUKTUR

Ein ZIP-Package hat folgende Struktur:

MyPlugin.cszip (ZIP-Archiv mit):
├── MyPlugin.cs              (Hauptdatei mit [Info])
├── Commands.cs              (Commands-Modul)
├── Hooks.cs                 (Event-Hooks Modul)
├── Utilities.cs             (Hilfsfunktionen)
└── Config.cs                (Konfiguration)

HAUPTDATEI (MyPlugin.cs)

Die Hauptdatei muss:
- Im selben Namespace sein
- Das [Info] Attribut haben
- Als partial class definiert sein
- CarbonPlugin erben

namespace Carbon.Plugins;

[Info("MyPlugin", "Author", "1.0.0")]
[Description("Plugin Beschreibung")]
public partial class MyPlugin : CarbonPlugin
{
    private void Init()
    {
        Puts("Plugin initialisiert!");
    }
    
    private void OnServerInitialized()
    {
        Puts("Server bereit!");
    }
}

WEITERE DATEIEN (Commands.cs)

Weitere Dateien müssen:
- Den gleichen Namespace haben
- Die gleiche Klasse als partial class definieren
- KEINE Basisklasse angeben

namespace Carbon.Plugins;

public partial class MyPlugin
{
    [ChatCommand("hello")]
    private void HelloCommand(BasePlayer player, string command, string[] args)
    {
        player.ChatMessage("Hallo!");
    }
    
    [ChatCommand("goodbye")]
    private void GoodbyeCommand(BasePlayer player, string command, string[] args)
    {
        player.ChatMessage("Auf Wiedersehen!");
    }
}

WEITERE DATEIEN (Hooks.cs)

namespace Carbon.Plugins;

public partial class MyPlugin
{
    private void OnPlayerConnected(BasePlayer player)
    {
        player.ChatMessage("Willkommen!");
    }
    
    private void OnPlayerDisconnected(BasePlayer player)
    {
        Puts($"{player.displayName} hat den Server verlassen");
    }
}

WEITERE DATEIEN (Utilities.cs)

namespace Carbon.Plugins;

public partial class MyPlugin
{
    private string FormatPrice(int price)
    {
        return $"${price:N0}";
    }
    
    private bool IsAdmin(BasePlayer player)
    {
        return permission.UserHasPermission(player.UserIDString, "admin.access");
    }
}

ZIP-PACKAGE ERSTELLEN

1. Alle .cs Dateien vorbereiten
2. In ein ZIP-Archiv packen
3. ZIP-Datei umbenennen in .cszip
4. In /carbon/plugins/ verschieben
5. Server neu starten (oder reload)

DEBUGGING / ENTWICKLUNG

Während der Entwicklung können Sie die Dateien im Dev-Ordner ablegen, anstatt immer neu zu packen:

/carbon/plugins/cszip_dev/MyPlugin/
├── MyPlugin.cs
├── Commands.cs
├── Hooks.cs
├── Utilities.cs
└── Config.cs

Carbon laden diese Dateien automatisch neu, wenn sie geändert werden. Dies funktioniert nur im Debug-Build.

PRAKTISCHES STRUKTUR-BEISPIEL

Ein großes Plugin könnte so strukturiert sein:

MyComplexPlugin.cszip:

MyComplexPlugin.cs:
- Plugin-Klasse
- Init()
- OnServerInitialized()
- Globale Variablen
- SaveData()/LoadData()

Commands.cs:
- Alle ChatCommands
- Alle ConsoleCommands
- Command-Validierung

Hooks.cs:
- OnPlayerConnected
- OnPlayerDisconnected
- OnEntitySpawned
- Alle Entity-Hooks

UI.cs:
- CreateMainUI()
- CreateShopUI()
- UpdateUI()
- HandleUICommands()

Database.cs:
- SavePlayerData()
- LoadPlayerData()
- UpdateDatabase()

Config.cs:
- ConfigClass Definition
- LoadConfig()
- SaveConfig()

================================================================================
12. BEST PRACTICES UND ERWEITERTE KONZEPTE
================================================================================

NULL-CHECKS UND DEFENSIVE PROGRAMMIERUNG

Immer auf null prüfen:

private void OnPlayerConnected(BasePlayer player)
{
    if (player == null || !player.IsConnected)
    {
        return;
    }
    
    // Jetzt sicher, player zu nutzen
    player.ChatMessage("Test");
}

In Commands:

[ChatCommand("test")]
private void TestCommand(BasePlayer player, string command, string[] args)
{
    if (player == null || !player.IsConnected)
    {
        return;
    }
    
    if (args == null || args.Length == 0)
    {
        player.ChatMessage("Keine Argumente!");
        return;
    }
    
    // Sicher zu nutzen
}

TRY-CATCH FÜR FEHLERBEHANDLUNG

Kritische Operationen in try-catch:

[ChatCommand("test")]
private void TestCommand(BasePlayer player, string command, string[] args)
{
    try
    {
        if (args.Length < 2)
        {
            player.ChatMessage("Syntax: /test <arg1> <arg2>");
            return;
        }
        
        int value = int.Parse(args[0]);
        
        // Operation...
    }
    catch (FormatException)
    {
        player.ChatMessage("Argument muss eine Zahl sein!");
    }
    catch (Exception ex)
    {
        Puts($"Fehler: {ex.Message}");
        player.ChatMessage("Ein Fehler ist aufgetreten!");
    }
}

PERFORMANCE-OPTIMIERUNGEN

Loops minimieren:

// SCHLECHT: Viele Lookups
foreach (var player in BasePlayer.activePlayerList)
{
    if (permission.UserHasPermission(player.UserIDString, "admin"))
    {
        // Viele Aufrufe von UserHasPermission
    }
}

// GUT: Cachen
var adminPlayers = BasePlayer.activePlayerList
    .Where(p => permission.UserHasPermission(p.UserIDString, "admin"))
    .ToList();

foreach (var player in adminPlayers)
{
    // Nur admin Spieler
}

Timer statt While-Loops:

// SCHLECHT: Blockiert Server
while (running)
{
    Thread.Sleep(1000);
    // Code...
}

// GUT: Asynchron
private void OnServerInitialized()
{
    timer.Every(1f, () =>
    {
        // Code läuft alle Sekunde, blockiert nicht
    });
}

UI sparsam updaten:

// SCHLECHT: Zu viele UI-Updates
[ChatCommand("test")]
private void TestCommand(BasePlayer player, string command, string[] args)
{
    for (int i = 0; i < 100; i++)
    {
        using CUI cui = new CUI(CuiHandler);
        cui.v2.UpdateText("element", "Item " + i);
        cui.v2.SendUi(player); // 100x senden!
    }
}

// GUT: Batchen
[ChatCommand("test")]
private void TestCommand(BasePlayer player, string command, string[] args)
{
    using CUI cui = new CUI(CuiHandler);
    
    for (int i = 0; i < 100; i++)
    {
        cui.v2.UpdateText("element", "Item " + i);
    }
    
    cui.v2.SendUi(player); // Nur 1x senden
}

INPUT-VALIDIERUNG

Immer Input validieren:

[ChatCommand("kick")]
private void KickCommand(BasePlayer player, string command, string[] args)
{
    if (args.Length < 1)
    {
        player.ChatMessage("Syntax: /kick <playername>");
        return;
    }
    
    var targetName = string.Join(" ", args);
    var target = BasePlayer.Find(targetName);
    
    if (target == null)
    {
        player.ChatMessage("Spieler nicht gefunden!");
        return;
    }
    
    if (target.IsAdmin && player != target)
    {
        player.ChatMessage("Kannst keine Admins kicken!");
        return;
    }
    
    if (player.userID == target.userID)
    {
        player.ChatMessage("Du kannst dich selbst nicht kicken!");
        return;
    }
    
    target.Kick("Du wurdest gekickt");
    Puts($"{player.displayName} hat {target.displayName} gekickt");
}

DATENSPEICHERUNG

Daten sollten in OnServerSave() gespeichert werden:

private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();

private void OnServerSave()
{
    var json = JsonConvert.SerializeObject(playerData);
    System.IO.File.WriteAllText("player_data.json", json);
    Puts("Daten gespeichert!");
}

LOGGING UND DEBUGGING

Für Debugging nutzen Sie Puts():

Puts("Debug: " + message);
Puts($"Player: {player.displayName}");

Für Fehler:

Puts($"ERROR: {ex.Message}");
Puts($"Stack: {ex.StackTrace}");

Mit Timestamps:

Puts($"[{DateTime.Now:HH:mm:ss}] Message");

RESSOURCEN-CLEANUP

Im Unload sollten Ressourcen freigegeben werden:

private Timer myTimer;
private List<ClientEntity> spawnedEntities = new List<ClientEntity>();

private void Unload()
{
    // Timer stoppen
    if (myTimer != null)
    {
        myTimer.Destroy();
    }
    
    // Entities löschen
    foreach (var entity in spawnedEntities)
    {
        if (entity != null)
        {
            entity.KillAll();
        }
    }
    
    // Daten speichern
    SaveData();
    
    Puts("Plugin entladen!");
}

HÄUFIGE UTILITY-METHODEN

Spieler-Finder:

private BasePlayer FindPlayerByName(string name)
{
    var players = BasePlayer.allPlayerList;
    
    var exact = players.FirstOrDefault(p => 
        p.displayName.Equals(name, StringComparison.OrdinalIgnoreCase));
    
    if (exact != null)
        return exact;
    
    var partial = players.FirstOrDefault(p => 
        p.displayName.Contains(name, StringComparison.OrdinalIgnoreCase));
    
    return partial;
}

Distanz-Prüfung:

private bool IsNearby(BasePlayer player1, BasePlayer player2, float distance)
{
    return Vector3.Distance(player1.transform.position, player2.transform.position) <= distance;
}

Position vor Spieler:

private Vector3 GetAheadPosition(BasePlayer player, float distance = 5f)
{
    return player.transform.position + 
           player.eyes.HeadRay().direction * distance;
}

Chat-Farben:

private void SendColoredMessage(BasePlayer player, string message, string color = "lime")
{
    player.ChatMessage($"<color={color}>{message}</color>");
}

ZUSAMMENFASSUNG DER BEST PRACTICES

1. Immer null-checks durchführen
2. Try-catch für kritische Code-Abschnitte nutzen
3. Input validieren
4. Loops minimieren und cachen
5. Timer statt Threads nutzen
6. UI sparsam updaten (batchen)
7. Protected Commands für UI verwenden
8. Daten in OnServerSave speichern
9. Ressourcen im Unload freigeben
10. Logging für Debugging nutzen
11. Statische Hilfsmethoden für Code-Reuse
12. Permissions für Sicherheit nutzen
13. Cooldowns für teure Operations
14. Aussagekräftige Fehlermeldungen zeigen
15. Code dokumentieren

================================================================================
ENDE DER DOKUMENTATION
================================================================================

Diese umfassende Dokumentation deckt alle wichtigen Aspekte der Carbon-Plugin-Entwicklung ab. Sie können diese als Referenz bei der Entwicklung nutzen.

Für weitere Informationen besuchen Sie: https://carbonmod.gg/devs/

Viel Erfolg bei der Plugin-Entwicklung!
