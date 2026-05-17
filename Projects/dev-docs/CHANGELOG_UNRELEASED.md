# Unreleased Changes

## Harvesting Automation

### Player-Facing
- Mining, lumberjacking, and fishing can automatically continue harvesting the same targeted node after a successful harvest.
- Mining, lumberjacking, and fishing automation also continues after normal failed harvest attempts while the target remains valid and resources remain available.
- Auto-harvesting stops when the node runs out of resources, the player moves out of range, the target becomes invalid, the tool breaks, the feature flag is disabled, or the player disconnects or dies.
- Auto-harvesting stops before the player gets too close to their weight limit, using a 15-stone buffer below max weight.
- Players receive a message when automation stops because a harvest node is depleted.
- Players receive a message when automation stops to avoid becoming overweight.
- Fishing automation stops immediately when a catch spawns a sea-serpent encounter.

### Dev-Facing
- Added `HarvestingAutomationService` under `UOContent/Custom/Systems/HarvestingAutomation/`.
- Added `harvesting_automation` to `CustomFeatureFlagKeys`.
- Registered the `Harvesting Automation` feature flag in `CustomFeatureFlagBootstrap`.
- Added a scoped continuation hook to `HarvestSystem.FinishHarvesting`.
- Reuses existing harvest validation before each automated repeat, including tool, player, target, map, range, tile validation, and resource bank checks.
- Tracks successful resource delivery separately from failed harvest checks so automation can continue after ordinary failures without repeating after pack-full delivery failures.
- Applies automation to mining, lumberjacking, and fishing while excluding unsupported harvest systems.
- Preserves fishing SOS behavior by allowing nearby SOS fishing to continue even when the normal fish bank is empty.
- Stops fishing automation after serpent-trigger catch types: `TreasureMap`, `MessageInABottle`, and `SpecialFishingNet`.

### Config / Admin
- Feature flag: `harvesting_automation`
- Default state: enabled
- Category: `Custom Systems`

### Verification
- `dotnet build UOContent\UOContent.csproj --output "G:\UO Emulation\ModernUO\build-temp\uocontent-harvest-auto"` passed with 0 warnings and 0 errors.

### Risks / Notes
- Automation is intentionally conservative and only starts the next cycle after the normal harvest flow completes.
- The initial manual harvest behavior is unchanged; the new guards apply to automated continuation.
- Pack-full delivery failures still stop automation so the system does not repeatedly destroy harvested resources.
