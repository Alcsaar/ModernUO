using System;
using System.Collections.Generic;
using Server.Network;
using Server.Spells;

namespace Server.Gumps;

public sealed class TravelRestrictionsGump : DynamicGump
{
    private const int Width = 670;
    private const int Height = 520;
    private const int LeftX = 24;
    private const int LeftY = 54;
    private const int LeftWidth = 250;
    private const int RightX = 300;
    private const int RightY = 54;
    private const int RowHeight = 20;
    private const int SelectRuleButtonBase = 100;
    private const int ToggleTypeButtonBase = 1000;
    private const int AllowButtonId = 4005;
    private const int AllowButtonPressedId = 4007;
    private const int BlockButtonId = 4017;
    private const int BlockButtonPressedId = 4019;

    private readonly int _selectedRule;

    public override bool Singleton => true;

    public TravelRestrictionsGump(int selectedRule = 0) : base(50, 40)
    {
        _selectedRule = selectedRule;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var rules = TravelRestrictionSystem.Rules;
        var selectedRule = NormalizeRuleIndex(_selectedRule, rules.Count);

        builder.AddPage();
        builder.AddBackground(0, 0, Width, Height, 5054);
        builder.AddImageTiled(10, 10, Width - 20, Height - 20, 2624);
        builder.AddAlphaRegion(10, 10, Width - 20, Height - 20);

        builder.AddLabel(24, 18, 0x481, "Travel Restrictions");
        builder.AddButton(470, 18, 4005, 4007, 1);
        builder.AddLabel(505, 18, 0x44, "Reload");
        builder.AddButton(570, 18, 4017, 4019, 2);
        builder.AddLabel(605, 18, 0x20, "Reset");

        builder.AddLabel(LeftX, 36, 0x35, "Rules");
        builder.AddLabel(RightX, 36, 0x35, "Selected Rule");

        RenderRuleList(ref builder, rules, selectedRule);

        if (rules.Count > 0)
        {
            RenderRuleEditor(ref builder, rules[selectedRule], selectedRule);
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        var selectedRule = NormalizeRuleIndex(_selectedRule, TravelRestrictionSystem.Rules.Count);

        switch (info.ButtonID)
        {
            case 0:
                return;
            case 1:
                TravelRestrictionSystem.Load();
                from.SendMessage("Travel restrictions reloaded.");
                break;
            case 2:
                TravelRestrictionSystem.ResetToDefaults();
                from.SendMessage("Travel restrictions reset to defaults.");
                selectedRule = 0;
                break;
            case 3:
                TravelRestrictionSystem.ToggleRuleEnabled(selectedRule);
                break;
            default:
                HandleRuleButton(info.ButtonID, ref selectedRule);
                break;
        }

        from.SendGump(new TravelRestrictionsGump(selectedRule));
    }

    private static void RenderRuleList(
        ref DynamicGumpBuilder builder,
        IReadOnlyList<TravelRestrictionRuleConfig> rules,
        int selectedRule
    )
    {
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var y = LeftY + i * RowHeight;
            var selected = i == selectedRule;

            builder.AddButton(
                LeftX,
                y + 2,
                selected ? AllowButtonId : 4006,
                selected ? AllowButtonPressedId : 4007,
                SelectRuleButtonBase + i
            );

            builder.AddLabel(LeftX + 30, y, selected ? 0x44 : rule.Enabled ? 0x480 : 0x3B1, rule.Name);
            builder.AddLabel(LeftX + LeftWidth - 42, y, rule.Enabled ? 0x44 : 0x20, rule.Enabled ? "On" : "Off");
        }
    }

    private static void RenderRuleEditor(ref DynamicGumpBuilder builder, TravelRestrictionRuleConfig rule, int ruleIndex)
    {
        builder.AddLabel(RightX, RightY, rule.Enabled ? 0x480 : 0x3B1, rule.Name);

        builder.AddButton(
            RightX,
            RightY + 30,
            rule.Enabled ? AllowButtonId : BlockButtonId,
            rule.Enabled ? AllowButtonPressedId : BlockButtonPressedId,
            3
        );
        builder.AddLabel(RightX + 35, RightY + 28, rule.Enabled ? 0x44 : 0x20, rule.Enabled ? "Rule enabled" : "Rule disabled");

        builder.AddLabel(RightX, RightY + 64, 0x35, "Allowed Travel");

        var y = RightY + 92;
        AddPermissionRow(ref builder, y, "Recall", ruleIndex, TravelCheckType.RecallFrom, rule.RecallFrom);
        AddPermissionRow(ref builder, y + 24, "", ruleIndex, TravelCheckType.RecallTo, rule.RecallTo);

        y += 58;
        AddPermissionRow(ref builder, y, "Gate", ruleIndex, TravelCheckType.GateFrom, rule.GateFrom);
        AddPermissionRow(ref builder, y + 24, "", ruleIndex, TravelCheckType.GateTo, rule.GateTo);

        y += 58;
        AddPermissionRow(ref builder, y, "Mark", ruleIndex, TravelCheckType.Mark, rule.Mark);

        y += 44;
        AddPermissionRow(ref builder, y, "Teleport", ruleIndex, TravelCheckType.TeleportFrom, rule.TeleportFrom);
        AddPermissionRow(ref builder, y + 24, "", ruleIndex, TravelCheckType.TeleportTo, rule.TeleportTo);
    }

    private static void AddPermissionRow(
        ref DynamicGumpBuilder builder,
        int y,
        string group,
        int ruleIndex,
        TravelCheckType type,
        bool allowed
    )
    {
        if (group.Length > 0)
        {
            builder.AddLabel(RightX, y, 0x481, group);
        }

        builder.AddLabel(RightX + 86, y, 0x480, GetDirectionLabel(type));
        builder.AddButton(
            RightX + 190,
            y + 2,
            allowed ? AllowButtonId : BlockButtonId,
            allowed ? AllowButtonPressedId : BlockButtonPressedId,
            ToggleTypeButtonBase + ruleIndex * 10 + (int)type
        );
        builder.AddLabel(RightX + 225, y, allowed ? 0x44 : 0x20, allowed ? "Allowed" : "Blocked");
    }

    private static void HandleRuleButton(int buttonId, ref int selectedRule)
    {
        if (buttonId >= SelectRuleButtonBase && buttonId < ToggleTypeButtonBase)
        {
            selectedRule = buttonId - SelectRuleButtonBase;
            return;
        }

        if (buttonId < ToggleTypeButtonBase)
        {
            return;
        }

        var offset = buttonId - ToggleTypeButtonBase;
        var ruleIndex = offset / 10;
        var type = (TravelCheckType)(offset % 10);

        if (TravelRestrictionSystem.ToggleRule(ruleIndex, type))
        {
            selectedRule = ruleIndex;
        }
    }

    private static int NormalizeRuleIndex(int ruleIndex, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return Math.Clamp(ruleIndex, 0, count - 1);
    }

    private static string GetDirectionLabel(TravelCheckType type) =>
        type switch
        {
            TravelCheckType.RecallFrom   => "From area",
            TravelCheckType.RecallTo     => "To area",
            TravelCheckType.GateFrom     => "From area",
            TravelCheckType.GateTo       => "To area",
            TravelCheckType.Mark         => "Mark rune",
            TravelCheckType.TeleportFrom => "From area",
            TravelCheckType.TeleportTo   => "To area",
            _                            => type.ToString()
        };
}
