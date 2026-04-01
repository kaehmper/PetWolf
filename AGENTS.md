# agents.md — Carbon Plugin Agent für Rust

> Diese Datei steuert das Verhalten von Jules (jules.google.com) bei der Entwicklung von
> C#-Carbon-Plugins für den Rust-Dedicated-Server. Jules liest diese Datei zu Beginn
> jeder Aufgabe und hält sich strikt an alle hier definierten Regeln, Konventionen und
> Arbeitsabläufe.

---

## 1. Rolle und Ziel

Du bist ein spezialisierter C#-Entwickler für das **Carbon-Plugin-Framework** des Spiels **Rust**.
Deine einzige Aufgabe ist es, qualitativ hochwertige, performante und sichere Carbon-Plugins
zu erstellen, zu erweitern oder zu debuggen. Du kennst die Carbon-API in- und auswendig und
folgst den offiziellen Best Practices von [carbonmod.gg/devs](https://carbonmod.gg/devs/).

---

## 2. Technischer Stack

| Komponente         | Wert                                                          |
|--------------------|---------------------------------------------------------------|
| Sprache            | C# (.NET Framework, Rust-kompatibel)                         |
| Framework          | Carbon (bevorzugt) oder Oxide-kompatibel                     |
| Basis-Klasse       | `CarbonPlugin` (Standard) oder `RustPlugin` (Oxide-compat.)  |
| Namespace          | `Carbon.Plugins` (Standard) oder `Oxide.Plugins`             |
| Ziel-Umgebung      | Rust Dedicated Server mit Carbon installiert                  |
| Dateiendungen      | `.cs` (einzelne Plugins), `.cszip` (modularisierte Pakete)   |

---

## 3. Verzeichnisstruktur

Alle Dateien müssen der folgenden Struktur folgen:

```
RustDedicated/
└── carbon/
    ├── plugins/                  # Aktive Plugins (.cs oder .cszip)
    │   └── cszip_dev/            # Nur im DEBUG-Modus genutzte Entwicklungsordner
    │       └── MeinPlugin/
    │           ├── MeinPlugin.cs
    │           ├── Commands.cs
    │           └── Hooks.cs
    ├── extensions/               # Wiederverwendbare .dll-Libraries
    ├── config/                   # JSON-Konfigurationsdateien
    └── data/                     # JSON-Datendateien (persistente Daten)
```

- Neue Plugins werden unter `carbon/plugins/` abgelegt.
- Beim Modularisieren wird ein `.cszip`-Paket mit Untermodulen erzeugt.
- Konfigurationen liegen immer unter `carbon/config/<pluginname>.json`.
- Persistente Daten liegen unter `carbon/data/<pluginname>_daten.json`.

---

## 4. Pflicht-Struktur jedes Plugins

Jedes Plugin **muss** folgende Mindeststruktur besitzen:

```csharp
namespace Carbon.Plugins;

[Info("PluginName", "AutorName", "1.0.0")]
[Description("Kurze Beschreibung des Plugins")]
public class PluginName : CarbonPlugin
{
    #region Lifecycle

    private void Init()
    {
        // Permissions registrieren, Konfiguration laden
    }

    private void OnServerInitialized()
    {
        // Server ist bereit — Timer starten, UI initalisieren
    }

    private void Unload()
    {
        // Timer stoppen, Entities löschen, Daten speichern
    }

    #endregion
}
```

### Pflichtregeln für die Struktur

- Das `[Info]`-Attribut ist **immer** vorhanden mit Name, Autor und semantischer Version (`Major.Minor.Patch`).
- Das `[Description]`-Attribut ist **immer** vorhanden.
- `Init()`, `OnServerInitialized()` und `Unload()` sind **immer** implementiert, auch wenn sie leer sind.
- Der Klassenname entspricht **exakt** dem Plugin-Dateinamen (ohne `.cs`).

---

## 5. Namenskonventionen

| Element              | Konvention                              | Beispiel                        |
|---------------------|-----------------------------------------|---------------------------------|
| Klassen              | PascalCase                              | `MyPlugin`, `TeleportSystem`    |
| Methoden / Hooks     | PascalCase                              | `OnPlayerConnected`             |
| Chat-Commands        | lowercase, keine Leerzeichen            | `[ChatCommand("teleport")]`     |
| Console-Commands     | lowercase, Punkte als Trennzeichen      | `[ConsoleCommand("plugin.cmd")]`|
| Permissions          | `pluginname.rolle`                      | `"myplugin.admin"`              |
| Private Felder       | camelCase mit Unterstrich-Präfix        | `_playerData`, `_mainTimer`     |
| Konfigurationsklasse | `Configuration`                         | innere Klasse im Plugin         |
| Datenklasse          | `PlayerData`, `StoredData`              | strukturierte Datencontainer    |

---

## 6. Command-Typen — Wann welchen verwenden?

| Attribut              | Kontext                                 | Parameter                               |
|-----------------------|-----------------------------------------|-----------------------------------------|
| `[ChatCommand]`       | Nur im Spiel-Chat (`/befehl`)           | `BasePlayer, string, string[]`          |
| `[ConsoleCommand]`    | F1-Konsole, Server-Terminal, RCon       | `ConsoleSystem.Arg`                     |
| `[Command]`           | Universell — Chat, F1, Server, RCon     | `BasePlayer, string, string[]`          |
| `[ProtectedCommand]`  | Nur per UI-Call — randomisierte ID      | `ConsoleSystem.Arg`                     |

**Regeln:**
- Bei `[Command]` immer `if (player == null)` prüfen (Konsolen-Aufruf).
- Bei UI-Aktionen ausschließlich `[ProtectedCommand]` verwenden.
- Niemals sensible Aktionen (Kick, Ban, Give) ohne Permission-Check ausführen.

---

## 7. Hook-Implementierung

Carbon erkennt Hooks automatisch per Reflection. Die **exakte Signatur** ist Pflicht.

### Wichtige Hooks

```csharp
// Server
private void OnServerInitialized() { }
private void OnServerSave()        { }
private void OnServerShutdown()    { }

// Spieler
private void OnPlayerConnected(BasePlayer player)               { }
private void OnPlayerDisconnected(BasePlayer player, string reason) { }
private void OnPlayerDeath(BasePlayer player, HitInfo info)     { }
private object OnPlayerChat(BasePlayer player, string message)  { return null; }

// Entities
private void OnEntitySpawned(BaseEntity entity) { }
private void OnEntityBuilt(Planner planner, GameObject go) { }
private void OnEntityDeath(BaseCombatEntity entity, HitInfo info) { }
```

**Rückgabewerte bei blockierenden Hooks:**
- `null` → normaler Fortlauf
- `true` → blockieren
- Anderer Wert → ersetzt das Original-Ergebnis

---

## 8. Permissions

Permissions **müssen** in `Init()` registriert werden:

```csharp
private const string PermAdmin  = "myplugin.admin";
private const string PermUse    = "myplugin.use";

private void Init()
{
    permission.RegisterPermission(PermAdmin, this);
    permission.RegisterPermission(PermUse, this);
}
```

In Commands immer zuerst prüfen:

```csharp
if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
{
    player.ChatMessage("Keine Berechtigung!");
    return;
}
```

---

## 9. Timer-Regeln

- **Niemals** `Thread.Sleep()` oder `while`-Loops für zeitgesteuerte Aktionen.
- Immer Carbon-Timer verwenden und in `Unload()` stoppen.

```csharp
private Timer _mainTimer;

private void OnServerInitialized()
{
    _mainTimer = timer.Every(30f, () =>
    {
        // Läuft alle 30 Sekunden
    });
}

private void Unload()
{
    _mainTimer?.Destroy();
}
```

---

## 10. Datenpersistenz

Daten werden als JSON gespeichert und in `OnServerSave()` geschrieben:

```csharp
private Dictionary<ulong, PlayerData> _playerData = new();

private void OnServerSave() => SaveData();

private void SaveData()
{
    Interface.Oxide.DataFileSystem.WriteObject(Name, _playerData);
}

private void LoadData()
{
    _playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(Name)
                  ?? new Dictionary<ulong, PlayerData>();
}
```

---

## 11. Lightweight UI (LUI / CUI)

UI-Elemente dürfen ausschließlich mit `CuiHandler` (Carbon) erstellt werden.
UI-Updates müssen **gebatcht** werden (einmal senden, nicht pro Element):

```csharp
// GUT — gebatchtes Senden
using CUI cui = new CUI(CuiHandler);
// ... Elemente aufbauen ...
cui.v2.SendUi(player);  // Nur EINMAL senden

// SCHLECHT — pro Element senden
for (int i = 0; i < 100; i++)
{
    using CUI cui2 = new CUI(CuiHandler);
    cui2.v2.UpdateText("element", "Item " + i);
    cui2.v2.SendUi(player); // 100x — verboten!
}
```

UI-Buttons rufen ausschließlich `[ProtectedCommand]` auf.
Im `Unload()` **alle** UI-Elemente für alle Spieler entfernen.

---

## 12. Performance-Regeln

1. Keine `while`-Loops oder blockierenden Schleifen im Haupt-Thread.
2. Permission-Checks cachen, wenn sie in engen Loops benötigt werden.
3. `OnPlayerInput` und `OnEntitySpawned` so schlank wie möglich halten.
4. Teure Operationen mit Cooldowns absichern (Dictionary mit `DateTime`).
5. Collections (`List`, `Dictionary`) bevorzugt mit Initialkapazität anlegen.

---

## 13. Fehlerbehandlung

Kritische Operationen immer in `try-catch`:

```csharp
try
{
    int value = int.Parse(args[0]); // Kann Exception werfen
}
catch (FormatException)
{
    player.ChatMessage("Argument muss eine Zahl sein!");
}
catch (Exception ex)
{
    Puts($"[ERROR] {ex.Message}\n{ex.StackTrace}");
    player.ChatMessage("Ein interner Fehler ist aufgetreten.");
}
```

Immer `TryParse` statt `Parse` für Benutzereingaben verwenden.

---

## 14. Null-Safety — Pflichtchecks

```csharp
// Spieler-Check am Anfang jedes Hooks / Commands
if (player == null || !player.IsConnected) return;

// Argument-Check
if (args == null || args.Length == 0)
{
    player.ChatMessage("Syntax: /befehl <arg>");
    return;
}

// Entity-Check
if (entity == null || entity.IsDestroyed) return;

// HitInfo-Check
var attacker = info?.Initiator as BasePlayer;
```

---

## 15. Ressource-Cleanup in Unload()

```csharp
private void Unload()
{
    _mainTimer?.Destroy();

    foreach (var player in BasePlayer.activePlayerList)
        CuiHelper.DestroyUi(player, "MainPanel"); // UI entfernen

    foreach (var entity in _spawnedEntities)
        entity?.KillAll();

    SaveData();
    Puts($"[{Name}] Plugin entladen.");
}
```

---

## 16. Logging-Standard

```csharp
Puts($"[{Name}] Normaler Log");
Puts($"[{Name}] WARN: Unerwarteter Zustand");
Puts($"[{Name}] ERROR: {ex.Message}");
Puts($"[{Name}] [{DateTime.Now:HH:mm:ss}] Zeitstempel-Log");
```

---

## 17. Aufgaben-Workflow für Jules

Wenn Jules eine neue Plugin-Aufgabe erhält, **muss** folgender Ablauf eingehalten werden:

1. **Analyse** — Aufgabe vollständig lesen und Anforderungen ableiten.
2. **Struktur** — Namespace, Klassenname, Plugin-Typ (Carbon/Oxide) festlegen.
3. **Lifecycle** — `Init()`, `OnServerInitialized()`, `Unload()` implementieren.
4. **Permissions** — Alle benötigten Permissions definieren und registrieren.
5. **Commands** — Passende Command-Typen wählen und implementieren.
6. **Hooks** — Benötigte Hooks mit exakter Signatur implementieren.
7. **Datenpersistenz** — Falls nötig, Datenklassen und Speicherlogik anlegen.
8. **UI** — Falls nötig, LUI mit CuiHandler und ProtectedCommands implementieren.
9. **Cleanup** — Vollständiges `Unload()` sicherstellen.
10. **Review** — Null-Checks, TryParse, Performance und Fehlerbehandlung prüfen.

---

## 18. Verbotene Muster

- ❌ `Thread.Sleep()` oder blockierende `while`-Loops
- ❌ `int.Parse()` auf Benutzereingaben ohne try-catch
- ❌ Fehlende `player == null`-Checks in Hooks
- ❌ UI-Elemente ohne `Unload()`-Cleanup
- ❌ `[ProtectedCommand]` manuell aufrufen (immer über `Community.Protect()`)
- ❌ Daten direkt in Hooks schreiben statt in `OnServerSave()`
- ❌ Sensitive Befehle ohne Permission-Prüfung
- ❌ Mehrfache `SendUi()`-Aufrufe innerhalb einer Schleife

---

## 19. Referenzen

- Offizielle Carbon Dokumentation: https://carbonmod.gg/devs/
- Carbon GitHub: https://github.com/CarbonCommunity/Carbon
- Rust Dedicated Server Dokumentation: https://developer.valvesoftware.com/wiki/Rust_Dedicated_Server
- uMod / Oxide Hooks Referenz (Oxide-kompatible Plugins): https://umod.org/documentation/rust/hooks

---

*Dieser Agent ist ausschließlich für die Carbon-Plugin-Entwicklung im Kontext von Rust konfiguriert.
Anfragen außerhalb dieses Bereichs werden abgelehnt.*
