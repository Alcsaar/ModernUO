using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Custom.Systems.AchievementSystem;

/* BEGIN ACHIEVEMENT ADMIN CONTROLS: staff gumps collect achievement testing commands behind buttons */
public enum AchievementAdminAchievementMode
{
    Grant,
    Remove
}

public sealed class AchievementAdminGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int ButtonOpenJournal = 1;
    private const int ButtonRefreshSelf = 2;
    private const int ButtonTargetReset = 3;
    private const int ButtonTargetGrant = 4;
    private const int ButtonTargetRemove = 5;
    private const int ButtonServerFirsts = 6;
    private const int ButtonToggleStaffFirsts = 7;
    private const int ButtonClearServerFirsts = 8;
    private const int ButtonResetAll = 9;
    private const int ButtonToggleSystem = 10;
    private const int GumpWidth = 520;
    private const int GumpHeight = 520;

    private readonly Mobile _from;

    public override bool Singleton => true;

    private AchievementAdminGump(Mobile from) : base(80, 60)
    {
        _from = from;
    }

    public static void DisplayTo(Mobile from)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        from.CloseGump<AchievementAdminGump>();
        from.SendGump(new AchievementAdminGump(from));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);
        builder.AddLabel(170, 22, HueTitle, "Achievement Admin Control");
        var status = AchievementService.GetSystemFlagStatus();
        var systemEnabled = status?.EffectiveEnabled == true;
        var hasAdminAccess = _from.AccessLevel >= AccessLevel.Administrator;
        builder.AddLabel(
            34,
            52,
            systemEnabled ? HueReady : HueMuted,
            $"System: {(systemEnabled ? "enabled" : "disabled")} (stored {(status?.StoredEnabled == true ? "on" : "off")})"
        );
        DrawRule(ref builder, 30, 78, 460);

        DrawButton(ref builder, 42, 104, ButtonToggleSystem, systemEnabled ? "Disable System" : "Enable System", hasAdminAccess);
        DrawButton(ref builder, 270, 104, ButtonOpenJournal, "Open My Journal", systemEnabled);
        DrawButton(ref builder, 42, 150, ButtonRefreshSelf, "Refresh My Progress", systemEnabled);
        DrawButton(ref builder, 270, 150, ButtonTargetGrant, "Grant Achievement", systemEnabled);
        DrawButton(ref builder, 42, 196, ButtonTargetRemove, "Remove Achievement", systemEnabled);
        DrawButton(ref builder, 270, 196, ButtonTargetReset, "Reset Player");

        builder.AddLabel(42, 252, HueHeader, "Administrator");
        DrawRule(ref builder, 42, 278, 430);
        DrawButton(ref builder, 42, 304, ButtonToggleStaffFirsts, "Toggle Staff Firsts", hasAdminAccess);
        DrawButton(ref builder, 270, 304, ButtonServerFirsts, "Server First Claims", hasAdminAccess);
        DrawButton(ref builder, 42, 350, ButtonClearServerFirsts, "Clear Firsts", hasAdminAccess);
        DrawButton(ref builder, 270, 350, ButtonResetAll, "Reset All Players", hasAdminAccess);

        builder.AddLabel(
            42,
            400,
            AchievementService.AllowStaffServerFirstsForTesting ? HueReady : HueMuted,
            AchievementService.AllowStaffServerFirstsForTesting ? "Staff firsts: on" : "Staff firsts: off"
        );

        if (!hasAdminAccess)
        {
            builder.AddLabel(270, 400, HueMuted, "Administrator access required");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        var systemEnabled = AchievementService.IsSystemEnabled();

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonToggleSystem:
                if (from.AccessLevel >= AccessLevel.Administrator)
                {
                    if (AchievementService.TryToggleSystemEnabled(from, out var failureReason))
                    {
                        from.SendMessage(
                            AchievementService.IsSystemEnabled() ? 0x35 : 0x22,
                            $"Achievement system is {(AchievementService.IsSystemEnabled() ? "enabled" : "disabled")}."
                        );
                    }
                    else
                    {
                        from.SendMessage(0x22, failureReason ?? "Unable to toggle achievement system.");
                    }
                }
                else
                {
                    from.SendMessage(0x22, "Administrator access is required to enable or disable the achievement system.");
                }

                DisplayTo(from);
                return;
            case ButtonOpenJournal:
                if (from is PlayerMobile player)
                {
                    AchievementService.DisplayAchievementGump(player);
                }
                return;
            case ButtonRefreshSelf:
                if (!systemEnabled)
                {
                    from.SendMessage(0x22, "Achievement system is disabled.");
                    DisplayTo(from);
                    return;
                }

                if (from is PlayerMobile refreshPlayer)
                {
                    AchievementService.EvaluatePlayer(refreshPlayer);
                    from.SendMessage(0x35, "Achievement progress refreshed.");
                }
                DisplayTo(from);
                return;
            case ButtonTargetReset:
                from.SendMessage("Target a player to reset achievements.");
                from.Target = new AchievementAdminPlayerTarget(AchievementAdminTargetAction.Reset);
                return;
            case ButtonTargetGrant:
                if (!systemEnabled)
                {
                    from.SendMessage(0x22, "Achievement system is disabled.");
                    DisplayTo(from);
                    return;
                }

                from.SendMessage("Target a player to choose an unearned achievement to grant.");
                from.Target = new AchievementAdminPlayerTarget(AchievementAdminTargetAction.Grant);
                return;
            case ButtonTargetRemove:
                if (!systemEnabled)
                {
                    from.SendMessage(0x22, "Achievement system is disabled.");
                    DisplayTo(from);
                    return;
                }

                from.SendMessage("Target a player to choose an earned achievement to remove.");
                from.Target = new AchievementAdminPlayerTarget(AchievementAdminTargetAction.Remove);
                return;
            case ButtonServerFirsts:
                if (from.AccessLevel >= AccessLevel.Administrator)
                {
                    AchievementAdminServerFirstGump.DisplayTo(from, 0);
                }
                else
                {
                    from.SendMessage(0x22, "Administrator access is required for server-first claims.");
                    DisplayTo(from);
                }
                return;
            case ButtonToggleStaffFirsts:
                if (from.AccessLevel >= AccessLevel.Administrator)
                {
                    AchievementService.AllowStaffServerFirstsForTesting =
                        !AchievementService.AllowStaffServerFirstsForTesting;
                    from.SendMessage(
                        0x35,
                        $"Staff server-first testing is {(AchievementService.AllowStaffServerFirstsForTesting ? "enabled" : "disabled")}."
                    );
                }
                DisplayTo(from);
                return;
            case ButtonClearServerFirsts:
                if (from.AccessLevel >= AccessLevel.Administrator)
                {
                    AchievementService.ResetServerFirstsForTesting(
                        out var clearedClaims,
                        out var clearedCandidateGroups,
                        out var clearedPlayerEntries
                    );

                    from.SendMessage(
                        0x35,
                        $"Cleared {clearedClaims} server-first claims, {clearedCandidateGroups} candidate groups, and {clearedPlayerEntries} player unlock/progress entries."
                    );
                }
                DisplayTo(from);
                return;
            case ButtonResetAll:
                if (from.AccessLevel >= AccessLevel.Administrator)
                {
                    AchievementService.ResetAllPlayers();
                    from.SendMessage(0x35, "Achievement state has been reset for all players.");
                }
                DisplayTo(from);
                return;
        }
    }

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label, bool enabled = true)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 36, y + 2, enabled ? HueText : HueMuted, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }
}

public sealed class AchievementAdminAchievementListGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int ButtonBack = 1;
    private const int ButtonPrevPage = 2;
    private const int ButtonNextPage = 3;
    private const int ButtonAchievementBase = 100;
    private const int GumpWidth = 760;
    private const int GumpHeight = 570;
    private const int EntriesPerPage = 7;

    private readonly Mobile _from;
    private readonly PlayerMobile _target;
    private readonly AchievementAdminAchievementMode _mode;
    private readonly int _pageIndex;
    private readonly List<AchievementDefinition> _definitions;

    public override bool Singleton => true;

    private AchievementAdminAchievementListGump(
        Mobile from,
        PlayerMobile target,
        AchievementAdminAchievementMode mode,
        int pageIndex
    ) : base(60, 45)
    {
        _from = from;
        _target = target;
        _mode = mode;
        _pageIndex = Math.Max(0, pageIndex);
        _definitions = BuildDefinitions(target, mode);
    }

    public static void DisplayTo(Mobile from, PlayerMobile target, AchievementAdminAchievementMode mode, int pageIndex)
    {
        if (from?.NetState == null || target == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        if (!AchievementService.IsSystemEnabled())
        {
            from.SendMessage(0x22, "Achievement system is disabled.");
            AchievementAdminGump.DisplayTo(from);
            return;
        }

        from.CloseGump<AchievementAdminAchievementListGump>();
        from.SendGump(new AchievementAdminAchievementListGump(from, target, mode, pageIndex));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);
        builder.AddLabel(280, 20, HueTitle, _mode == AchievementAdminAchievementMode.Grant ? "Grant Achievement" : "Remove Achievement");
        builder.AddLabel(34, 50, HueText, $"Target: {_target.Name}");
        builder.AddLabel(565, 50, HueMuted, $"{_definitions.Count} available");
        DrawRule(ref builder, 30, 78, 700);

        var totalPages = GetTotalPages(_definitions.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, _definitions.Count);
        var y = 104;

        if (_definitions.Count == 0)
        {
            builder.AddLabel(
                42,
                y,
                HueMuted,
                _mode == AchievementAdminAchievementMode.Grant
                    ? "This player has every registered achievement."
                    : "This player has no earned achievements to remove."
            );
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var definition = _definitions[i];
                DrawEntry(ref builder, definition, i, y);
                y += 58;
            }
        }

        DrawRule(ref builder, 30, 500, 700);
        builder.AddButton(42, 518, 4014, 4016, ButtonBack);
        builder.AddLabel(76, 520, HueText, "Back");
        builder.AddLabel(310, 520, HueText, $"Page {pageIndex + 1}/{Math.Max(1, totalPages)}");

        if (pageIndex > 0)
        {
            builder.AddButton(580, 518, 4014, 4016, ButtonPrevPage);
            builder.AddLabel(614, 520, HueText, "Prev");
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(660, 518, 4005, 4007, ButtonNextPage);
            builder.AddLabel(694, 520, HueText, "Next");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonBack:
                AchievementAdminGump.DisplayTo(from);
                return;
            case ButtonPrevPage:
                DisplayTo(from, _target, _mode, Math.Max(0, _pageIndex - 1));
                return;
            case ButtonNextPage:
                DisplayTo(from, _target, _mode, _pageIndex + 1);
                return;
        }

        var index = info.ButtonID - ButtonAchievementBase;

        if (index < 0 || index >= _definitions.Count)
        {
            return;
        }

        var definition = _definitions[index];

        if (_mode == AchievementAdminAchievementMode.Grant)
        {
            if (AchievementService.TryGrantAchievement(from, _target, definition.Id, out _, out var failureReason))
            {
                from.SendMessage(0x35, $"Granted {definition.Id} ({definition.Name}) to {_target.Name}.");
                _target.SendMessage(0x35, $"Achievement '{definition.Name}' was granted by staff.");
            }
            else
            {
                from.SendMessage(0x22, failureReason);
            }
        }
        else if (
            AchievementService.TryRemoveAchievement(
                from,
                _target,
                definition.Id,
                out _,
                out var removedUnlock,
                out var removedProgress,
                out var failureReason
            )
        )
        {
            var changedText = removedUnlock || removedProgress
                ? "Removed saved unlock/progress"
                : "No saved unlock/progress existed";

            from.SendMessage(0x35, $"{changedText} for {definition.Id} ({definition.Name}) on {_target.Name}.");
            _target.SendMessage(0x35, $"Achievement '{definition.Name}' was removed by staff.");
        }
        else
        {
            from.SendMessage(0x22, failureReason);
        }

        DisplayTo(from, _target, _mode, _pageIndex);
    }

    private void DrawEntry(ref DynamicGumpBuilder builder, AchievementDefinition definition, int index, int y)
    {
        var adminOnly = definition.TriggerType == AchievementTriggerType.ServerFirstSkillMilestone;
        var canUse = !adminOnly || _from.AccessLevel >= AccessLevel.Administrator;

        builder.AddImageTiled(42, y - 4, 650, 1, 5058);
        builder.AddButton(50, y + 13, _mode == AchievementAdminAchievementMode.Grant ? 4005 : 4017, _mode == AchievementAdminAchievementMode.Grant ? 4007 : 4019, ButtonAchievementBase + index);
        builder.AddLabel(88, y, canUse ? (_mode == AchievementAdminAchievementMode.Grant ? HueReady : HueHeader) : HueMuted, definition.Name);
        builder.AddLabel(520, y, HueMuted, adminOnly ? "Admin only" : AchievementService.GetScopeDisplayName(definition.Scope));
        builder.AddLabel(88, y + 24, HueText, definition.Id);
    }

    private static List<AchievementDefinition> BuildDefinitions(PlayerMobile target, AchievementAdminAchievementMode mode)
    {
        var allDefinitions = AchievementService.GetDefinitions(AchievementJournalView.Overview);
        var definitions = new List<AchievementDefinition>();

        for (var i = 0; i < allDefinitions.Count; i++)
        {
            var definition = allDefinitions[i];
            var unlocked = AchievementService.IsUnlocked(target, definition.Id);

            if (
                mode == AchievementAdminAchievementMode.Grant && !unlocked ||
                mode == AchievementAdminAchievementMode.Remove && unlocked
            )
            {
                definitions.Add(definition);
            }
        }

        return definitions;
    }

    private static int GetTotalPages(int count)
    {
        return Math.Max(1, (count + EntriesPerPage - 1) / EntriesPerPage);
    }

    private static int ClampPageIndex(int pageIndex, int totalPages)
    {
        return Math.Max(0, Math.Min(pageIndex, Math.Max(0, totalPages - 1)));
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }
}

public sealed class AchievementAdminServerFirstGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int ButtonBack = 1;
    private const int ButtonPrevPage = 2;
    private const int ButtonNextPage = 3;
    private const int ButtonResetBase = 100;
    private const int GumpWidth = 720;
    private const int GumpHeight = 520;
    private const int EntriesPerPage = 7;

    private readonly Mobile _from;
    private readonly int _pageIndex;
    private readonly List<AchievementServerFirstRecord> _records;

    public override bool Singleton => true;

    private AchievementAdminServerFirstGump(Mobile from, int pageIndex) : base(70, 55)
    {
        _from = from;
        _pageIndex = Math.Max(0, pageIndex);
        _records = AchievementService.GetServerFirstRecords();
    }

    public static void DisplayTo(Mobile from, int pageIndex)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.Administrator)
        {
            return;
        }

        from.CloseGump<AchievementAdminServerFirstGump>();
        from.SendGump(new AchievementAdminServerFirstGump(from, pageIndex));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);
        builder.AddLabel(258, 20, HueTitle, "Server First Claims");
        builder.AddLabel(34, 50, HueMuted, $"{_records.Count} claimed");
        DrawRule(ref builder, 30, 78, 660);

        var totalPages = GetTotalPages(_records.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, _records.Count);
        var y = 104;

        if (_records.Count == 0)
        {
            builder.AddLabel(42, y, HueMuted, "No server-first achievements have been claimed.");
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var record = _records[i];
                builder.AddImageTiled(42, y - 4, 620, 1, 5058);
                builder.AddButton(50, y + 13, 4017, 4019, ButtonResetBase + i);
                builder.AddLabel(88, y, HueHeader, record.SkillDisplayName);
                builder.AddLabel(310, y, HueText, record.PlayerName);
                builder.AddLabel(500, y, HueMuted, $"{record.AchievedUtc:yyyy-MM-dd}");
                builder.AddLabel(88, y + 24, HueMuted, record.AchievementId);
                y += 54;
            }
        }

        DrawRule(ref builder, 30, 450, 660);
        builder.AddButton(42, 468, 4014, 4016, ButtonBack);
        builder.AddLabel(76, 470, HueText, "Back");
        builder.AddLabel(292, 470, HueText, $"Page {pageIndex + 1}/{Math.Max(1, totalPages)}");

        if (pageIndex > 0)
        {
            builder.AddButton(540, 468, 4014, 4016, ButtonPrevPage);
            builder.AddLabel(574, 470, HueText, "Prev");
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(620, 468, 4005, 4007, ButtonNextPage);
            builder.AddLabel(654, 470, HueText, "Next");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.Administrator)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonBack:
                AchievementAdminGump.DisplayTo(from);
                return;
            case ButtonPrevPage:
                DisplayTo(from, Math.Max(0, _pageIndex - 1));
                return;
            case ButtonNextPage:
                DisplayTo(from, _pageIndex + 1);
                return;
        }

        var index = info.ButtonID - ButtonResetBase;

        if (index < 0 || index >= _records.Count)
        {
            return;
        }

        var record = _records[index];

        if (AchievementService.TryResetServerFirst(record.AchievementId, out var removed, out var promoted))
        {
            from.SendMessage(0x35, $"Reset {removed.AchievementId}, previously claimed by {removed.PlayerName}.");

            if (promoted != null)
            {
                from.SendMessage(0x35, $"Promoted next eligible claim: {promoted.PlayerName} - {promoted.SkillDisplayName}.");
            }
        }

        DisplayTo(from, _pageIndex);
    }

    private static int GetTotalPages(int count)
    {
        return Math.Max(1, (count + EntriesPerPage - 1) / EntriesPerPage);
    }

    private static int ClampPageIndex(int pageIndex, int totalPages)
    {
        return Math.Max(0, Math.Min(pageIndex, Math.Max(0, totalPages - 1)));
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }
}

internal enum AchievementAdminTargetAction
{
    Reset,
    Grant,
    Remove
}

internal sealed class AchievementAdminPlayerTarget : Target
{
    private readonly AchievementAdminTargetAction _action;

    public AchievementAdminPlayerTarget(AchievementAdminTargetAction action) : base(-1, false, TargetFlags.None)
    {
        _action = action;
    }

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (targeted is not PlayerMobile player)
        {
            from.SendMessage(0x22, "That is not a player.");
            return;
        }

        switch (_action)
        {
            case AchievementAdminTargetAction.Reset:
                AchievementService.ResetPlayer(player);
                from.SendMessage(0x35, $"Reset achievements for {player.Name}.");
                player.SendMessage(0x35, "Your achievements were reset by staff.");
                AchievementAdminGump.DisplayTo(from);
                return;
            case AchievementAdminTargetAction.Grant:
                if (!AchievementService.IsSystemEnabled())
                {
                    from.SendMessage(0x22, "Achievement system is disabled.");
                    AchievementAdminGump.DisplayTo(from);
                    return;
                }

                AchievementAdminAchievementListGump.DisplayTo(from, player, AchievementAdminAchievementMode.Grant, 0);
                return;
            case AchievementAdminTargetAction.Remove:
                if (!AchievementService.IsSystemEnabled())
                {
                    from.SendMessage(0x22, "Achievement system is disabled.");
                    AchievementAdminGump.DisplayTo(from);
                    return;
                }

                AchievementAdminAchievementListGump.DisplayTo(from, player, AchievementAdminAchievementMode.Remove, 0);
                return;
        }
    }
}
/* END ACHIEVEMENT ADMIN CONTROLS */
