using System;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom.Systems.AchievementSystem;

public sealed class AchievementUnlockGump : DynamicGump
{
    private const int ButtonOpenJournal = 1;
    private static readonly TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(3.0);

    private readonly PlayerMobile _from;
    private readonly AchievementNotificationRecord _notification;
    private TimerExecutionToken _closeToken;
    private bool _completed;

    public override bool Singleton => true;

    private AchievementUnlockGump(PlayerMobile from, AchievementNotificationRecord notification) : base(440, 38)
    {
        _from = from;
        _notification = notification;
    }

    public static void DisplayTo(PlayerMobile from, AchievementNotificationRecord notification)
    {
        if (from?.NetState == null || notification == null)
        {
            return;
        }

        from.CloseGump<AchievementUnlockGump>();

        var gump = new AchievementUnlockGump(from, notification);
        from.SendGump(gump);
        gump.StartAutoClose();
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var hasReward = !string.IsNullOrWhiteSpace(_notification.RewardText);

        builder.AddPage();
        builder.AddBackground(0, 0, 390, hasReward ? 178 : 148, 9270);
        builder.AddAlphaRegion(15, 15, 360, hasReward ? 148 : 118);

        builder.AddHtml(22, 18, 220, 20, HtmlColor("Achievement Unlocked", "#00FF99"));
        builder.AddHtml(22, 46, 340, 20, HtmlColor(_notification.Name, "#FFFFFF"));
        builder.AddHtml(22, 72, 340, 32, HtmlColor(_notification.Description, "#C0C0C0"));

        var footerY = hasReward ? 140 : 110;

        if (hasReward)
        {
            builder.AddHtml(22, 108, 340, 20, HtmlColor($"Reward: {_notification.RewardText}", "#FFD27F"));
        }

        builder.AddImageTiled(22, footerY, 210, 12, 2624);
        builder.AddImageTiled(22, footerY, 210, 12, 9304);

        builder.AddButton(248, footerY - 4, 4005, 4007, ButtonOpenJournal);
        builder.AddHtml(283, footerY - 2, 90, 20, HtmlColor("Open Journal", "#FFFFFF"));
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        Complete(info.ButtonID == ButtonOpenJournal);
    }

    public override void OnServerClose(NetState owner)
    {
        base.OnServerClose(owner);

        Complete(false);
    }

    private void StartAutoClose()
    {
        Timer.StartTimer(AutoCloseDelay, () => Complete(false), out _closeToken);
    }

    private void Complete(bool openJournal)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _closeToken.Cancel();
        AchievementService.CompleteActiveNotification(_from, _notification.AchievementId, openJournal);
    }

    private static string HtmlColor(string text, string hex)
    {
        return $"<BASEFONT COLOR={hex}>{text}</BASEFONT>";
    }
}
