# Unreleased Changes

## 2026-06-17

### Virtual Ecology Chatter Controls

#### Player-Facing
- Town NPCs now use a broader ambient chatter pool that can include local town flavor, approved wilderness references, and occasional rumors about other real Britannian towns.
- Ambient AI chatter is less likely to repeat the same cached line immediately because used lines now have a configurable reuse cooldown.

#### Dev-Facing
- Added JSON-backed Virtual Ecology settings with live staff editing from the chatter gump.
- Added real location/shop/building prompt context and validation defaults through `VirtualEcologyLocations`.
- Added stricter generated-line filtering for fake proper names, blocked towns/facets, capitalization, and line length.
- Expanded movement chatter selection to randomize eligible NPC speakers, respect line of sight, and keep real server facts higher priority than AI flavor.

#### Config / Admin
- New staff settings include cache sizes, generation timing, movement chatter chances, speaker/player cooldowns, line reuse cooldown, and staff trigger behavior.
- Added clear-all cache actions through the chatter gump and `[TCClearAll`.

#### Verification
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors.

#### Risks / Notes
- Runtime JSON under `Distribution/Configuration/VirtualEcology/` remains local/generated and is not committed.

### Normal Felucca Harvest Output

#### Player-Facing
- Felucca mining and lumberjacking now award the normal 1.0x material output instead of the doubled Felucca output.

#### Dev-Facing
- Set mining `ConsumedPerFeluccaHarvest` to `1` and lumberjacking `ConsumedPerFeluccaHarvest` to `10` to match the normal harvest consumption/output values.

#### Verification
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors.

### Virtual Ecology Town Crier Announcements

#### Player-Facing
- Town criers can now share in-world news about notable first Grandmaster achievements.
- Players may hear recent realm news by speaking to town criers.

#### Dev-Facing
- Added a Virtual Ecology bridge from `TownChatterService` to `TownCrier` so achievement facts can be presented through the existing town crier shout flow.
- Added round-robin announcement selection with persisted rotation state in `TownChatter` persistence data version 4.
- Town criers now keep their auto-shout timer alive while eligible first-achievement announcements remain available.
- First-achievement announcements expire after 3 days and use in-world phrasing instead of out-of-character server terminology.

#### Config / Admin
- Existing diagnostics remain available through `[achfirsts` and `[TCFactLine`.
- No new feature flag or configuration file was added in this slice.

#### Verification
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors.

#### Risks / Notes
- Staff test characters still need staff server-first testing enabled before they can create eligible records.

### Modular Custom Admin UI

#### Player-Facing
- No direct player-facing changes.

#### Dev-Facing
- Added a registry-based custom admin hub under `UOContent/Custom/Systems/CustomAdmin/`.
- Added modular admin entries for Achievements, Rare Spawns, Custom Feature Flags, Virtual Ecology, and AI Integration.
- Existing admin gumps can now be opened from the shared `[CustomAdmin` / `[CAdmin` / `[AdminUI` command surface while future systems can register their own hub modules.
- Added a module contract that supports inline overview panels and linked legacy gumps, so systems can migrate away from standalone gump entry points incrementally.

#### Config / Admin
- New staff commands: `[CustomAdmin`, `[CAdmin`, and `[AdminUI`.
- Achievements, Rare Spawns, and Virtual Ecology are visible to Game Masters.
- Custom Feature Flags remains Administrator-only.

#### Verification
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors.

#### Risks / Notes
- The first slice preserves existing system-specific admin gumps and routes them through the shared hub instead of rewriting all admin controls into one large gump.

## 2026-06-16

### AI Integration and Virtual Ecology

#### Player-Facing
- Town NPCs can now share more varied local chatter and react to recent happenings in the world.
- NPC rumors may mention notable player events, dangerous encounters, and major achievements.

#### Dev-Facing
- Split reusable backend AI access into `UOContent/Custom/Systems/AIIntegration/`.
- Moved town-life gameplay behavior into `UOContent/Custom/Systems/VirtualEcology/`.
- Renamed town chatter classes to `TownChatterService`, `TownChatterCommands`, `TownChatterGump`, `TownChatterPersistence`, and `WorldFact`.
- Added a chatter-pool AI request profile for generating cached ambient lines while keeping factual server-event chatter template-based.
- Hooked achievement server-first changes and reported murder reports into Virtual Ecology fact tracking.
- Death rumors are throttled per character so players cannot flood the fact buffer by repeatedly dying.
- Server-first achievement rumors remain permanent but resync from the achievement system so reset or promoted claims stay truthful.

#### Config / Admin
- AI backend feature flag: `ai_integration`
- Town chatter commands include `[VirtualEcologyAdmin`, `[VEAdmin`, `[VEGump`, `[TownChatterAdmin`, `[TCGump`, `[TCFacts`, and `[TCFactLine`.
- Town chatter persistence key is `TownChatter`.
- Town chatter auto top-up now runs every 10 minutes and writes timestamped generation start/finish log entries.

#### Verification
- `dotnet build` passed with 0 warnings and 0 errors.

#### Risks / Notes
- Virtual Ecology currently depends on the AI backend only for generated ambient and dynamic chatter; factual server-event chatter uses local templates.
- The persistence key was intentionally renamed from the earlier AI town chatter name because this branch is not live.

## 2026-05-20

### Better Go Command

#### Player-Facing
- No direct player-facing changes.

#### Dev-Facing
- Added `BetterGoCommand` under `UOContent/Custom/Systems/BetterGoCommand/`.
- Replaced `[Go` with a custom staff destination gump that opens directly from the command.
- Added map filter buttons so staff can browse destinations on other facets without teleporting there first.
- Expanded Trammel/Felucca town destinations with direct stops for banks first, plus docks, stables, mage shops, and other useful town landmarks.
- Kept direct `[Go` shortcuts for target, serial, map, region, coordinate, and sextant travel.
- Preserved ModernUO's stock Go handler as `[GoLegacy]` instead of replacing the old implementation in place.
- Reuses legacy Go location data for non-town categories while substituting enhanced town data for Britannia towns.

#### Config / Admin
- Staff commands: `[Go`, `[BetterGo`, `[BGo`, and fallback `[GoLegacy]`.
- Access level remains `Counselor`.

#### Verification
- `dotnet build` passed with 0 warnings and 0 errors.

#### Risks / Notes
- Town point coordinates are curated static staff destinations and may need in-game adjustment if a shard's decoration or service layout differs from stock assumptions.

### Launch Systems, Missions, Travel, and Rare Spawns

#### Player-Facing
- Added mission board content with player progress and reward handling.
- Travel, seasonal presentation, and rare collectible systems have expanded support for future live events and shard content.

#### Dev-Facing
- Added mission system services, models, objectives, rewards, board gumps, persistence, and export support.
- Added launch audit commands and gump tooling for reviewing launch-readiness state.
- Added rare spawn import/export commands for spawn point backup and fresh-server restore workflows.
- Added TravelCodex location export support.
- Expanded achievement settings and achievement UI/service support for the new launch and mission workflows.
- Added travel restriction handling so blocked recall, gate, and related travel flows can be managed through the custom system.
- Added map season override controls for staff-run launch or seasonal presentation changes.

#### Config / Admin
- Added admin command surfaces for mission, launch audit, map season override, rare spawn import/export, and travel restriction workflows.
- Left `Distribution/Configuration/travelrestrictions.json` untracked so local travel restriction configuration does not get committed.

#### Verification
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors.

#### Risks / Notes
- This batch touches several custom systems plus travel spell helper behavior, so the validation build is the minimum gate before pushing `develop`.

## 2026-05-17

### Harvesting Automation

#### Player-Facing
- Mining, lumberjacking, and fishing can automatically continue harvesting the same targeted node after a successful harvest.
- Automation can continue through ordinary failed harvest attempts while the target remains valid.
- Auto-harvesting stops when it is no longer safe or useful to continue, such as depleted resources, movement, invalid targets, broken tools, death, disconnects, or heavy packs.
- Players receive messages when automation stops for depleted resources or weight concerns.
- Fishing automation stops immediately when a catch spawns a sea-serpent encounter.

#### Dev-Facing
- Added `HarvestingAutomationService` under `UOContent/Custom/Systems/HarvestingAutomation/`.
- Added `harvesting_automation` to `CustomFeatureFlagKeys`.
- Registered the `Harvesting Automation` feature flag in `CustomFeatureFlagBootstrap`.
- Added a scoped continuation hook to `HarvestSystem.FinishHarvesting`.
- Reuses existing harvest validation before each automated repeat, including tool, player, target, map, range, tile validation, and resource bank checks.
- Tracks successful resource delivery separately from failed harvest checks so automation can continue after ordinary failures without repeating after pack-full delivery failures.
- Applies automation to mining, lumberjacking, and fishing while excluding unsupported harvest systems.
- Preserves fishing SOS behavior by allowing nearby SOS fishing to continue even when the normal fish bank is empty.
- Stops fishing automation after serpent-trigger catch types: `TreasureMap`, `MessageInABottle`, and `SpecialFishingNet`.
- Auto-harvesting uses a 15-stone buffer below max weight before stopping for weight safety.

#### Config / Admin
- Feature flag: `harvesting_automation`
- Default state: enabled
- Category: `Custom Systems`

#### Verification
- `dotnet build UOContent\UOContent.csproj --output "G:\UO Emulation\ModernUO\build-temp\uocontent-harvest-auto"` passed with 0 warnings and 0 errors.

#### Risks / Notes
- Automation is intentionally conservative and only starts the next cycle after the normal harvest flow completes.
- The initial manual harvest behavior is unchanged; the new guards apply to automated continuation.
- Pack-full delivery failures still stop automation so the system does not repeatedly destroy harvested resources.
