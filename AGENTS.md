# AGENTS.md - Rust Carbon Plugin Development

This document provides strict technical instructions, boundaries, and code patterns for AI coding assistants (specifically Jules/Gemini) developing C# plugins for Rust using the modern Carbon Framework.

## Technology Stack
- **Language**: C# (.NET Framework compatible with Rust Dedicated Server)
- **Framework**: Carbon (Modern API, replacing legacy Oxide)
- **Target Environment**: Rust Dedicated Server
- **UI System**: LUI (Carbon Lightweight UI)
- **Modularity**: `.cszip` packaged files or `cszip_dev` folders for hot-reloading

## Architecture & Project Structure

We strictly use the **`.cszip` modular approach** for maintainability. Do not generate 5,000-line monolithic `.cs` files. 

### Development Directory (`/carbon/plugins/cszip_dev/MyPlugin/`)
During development, files sit in the `cszip_dev` directory for hot-reloading. All files must share the `Carbon.Plugins` namespace and use `public partial class MyPlugin`.
- `MyPlugin.cs` - Main entry (contains `[Info]`, `[Description]`, `Init()`, `OnServerInitialized()`, `Unload()`)
- `Commands.cs` - Chat, Console, and Universal commands
- `Hooks.cs` - Rust/Carbon event hooks (e.g., `OnPlayerConnected`, `OnEntitySpawned`)
- `UI.cs` - LUI logic and `[ProtectedCommand]` handlers
- `Config.cs` - Data and configuration saving/loading
- `Utilities.cs` - Helper methods

## Environment Setup & Commands

### Local Validation
```bash
# Verify C# syntax (requires local dummy .csproj with Rust/Carbon DLLs)
dotnet build
# Format code to standard C# styles
dotnet format
Build and Deploy (Production)
code
Bash
# Package into a Carbon .cszip archive
zip -j MyPlugin.cszip src/*.cs
# Deploy to server
cp MyPlugin.cszip /path/to/RustDedicated/carbon/plugins/
Permissions for AI Agent
Allowed Without Prompting
Read files, analyze project structure.
Lint, format, and type-check single files.
Add null-checks, try-catch blocks, and performance optimizations.
Split large files into smaller partial class files.
Require Approval First
Executing system shell commands or git pushes.
Deleting core files.
Changing the overall architecture from .cszip to a monolithic single .cs file.
Hardcoding any configuration data.
Code Style & Core Patterns
1. Plugin Definition
Always use the modern Carbon implementation. Do not use Oxide.Plugins unless explicitly porting legacy code.
code
C#
namespace Carbon.Plugins;[Info("PluginName", "AuthorName", "1.0.0")]
[Description("Description of what the plugin does")]
public partial class PluginName : CarbonPlugin
{
    private void Init() { /* Register permissions here */ }
    private void OnServerInitialized() { /* Start timers, init UI here */ }
    private void Unload() { /* Destroy timers, kill entities here */ }
}
2. Commands & Authentication
Chat: [ChatCommand("name")] (Use BasePlayer player, string command, string[] args)
Console: [ConsoleCommand("name")] (Use ConsoleSystem.Arg arg)
Universal: [Command("name")] (Check if (player == null) to branch logic)
UI Actions: [ProtectedCommand("name")] (Server-side randomized ID to prevent exploitation)
Security Attributes: Stack these heavily for admin/VIP commands: [Permission("myplugin.admin")], [AuthLevel(1)], [Group("vip")], [Cooldown(5000)].
3. Lightweight UI (LUI) Implementation
Never use raw JSON for CUI. Use Carbon's LUI wrapper, utilize using for automatic disposal, and batch your updates.
code
Csharp[ChatCommand("shop")]
private void ShopCommand(BasePlayer player, string command, string[] args)
{
    using CUI cui = new CUI(CuiHandler);
    
    var panel = cui.v2.CreatePanel("Hud.Root", LuiPosition.Full, new LuiOffset(100, 100, 400, 250), "0.1 0.1 0.1 0.9");
    cui.v2.CreateText(panel, LuiPosition.Full, LuiOffset.Zero, 16, "1 1 1 1", "Welcome to Shop");
    cui.v2.CreateButton(panel, LuiPosition.Full, new LuiOffset(10, 10, 190, 50), "shop_buy_item", "0.2 0.8 0.2 1", true); // true = isProtected
    
    cui.v2.SendUi(player); // Only call SendUi ONCE at the end
}[ProtectedCommand("shop_buy_item")]
private void OnBuyItem(ConsoleSystem.Arg arg)
{
    if (arg.Player == null) return;
    arg.Player.ChatMessage("Item bought!");
}
4. Timers and Frame Scheduling
Do not use System.Threading or Task.Delay as they desync the game server.
code
C#
// Delay action
timer.In(5f, () => { /* logic */ });

// Repeat action
var myTimer = timer.Every(60f, () => { /* logic */ });

// Ensure Entity is fully spawned before interacting
private void OnEntitySpawned(BaseEntity entity)
{
    NextFrame(() => {
        if (entity != null && !entity.IsDestroyed) {
            // Safe to interact
        }
    });
}
Security & Secrets Handling
NEVER include RCON passwords, Server IPs, database connection strings, or real Steam IDs in the code.
NEVER hardcode permissions. Register them dynamically in Init():
permission.RegisterPermission("myplugin.admin", this);
Validate arg.Player != null inside [ConsoleCommand] and [ProtectedCommand] before assuming a player executed it.
Defensive Programming (Good vs. Bad Examples)
✅ Good Patterns
Null Checks: ALWAYS verify if (player == null || !player.IsConnected) return;
Safe Parsing: Use int.TryParse(args[0], out int val) instead of int.Parse().
Resource Cleanup:
code
C#
private void Unload()
{
    myTimer?.Destroy();
    foreach(var entity in spawnedEntities) if (entity != null) entity.KillAll();
}
Caching Lookups: Cache results of BasePlayer.activePlayerList.Where(...) into a List<BasePlayer> before iterating.
❌ Avoid These Patterns
Blocking Threads: Never use Thread.Sleep() or while(true). They will freeze the entire Rust server. Use timer.Every().
UI Spam: Do not call cui.v2.SendUi(player) inside a loop. Batch UI creations and send once outside the loop.
Blind Casting: Do not assume info.Initiator in OnPlayerDeath is a BasePlayer. Always use as BasePlayer and check for null (could be an NPC, animal, or trap).
Self-Actions: Failing to prevent a player from kicking/banning themselves (if (player.userID == target.userID) return;).
Data Saving
Use OnServerSave() to write data to disk synchronously with the server.
code
C#
private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();

private void OnServerSave()
{
    var json = JsonConvert.SerializeObject(playerData);
    System.IO.File.WriteAllText($"carbon/data/{Name}.json", json);
}
Common Utilities Reference
Check Permission: permission.UserHasPermission(player.UserIDString, "myplugin.use")
Spawn Client Entity (Visual only):
code
C#
var entity = ClientEntity.Create("assets/prefabs/.../prefab", pos, rot);
entity.SpawnFor(player);
Chat Colors: player.ChatMessage("<color=lime>Success!</color>");
