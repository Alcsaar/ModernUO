using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.TemplateSaver;

public sealed class TemplatePresetGump : DynamicGump
{
    private const int EntriesPerPage = 4;

    private const int ButtonClose = 0;
    private const int ButtonRefresh = 1;
    private const int ButtonCreateFromSelf = 2;
    private const int ButtonPrevPage = 3;
    private const int ButtonNextPage = 4;

    private const int EntryButtonLoadBase = 1000;
    private const int EntryButtonSkillsBase = 1100;
    private const int EntryButtonDeleteBase = 1200;

    private const int EntryPresetName = 1;
    private const int EntryTierName = 2;

    private readonly Mobile _from;
    private readonly int _pageIndex;
    private readonly List<TemplatePresetTierDefinition> _entries;

    public override bool Singleton => true;

    private TemplatePresetGump(Mobile from, int pageIndex) : base(40, 40)
    {
        _from = from;
        _pageIndex = pageIndex;
        _entries = TemplatePresetManager.GetAllPresetTiers();
    }

    public static void DisplayTo(Mobile from, int pageIndex = 0)
    {
        if (from?.NetState == null)
        {
            return;
        }

        if (!TemplatePresetManager.CanUse(from, out var message))
        {
            from.SendMessage(0x22, message);
            return;
        }

        from.SendGump(new TemplatePresetGump(from, pageIndex));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 820, 620, 9270);
        builder.AddAlphaRegion(15, 15, 790, 590);

        builder.AddHtml(20, 20, 250, 25, HtmlColor("Staff Template Presets", "#00FFFF"));
        builder.AddHtml(530, 20, 220, 20, HtmlColor($"Preset tiers: {_entries.Count}", "#FFFFFF"));
        builder.AddButton(760, 20, 4017, 4019, ButtonClose);

        builder.AddButton(680, 20, 4014, 4016, ButtonRefresh);
        builder.AddHtml(715, 22, 50, 20, HtmlColor("Refresh", "#FFFFFF"));

        var isOwner = _from.AccessLevel >= AccessLevel.Owner;

        builder.AddHtml(20, 55, 200, 20, HtmlColor("Staff Testing Preset Library", "#FFFFFF"));
        builder.AddHtml(20, 76, 770, 20, HtmlColor("Presets are separate from player-owned template slots and deleted-template restore.", "#C0C0C0"));

        if (isOwner)
        {
            builder.AddHtml(20, 102, 140, 20, HtmlColor("Preset Name:", "#FFFFFF"));
            builder.AddTextEntry(120, 101, 220, 20, 0x480, EntryPresetName, string.Empty);

            builder.AddHtml(360, 102, 60, 20, HtmlColor("Tier:", "#FFFFFF"));
            builder.AddTextEntry(400, 101, 150, 20, 0x480, EntryTierName, string.Empty);

            builder.AddButton(565, 100, 4005, 4007, ButtonCreateFromSelf);
            builder.AddHtml(600, 102, 150, 20, HtmlColor("Create From Self", "#66FF66"));
            builder.AddHtml(20, 126, 760, 18, HtmlColor("Owner-only. Valid tiers: Apprentice, Journeyman, Expert, Adept, Master, Grandmaster.", "#C0C0C0"));
        }
        else
        {
            builder.AddHtml(20, 102, 760, 20, HtmlColor("Owner-only editing is disabled on this character. Staff may browse and load presets onto themselves.", "#C0C0C0"));
        }

        builder.AddImageTiled(20, 150, 760, 2, 5058);
        builder.AddImageTiled(20, 152, 760, 2, 2624);

        var totalPages = GetTotalPages(_entries.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, _entries.Count);
        var y = 165;

        if (_entries.Count == 0)
        {
            builder.AddHtml(30, y, 500, 20, HtmlColor("No preset tiers were found.", "#C0C0C0"));
            builder.AddHtml(30, y + 22, 720, 20, HtmlColor("The file will be created automatically at Configuration/TemplateSaver/template-presets.json.", "#C0C0C0"));
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var entry = _entries[i];
                var rowIndex = i - start;
                var previewLine = BuildTopSkillsPreview(entry, out var secondLine);

                builder.AddHtml(30, y, 240, 20, HtmlColor($"{entry.PresetName} [{entry.Tier}]", "#FFFFFF"));
                builder.AddHtml(270, y, 250, 20, HtmlColor($"Updated: {entry.UpdatedAt:G}", "#C0C0C0"));
                builder.AddHtml(30, y + 22, 500, 20, BuildStatsHtml(entry));
                builder.AddHtml(30, y + 44, 500, 18, HtmlColor(previewLine, "#C0C0C0"));

                if (!string.IsNullOrEmpty(secondLine))
                {
                    builder.AddHtml(30, y + 62, 500, 18, HtmlColor(secondLine, "#C0C0C0"));
                }

                builder.AddButton(575, y + 2, 4005, 4007, EntryButtonLoadBase + rowIndex);
                builder.AddHtml(610, y + 4, 70, 20, HtmlColor("Load", "#FFFFFF"));

                builder.AddButton(575, y + 24, 4005, 4007, EntryButtonSkillsBase + rowIndex);
                builder.AddHtml(610, y + 26, 80, 20, HtmlColor("All Skills", "#66FF66"));

                if (isOwner)
                {
                    builder.AddButton(675, y + 2, 4017, 4019, EntryButtonDeleteBase + rowIndex);
                    builder.AddHtml(710, y + 4, 60, 20, HtmlColor("Delete", "#FF0000"));
                }

                builder.AddImageTiled(20, y + 82, 740, 2, 5058);
                builder.AddImageTiled(20, y + 84, 740, 2, 2624);
                y += 92;
            }
        }

        builder.AddHtml(20, 585, 180, 20, HtmlColor($"Page {pageIndex + 1}/{Math.Max(1, totalPages)}", "#FFFFFF"));

        if (pageIndex > 0)
        {
            builder.AddButton(650, 583, 4014, 4016, ButtonPrevPage);
            builder.AddHtml(685, 585, 45, 20, HtmlColor("Prev", "#FFFFFF"));
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(710, 583, 4005, 4007, ButtonNextPage);
            builder.AddHtml(745, 585, 45, 20, HtmlColor("Next", "#FFFFFF"));
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        switch (info.ButtonID)
        {
            case ButtonClose:
                return;

            case ButtonRefresh:
                DisplayTo(_from, _pageIndex);
                return;

            case ButtonPrevPage:
                DisplayTo(_from, Math.Max(0, _pageIndex - 1));
                return;

            case ButtonNextPage:
                DisplayTo(_from, _pageIndex + 1);
                return;

            case ButtonCreateFromSelf:
                {
                    var presetName = info.GetTextEntry(EntryPresetName)?.Trim();
                    var tierName = info.GetTextEntry(EntryTierName)?.Trim();

                    if (TemplatePresetManager.CreateOrUpdateFromSelf(_from, presetName, tierName, out var createMessage))
                    {
                        _from.SendMessage(0x35, createMessage);
                    }
                    else
                    {
                        _from.SendMessage(0x22, createMessage);
                    }

                    DisplayTo(_from, _pageIndex);
                    return;
                }
        }

        var totalPages = GetTotalPages(_entries.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;

        if (info.ButtonID >= EntryButtonLoadBase && info.ButtonID < EntryButtonLoadBase + EntriesPerPage)
        {
            var entry = GetEntry(start, info.ButtonID - EntryButtonLoadBase);
            if (entry != null)
            {
                if (TemplatePresetManager.LoadPresetOntoSelf(_from, entry.Id, out var loadMessage))
                {
                    _from.SendMessage(0x35, loadMessage);
                }
                else
                {
                    _from.SendMessage(0x22, loadMessage);
                }
            }

            DisplayTo(_from, pageIndex);
            return;
        }

        if (info.ButtonID >= EntryButtonSkillsBase && info.ButtonID < EntryButtonSkillsBase + EntriesPerPage)
        {
            var entry = GetEntry(start, info.ButtonID - EntryButtonSkillsBase);
            if (entry != null)
            {
                TemplateSkillListGump.DisplayTo(
                    _from,
                    $"Preset Skills: {entry.PresetName} [{entry.Tier}]",
                    entry.Skills,
                    m => DisplayTo(m, pageIndex)
                );
            }

            return;
        }

        if (info.ButtonID >= EntryButtonDeleteBase && info.ButtonID < EntryButtonDeleteBase + EntriesPerPage)
        {
            var entry = GetEntry(start, info.ButtonID - EntryButtonDeleteBase);
            if (entry != null)
            {
                if (TemplatePresetManager.DeletePresetTier(_from, entry.PresetName, entry.Tier, out var deleteMessage))
                {
                    _from.SendMessage(0x35, deleteMessage);
                }
                else
                {
                    _from.SendMessage(0x22, deleteMessage);
                }
            }

            DisplayTo(_from, pageIndex);
        }
    }

    private TemplatePresetTierDefinition GetEntry(int start, int rowOffset)
    {
        var index = start + rowOffset;
        if (index < 0 || index >= _entries.Count)
        {
            return null;
        }

        return _entries[index];
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

    private static string BuildStatsHtml(TemplatePresetTierDefinition entry)
    {
        var strLock = StatLockToText(entry.Stats.StrLock);
        var dexLock = StatLockToText(entry.Stats.DexLock);
        var intLock = StatLockToText(entry.Stats.IntLock);

        return
            HtmlColor($"Str {entry.Stats.Str}", "#FF4040") +
            HtmlColor($" [{strLock}]   ", "#FFFFFF") +
            HtmlColor($"Dex {entry.Stats.Dex}", "#66FF66") +
            HtmlColor($" [{dexLock}]   ", "#FFFFFF") +
            HtmlColor($"Int {entry.Stats.Int}", "#66B2FF") +
            HtmlColor($" [{intLock}]", "#FFFFFF");
    }

    private static string BuildTopSkillsPreview(TemplatePresetTierDefinition entry, out string secondLine)
    {
        secondLine = null;

        if (entry?.Skills == null || entry.Skills.Count == 0)
        {
            return "No saved skills.";
        }

        var sortedSkills = new List<SkillTemplateSnapshot>(entry.Skills);
        sortedSkills.Sort(CompareSkills);

        var lineOne = string.Empty;
        var lineTwo = string.Empty;
        var previewCount = Math.Min(7, sortedSkills.Count);

        for (var i = 0; i < previewCount; i++)
        {
            var skill = sortedSkills[i];
            var text = $"{skill.SkillName} {skill.Base:0.0}";

            if (i < 4)
            {
                if (lineOne.Length > 0)
                {
                    lineOne += "  |  ";
                }

                lineOne += text;
            }
            else
            {
                if (lineTwo.Length > 0)
                {
                    lineTwo += "  |  ";
                }

                lineTwo += text;
            }
        }

        secondLine = lineTwo;
        return lineOne;
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

        var compare = b.Base.CompareTo(a.Base);
        if (compare != 0)
        {
            return compare;
        }

        return string.Compare(a.SkillName, b.SkillName, StringComparison.OrdinalIgnoreCase);
    }

    private static string StatLockToText(StatLockType statLock)
    {
        return statLock switch
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
