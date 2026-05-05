# ExitToDesktop

A Slay the Spire 2 mod that adds a **Save and Exit to Desktop** button below the existing **Save and Quit** button on the in-run pause menu, with a confirmation prompt. Skips the trip through the main menu when you just want to close the game.

I made this for myself because I open and close the game constantly while developing other mods. It's small enough that the source is also intended to be useful as a reference for common modding patterns — see [For Modders](#for-modders) below.

---

## For Players

### What It Does

Adds one extra button to the in-run pause menu:

- **Save and Exit to Desktop** — saves the run (waits for any in-flight save to flush) and quits the game process. A confirmation popup appears first.

The button is hidden in multiplayer for non-host clients, since they can't trigger the host's save.

The button label is localized into 12 languages and follows the game's language setting.

### Requirements

- [BaseLib](https://www.nexusmods.com/slaythespire2/mods/103) (any recent version).

### Installation

1. Download and extract BaseLib into your mods folder if you haven't already.
2. Download `ExitToDesktop-vX.X.X.zip` from the releases page and extract it into your STS2 `mods/` folder. The result should be a `ExitToDesktop/` folder containing `ExitToDesktop.dll`, `ExitToDesktop.json`, and `ExitToDesktop.pck`.

### Uninstalling

Delete the `ExitToDesktop/` folder from your STS2 `mods/` directory. The mod has no save data or config files.

### Translations

The button label is localized in-game into 12 languages: Русский, 中文 (Simplified), Deutsch, Español, Français, Italiano, 日本語, 한국어, Polski, Português (Brasil), ไทย, Türkçe.

Want to help translate into another language? DM me on the Slay the Spire Discord — handle is `.theshoe`.

---

## For Modders

This mod is small (~120 lines across two files) but exercises a handful of patterns that come up often in STS2 mods. The notes below cross-reference the source so you can lift what you need.

### Tech Stack

- **Language:** C# (.NET 9)
- **Modding layer:** [BaseLib](https://github.com/Alchyr/BaseLib-StS2) + HarmonyX (uses STS2's built-in mod loader)
- **Build target:** Godot 4.5.1 / STS2

### Project Structure

| File | Purpose |
|---|---|
| `MainFile.cs` | Mod entry point. `[ModInitializer]` hook scans the assembly and applies every `[HarmonyPatch]` class via `Harmony.CreateClassProcessor` |
| `Patches.cs` | The single Harmony patch — postfix on `NPauseMenu._Ready` that injects the button, wires its handler, and triggers the confirm-and-quit flow |
| `ExitToDesktop.json` | BaseLib mod manifest (`id`, `name`, `version`, `has_pck`, `has_dll`, `dependencies`) |
| `project.godot` + `export_presets.cfg` | Godot project metadata. The export preset `BasicExport` is what the build uses to package `localization/` into the `.pck` |
| `ExitToDesktop/localization/<lang>/gameplay_ui.json` | Per-language string tables. Each contains a single key `EXITTODESKTOP.button_label`. Packaged into `.pck`, loaded at runtime by the game's `LocManager` |
| `ExitToDesktop.csproj` | Build configuration. See [Build system](#build-system) below |

### Patterns Worth Copying

**Auto-applying every `[HarmonyPatch]` in the assembly** — `MainFile.cs:21-26` enumerates types with the attribute and runs them through Harmony's class processor inside a try/catch, so a single broken patch logs a warning but doesn't take the rest of the mod down with it. Drop-in pattern for any mod that grows past one patch.

**Adding a button to a vanilla menu by duplicating an existing one** — `Patches.cs:25-37`. Postfix `NPauseMenu._Ready`, find a sibling button via `GetNodeOrNull<NPauseMenuButton>("%ButtonContainer/SaveAndQuit")` (the `%` is Godot's unique-name lookup), `Duplicate()` it, rename, and add it back to the same container. You inherit all of the original's styling for free — fonts, sizing, hover/press shaders, click sounds — and the menu's flex layout absorbs the new child without code changes.

**Avoiding shared-material side effects after `Duplicate()`** — `Patches.cs:39-46`. Godot's `Duplicate()` shallow-copies node references; the child `TextureRect`'s `ShaderMaterial` is *shared* with the original button by default, so animating one (e.g. the hover `_hsv` shift) animates both. Fix: deep-duplicate the material with `sharedMat.Duplicate(true)` and assign it back. The extra wrinkle: `NPauseMenuButton` caches the material in a private `_hsv` field, which the shader-driving code reads from; we update that field reflectively via `AccessTools.Field(...).SetValue(...)` so the button's own animation logic uses the duplicated material instead of the original.

**Keyboard / controller focus navigation** — `Patches.cs:55-61`. `Control.FocusNeighbor{Top,Bottom,Left,Right}` are `NodePath` properties; set them so D-pad/arrow input traverses your new button correctly. Pointing left/right back at the button itself prevents focus from sliding off into unrelated UI. Don't forget to also update the *original* button's `FocusNeighborBottom` to point at your new one — focus is bidirectional but configured per-node.

**Wiring a Godot signal from C#** — `Patches.cs:63-66`. Connect to `NClickableControl.SignalName.Released` with `Callable.From<NButton>(_ => OnExitToDesktopPressed())`. The generic type matches the signal's argument list; the lambda lets you ignore the argument and dispatch to a static method.

**Multiplayer-aware UI** — `Patches.cs:51-52`. `RunManager.Instance.NetService.Type != NetGameType.Client` is the canonical check for "not a multiplayer guest". Hide UI that only the host can act on. (Doesn't hide for solo or for the host of a multiplayer run.)

**Confirmation popup with localized strings** — `Patches.cs:92-101`. `NGenericPopup.Create()` plus `NModalContainer.Instance.Add(...)` is how you stack a modal. `popup.WaitForConfirmation(body, header, cancel, confirm)` is async-awaitable and returns a `bool`. The four args are all `LocString`s — `new LocString(table, key)` — which resolve at display time, so the popup re-localizes automatically if the language changes. Reuses vanilla strings (`main_menu_ui` table's `QUIT_CONFIRM_POPUP.*` and `GENERIC_POPUP.*`) for the popup chrome rather than shipping our own.

**Localized button label with English fallback** — `Patches.cs:71-84`. Per-mod localization lives at `res://<ModId>/localization/<lang>/<table>.json` *inside the mod's .pck*. The game's `LocManager` merges these into its tables at load time. Look up by table + key with `LocManager.Instance?.GetTable("gameplay_ui").GetRawText("EXITTODESKTOP.button_label")`. Wrap in try/catch and fall back to the English literal so the mod still loads correctly in environments without the .pck (e.g. a dev machine without Godot installed — see [Build system](#build-system)).

Note that the json file is laid out as `localization/<lang>/<table_name>.json`, not the schema BaseLib's docs lean on. The game merges per-mod tables by name with the shipped tables — so dropping new keys into `gameplay_ui.json` makes them lookupable as if they were vanilla entries. Pick a key prefix (we use `EXITTODESKTOP.`) to avoid collisions.

**Async save-aware quit** — `Patches.cs:104-112`. `SaveManager.Instance.CurrentRunSaveTask` is non-null while a save is in flight. Await it before calling `NGame.Instance?.Quit()` so a pause-menu save doesn't get cut off mid-write. `TaskHelper.RunSafely(...)` (`Patches.cs:89`) is BaseLib's wrapper that logs uncaught exceptions instead of letting them disappear into the void — use it any time you fire-and-forget a Task from a sync context like a signal handler.

### Build System

Two things make the build interesting:

**`DeployToMods` gating** (`ExitToDesktop.csproj:62-64`, `:99`, `:125`). A plain `dotnet build` only verifies the C# compiles — it does *not* copy anything to your live mods folder. The post-build copy and the Godot .pck export are both gated behind `<DeployToMods>true</DeployToMods>`, set on the command line by `deploy.sh` via `/p:DeployToMods=true`. This means I can run `dotnet build` from the IDE without trashing my live install with intermediate states. Worth copying for any mod where the build target *is* the live game.

**Incremental Godot .pck export** (`ExitToDesktop.csproj:120-132`). The `GodotPublish` target uses MSBuild's `Inputs="@(GodotPckInput)"` / `Outputs="$(ModsPath).../$(Name).pck"` pair to skip the export when no localization or `project.godot` change is newer than the .pck on disk. Without that, every build kicks off a 5–10s headless Godot run for no reason. The `IsInnerGodotExport` env var guard prevents the recursive build that Godot's headless export would otherwise trigger by trying to compile its own `.csproj`.

**`local.props` for per-machine paths** (`ExitToDesktop.csproj:140`). `local.props` is gitignored (`.gitignore`); it overrides `ModsPath` (where to deploy) and `GodotPath` (which Godot binary to use for .pck export). On a dev machine without Godot, `GodotPath` is unset, the `CheckDependencyPaths` target prints a warning, the `.pck` step is skipped, and the mod still loads — just with the English label everywhere because the localization tables don't ship without the .pck.

### Prerequisites

- [.NET 9.0 SDK](https://dot.net)
- `sts2.dll` and `0Harmony.dll` from your STS2 install, in `ExitToDesktop/libs/`
  - Windows: `steamapps\common\Slay the Spire 2\`
  - Linux:   `~/.steam/steam/steamapps/common/Slay the Spire 2/data_sts2_linux_x86_64/`
- (Optional, for localized labels) Godot/MegaDot 4.5.1 mono — same major.minor as the game's engine, otherwise the resulting `.pck` won't load.

### Setup

1. Copy `ExitToDesktop/local.props.example` to `ExitToDesktop/local.props` (this repo doesn't ship one — write it from the example in the slay-the-stats repo or by hand).
2. Set `ModsPath` to your STS2 mods directory.
3. Optionally set `GodotPath` to your Godot 4.5.1 mono binary.

Without `local.props`, build output goes to `ExitToDesktop/dist/` and you copy it to `mods/` yourself.

### Building

```bash
./deploy.sh
```

Compiles, copies the DLL and manifest to `ModsPath`, and (if `GodotPath` is configured) exports the localization `.pck`.

To build a release archive:

```bash
./deploy.sh --release
```

Produces `ExitToDesktop-vX.X.X.zip` next to `deploy.sh`.

### Resources

- [STS2 Hello World mod guide](https://github.com/giulianoconte/slay-the-spire-2-mod-guide)
- [Mod template](https://github.com/Alchyr/ModTemplate-StS2)
- [BaseLib](https://github.com/Alchyr/BaseLib-StS2) / [BaseLib wiki](https://alchyr.github.io/BaseLib-Wiki/)
