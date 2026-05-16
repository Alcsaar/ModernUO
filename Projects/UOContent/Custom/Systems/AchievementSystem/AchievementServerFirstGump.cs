using System;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.AchievementSystem;

public sealed class AchievementServerFirstGump : DynamicGump
{
    private const int GumpWidth = 420;
    private const int GumpHeight = 170;
    private const int ContentX = 28;
    private const int ContentWidth = 364;
    private static readonly TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(8.0);

    private readonly AchievementServerFirstRecord _record;
    private TimerExecutionToken _closeToken;
    private bool _completed;

    public override bool Singleton => true;

    private AchievementServerFirstGump(AchievementServerFirstRecord record) : base(300, 120)
    {
        _record = record;
    }

    public static void DisplayTo(PlayerMobile from, AchievementServerFirstRecord record)
    {
        if (from?.NetState == null || record == null)
        {
            return;
        }

        from.CloseGump<AchievementServerFirstGump>();

        var gump = new AchievementServerFirstGump(record);
        from.SendGump(gump);
        gump.StartAutoClose();
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, 390, 140);

        builder.AddHtml(ContentX, 20, ContentWidth, 24, CenteredHtmlColor("Server First", "#FFD700"));
        builder.AddImageTiled(28, 48, 360, 2, 5058);
        builder.AddImageTiled(28, 50, 360, 2, 2624);

        builder.AddHtml(ContentX, 66, ContentWidth, 22, CenteredHtmlColor(_record.PlayerName, "#FFFFFF"));
        builder.AddHtml(
            ContentX,
            92,
            ContentWidth,
            22,
            CenteredHtmlColor($"is the first to reach Grandmaster in {_record.SkillDisplayName}!", "#00FF99")
        );
        builder.AddHtml(
            ContentX,
            122,
            ContentWidth,
            20,
            CenteredHtmlColor($"{_record.AchievedUtc:yyyy-MM-dd HH:mm} UTC", "#C0C0C0")
        );
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        Complete();
    }

    public override void OnServerClose(NetState owner)
    {
        base.OnServerClose(owner);

        Complete();
    }

    private void StartAutoClose()
    {
        Timer.StartTimer(AutoCloseDelay, Complete, out _closeToken);
    }

    private void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _closeToken.Cancel();
    }

    private static string HtmlColor(string text, string hex)
    {
        return $"<BASEFONT COLOR={hex}>{text}</BASEFONT>";
    }

    private static string CenteredHtmlColor(string text, string hex)
    {
        return $"<CENTER>{HtmlColor(text, hex)}</CENTER>";
    }
}
