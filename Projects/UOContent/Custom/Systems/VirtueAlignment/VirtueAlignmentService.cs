using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ModernUO.CodeGeneratedEvents;
using Server.Commands;
using Server.Custom.Systems.CustomFeatureFlags;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Custom.Systems.VirtueAlignment;

public static class VirtueAlignmentService
{
    private const int MinimumExpressedTendency = 25;
    private const int MaxTendencyValue = 10000;
    private const int MinorOppositionShockThreshold = 25;
    private const int ModerateOppositionShockThreshold = 50;
    private const int MajorOppositionShockThreshold = 100;
    private const int MinorOppositionShockPercent = 125;
    private const int ModerateOppositionShockPercent = 150;
    private const int MajorOppositionShockPercent = 200;

    private static readonly Dictionary<PlayerMobile, VirtueAlignmentProfile> _profiles = new();

    private static readonly int[] _convictionRankThresholds =
    [
        0,
        250,
        1000,
        3000,
        7500,
        15000
    ];

    public static void Configure()
    {
        VirtueAlignmentSettings.Configure();
        VirtueAlignmentPersistence.Configure();
        VirtueAlignmentCommands.Configure();
        CustomFeatureFlagManager.Configure();
        CustomFeatureFlagManager.Register(
            CustomFeatureFlagKeys.VirtueAlignment,
            "Virtue Alignment",
            "Player aspirations and deed-driven virtue and vice expression.",
            "Custom Systems",
            defaultEnabled: true
        );
    }

    public static bool IsEnabled() =>
        VirtueAlignmentSettings.Enabled && CustomFeatureFlagManager.IsEnabled(CustomFeatureFlagKeys.VirtueAlignment);

    public static bool StockVirtuesEnabled => VirtueAlignmentSettings.StockVirtuesEnabled;

    public static IReadOnlyList<VirtueAlignmentPath> Virtues { get; } =
    [
        VirtueAlignmentPath.Compassion,
        VirtueAlignmentPath.Justice,
        VirtueAlignmentPath.Honesty,
        VirtueAlignmentPath.Honor,
        VirtueAlignmentPath.Spirituality,
        VirtueAlignmentPath.Valor,
        VirtueAlignmentPath.Sacrifice,
        VirtueAlignmentPath.Humility
    ];

    public static IReadOnlyList<VirtueAlignmentPath> Vices { get; } =
    [
        VirtueAlignmentPath.Cruelty,
        VirtueAlignmentPath.Vengeance,
        VirtueAlignmentPath.Deceit,
        VirtueAlignmentPath.Treachery,
        VirtueAlignmentPath.Corruption,
        VirtueAlignmentPath.Cowardice,
        VirtueAlignmentPath.Greed,
        VirtueAlignmentPath.Pride
    ];

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile pm) => _profiles.Remove(pm);

    public static VirtueAlignmentProfile GetProfile(PlayerMobile player) =>
        player != null && _profiles.TryGetValue(player, out var profile) ? profile : null;

    public static VirtueAlignmentProfile GetOrCreateProfile(PlayerMobile player)
    {
        if (player == null)
        {
            return null;
        }

        ref var profile = ref CollectionsMarshal.GetValueRefOrAddDefault(_profiles, player, out var exists);

        if (!exists)
        {
            profile = new VirtueAlignmentProfile { Player = player };
        }

        return profile;
    }

    public static bool TrySetAspirations(
        PlayerMobile player,
        VirtueAlignmentPath primary,
        VirtueAlignmentPath secondary,
        Mobile actor,
        bool staffOverride,
        out string reason
    )
    {
        reason = null;

        if (player == null)
        {
            reason = "No player was selected.";
            return false;
        }

        if (!IsEnabled())
        {
            reason = "The Virtue Alignment system is disabled.";
            return false;
        }

        if (!HasOppositeSides(primary, secondary))
        {
            reason = "Choose one Virtue and one Vice. A primary Virtue requires a secondary Vice, and a primary Vice requires a secondary Virtue.";
            return false;
        }

        if (IsCounterpartPair(primary, secondary))
        {
            reason = "Your secondary path cannot be the direct counterpart to your primary path.";
            return false;
        }

        var profile = GetOrCreateProfile(player);

        if (profile.HasAspirations && !staffOverride && !VirtueAlignmentSettings.AllowPlayerReselection)
        {
            reason = "Your aspirations are already chosen.";
            return false;
        }

        profile.PrimaryAspiration = primary;
        profile.SecondaryAspiration = secondary;
        profile.AspirationsChosenAt = Core.Now;
        profile.AspirationsChosenBy = actor?.Name ?? "System";
        return true;
    }

    public static bool TrySetAlignment(
        PlayerMobile player,
        VirtueAlignmentPath primary,
        VirtueAlignmentPath secondary,
        Mobile actor,
        bool staffOverride,
        out string reason
    ) => TrySetAspirations(player, primary, secondary, actor, staffOverride, out reason);

    public static bool ClearAspirations(PlayerMobile player, Mobile actor, out string reason)
    {
        reason = null;

        if (player == null)
        {
            reason = "No player was selected.";
            return false;
        }

        if (!_profiles.TryGetValue(player, out var profile) || !profile.HasAspirations)
        {
            reason = "That player does not have aspirations selected.";
            return false;
        }

        profile.PrimaryAspiration = VirtueAlignmentPath.None;
        profile.SecondaryAspiration = VirtueAlignmentPath.None;
        profile.AspirationsChosenAt = DateTime.MinValue;
        profile.AspirationsChosenBy = actor?.Name ?? "System";
        return true;
    }

    public static bool ClearAlignment(PlayerMobile player, Mobile actor, out string reason) =>
        ClearAspirations(player, actor, out reason);

    public static bool ClearTendencies(PlayerMobile player, out string reason)
    {
        reason = null;

        if (player == null)
        {
            reason = "No player was selected.";
            return false;
        }

        var profile = GetOrCreateProfile(player);
        EnsureTendencyStorage(profile);
        Array.Clear(profile.Tendencies);
        return true;
    }

    public static bool ResetConviction(PlayerMobile player, out string reason)
    {
        reason = null;

        if (player == null)
        {
            reason = "No player was selected.";
            return false;
        }

        var profile = GetOrCreateProfile(player);
        profile.Conviction = 0;
        return true;
    }

    /*
     * Conviction is global progression. It should be awarded by meaningful deeds
     * and missions, while tendency scores decide how the character is expressed.
     */
    public static bool AwardConviction(PlayerMobile player, int amount, string reasonText, Mobile source, out string reason)
    {
        reason = null;

        if (player == null)
        {
            reason = "No player was selected.";
            return false;
        }

        if (!IsEnabled())
        {
            reason = "The Virtue Alignment system is disabled.";
            return false;
        }

        if (amount == 0)
        {
            reason = "Conviction amount cannot be zero.";
            return false;
        }

        var profile = GetOrCreateProfile(player);
        var oldRank = GetConvictionRank(profile.Conviction);
        profile.Conviction = Math.Max(0, profile.Conviction + amount);
        var newRank = GetConvictionRank(profile.Conviction);

        if (newRank > oldRank)
        {
            player.SendMessage(0x35, $"Your Conviction has grown. You are now {GetConvictionRankName(newRank)}.");
        }

        return true;
    }

    /*
     * Central tendency entry point for future gameplay hooks. Actions should call
     * this instead of mutating profile scores directly so caps, decay, and audit
     * behavior can be added in one place.
     */
    public static bool AwardTendency(
        PlayerMobile player,
        VirtueAlignmentPath path,
        int amount,
        VirtueAlignmentActionKind actionKind,
        Mobile source,
        out string reason
    )
    {
        reason = null;

        if (player == null)
        {
            reason = "No player was selected.";
            return false;
        }

        if (!IsEnabled())
        {
            reason = "The Virtue Alignment system is disabled.";
            return false;
        }

        if (GetSide(path) == VirtueAlignmentSide.None)
        {
            reason = "Choose a Virtue or Vice path.";
            return false;
        }

        if (amount == 0)
        {
            reason = "Tendency amount cannot be zero.";
            return false;
        }

        var profile = GetOrCreateProfile(player);
        EnsureTendencyStorage(profile);

        var originalAmount = amount;
        var index = (int)path;

        if (amount > 0)
        {
            amount = ApplyOppositionShock(profile, path, amount);
        }

        var nextValue = (long)profile.Tendencies[index] + amount;
        profile.Tendencies[index] = (int)Math.Clamp(nextValue, 0, MaxTendencyValue);

        SendTendencyAwardMessage(player, path, originalAmount);
        return true;
    }

    /*
     * Virtue/Vice counterparts are a balance, not two independent totals. An
     * opposing act first erodes the existing counterpart, and the erosion becomes
     * harsher when the character is already strongly established on that side.
     */
    private static int ApplyOppositionShock(VirtueAlignmentProfile profile, VirtueAlignmentPath path, int amount)
    {
        var counterpart = GetCounterpart(path);
        var counterpartIndex = (int)counterpart;
        var counterpartScore = profile.Tendencies[counterpartIndex];

        if (counterpartScore <= 0)
        {
            return amount;
        }

        var shockPercent = GetOppositionShockPercent(counterpartScore);
        var erosionCapacity = (int)Math.Min(MaxTendencyValue, Math.Max(1, (long)amount * shockPercent / 100));
        var erosion = Math.Min(counterpartScore, erosionCapacity);

        profile.Tendencies[counterpartIndex] -= erosion;

        var consumedAmount = Math.Max(1, (erosion * 100 + shockPercent - 1) / shockPercent);
        return Math.Max(0, amount - consumedAmount);
    }

    private static int GetOppositionShockPercent(int counterpartScore)
    {
        if (counterpartScore >= MajorOppositionShockThreshold)
        {
            return MajorOppositionShockPercent;
        }

        if (counterpartScore >= ModerateOppositionShockThreshold)
        {
            return ModerateOppositionShockPercent;
        }

        if (counterpartScore >= MinorOppositionShockThreshold)
        {
            return MinorOppositionShockPercent;
        }

        return 100;
    }

    public static bool RecordPlayerResurrection(PlayerMobile healer, PlayerMobile resurrected)
    {
        if (healer == null || resurrected == null || healer == resurrected)
        {
            return false;
        }

        if (!AwardTendency(
                healer,
                VirtueAlignmentPath.Compassion,
                25,
                VirtueAlignmentActionKind.CompassionateAid,
                resurrected,
                out _
            ))
        {
            return false;
        }

        AwardConviction(healer, 10, "resurrecting another player", resurrected, out _);
        return true;
    }

    public static bool RecordPetResurrection(PlayerMobile healer, BaseCreature pet)
    {
        if (healer == null || pet == null || pet.ControlMaster == healer)
        {
            return false;
        }

        if (!AwardTendency(
                healer,
                VirtueAlignmentPath.Compassion,
                10,
                VirtueAlignmentActionKind.CompassionateAid,
                pet.ControlMaster,
                out _
            ))
        {
            return false;
        }

        AwardConviction(healer, 4, "resurrecting another player's pet", pet.ControlMaster, out _);
        return true;
    }

    public static bool RecordReportedMurder(PlayerMobile murderer, PlayerMobile victim)
    {
        if (murderer == null || victim == null || murderer == victim)
        {
            return false;
        }

        if (!AwardTendency(
                murderer,
                VirtueAlignmentPath.Cruelty,
                40,
                VirtueAlignmentActionKind.CruelAct,
                victim,
                out _
            ))
        {
            return false;
        }

        AwardConviction(murderer, 8, "being reported for murder", victim, out _);
        return true;
    }

    public static bool IsValidPair(VirtueAlignmentPath primary, VirtueAlignmentPath secondary)
    {
        return HasOppositeSides(primary, secondary) && !IsCounterpartPair(primary, secondary);
    }

    public static bool HasOppositeSides(VirtueAlignmentPath primary, VirtueAlignmentPath secondary)
    {
        var primarySide = GetSide(primary);
        var secondarySide = GetSide(secondary);

        return primarySide != VirtueAlignmentSide.None &&
               secondarySide != VirtueAlignmentSide.None &&
               primarySide != secondarySide;
    }

    public static bool IsCounterpartPair(VirtueAlignmentPath primary, VirtueAlignmentPath secondary) =>
        GetCounterpart(primary) == secondary;

    public static VirtueAlignmentPath GetCounterpart(VirtueAlignmentPath path) =>
        path switch
        {
            VirtueAlignmentPath.Compassion => VirtueAlignmentPath.Cruelty,
            VirtueAlignmentPath.Cruelty => VirtueAlignmentPath.Compassion,
            VirtueAlignmentPath.Justice => VirtueAlignmentPath.Vengeance,
            VirtueAlignmentPath.Vengeance => VirtueAlignmentPath.Justice,
            VirtueAlignmentPath.Honesty => VirtueAlignmentPath.Deceit,
            VirtueAlignmentPath.Deceit => VirtueAlignmentPath.Honesty,
            VirtueAlignmentPath.Honor => VirtueAlignmentPath.Treachery,
            VirtueAlignmentPath.Treachery => VirtueAlignmentPath.Honor,
            VirtueAlignmentPath.Spirituality => VirtueAlignmentPath.Corruption,
            VirtueAlignmentPath.Corruption => VirtueAlignmentPath.Spirituality,
            VirtueAlignmentPath.Valor => VirtueAlignmentPath.Cowardice,
            VirtueAlignmentPath.Cowardice => VirtueAlignmentPath.Valor,
            VirtueAlignmentPath.Sacrifice => VirtueAlignmentPath.Greed,
            VirtueAlignmentPath.Greed => VirtueAlignmentPath.Sacrifice,
            VirtueAlignmentPath.Humility => VirtueAlignmentPath.Pride,
            VirtueAlignmentPath.Pride => VirtueAlignmentPath.Humility,
            _ => VirtueAlignmentPath.None
        };

    public static VirtueAlignmentSide GetSide(VirtueAlignmentPath path) =>
        path switch
        {
            VirtueAlignmentPath.Compassion or
                VirtueAlignmentPath.Justice or
                VirtueAlignmentPath.Honesty or
                VirtueAlignmentPath.Honor or
                VirtueAlignmentPath.Spirituality or
                VirtueAlignmentPath.Valor or
                VirtueAlignmentPath.Sacrifice or
                VirtueAlignmentPath.Humility => VirtueAlignmentSide.Virtue,
            VirtueAlignmentPath.Cruelty or
                VirtueAlignmentPath.Vengeance or
                VirtueAlignmentPath.Deceit or
                VirtueAlignmentPath.Treachery or
                VirtueAlignmentPath.Corruption or
                VirtueAlignmentPath.Cowardice or
                VirtueAlignmentPath.Greed or
                VirtueAlignmentPath.Pride => VirtueAlignmentSide.Vice,
            _ => VirtueAlignmentSide.None
        };

    public static string GetDisplayName(VirtueAlignmentPath path) =>
        path == VirtueAlignmentPath.None ? "None" : path.ToString();

    /*
     * Reuses the stock virtue gump icon IDs from VirtueGump. Vice paths intentionally
     * share the corresponding virtue symbol until custom vice art exists.
     */
    public static int GetIconGumpId(VirtueAlignmentPath path) =>
        path switch
        {
            VirtueAlignmentPath.Compassion or VirtueAlignmentPath.Cruelty => 105,
            VirtueAlignmentPath.Honesty or VirtueAlignmentPath.Deceit => 106,
            VirtueAlignmentPath.Honor or VirtueAlignmentPath.Treachery => 107,
            VirtueAlignmentPath.Humility or VirtueAlignmentPath.Pride => 108,
            VirtueAlignmentPath.Justice or VirtueAlignmentPath.Vengeance => 109,
            VirtueAlignmentPath.Sacrifice or VirtueAlignmentPath.Greed => 110,
            VirtueAlignmentPath.Spirituality or VirtueAlignmentPath.Corruption => 111,
            VirtueAlignmentPath.Valor or VirtueAlignmentPath.Cowardice => 112,
            _ => 0
        };

    public static int GetIconHue(VirtueAlignmentPath path) =>
        GetSide(path) == VirtueAlignmentSide.Vice ? 33 : 68;

    public static string GetShortDescription(VirtueAlignmentPath path) =>
        path switch
        {
            VirtueAlignmentPath.Compassion => "Love of others, mercy, and care for those in need.",
            VirtueAlignmentPath.Justice => "Truth tempered by Love; protection, fairness, and rightful judgment.",
            VirtueAlignmentPath.Honesty => "Respect for Truth, plain dealing, and refusing false advantage.",
            VirtueAlignmentPath.Honor => "Courage to seek and uphold Truth through worthy conduct.",
            VirtueAlignmentPath.Spirituality => "Inner balance of Truth, Love, and Courage within the self and world.",
            VirtueAlignmentPath.Valor => "Courage to stand against danger and accept meaningful risk.",
            VirtueAlignmentPath.Sacrifice => "Courage to give of oneself in the name of Love.",
            VirtueAlignmentPath.Humility => "Freedom from conceit and awareness of one's place among others.",
            VirtueAlignmentPath.Cruelty => "Power through indifference to suffering and the denial of mercy.",
            VirtueAlignmentPath.Vengeance => "Judgment replaced by personal retribution and unpaid grudges.",
            VirtueAlignmentPath.Deceit => "Falsehood, misdirection, and the useful mask over the true face.",
            VirtueAlignmentPath.Treachery => "Broken oaths, betrayal, and advantage taken through false loyalty.",
            VirtueAlignmentPath.Corruption => "The inward distortion of Truth, Love, and Courage into self-serving rot.",
            VirtueAlignmentPath.Cowardice => "Survival by avoidance, retreat, and refusal of worthy risk.",
            VirtueAlignmentPath.Greed => "Taking, hoarding, and valuing possession above shared need.",
            VirtueAlignmentPath.Pride => "Self-exaltation, contempt for limits, and blindness to one's place.",
            _ => "No path selected."
        };

    public static string GetLongDescription(VirtueAlignmentPath path) =>
        path switch
        {
            VirtueAlignmentPath.Compassion =>
                "Compassion is Love of others. A character who follows Compassion may define themselves by mercy, rescue, healing, charity, and restraint when another life is in their hands.",
            VirtueAlignmentPath.Justice =>
                "Justice is Truth tempered by Love. It favors protection of the innocent and judgment that answers wrongdoing without becoming personal vengeance.",
            VirtueAlignmentPath.Honesty =>
                "Honesty is respect for Truth. It is the path of plain dealing, recovered trust, clear testimony, and rejecting profit gained through lies.",
            VirtueAlignmentPath.Honor =>
                "Honor is Courage to seek and uphold Truth. It is oath, reputation, worthy contest, and the discipline to win without lowering oneself.",
            VirtueAlignmentPath.Spirituality =>
                "Spirituality seeks Truth, Love, and Courage from within and from the world around. It is the inward balance that keeps the other virtues from becoming hollow forms.",
            VirtueAlignmentPath.Valor =>
                "Valor is Courage to stand against risk. It is the chosen step toward danger when fear, comfort, or hesitation would pull one away.",
            VirtueAlignmentPath.Sacrifice =>
                "Sacrifice is Courage to give oneself in the name of Love. It accepts loss, cost, and burden so that another person or purpose may endure.",
            VirtueAlignmentPath.Humility =>
                "Humility strips away conceit. It is not weakness, but a clear understanding that worth is not measured only by fame, victory, or possession.",
            VirtueAlignmentPath.Cruelty =>
                "Cruelty is the rejection of Compassion. It treats suffering as a tool, a lesson, or a deserved consequence rather than a call to mercy.",
            VirtueAlignmentPath.Vengeance =>
                "Vengeance is Justice turned inward and sharpened by injury. It seeks repayment, not balance, and may call any punishment righteous if the wound is deep enough.",
            VirtueAlignmentPath.Deceit =>
                "Deceit is the rejection of Honesty. It values masks, hidden meanings, selective truths, and the victory of the clever lie over the open hand.",
            VirtueAlignmentPath.Treachery =>
                "Treachery is Honor broken deliberately. It is betrayal under cover of trust, the false oath, and the blade drawn from the expected ally.",
            VirtueAlignmentPath.Corruption =>
                "Corruption is Spirituality inverted. It warps inner balance into hunger, obsession, and decay while still pretending to serve a higher purpose.",
            VirtueAlignmentPath.Cowardice =>
                "Cowardice is Valor denied. It is not ordinary fear, but the repeated choice to let others bear the risk that one's own path demands.",
            VirtueAlignmentPath.Greed =>
                "Greed is the refusal of Sacrifice. It gathers, keeps, and consumes beyond need, placing possession above duty, fellowship, and cost to others.",
            VirtueAlignmentPath.Pride =>
                "Pride is the absence that Humility answers. It mistakes achievement for worth, rank for wisdom, and self-regard for truth.",
            _ => "No path selected."
        };

    private static void SendTendencyAwardMessage(
        PlayerMobile player,
        VirtueAlignmentPath path,
        int amount
    )
    {
        var side = GetSide(path);
        var hue = side == VirtueAlignmentSide.Vice ? 0x22 : 0x35;
        var absoluteAmount = Math.Abs(amount);

        if (absoluteAmount == 0)
        {
            return;
        }

        player.SendMessage(hue, GetTendencyAwardMessage(path, amount, absoluteAmount));
    }

    private static string GetTendencyAwardMessage(VirtueAlignmentPath path, int amount, int absoluteAmount)
    {
        var magnitude = GetTendencyChangeMagnitude(absoluteAmount);

        if (amount < 0)
        {
            return $"You feel {magnitude} change as your connection to {GetDisplayName(path)} recedes.";
        }

        return GetSide(path) == VirtueAlignmentSide.Vice
            ? $"You sense {magnitude} shadow of {GetDisplayName(path)} settling over your deeds."
            : $"You feel {magnitude} change drawing your deeds toward {GetDisplayName(path)}.";
    }

    private static string GetTendencyChangeMagnitude(int amount)
    {
        if (amount >= 100)
        {
            return "a profound";
        }

        if (amount >= 50)
        {
            return "a significant";
        }

        if (amount >= 25)
        {
            return "a moderate";
        }

        if (amount >= 10)
        {
            return "a minor";
        }

        return "a faint";
    }

    public static int GetTendency(PlayerMobile player, VirtueAlignmentPath path)
    {
        var profile = GetProfile(player);

        if (profile == null || GetSide(path) == VirtueAlignmentSide.None)
        {
            return 0;
        }

        EnsureTendencyStorage(profile);
        return profile.Tendencies[(int)path];
    }

    public static int GetConviction(PlayerMobile player) => GetProfile(player)?.Conviction ?? 0;

    public static VirtueConvictionRank GetConvictionRank(PlayerMobile player) =>
        GetConvictionRank(GetConviction(player));

    public static VirtueConvictionRank GetConvictionRank(int conviction)
    {
        var rank = VirtueConvictionRank.Unproven;

        for (var i = 0; i < _convictionRankThresholds.Length; i++)
        {
            if (conviction < _convictionRankThresholds[i])
            {
                break;
            }

            rank = (VirtueConvictionRank)i;
        }

        return rank;
    }

    public static string GetConvictionRankName(VirtueConvictionRank rank) =>
        rank switch
        {
            VirtueConvictionRank.Unproven => "Unproven",
            VirtueConvictionRank.Initiate => "an Initiate",
            VirtueConvictionRank.Seeker => "a Seeker",
            VirtueConvictionRank.Follower => "a Follower",
            VirtueConvictionRank.Adept => "an Adept",
            VirtueConvictionRank.Exemplar => "an Exemplar",
            _ => "Unproven"
        };

    public static int GetNextConvictionThreshold(PlayerMobile player)
    {
        var conviction = GetConviction(player);

        for (var i = 0; i < _convictionRankThresholds.Length; i++)
        {
            if (conviction < _convictionRankThresholds[i])
            {
                return _convictionRankThresholds[i];
            }
        }

        return 0;
    }

    public static VirtueAlignmentPath GetExpressedVirtue(PlayerMobile player)
    {
        var profile = GetProfile(player);
        return GetExpressedPath(profile, VirtueAlignmentSide.Virtue);
    }

    public static VirtueAlignmentPath GetExpressedVice(PlayerMobile player)
    {
        var profile = GetProfile(player);
        return GetExpressedPath(profile, VirtueAlignmentSide.Vice);
    }

    public static VirtueAlignmentPath GetPrimaryAspiration(PlayerMobile player) =>
        GetProfile(player)?.PrimaryAspiration ?? VirtueAlignmentPath.None;

    public static VirtueAlignmentPath GetSecondaryAspiration(PlayerMobile player) =>
        GetProfile(player)?.SecondaryAspiration ?? VirtueAlignmentPath.None;

    private static VirtueAlignmentPath GetExpressedPath(VirtueAlignmentProfile profile, VirtueAlignmentSide side)
    {
        if (profile == null)
        {
            return VirtueAlignmentPath.None;
        }

        EnsureTendencyStorage(profile);

        var paths = side == VirtueAlignmentSide.Virtue ? Virtues : Vices;
        var bestPath = VirtueAlignmentPath.None;
        var bestScore = 0;

        for (var i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            var score = profile.Tendencies[(int)path];

            if (score > bestScore)
            {
                bestScore = score;
                bestPath = path;
            }
        }

        if (bestPath != VirtueAlignmentPath.None && bestScore >= MinimumExpressedTendency)
        {
            return bestPath;
        }

        return VirtueAlignmentPath.None;
    }

    public static string GetAspirationSummary(PlayerMobile player)
    {
        var profile = GetProfile(player);

        if (profile?.HasAspirations != true)
        {
            return "No aspirations declared.";
        }

        return $"Aspires toward {GetDisplayName(profile.PrimaryAspiration)}, tempered by {GetDisplayName(profile.SecondaryAspiration)}.";
    }

    public static string GetExpressionSummary(PlayerMobile player)
    {
        var profile = GetProfile(player);

        if (profile == null)
        {
            return "No deeds recorded.";
        }

        var expressedVirtue = GetExpressedPath(profile, VirtueAlignmentSide.Virtue);
        var expressedVice = GetExpressedPath(profile, VirtueAlignmentSide.Vice);

        if (expressedVirtue != VirtueAlignmentPath.None && expressedVice != VirtueAlignmentPath.None)
        {
            return $"Expresses {GetDisplayName(expressedVirtue)} and {GetDisplayName(expressedVice)}.";
        }

        if (expressedVirtue != VirtueAlignmentPath.None)
        {
            return $"Expresses {GetDisplayName(expressedVirtue)}; no Vice has emerged.";
        }

        if (expressedVice != VirtueAlignmentPath.None)
        {
            return $"Expresses {GetDisplayName(expressedVice)}; no Virtue has emerged.";
        }

        return "No clear Virtue or Vice has emerged.";
    }

    public static string GetSummary(PlayerMobile player)
    {
        var profile = GetProfile(player);

        if (profile == null)
        {
            return "No aspirations or deeds recorded.";
        }

        return $"{GetExpressionSummary(player)} {GetAspirationSummary(player)}";
    }

    private static void EnsureTendencyStorage(VirtueAlignmentProfile profile)
    {
        var requiredLength = Enum.GetValues<VirtueAlignmentPath>().Length;

        if (profile.Tendencies is { Length: var length } && length == requiredLength)
        {
            return;
        }

        var next = new int[requiredLength];

        if (profile.Tendencies != null)
        {
            var copyLength = Math.Min(profile.Tendencies.Length, next.Length);
            Array.Copy(profile.Tendencies, next, copyLength);
        }

        profile.Tendencies = next;
    }

    public static void Serialize(IGenericWriter writer)
    {
        var usedCount = 0;

        foreach (var (_, profile) in _profiles)
        {
            if (profile.Player?.Deleted == false && (profile.HasAspirations || HasAnyTendency(profile)))
            {
                usedCount++;
            }
        }

        writer.WriteEncodedInt(usedCount);

        foreach (var (player, profile) in _profiles)
        {
            if (player?.Deleted != false || (!profile.HasAspirations && !HasAnyTendency(profile)))
            {
                continue;
            }

            EnsureTendencyStorage(profile);

            writer.Write(player);
            writer.WriteEncodedInt((int)profile.PrimaryAspiration);
            writer.WriteEncodedInt((int)profile.SecondaryAspiration);
            writer.Write(profile.AspirationsChosenAt);
            writer.Write(profile.AspirationsChosenBy);
            writer.WriteEncodedInt(profile.Conviction);
            writer.WriteEncodedInt(profile.Tendencies.Length);

            for (var i = 0; i < profile.Tendencies.Length; i++)
            {
                writer.WriteEncodedInt(profile.Tendencies[i]);
            }
        }
    }

    private static bool HasAnyTendency(VirtueAlignmentProfile profile)
    {
        EnsureTendencyStorage(profile);

        for (var i = 0; i < profile.Tendencies.Length; i++)
        {
            if (profile.Tendencies[i] > 0)
            {
                return true;
            }
        }

        return false;
    }

    public static void Deserialize(IGenericReader reader, int version)
    {
        _profiles.Clear();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var player = reader.ReadEntity<PlayerMobile>();
            var primary = (VirtueAlignmentPath)reader.ReadEncodedInt();
            var secondary = (VirtueAlignmentPath)reader.ReadEncodedInt();
            var chosenAt = reader.ReadDateTime();
            var chosenBy = reader.ReadString();
            var conviction = 0;
            int[] tendencies = null;

            if (version >= 1)
            {
                conviction = reader.ReadEncodedInt();
                var tendencyCount = reader.ReadEncodedInt();
                tendencies = new int[Math.Max(0, tendencyCount)];

                for (var j = 0; j < tendencyCount; j++)
                {
                    tendencies[j] = reader.ReadEncodedInt();
                }
            }

            if (player == null || !IsValidPair(primary, secondary))
            {
                continue;
            }

            var profile = new VirtueAlignmentProfile
            {
                Player = player,
                PrimaryAspiration = primary,
                SecondaryAspiration = secondary,
                AspirationsChosenAt = chosenAt,
                AspirationsChosenBy = chosenBy,
                Conviction = conviction
            };

            if (tendencies != null)
            {
                EnsureTendencyStorage(profile);
                var copyLength = Math.Min(tendencies.Length, profile.Tendencies.Length);

                for (var j = 0; j < copyLength; j++)
                {
                    profile.Tendencies[j] = Math.Clamp(tendencies[j], 0, MaxTendencyValue);
                }
            }

            _profiles[player] = profile;
        }
    }
}

public static class VirtueAlignmentCommands
{
    public static void Configure()
    {
        CommandSystem.Register("VirtueAlignment", AccessLevel.Player, VirtueAlignment_OnCommand);
        CommandSystem.Register("VA", AccessLevel.Player, VirtueAlignment_OnCommand);
        CommandSystem.Register("VirtuePath", AccessLevel.Player, VirtueAlignment_OnCommand);
        CommandSystem.Register("VP", AccessLevel.Player, VirtueAlignment_OnCommand);
        CommandSystem.Register("VirtueAlignmentClear", AccessLevel.GameMaster, VirtueAlignmentClear_OnCommand);
        CommandSystem.Register("VAClear", AccessLevel.GameMaster, VirtueAlignmentClear_OnCommand);
        CommandSystem.Register("VirtueAlignmentSet", AccessLevel.GameMaster, VirtueAlignmentSet_OnCommand);
        CommandSystem.Register("VASet", AccessLevel.GameMaster, VirtueAlignmentSet_OnCommand);
        CommandSystem.Register("VirtueAlignmentTendency", AccessLevel.GameMaster, VirtueAlignmentTendency_OnCommand);
        CommandSystem.Register("VATendency", AccessLevel.GameMaster, VirtueAlignmentTendency_OnCommand);
        CommandSystem.Register("VirtueAlignmentResetTendencies", AccessLevel.GameMaster, VirtueAlignmentResetTendencies_OnCommand);
        CommandSystem.Register("VAResetTendencies", AccessLevel.GameMaster, VirtueAlignmentResetTendencies_OnCommand);
        CommandSystem.Register("VirtueAlignmentConviction", AccessLevel.GameMaster, VirtueAlignmentConviction_OnCommand);
        CommandSystem.Register("VAConviction", AccessLevel.GameMaster, VirtueAlignmentConviction_OnCommand);
        CommandSystem.Register("VirtueAlignmentResetConviction", AccessLevel.GameMaster, VirtueAlignmentResetConviction_OnCommand);
        CommandSystem.Register("VAResetConviction", AccessLevel.GameMaster, VirtueAlignmentResetConviction_OnCommand);
    }

    private static void VirtueAlignment_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is PlayerMobile player)
        {
            VirtueAlignmentGump.DisplayTo(player);
        }
    }

    private static void VirtueAlignmentClear_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            return;
        }

        if (!VirtueAlignmentService.ClearAspirations(player, e.Mobile, out var reason))
        {
            e.Mobile.SendMessage(0x22, reason);
            return;
        }

        e.Mobile.SendMessage(0x35, "Your Virtue Alignment aspirations have been cleared.");
    }

    private static void VirtueAlignmentSet_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            return;
        }

        if (e.Length < 2 ||
            !Enum.TryParse(e.GetString(0), true, out VirtueAlignmentPath primary) ||
            !Enum.TryParse(e.GetString(1), true, out VirtueAlignmentPath secondary))
        {
            e.Mobile.SendMessage(0x22, "Usage: [VASet <primary aspiration> <secondary aspiration>");
            return;
        }

        if (!VirtueAlignmentService.TrySetAspirations(player, primary, secondary, e.Mobile, true, out var reason))
        {
            e.Mobile.SendMessage(0x22, reason);
            return;
        }

        e.Mobile.SendMessage(0x35, $"Virtue Alignment aspirations set: {VirtueAlignmentService.GetAspirationSummary(player)}");
    }

    private static void VirtueAlignmentTendency_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            return;
        }

        if (e.Length < 2 ||
            !Enum.TryParse(e.GetString(0), true, out VirtueAlignmentPath path) ||
            !int.TryParse(e.GetString(1), out var amount))
        {
            e.Mobile.SendMessage(0x22, "Usage: [VATendency <path> <amount>");
            return;
        }

        if (!VirtueAlignmentService.AwardTendency(
                player,
                path,
                amount,
                VirtueAlignmentActionKind.Staff,
                e.Mobile,
                out var reason
            ))
        {
            e.Mobile.SendMessage(0x22, reason);
            return;
        }

        e.Mobile.SendMessage(
            0x35,
            $"{VirtueAlignmentService.GetDisplayName(path)} tendency is now {VirtueAlignmentService.GetTendency(player, path)}."
        );
    }

    private static void VirtueAlignmentResetTendencies_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            return;
        }

        if (!VirtueAlignmentService.ClearTendencies(player, out var reason))
        {
            e.Mobile.SendMessage(0x22, reason);
            return;
        }

        e.Mobile.SendMessage(0x35, "Your Virtue Alignment tendency scores have been reset.");
    }

    private static void VirtueAlignmentConviction_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            return;
        }

        if (e.Length < 1 || !int.TryParse(e.GetString(0), out var amount))
        {
            e.Mobile.SendMessage(0x22, "Usage: [VAConviction <amount>");
            return;
        }

        if (!VirtueAlignmentService.AwardConviction(player, amount, "staff adjustment", e.Mobile, out var reason))
        {
            e.Mobile.SendMessage(0x22, reason);
            return;
        }

        var rank = VirtueAlignmentService.GetConvictionRank(player);
        e.Mobile.SendMessage(
            0x35,
            $"Conviction is now {VirtueAlignmentService.GetConviction(player)} ({VirtueAlignmentService.GetConvictionRankName(rank)})."
        );
    }

    private static void VirtueAlignmentResetConviction_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile player)
        {
            return;
        }

        if (!VirtueAlignmentService.ResetConviction(player, out var reason))
        {
            e.Mobile.SendMessage(0x22, reason);
            return;
        }

        e.Mobile.SendMessage(0x35, "Your Virtue Alignment Conviction has been reset.");
    }
}
