using Server.Network;
using Server.Spells;

namespace Server.Gumps;

public sealed class TravelRestrictionsGump : DynamicGump
{
    private const int Width = 760;
    private const int HeaderY = 45;
    private const int RowStartY = 72;
    private const int RowHeight = 22;
    private const int NameX = 28;
    private const int EnabledX = 205;
    private const int RuleStartX = 280;
    private const int RuleWidth = 68;
    private const int ToggleButtonId = 4005;
    private const int ToggleButtonPressedId = 4007;
    private const int DisabledButtonId = 4017;
    private const int DisabledButtonPressedId = 4019;

    public override bool Singleton => true;

    public TravelRestrictionsGump() : base(50, 40)
    {
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var rules = TravelRestrictionSystem.Rules;
        var height = RowStartY + rules.Count * RowHeight + 62;

        builder.AddPage();
        builder.AddBackground(0, 0, Width, height, 5054);
        builder.AddImageTiled(10, 10, Width - 20, height - 20, 2624);
        builder.AddAlphaRegion(10, 10, Width - 20, height - 20);

        builder.AddLabel(24, 18, 0x481, "Travel Restrictions");
        builder.AddButton(560, 18, 4005, 4007, 1);
        builder.AddLabel(595, 18, 0x44, "Reload");
        builder.AddButton(655, 18, 4017, 4019, 2);
        builder.AddLabel(690, 18, 0x20, "Reset");

        builder.AddLabel(NameX, HeaderY, 0x35, "Rule");
        builder.AddLabel(EnabledX, HeaderY, 0x35, "On");

        var travelTypes = TravelRestrictionSystem.TravelTypes;

        for (var i = 0; i < travelTypes.Length; i++)
        {
            builder.AddLabel(RuleStartX + i * RuleWidth, HeaderY, 0x35, GetShortLabel(travelTypes[i]));
        }

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            var y = RowStartY + i * RowHeight;

            builder.AddLabel(NameX, y, rule.Enabled ? 0x480 : 0x3B1, rule.Name);
            AddToggle(ref builder, EnabledX, y, rule.Enabled, 1000 + i);

            for (var j = 0; j < travelTypes.Length; j++)
            {
                AddToggle(ref builder, RuleStartX + j * RuleWidth, y, rule.Allows(travelTypes[j]), 2000 + i * 10 + j);
            }
        }

        builder.AddLabel(24, height - 32, 0x481, "Green allows travel when the rule matches. Red blocks travel.");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

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
                break;
            default:
                HandleToggle(info.ButtonID);
                break;
        }

        from.SendGump(new TravelRestrictionsGump());
    }

    private static void HandleToggle(int buttonId)
    {
        if (buttonId >= 1000 && buttonId < 2000)
        {
            TravelRestrictionSystem.ToggleRuleEnabled(buttonId - 1000);
            return;
        }

        if (buttonId < 2000)
        {
            return;
        }

        var offset = buttonId - 2000;
        var ruleIndex = offset / 10;
        var typeIndex = offset % 10;
        var types = TravelRestrictionSystem.TravelTypes;

        if (typeIndex >= 0 && typeIndex < types.Length)
        {
            TravelRestrictionSystem.ToggleRule(ruleIndex, types[typeIndex]);
        }
    }

    private static void AddToggle(ref DynamicGumpBuilder builder, int x, int y, bool enabled, int buttonId)
    {
        builder.AddButton(
            x,
            y + 2,
            enabled ? ToggleButtonId : DisabledButtonId,
            enabled ? ToggleButtonPressedId : DisabledButtonPressedId,
            buttonId
        );

        builder.AddLabel(x + 28, y, enabled ? 0x44 : 0x20, enabled ? "Yes" : "No");
    }

    private static string GetShortLabel(TravelCheckType type) =>
        type switch
        {
            TravelCheckType.RecallFrom   => "RecF",
            TravelCheckType.RecallTo     => "RecT",
            TravelCheckType.GateFrom     => "GateF",
            TravelCheckType.GateTo       => "GateT",
            TravelCheckType.Mark         => "Mark",
            TravelCheckType.TeleportFrom => "TelF",
            TravelCheckType.TeleportTo   => "TelT",
            _                            => type.ToString()
        };
}
