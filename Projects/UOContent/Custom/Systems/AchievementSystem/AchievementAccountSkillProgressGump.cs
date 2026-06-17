using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.AchievementSystem;

public sealed class AchievementAccountSkillProgressGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int ButtonBack = 1;
    private const int GumpWidth = 620;
    private const int GumpHeight = 560;

    private readonly PlayerMobile _from;

    public override bool Singleton => true;

    private AchievementAccountSkillProgressGump(PlayerMobile from) : base(90, 55)
    {
        _from = from;
    }

    public static void DisplayTo(PlayerMobile from)
    {
        if (from?.NetState == null)
        {
            return;
        }

        from.CloseGump<AchievementAccountSkillProgressGump>();
        from.SendGump(new AchievementAccountSkillProgressGump(from));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var entries = AchievementService.GetAccountGrandmasterSkillProgress(_from);
        var completed = 0;

        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Completed)
            {
                completed++;
            }
        }

        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);

        builder.AddLabel(210, 18, HueTitle, "Account Skill Mastery");
        builder.AddLabel(226, 44, HueText, $"{completed}/{entries.Count} Grandmaster skills");
        DrawRule(ref builder, 24, 66, 572);

        builder.AddLabel(40, 92, HueHeader, "Skills");
        DrawSkillGrid(ref builder, entries);

        DrawRule(ref builder, 24, 500, 572);
        builder.AddButton(476, 516, 4014, 4016, ButtonBack);
        builder.AddLabel(510, 518, HueText, "Back");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is not PlayerMobile player)
        {
            return;
        }

        if (info.ButtonID == ButtonBack)
        {
            AchievementGump.DisplayTo(player, AchievementJournalView.Account);
        }
    }

    private static void DrawSkillGrid(ref DynamicGumpBuilder builder, System.Collections.Generic.List<AchievementAccountSkillProgressEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var column = i / 25;
            var row = i % 25;
            var x = column == 0 ? 40 : 330;
            var y = 122 + row * 15;

            DrawSkillRow(ref builder, x, y, entry.SkillDisplayName, entry.Completed);
        }
    }

    private static void DrawSkillRow(ref DynamicGumpBuilder builder, int x, int y, string skillName, bool completed)
    {
        builder.AddImageTiled(x, y + 5, 4, 8, completed ? 9304 : 2624);
        builder.AddLabel(x + 14, y, completed ? HueReady : HueMuted, skillName);
        builder.AddLabel(x + 192, y, completed ? HueReady : HueMuted, completed ? "Done" : "Needed");
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }
}
