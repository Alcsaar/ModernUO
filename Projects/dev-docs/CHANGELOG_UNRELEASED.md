# Unreleased Changes

## 2026-06-18

### Township System Core

#### Player-Facing
- Added the first township system slice so guilds can found a township, see township dashboard information, receive enter/leave boundary notifications, deposit gold or bank checks into a township treasury, and visualize town borders/founding points.
- Township activity levels now use the `Camp`, `Hamlet`, `Village`, `Town`, and `City` progression names.
- Guildmasters can now choose a unique township name during founding and rename the township later from the townstone.
- Township founding now sends online players a founded announcement gump with a roleplay-style location description such as nearby towns and dungeons.
- Townstone controls now use a more compact township-management layout with colored treasury/activity/destructive-action accents.
- Townstone navigation now separates overview, treasury, township control, upkeep, and activity views, with upkeep costs shown as red negative gold values.
- Guild members now receive a login warning gump when their township treasury has less than three days of estimated upkeep remaining.
- Townstone navigation now sits at the top of the gump, the overview uses a wider two-column layout, and the Activity page shows recent activity-gain entries.
- Expansion selections that do not meet the shared-border requirement now show a focused retry gump instead of the full purchase confirmation.
- Staff treasury adjustments now appear anonymously as `Staff member` in the player-facing treasury log, while CAdmin staff logs retain the staff character/account and internal note.
- Township treasury history and CAdmin staff logs now support paging, and staff log entries wrap longer details instead of truncating the reason/note.
- Player-facing township text now refers to the expansion limit as `Max Border Range` instead of `Envelope`.
- Renamed the player-facing treasury history to `Treasury Activity Log`, with positive entries shown as gold `+` values and negative entries shown as red `-` values.
- Townships can now enter a delinquent state when weekly upkeep cannot be paid; paid services are suspended, guild members receive urgent login warnings, and the townstone upkeep page exposes delinquency details.
- Non-guild players may donate to a delinquent township treasury from the townstone to help restore town services.
- The Treasury tab now links directly to delinquency details whenever a delinquent balance is shown.
- Non-guild players can no longer browse full townstone details; when a township is delinquent they see only a limited donation view, and public donations are capped to the remaining delinquent balance.
- Added a Services tab that shows paid township services, service status, service upkeep, and projected service refund values.
- Staff can now remove paid township services from the Services tab with a confirmation that shows purchase cost, upkeep reduction, and vested refund before removal.
- Removed paid services are now omitted from the active Services list while still contributing to the removed-services count.
- Township managers can now remove paid services from the Services tab with the same refund confirmation flow.
- Township managers can now purchase and place a township banker service from the Services tab.
- Township bankers stop offering bank access while their service is suspended by delinquency and are deleted when the service is removed.
- Township managers can now set paid-service NPC roam range from the Services tab; township bankers stay inside their anchored house region and do not open doors.
- Township managers can now move paid-service NPCs to another compatible township house or another valid spot inside the same house.
- Township banker NPC controls now live on the NPC itself through double-click or context-menu management, including rename, roam range, relocation, and gender controls.
- Township banker NPC management now includes the outfit-capable vendor customization menu for clothing, held items, hair, facial hair, and dye choices.
- Township banker context menus now use the player-vendor `Customize` label, and the NPC customization gump makes profanity-filtered rename controls explicit.
- Township banker gender changes now mirror player-vendor ordering, clear body-mod state before applying male/female human bodies, and refresh the changed mobile state.
- Township banker gender changes now validate gender-specific hair/facial-hair IDs and force a full nearby-client redraw to refresh body, hair, and clothing visuals.
- Township bankers now rely on the standard town-vendor invulnerability path instead of `Blessed`, preventing the yellow healthbar while keeping them protected.
- Township-owned service NPCs no longer appear grey/attackable solely because they are inside a private guild-member house.
- Township-owned service NPCs can no longer be ejected, banned, or bulk-moved out by house security and customization flows.
- Township managers can now use `[Township`, `[Town`, or `[TS` while standing inside their town to open the township gump without returning to the townstone.
- Paid services now leave the active Services list if their backing NPC is deleted or otherwise removed.
- Township banker NPCs now use the normal `the banker` title and show their township as `[ TownshipName ]` on single-click instead of relative danger labels.
- Townships can now purchase and place mage, alchemist, stablemaster, and innkeeper service NPCs from the Services tab.
- Active township innkeepers now allow anyone inside their anchored building to log out with inn-style no-delay rules, while preserving combat/criminal restrictions.
- Added a Perks tab for township-wide upgrades, starting with a Town Militia perk and a Hunting Bonus perk.
- Township managers can purchase Town Militia status, maintaining mounted militia patrols while the perk is active and paid.
- Township managers can purchase a Hunting Bonus perk that generates treasury gold from eligible guild-member monster kills without removing gold from the corpse or hunter.
- Players now receive a system message showing how much bonus treasury gold was generated when the Hunting Bonus applies to their kill.
- Town Militia guards now actively pursue criminals and hostile monsters near their patrols instead of using standard instant town-guard behavior.
- Town Militia guards chase threats only within the township's max border range and recall back into claimed town land if they reach or leave that envelope.
- Town Militia guards use randomized horse mounts, are mortal, generate no loot, and use immovable equipment so they cannot be farmed for gear.
- Town Militia guards are now substantially stronger, detect threats from farther away, and all nearby patrols that can see an alerted criminal or monster will respond instead of only the nearest guard.
- Town Militia guards now force active movement speed as soon as they acquire or are alerted to a combat target.
- Town Militia guards now use more player-like survivability, with reduced HP/damage, self-bandaging, and limited cure/heal/refresh potion-style recovery.
- Town Militia guard spawn and recall points now prefer spread-out patrol locations, with a wider home patrol radius to reduce guard clustering.
- Townstone navigation now uses two rows of full labels so the added Services, Perks, Upkeep, and Activity tabs have room to render clearly.
- Township managers can now disable and re-enable purchased perks from the Perks tab without removing the purchase record.
- Low-value monster kills that produce a positive Hunting Bonus now generate at least 1 gp, making small bonus rates visible during normal testing.
- Staff characters who are actual members of the owning guild are now eligible to generate Hunting Bonus revenue like normal guild members.
- Hunting Bonus now uses a staff-configured global percentage instead of a guildmaster-set town rate.
- NPC escorts can now choose active townships as escort destinations, and successful township escorts generate a separate 50% reward bonus into the township treasury without reducing the escorter's pay.
- Automated township treasury revenue such as Hunting Bonus and escort revenue now condenses into daily treasury activity rows with expandable contribution details instead of spamming one visible row per event.
- Township treasury contribution details now word escort rows as the player escorting the NPC and hunting rows as the killed monster type instead of the monster's individual display name.
- Township-owned service vendor purchases now contribute a configurable share of the sale total to the township treasury and appear in the daily NPC vendor revenue treasury entry with purchased item quantities in the contribution details.
- Township treasury contribution rows now include a per-entry details view so long NPC vendor purchase lists can be reviewed without clipping.
- Added generic township ranks: Citizen, Aide, Officer, Councilor, Regent, and automatic Governor for the guildmaster.
- All township guild members can now view township overview, treasury, logs, upkeep, activity, services, perks, borders, and founding-point information, while rank permissions control destructive or treasury-spending actions.
- Township Regents and Governors can manage township ranks from the Control page, and perk enable/disable controls are restricted to Regent/Governor access.
- Township rank management now uses an explicit rank picker and confirmation gump, and all players can view a rank-permissions reference from the Control page.

#### Dev-Facing
- Added `Projects/UOContent/Custom/Systems/Townships/` with township state, persistence, deed/stone items, flexible claimed-tile row ranges, founding validation, expansion preview/confirmation, activity tracking, upkeep scaffolding, and staff activity logging.
- Expansion claims support two-corner rectangle targeting, skip invalid tiles, require shared edge contact, enforce max envelopes centered on the original founding point, and respect non-guild house footprint plus buffer blockers.
- Township visualizations use client-side location effects only, with perimeter rendering and no spawned marker items, so markers do not affect pathing, line of sight, placement, or combat.
- Township names are profanity-checked using the guild naming rules and must be unique.
- Added township abolish flow with confirmation and cleanup scaffolding for future township-owned objects.
- Border visualization now refreshes while the viewer moves so client-side effect borders reappear as the visible screen changes.
- Border visualization now uses a configurable wider render radius around the viewer for zoomed-out ClassicUO testing while clipping packets to avoid sending the full perimeter every refresh.
- Added shared upkeep-days estimation used by the townstone upkeep page and the guild-member login warning.
- Activity gains from player entry are now recorded in township logs so players and staff can review what recently improved township activity.
- Founding now defaults the max expansion envelope to the first configured size larger than the initial claim, so a 50x50 claim starts with a 75x75 expansion envelope when using the default settings.
- Expansion preview markers now refresh while the player moves through the expansion flow and clear when the flow is cancelled or completed.
- Expansion preview markers now use a shorter configurable duration than regular border markers, reducing stale visuals after purchase, cancel, or retry.
- Added runtime `TownshipRegion` registration rebuilt from compressed claimed-land rectangles while keeping the township claim overlay as the authoritative territory model.
- Added persisted township financial status, delinquent balance, delinquency start/removal-check timestamps, and paid-service suspension state.
- Added the paid-service framework for future township bankers, vendors, guards, moongates, stables, and perks, including persisted service records, purchase cost, daily upkeep, lifecycle state, refund basis, and service-created object tracking.
- Township daily upkeep now includes paid-service upkeep as well as land upkeep.
- Delinquent townships now suspend paid services and can remove the highest-upkeep paid services after the configured grace period, applying vested delinquency refunds to the township treasury.
- Paid service removal now marks the service removed, stops its upkeep contribution, applies the vested refund to the treasury, records treasury activity, and writes a staff/audit log entry.
- Added a serialized `TownshipBanker` mobile tied to a township/service id so paid-service lifecycle state controls the banker and township cleanup can delete it by serial.
- Paid-service records now persist NPC home, roam range, and anchor-house serial, and township service ticks reconcile missing backing mobiles.
- Paid-service NPC relocation now updates the service home and anchor-house serial so later roaming remains constrained to the new house.
- Removed NPC roam/move controls from the townstone Services row to keep the townstone focused on service status, cost, refund, and removal.
- Reused the outfit-capable vendor customization gump for township bankers with township-manager permission checks and a township NPC rename action.
- Township banker context menu labeling now uses the player-vendor customization cliloc instead of generic management labels that can render incorrectly on some clients.
- Township banker runtime normalization clears legacy `Blessed`/yellow-healthbar state on construction and deserialization.
- Township paid-service NPC gender changes now send an incoming-mobile refresh packet after updating the human body and appearance fields.
- Added township-owned mobile exceptions to private-house notoriety, access, ban, kick, and house-customization relocation checks.
- Added a township-owned mobile exception to relative-threat single-click labels so service NPCs can use township identity labels instead of combat danger labels.
- Generalized township service NPC management/customization so banker, mage, alchemist, stablemaster, and innkeeper services share rename, appearance, roam, relocation, cleanup, and delinquency lifecycle behavior.
- Added a HouseRegion logout-delay hook for active township innkeeper services anchored to that house.
- Township regions no longer act as standard guarded regions; the Town Militia perk now uses custom mounted patrol guards and local criminal/aggression alerts.
- Added persisted hunting bonus configuration scaffolding with retained legacy fields for save compatibility.
- Added a monster corpse-gold bonus hook that generates configured guild-member hunting revenue into the township treasury without changing lootable corpse gold.
- Added a reusable custom world-location description utility for roleplay-friendly landmark descriptions outside the township system.
- Added persisted township treasury contribution details behind aggregated treasury activity rows so future leaderboard/reporting work can calculate player contribution totals by source.
- Added township service-vendor purchase revenue accounting that records successful player purchases only after payment and delivery, skips staff-free purchases, and requires the active service vendor to still be inside its township claim.
- Persisted full treasury contribution details separately from the compact summary note, preserving complete NPC vendor purchase lists while keeping aggregate log rows short.
- Added persisted township rank assignments with explicit permission gates for service NPC management, service/perk purchases, service removal, expansion, moving the charter, renaming, rank management, and abolishing.
- The `[Township`, `[Town`, and `[TS` commands now open the township gump for any guild member standing inside their township, not only managers.
- Township rank changes now audit the actor, target, previous rank, and new rank, and staff uses the same Governor/Regent Control-page rank UI instead of a separate staff-only rank tool.
- Added persisted township patrol guard tracking with automatic spawn, replacement, and cleanup tied to Town Militia perk state.
- Added a distinct disabled paid-service state so manually disabled perks are not restored by delinquency recovery and do not contribute daily upkeep while disabled.
- Hunting Bonus generation now handles single eligible looters with zero recorded damage as a full-share kill, improving compatibility with staff/testing kill flows.
- Hunting Bonus eligibility now uses actual guild membership instead of staff access, preventing unrelated staff from generating bonus revenue for a township they only have administrative access to.
- Hunting Bonus damage shares use eligible looter damage totals so mixed guild/non-guild kills only generate bonus gold from the guild member's share of the kill.
- Scoped private-house bypasses to township NPCs while they are inside houses that belong to their township, preventing unrelated township-owned objects or off-township placements from inheriting house exemptions.
- Hardened township service purchases and treasury accounting so unsupported service types cannot debit treasury funds before failing, and repeated/generated treasury revenue cannot overflow stored balances.

#### Config / Admin
- Added township settings under `Distribution/Configuration/Townships/settings.json`.
- Added CAdmin module and commands `[TownshipAdmin` / `[TSAdmin`, `[TownshipDeed` / `[TSDeed`, and `[TownshipInfo` / `[TSInfo`.
- Township settings include deed cost, initial claim size, house buffer, edge contact requirement, tile cost, marker durations/hues/item IDs, Felucca-only gating, upkeep toggle, grace days, and land upkeep rate.
- Township settings and CAdmin controls now include `VendorRevenueContributionPercent`, defaulting to 10%, for the share of township service-vendor purchase totals credited to the treasury.
- Township admin now supports selecting active townships, teleporting to the selected township, opening its townstone gump, reviewing recent staff logs, manually adjusting treasury balances, and tuning border render range.
- Township admin treasury adjustments now accept separate player-facing and staff-only notes.
- Added township staff test commands `[TSTestActivity` / `[TownshipTestActivity` and `[TSTestTreasury` / `[TownshipTestTreasury`.
- Added staff-only townstone tools for clearing treasury balance, clearing treasury history, clearing staff/activity logs, setting/adjusting activity score, and refreshing township regions/border viewers. Destructive tools require confirmation and are staff-audited.
- Moved the townstone staff-tools shortcut out of the main navigation row and added staff-log access directly from the staff tools page.
- Adjusted the townstone staff tools layout so staff log and activity adjustment controls no longer overlap.
- Moved treasury adjustment out of the CAdmin township module and into the townstone staff tools page with a confirmation gump.
- Restricted clearing the township staff/activity log to Owner access; lower staff can still view the staff log and use non-Owner staff tools.
- Removed the redundant staff shortcut from the staff tools page and relabeled the Owner-only clear-log action as `Clear Staff Log` to make it easier to find.
- Moved the Owner-only `Clear Staff Log` action into the township staff log gump and simplified the main staff tools action list.
- Added a CAdmin upkeep-enabled toggle, moved scheduled upkeep charging to 6 PM server time, and added a staff townstone debug button to set the next upkeep charge time.
- Staff-triggered upkeep treasury entries now appear anonymously as `Staff member` in the player-facing treasury activity log.
- The staff next-charge debug prompt now gives explicit server-time formatting examples and processes due-or-overdue charge times immediately instead of waiting for the next township tick.
- The townstone Upkeep tab now shows the last recorded upkeep payment and a clearer current weekly cost breakdown.
- Added a confirmed staff townstone action for clearing the lifetime deposits total without changing treasury balance or treasury activity history.
- Township upkeep now accrues daily and settles weekly, so land changes affect future daily assessments instead of recalculating the entire weekly payment from the current claim size.
- Upkeep wording now distinguishes daily assessments, accrued due, next daily assessment, and next weekly payment.
- Added capped, vested service-refund configuration for future paid township services, including CAdmin controls for refund cap, voluntary/delinquency refund percentages, and vesting milestones.
- Township activity-gain log entries now merge repeated triggers from the same player within a configurable 24-hour window to reduce activity log spam.
- Added confirmed staff townstone controls for setting or clearing a township delinquent balance.
- Added CAdmin settings for delinquency grace days and delinquent service-removal check interval.
- Added staff-only placeholder service creation from the Services tab and `[TownshipTestService` / `[TSTestService` for testing service upkeep, suspension, refund, and delinquency-removal behavior before real NPC services are added.
- Added staff-only paid-service removal controls with server-side staff validation and confirmation.
- Added CAdmin controls for banker purchase cost and banker daily upkeep.
- Added CAdmin controls for mage, alchemist, stablemaster, and innkeeper service purchase costs and daily upkeep.
- Added CAdmin controls for Town Militia and Hunting Bonus perk costs/upkeep and the global Hunting Bonus percentage.
- Added a CAdmin control for the number of militia patrol guards maintained by townships.
- Added a player-facing township management command with shorthand aliases that opens the township gump only for users with township management access while inside claimed township land.
- Split the CAdmin township configuration form into compact General, Refunds, Services, and Perks pages while preserving selected-township review and teleport/open/log actions.

#### Verification
- `dotnet build` passed with 0 warnings and 0 errors.
- Build was not rerun after the latest admin-panel and border-render-range edits because live testing was in progress.
- `dotnet build` passed again after the townstone navigation, activity logging, default envelope, and expansion preview fixes.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after the expansion retry and preview-marker-duration changes. A full root build was blocked only by the running `ModernUO` process locking `Distribution\Assemblies\UOContent.dll`.
- `dotnet build` passed with 0 warnings and 0 errors after the treasury adjustment audit/logging updates.
- `dotnet build` passed with 0 warnings and 0 errors after adding township staff test commands.
- `dotnet build` passed with 0 warnings and 0 errors after adding paged/wrapped township log views.
- `dotnet build` passed with 0 warnings and 0 errors after adding staff-only townstone tools and max-border-range wording.
- `dotnet build` passed with 0 warnings and 0 errors after repositioning staff tools and adding townstone staff-log access.
- `dotnet build` passed with 0 warnings and 0 errors after the staff tools layout fix.
- `dotnet build` passed with 0 warnings and 0 errors after moving treasury adjustment to townstone staff tools and restricting staff-log clearing.
- `dotnet build` passed with 0 warnings and 0 errors after removing the staff-page shortcut overlap and clarifying the Owner-only staff-log clear action.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after moving staff-log clearing into the staff log gump. A full root build was blocked only by the running `ModernUO` process locking `Distribution\Assemblies\UOContent.dll`.
- `dotnet build` passed with 0 warnings and 0 errors after the upkeep toggle, 6 PM upkeep scheduling, force-payment action, treasury activity formatting, and activity-log merge changes.
- `dotnet build` passed with 0 warnings and 0 errors after replacing force upkeep payment with the next-charge debug prompt and anonymizing staff-triggered upkeep treasury entries.
- `dotnet build` passed with 0 warnings and 0 errors after adding the upkeep payment summary, cost breakdown, and lifetime-deposits staff clear action.
- `dotnet build` passed with 0 warnings and 0 errors after adding capped, vested township service-refund settings and CAdmin controls.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after adding township delinquency state, warnings, public delinquency donations, CAdmin timing controls, and the staff delinquency setter.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after adding the Treasury-tab delinquency details link.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after restricting non-guild townstone access and capping public delinquency donations.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after adding the paid-service framework, Services tab, service upkeep, and service delinquency-removal scaffolding.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after adding staff paid-service removal and refund confirmation.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after hiding removed services from the active Services list.
- `dotnet build Projects\UOContent\UOContent.csproj -p:OutDir=..\..\tmp-build\UOContent\` passed with 0 warnings and 0 errors after adding township-manager service removal, banker purchase/placement, township banker suspension, and banker service settings.
- `dotnet build` passed with 0 warnings and 0 errors after adding house-anchored township banker roaming and missing-NPC service reconciliation.
- `dotnet build` passed with 0 warnings and 0 errors after adding township manager paid-service NPC relocation.
- `dotnet build` passed with 0 warnings and 0 errors after moving township banker NPC controls off the townstone and onto the NPC management gump.
- `dotnet build` passed with 0 warnings and 0 errors after adding township banker vendor-appearance customization support.
- `dotnet build` passed with 0 warnings and 0 errors after updating township banker customization labels and rename access.
- `dotnet build` passed with 0 warnings and 0 errors after fixing township banker gender application and replacing blessed invulnerability with vendor-style invulnerability.
- `dotnet build` passed with 0 warnings and 0 errors after forcing township service NPC redraws on gender change.
- `dotnet build` passed with 0 warnings and 0 errors after restoring township bankers to the outfit-capable vendor customization gump.
- `dotnet build` passed with 0 warnings and 0 errors after excluding township-owned service NPCs from private-house grey flagging and house ejection/ban flows.
- `dotnet build` passed with 0 warnings and 0 errors after adding the player-facing township gump command.
- `dotnet build` passed with 0 warnings and 0 errors after adding township service NPC identity labels and relative-threat label exclusion.
- `dotnet build` passed with 0 warnings and 0 errors after adding mage, alchemist, stablemaster, and innkeeper township services.
- `dotnet build` passed with 0 warnings and 0 errors after adding township perks, Town Militia behavior, and Hunting Bonus contributions.
- `dotnet build` passed with 0 warnings and 0 errors after adding Town Militia patrol guards.
- `dotnet build` passed with 0 warnings and 0 errors after adding Hunting Bonus notifications.
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors after adding reusable location descriptions, township founded announcements, township escort destinations, escort treasury revenue, and condensed automated treasury contribution logs.
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors after cleaning up escort and hunting contribution detail wording.
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors after adding township ranks, rank persistence, rank management controls, and rank-based permission gates.
- `dotnet build Projects\UOContent\UOContent.csproj` passed with 0 warnings and 0 errors after adding rank-permission reference UI, explicit rank selection/confirmation, improved rank-change audit details, and removing duplicate staff rank controls.

#### Risks / Notes
- The v1 slice scaffolds upkeep and costs, but tile/upgrade costs are still free by default and no NPC service removal hierarchy exists yet.
- Township deed vendor sale integration is not included in this slice; staff can create deeds for testing with `[TownshipDeed`.

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
