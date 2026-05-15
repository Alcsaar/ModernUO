using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.TemplateSaver;

public sealed class TemplateSaverGump : DynamicGump
{
    private const int SaveNameEntryId = 1;
    private const int TextEntryHue = 1153;

    private const int ButtonClose = 0;
    private const int ButtonSaveNew = 10;
    private const int ButtonUndoDelete = 11;
    private const int ButtonPrevPage = 12;
    private const int ButtonNextPage = 13;

    private const int ButtonLoadBase = 1000;
    private const int ButtonSkillsBase = 1100;
    private const int ButtonDeleteBase = 1200;

    private const int EntriesPerPage = 6;
    private const int PreviewSkillCount = 7;

    private readonly Mobile _from;
    private readonly int _pageIndex;
    private readonly string _saveName;

    public override bool Singleton => true;

    private TemplateSaverGump(Mobile from, int pageIndex, string saveName) : base(40, 40)
    {
        _from = from;
        _pageIndex = Math.Max(0, pageIndex);
        _saveName = saveName ?? string.Empty;
    }

    public static void DisplayTo(Mobile from, int pageIndex = 0, string saveName = "")
    {
        if (from?.NetState == null)
        {
            return;
        }

        if (!TemplateSaverManager.CanUse(from, out var message))
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                from.SendMessage(0x22, message);
            }

            return;
        }

        from.SendGump(new TemplateSaverGump(from, pageIndex, saveName));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 780, 520, 9270);
        builder.AddAlphaRegion(15, 15, 750, 490);

        var templates = TemplateSaverManager.GetTemplates(_from);
        var slotLimit = TemplateSaverManager.GetTemplateSlotLimit(_from);

        builder.AddHtml(20, 20, 300, 25, HtmlColor("Skill/Stat Templates", "#00FFFF"));
        builder.AddHtml(520, 20, 220, 25, HtmlColor("Values + Locks", "#66FF66"));
        builder.AddHtml(20, 48, 180, 20, HtmlColor($"Slots: {templates.Count}/{slotLimit}", "#FFFFFF"));

        builder.AddButton(720, 20, 4017, 4019, ButtonClose);

        BuildMainPage(ref builder, templates, slotLimit);
    }

    private void BuildMainPage(
        ref DynamicGumpBuilder builder,
        IReadOnlyList<CharacterTemplateEntry> templates,
        int slotLimit
    )
    {
        builder.AddHtml(20, 82, 120, 20, HtmlColor("Save current as:", "#C0C0C0"));
        builder.AddTextEntry(145, 80, 260, 22, TextEntryHue, SaveNameEntryId, _saveName);

        builder.AddButton(415, 80, 4005, 4007, ButtonSaveNew);
        builder.AddHtml(450, 82, 60, 20, HtmlColor("Save", "#FFFFFF"));

        builder.AddButton(520, 80, 4005, 4007, ButtonUndoDelete);
        builder.AddHtml(555, 82, 130, 20, HtmlColor("Undo Delete", "#C0C0C0"));

        builder.AddHtml(20, 115, 740, 2, "<basefont color=#808080><hr></basefont>");
        builder.AddHtml(20, 122, 180, 20, HtmlColor("Saved Templates", "#00FFFF"));

        // Outer frame (gives that defined boxed look)
        builder.AddImageTiled(20, 126, 740, 2, 5058); // dark line
        builder.AddImageTiled(20, 128, 740, 2, 2624); // highlight line

        var totalPages = GetTotalPages(templates.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, templates.Count);

        var y = 150;

        if (templates.Count == 0)
        {
            builder.AddHtml(30, y, 400, 20, HtmlColor("No templates saved yet.", "#C0C0C0"));
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var entry = templates[i];
                var rowIndex = i - start;
                var skillsPreview = BuildSkillsPreview(entry, out var secondLine);

                builder.AddHtml(30, y, 230, 20, HtmlColor(entry.Name, "#FFFFFF"));
                builder.AddHtml(270, y, 240, 20, HtmlColor($"Created: {entry.CreatedAt:G}", "#C0C0C0"));
                builder.AddHtml(30, y + 22, 500, 20, BuildStatsHtml(entry));
                builder.AddHtml(30, y + 44, 500, 18, HtmlColor(skillsPreview, "#C0C0C0"));

                if (!string.IsNullOrEmpty(secondLine))
                {
                    builder.AddHtml(30, y + 60, 500, 18, HtmlColor(secondLine, "#C0C0C0"));
                }

                builder.AddButton(560, y + 2, 4005, 4007, ButtonLoadBase + rowIndex);
                builder.AddHtml(595, y + 4, 50, 20, HtmlColor("Load", "#FFFFFF"));

                builder.AddButton(560, y + 24, 4005, 4007, ButtonSkillsBase + rowIndex);
                builder.AddHtml(595, y + 26, 80, 20, HtmlColor("All Skills", "#66FF66"));

                builder.AddButton(650, y + 2, 4017, 4019, ButtonDeleteBase + rowIndex);
                builder.AddHtml(685, y + 4, 60, 20, HtmlColor("Delete", "#FF0000"));

                // Outer frame (gives that defined boxed look)
                builder.AddImageTiled(20, y + 78, 740, 2, 5058); // dark line
                builder.AddImageTiled(20, y + 80, 740, 2, 2624); // highlight line

                y += 88;
            }
        }

        BuildFooter(ref builder, pageIndex, totalPages, templates.Count, slotLimit);
    }

    private void BuildFooter(
        ref DynamicGumpBuilder builder,
        int pageIndex,
        int totalPages,
        int entryCount,
        int slotLimit
    )
    {
        builder.AddHtml(20, 485, 250, 20, HtmlColor($"Page {pageIndex + 1}/{Math.Max(1, totalPages)}", "#FFFFFF"));
        builder.AddHtml(250, 485, 220, 20, HtmlColor($"Templates: {entryCount}/{slotLimit}", "#C0C0C0"));

        if (pageIndex > 0)
        {
            builder.AddButton(640, 483, 4014, 4016, ButtonPrevPage);
            builder.AddHtml(675, 485, 45, 20, HtmlColor("Prev", "#FFFFFF"));
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(700, 483, 4005, 4007, ButtonNextPage);
            builder.AddHtml(735, 485, 45, 20, HtmlColor("Next", "#FFFFFF"));
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        if (from == null)
        {
            return;
        }

        var saveName = info.GetTextEntry(SaveNameEntryId) ?? string.Empty;
        var templates = TemplateSaverManager.GetTemplates(from);

        switch (info.ButtonID)
        {
            case ButtonClose:
                return;

            case ButtonSaveNew:
                {
                    if (TemplateSaverManager.SaveNewTemplate(from, saveName, out var message))
                    {
                        from.SendMessage(0x35, message);
                    }
                    else
                    {
                        from.SendMessage(0x22, message);
                    }

                    DisplayTo(from, _pageIndex, string.Empty);
                    return;
                }

            case ButtonUndoDelete:
                {
                    if (TemplateSaverManager.RestoreMostRecentDeleted(from, out var message))
                    {
                        from.SendMessage(0x35, message);
                    }
                    else
                    {
                        from.SendMessage(0x22, message);
                    }

                    DisplayTo(from, _pageIndex, saveName);
                    return;
                }

            case ButtonPrevPage:
                DisplayTo(from, Math.Max(0, _pageIndex - 1), saveName);
                return;

            case ButtonNextPage:
                DisplayTo(from, _pageIndex + 1, saveName);
                return;
        }

        HandleMainPageResponse(from, info.ButtonID, templates, saveName);
    }

    private void HandleMainPageResponse(
        Mobile from,
        int buttonId,
        IReadOnlyList<CharacterTemplateEntry> templates,
        string saveName
    )
    {
        var totalPages = GetTotalPages(templates.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;

        if (buttonId >= ButtonLoadBase && buttonId < ButtonLoadBase + EntriesPerPage)
        {
            var entry = GetTemplateEntryForButton(templates, start, buttonId - ButtonLoadBase);
            if (entry != null)
            {
                if (TemplateSaverManager.LoadTemplate(from, entry.Id, out var message))
                {
                    from.SendMessage(0x35, message);
                }
                else
                {
                    from.SendMessage(0x22, message);
                }
            }

            DisplayTo(from, pageIndex, saveName);
            return;
        }

        if (buttonId >= ButtonSkillsBase && buttonId < ButtonSkillsBase + EntriesPerPage)
        {
            var entry = GetTemplateEntryForButton(templates, start, buttonId - ButtonSkillsBase);
            if (entry != null)
            {
                TemplateSkillListGump.DisplayTo(
                    from,
                    $"Skills: {entry.Name}",
                    entry.Skills,
                    m => DisplayTo(m, pageIndex, saveName)
                );
            }

            return;
        }

        if (buttonId >= ButtonDeleteBase && buttonId < ButtonDeleteBase + EntriesPerPage)
        {
            var entry = GetTemplateEntryForButton(templates, start, buttonId - ButtonDeleteBase);
            if (entry != null)
            {
                from.SendGump(new TemplateDeleteConfirmGump(from, entry.Id, entry.Name, pageIndex, saveName));
            }
        }
    }

    private sealed class TemplateDeleteConfirmGump : DynamicGump
    {
        private const int ButtonCancel = 0;
        private const int ButtonConfirm = 1;

        private readonly Mobile _from;
        private readonly Guid _templateId;
        private readonly string _templateName;
        private readonly int _pageIndex;
        private readonly string _saveName;

        public override bool Singleton => true;

        public TemplateDeleteConfirmGump(
            Mobile from,
            Guid templateId,
            string templateName,
            int pageIndex,
            string saveName
        ) : base(180, 160)
        {
            _from = from;
            _templateId = templateId;
            _templateName = templateName;
            _pageIndex = pageIndex;
            _saveName = saveName ?? string.Empty;
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 360, 170, 9270);
            builder.AddAlphaRegion(15, 15, 330, 140);

            builder.AddHtml(20, 20, 320, 20, HtmlColor("Confirm Delete", "#FF0000"));
            builder.AddHtml(20, 55, 320, 40, HtmlColor($"Delete template '{_templateName}'?", "#FFFFFF"));
            builder.AddHtml(20, 90, 320, 20, HtmlColor("Players can only restore the most recently deleted template.", "#C0C0C0"));

            builder.AddButton(70, 125, 4005, 4007, ButtonConfirm);
            builder.AddHtml(105, 127, 70, 20, HtmlColor("Delete", "#FF0000"));

            builder.AddButton(200, 125, 4017, 4019, ButtonCancel);
            builder.AddHtml(235, 127, 70, 20, HtmlColor("Cancel", "#FFFFFF"));
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (info.ButtonID == ButtonConfirm)
            {
                if (TemplateSaverManager.DeleteTemplate(_from, _from.Serial.Value, _templateId, out var message))
                {
                    _from.SendMessage(0x35, message);
                }
                else
                {
                    _from.SendMessage(0x22, message);
                }
            }

            DisplayTo(_from, _pageIndex, _saveName);
        }
    }

    private static CharacterTemplateEntry GetTemplateEntryForButton(
        IReadOnlyList<CharacterTemplateEntry> entries,
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

    private static int ClampPageIndex(int pageIndex, int totalPages)
    {
        if (pageIndex < 0)
        {
            return 0;
        }

        if (pageIndex >= totalPages)
        {
            return totalPages - 1;
        }

        return pageIndex;
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

    private static string BuildSkillsPreview(CharacterTemplateEntry entry, out string secondLine)
    {
        secondLine = string.Empty;

        if (entry.Skills == null || entry.Skills.Count == 0)
        {
            return "Top Skills: none saved";
        }

        var sortedSkills = CreateSortedSkills(entry.Skills);
        var count = Math.Min(PreviewSkillCount, sortedSkills.Count);
        var firstLine = string.Empty;

        for (var i = 0; i < count; i++)
        {
            var skill = sortedSkills[i];
            var segment = $"{skill.SkillName} {skill.Base:0.0}";

            if (i < 4)
            {
                if (firstLine.Length > 0)
                {
                    firstLine += "  |  ";
                }

                firstLine += segment;
            }
            else
            {
                if (secondLine.Length > 0)
                {
                    secondLine += "  |  ";
                }

                secondLine += segment;
            }
        }

        if (sortedSkills.Count > count)
        {
            if (secondLine.Length > 0)
            {
                secondLine += "  |  ...";
            }
            else
            {
                firstLine += "  |  ...";
            }
        }

        return firstLine;
    }

    private static List<SkillTemplateSnapshot> CreateSortedSkills(List<SkillTemplateSnapshot> skills)
    {
        var sortedSkills = new List<SkillTemplateSnapshot>(skills);
        sortedSkills.Sort(CompareSkills);
        return sortedSkills;
    }

    private static int CompareSkills(SkillTemplateSnapshot a, SkillTemplateSnapshot b)
    {
        if (ReferenceEquals(a, b))
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        var valueCompare = b.Base.CompareTo(a.Base);
        if (valueCompare != 0)
        {
            return valueCompare;
        }

        return string.Compare(a.SkillName, b.SkillName, StringComparison.OrdinalIgnoreCase);
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
