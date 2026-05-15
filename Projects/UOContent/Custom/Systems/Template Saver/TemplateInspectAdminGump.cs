using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.TemplateSaver;

public sealed class TemplateInspectAdminGump : DynamicGump
{
    private const int ButtonClose = 0;
    private const int ButtonPrevPage = 12;
    private const int ButtonNextPage = 13;

    private const int ButtonLoadToSelfBase = 1000;
    private const int ButtonSkillsBase = 1050;
    private const int ButtonDeleteBase = 1100;

    private const int EntriesPerPage = 6;

    private readonly Mobile _from;
    private readonly uint _ownerSerial;
    private readonly int _pageIndex;

    public override bool Singleton => true;

    private TemplateInspectAdminGump(Mobile from, uint ownerSerial, int pageIndex) : base(40, 40)
    {
        _from = from;
        _ownerSerial = ownerSerial;
        _pageIndex = Math.Max(0, pageIndex);
    }

    public static void DisplayTo(Mobile from, uint ownerSerial, int pageIndex = 0)
    {
        if (from?.NetState == null || from.AccessLevel <= AccessLevel.Player)
        {
            return;
        }

        from.SendGump(new TemplateInspectAdminGump(from, ownerSerial, pageIndex));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 820, 540, 9270);
        builder.AddAlphaRegion(15, 15, 790, 510);

        builder.AddHtml(20, 20, 300, 25, HtmlColor("Template Inspect Admin", "#00FFFF"));
        builder.AddButton(760, 20, 4017, 4019, ButtonClose);

        var store = TemplateSaverManager.GetStore(_ownerSerial);
        if (store == null)
        {
            builder.AddHtml(20, 70, 400, 20, HtmlColor("No template store found for that character.", "#C0C0C0"));
            return;
        }

        var templates = store.Templates;
        var totalSlots = TemplateSaverManager.GetTemplateSlotLimit(_ownerSerial);

        builder.AddHtml(20, 55, 500, 20, HtmlColor($"Character: {store.OwnerName} (0x{store.OwnerSerial:X8})", "#FFFFFF"));
        builder.AddHtml(20, 77, 300, 20, HtmlColor($"Templates: {templates.Count}/{totalSlots}", "#C0C0C0"));
        builder.AddHtml(20, 100, 770, 2, "<basefont color=#808080><hr></basefont>");

        var totalPages = GetTotalPages(templates.Count);
        var pageIndex = ClampIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, templates.Count);

        var y = 112;

        if (templates.Count == 0)
        {
            builder.AddHtml(30, y, 300, 20, HtmlColor("No saved templates for this character.", "#C0C0C0"));
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var entry = templates[i];
                var rowIndex = i - start;

                builder.AddHtml(30, y, 220, 20, HtmlColor(entry.Name, "#FFFFFF"));
                builder.AddHtml(260, y, 260, 20, HtmlColor($"Created: {entry.CreatedAt:G}", "#C0C0C0"));
                builder.AddHtml(30, y + 22, 500, 20, BuildStatsHtml(entry));
                builder.AddHtml(30, y + 44, 500, 18, HtmlColor(BuildSkillsPreview(entry), "#C0C0C0"));

                builder.AddButton(560, y + 2, 4005, 4007, ButtonLoadToSelfBase + rowIndex);
                builder.AddHtml(595, y + 4, 95, 20, HtmlColor("Load To Self", "#FFFFFF"));

                builder.AddButton(560, y + 24, 4005, 4007, ButtonSkillsBase + rowIndex);
                builder.AddHtml(595, y + 26, 60, 20, HtmlColor("Skills", "#66B2FF"));

                builder.AddButton(650, y + 2, 4017, 4019, ButtonDeleteBase + rowIndex);
                builder.AddHtml(685, y + 4, 60, 20, HtmlColor("Delete", "#FF0000"));

                builder.AddHtml(20, y + 67, 770, 2, "<basefont color=#808080><hr></basefont>");
                y += 72;
            }
        }

        builder.AddHtml(20, 505, 220, 20, HtmlColor($"Page {pageIndex + 1}/{Math.Max(1, totalPages)}", "#FFFFFF"));

        if (pageIndex > 0)
        {
            builder.AddButton(650, 503, 4014, 4016, ButtonPrevPage);
            builder.AddHtml(685, 505, 45, 20, HtmlColor("Prev", "#FFFFFF"));
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(710, 503, 4005, 4007, ButtonNextPage);
            builder.AddHtml(745, 505, 45, 20, HtmlColor("Next", "#FFFFFF"));
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_from == null)
        {
            return;
        }

        var store = TemplateSaverManager.GetStore(_ownerSerial);
        if (store == null)
        {
            return;
        }

        var templates = store.Templates;
        var totalPages = GetTotalPages(templates.Count);
        var pageIndex = ClampIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;

        switch (info.ButtonID)
        {
            case ButtonClose:
                return;
            case ButtonPrevPage:
                DisplayTo(_from, _ownerSerial, Math.Max(0, _pageIndex - 1));
                return;
            case ButtonNextPage:
                DisplayTo(_from, _ownerSerial, _pageIndex + 1);
                return;
        }

        if (info.ButtonID >= ButtonLoadToSelfBase && info.ButtonID < ButtonLoadToSelfBase + EntriesPerPage)
        {
            var entry = GetTemplateEntryForButton(templates, start, info.ButtonID - ButtonLoadToSelfBase);
            if (entry != null)
            {
                if (TemplateSaverManager.LoadTemplateToStaff(_from, _ownerSerial, entry.Id, out var message))
                {
                    _from.SendMessage(0x35, message);
                }
                else
                {
                    _from.SendMessage(0x22, message);
                }
            }

            DisplayTo(_from, _ownerSerial, pageIndex);
            return;
        }

        if (info.ButtonID >= ButtonSkillsBase && info.ButtonID < ButtonSkillsBase + EntriesPerPage)
        {
            var entry = GetTemplateEntryForButton(templates, start, info.ButtonID - ButtonSkillsBase);
            if (entry != null)
            {
                TemplateSkillListGump.DisplayTo(
                    _from,
                    $"Skills: {entry.Name}",
                    entry.Skills,
                    m => DisplayTo(m, _ownerSerial, pageIndex)
                );
            }

            return;
        }

        if (info.ButtonID >= ButtonDeleteBase && info.ButtonID < ButtonDeleteBase + EntriesPerPage)
        {
            var entry = GetTemplateEntryForButton(templates, start, info.ButtonID - ButtonDeleteBase);
            if (entry != null)
            {
                _from.SendGump(new TemplateInspectDeleteConfirmGump(_from, _ownerSerial, entry.Id, entry.Name, pageIndex));
            }
        }
    }

    private sealed class TemplateInspectDeleteConfirmGump : DynamicGump
    {
        private const int ButtonCancel = 0;
        private const int ButtonConfirm = 1;

        private readonly Mobile _from;
        private readonly uint _ownerSerial;
        private readonly Guid _templateId;
        private readonly string _templateName;
        private readonly int _pageIndex;

        public override bool Singleton => true;

        public TemplateInspectDeleteConfirmGump(
            Mobile from,
            uint ownerSerial,
            Guid templateId,
            string templateName,
            int pageIndex
        ) : base(180, 160)
        {
            _from = from;
            _ownerSerial = ownerSerial;
            _templateId = templateId;
            _templateName = templateName;
            _pageIndex = pageIndex;
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 360, 170, 9270);
            builder.AddAlphaRegion(15, 15, 330, 140);

            builder.AddHtml(20, 20, 320, 20, HtmlColor("Confirm Admin Delete", "#FF0000"));
            builder.AddHtml(20, 55, 320, 40, HtmlColor($"Delete template '{_templateName}' from the target player?", "#FFFFFF"));
            builder.AddHtml(20, 90, 320, 20, HtmlColor("This moves it into that player's deleted history and archive.", "#C0C0C0"));

            builder.AddButton(70, 125, 4005, 4007, ButtonConfirm);
            builder.AddHtml(105, 127, 70, 20, HtmlColor("Delete", "#FF0000"));

            builder.AddButton(200, 125, 4017, 4019, ButtonCancel);
            builder.AddHtml(235, 127, 70, 20, HtmlColor("Cancel", "#FFFFFF"));
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (info.ButtonID == ButtonConfirm)
            {
                if (TemplateSaverManager.DeleteTemplateAsStaff(_from, _ownerSerial, _templateId, out var message))
                {
                    _from.SendMessage(0x35, message);
                }
                else
                {
                    _from.SendMessage(0x22, message);
                }
            }

            DisplayTo(_from, _ownerSerial, _pageIndex);
        }
    }

    private static CharacterTemplateEntry GetTemplateEntryForButton(
        List<CharacterTemplateEntry> entries,
        int start,
        int rowOffset
    )
    {
        var index = start + rowOffset;
        if (index < 0 || index >= entries.Count)
        {
            return null;
        }

        return entries[index];
    }

    private static int GetTotalPages(int count)
    {
        return Math.Max(1, (count + EntriesPerPage - 1) / EntriesPerPage);
    }

    private static int ClampIndex(int value, int total)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value >= total)
        {
            return total - 1;
        }

        return value;
    }

    private static string BuildStatsHtml(CharacterTemplateEntry entry)
    {
        var stats = entry.Stats;

        return
            HtmlColor($"Str {stats.Str}", "#FF6666") + " " +
            HtmlColor($"({StatLockToText(stats.StrLock)})", "#C0C0C0") + "   " +
            HtmlColor($"Dex {stats.Dex}", "#66FF66") + " " +
            HtmlColor($"({StatLockToText(stats.DexLock)})", "#C0C0C0") + "   " +
            HtmlColor($"Int {stats.Int}", "#66B2FF") + " " +
            HtmlColor($"({StatLockToText(stats.IntLock)})", "#C0C0C0");
    }

    private static string BuildSkillsPreview(CharacterTemplateEntry entry)
    {
        if (entry.Skills == null || entry.Skills.Count == 0)
        {
            return "Saved skills: 0";
        }

        return $"Saved skills: {entry.Skills.Count} (click Skills to view)";
    }

    private static string StatLockToText(StatLockType lockType)
    {
        return lockType switch
        {
            StatLockType.Up => "Up",
            StatLockType.Down => "Down",
            _ => "Locked"
        };
    }

    private static string HtmlColor(string text, string hex)
    {
        return $"<BASEFONT COLOR=\"{hex}\">{text}</BASEFONT>";
    }
}
