using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.CustomFeatureFlags;

public sealed class CustomFeatureFlagAdminGump : DynamicGump
{
    private const int RowsPerPage = 12;

    private readonly Mobile _from;
    private readonly int _pageIndex;
    private readonly List<CustomFeatureFlagStatus> _statuses;

    public override bool Singleton => true;

    private CustomFeatureFlagAdminGump(Mobile from, int pageIndex, List<CustomFeatureFlagStatus> statuses)
        : base(40, 40)
    {
        _from = from;
        _pageIndex = pageIndex;
        _statuses = statuses;
    }

    public static void DisplayTo(Mobile from, int pageIndex = 0)
    {
        if (from?.NetState == null)
        {
            return;
        }

        var statuses = CustomFeatureFlagManager.GetAllStatuses(includeHidden: true);
        from.SendGump(new CustomFeatureFlagAdminGump(from, Math.Max(0, pageIndex), statuses));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 900, 470, 9270);
        builder.AddAlphaRegion(15, 15, 870, 440);
        builder.AddHtml(0, 15, 900, 25, "Custom Feature Flags".Center("#00FF99"));

        builder.AddHtml(20, 50, 180, 20, "Name".Color(0xFFFF));
        builder.AddHtml(220, 50, 140, 20, "Category".Color(0xFFFF));
        builder.AddHtml(360, 50, 80, 20, "Stored".Color(0xFFFF));
        builder.AddHtml(450, 50, 90, 20, "Effective".Color(0xFFFF));
        builder.AddHtml(550, 50, 320, 20, "Notes".Color(0xFFFF));

        var start = _pageIndex * RowsPerPage;
        var end = Math.Min(start + RowsPerPage, _statuses.Count);
        var y = 80;

        for (var i = start; i < end; i++)
        {
            var rowIndex = i - start;
            var status = _statuses[i];

            var note = status.BlockingDependencies.Length > 0
                ? $"Blocked By: {string.Join(", ", status.BlockingDependencies)}"
                : status.Description;

            builder.AddButton(
                20,
                y,
                status.StoredEnabled ? 2154 : 2151,
                status.StoredEnabled ? 2151 : 2154,
                1000 + rowIndex
            );

            builder.AddHtml(60, y, 150, 20, status.DisplayName.Color(0xFFFF));
            builder.AddHtml(220, y, 130, 20, status.Category.Color(0xBBBB));
            builder.AddHtml(360, y, 70, 20, (status.StoredEnabled ? "[ON]" : "[OFF]").Color(0xFFFF));
            builder.AddHtml(450, y, 80, 20, (status.EffectiveEnabled ? "[ON]" : "[OFF]").Color(0xFFFF));
            builder.AddHtml(550, y, 320, 36, note.Color(0xBBBB));

            y += 30;
        }

        var pageCount = Math.Max(1, (int)Math.Ceiling(_statuses.Count / (double)RowsPerPage));

        if (_pageIndex > 0)
        {
            builder.AddButton(280, 430, 4014, 4016, 1);
            builder.AddHtml(315, 430, 60, 20, "Prev".Color(0xFFFF));
        }

        builder.AddHtml(400, 430, 100, 20, $"{_pageIndex + 1}/{pageCount}".Center("#FFFFFF"));

        if (_pageIndex < pageCount - 1)
        {
            builder.AddButton(520, 430, 4005, 4007, 2);
            builder.AddHtml(555, 430, 60, 20, "Next".Color(0xFFFF));
        }

        builder.AddButton(20, 430, 4023, 4025, 3);
        builder.AddHtml(55, 430, 80, 20, "Save".Color(0xFFFF));
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile == null)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                {
                    return;
                }
            case 1:
                {
                    DisplayTo(_from, Math.Max(0, _pageIndex - 1));
                    return;
                }
            case 2:
                {
                    DisplayTo(_from, _pageIndex + 1);
                    return;
                }
            case 3:
                {
                    CustomFeatureFlagManager.Save();
                    _from.SendMessage(0x35, "Custom feature flag config saved.");
                    DisplayTo(_from, _pageIndex);
                    return;
                }
        }

        if (info.ButtonID >= 1000 && info.ButtonID < 1000 + RowsPerPage)
        {
            var row = info.ButtonID - 1000;
            var index = (_pageIndex * RowsPerPage) + row;

            if (index >= 0 && index < _statuses.Count)
            {
                var status = _statuses[index];

                if (CustomFeatureFlagManager.Toggle(status.Key, _from.Name, out var reason))
                {
                    _from.SendMessage(0x35, $"{status.DisplayName} toggled.");
                }
                else
                {
                    _from.SendMessage(0x22, reason ?? "Unable to toggle flag.");
                }
            }
        }

        DisplayTo(_from, _pageIndex);
    }
}
