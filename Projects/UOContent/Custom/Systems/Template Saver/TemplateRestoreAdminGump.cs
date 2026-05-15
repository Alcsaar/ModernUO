using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server.Custom.Systems.TemplateSaver;

public sealed class TemplateRestoreAdminGump : DynamicGump
{
    private const int ButtonClose = 0;
    private const int ButtonPrevEntry = 12;
    private const int ButtonNextEntry = 13;

    private const int ButtonRestoreOrigBase = 1000;
    private const int ButtonRestoreTargetBase = 1100;
    private const int ButtonSkillsBase = 1150;
    private const int ButtonPurgeBase = 1200;

    private const int EntriesPerPage = 6;

    private readonly Mobile _from;
    private readonly uint? _filterOwnerSerial;
    private readonly int _pageIndex;

    public override bool Singleton => true;

    private TemplateRestoreAdminGump(Mobile from, uint? filterOwnerSerial, int pageIndex) : base(40, 40)
    {
        _from = from;
        _filterOwnerSerial = filterOwnerSerial;
        _pageIndex = Math.Max(0, pageIndex);
    }

    public static void DisplayTo(Mobile from, int pageIndex = 0)
    {
        if (from?.NetState == null || from.AccessLevel <= AccessLevel.Player)
        {
            return;
        }

        from.SendGump(new TemplateRestoreAdminGump(from, null, pageIndex));
    }

    public static void DisplayTo(Mobile from, uint ownerSerial, int pageIndex = 0)
    {
        if (from?.NetState == null || from.AccessLevel <= AccessLevel.Player)
        {
            return;
        }

        from.SendGump(new TemplateRestoreAdminGump(from, ownerSerial, pageIndex));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 820, 540, 9270);
        builder.AddAlphaRegion(15, 15, 790, 510);

        builder.AddHtml(20, 20, 320, 25, HtmlColor(_filterOwnerSerial.HasValue ? "Template Restore Admin (Targeted)" : "Template Restore Admin", "#00FFFF"));
        builder.AddButton(760, 20, 4017, 4019, ButtonClose);

        List<DeletedTemplateArchiveEntry> entries = _filterOwnerSerial.HasValue
            ? TemplateSaverManager.GetDeletedArchiveEntriesForOwner(_filterOwnerSerial.Value)
            : new List<DeletedTemplateArchiveEntry>(TemplateSaverManager.GetDeletedArchiveEntries());

        builder.AddHtml(20, 52, 500, 20, HtmlColor($"Deleted templates found: {entries.Count}", "#FFFFFF"));

        if (_filterOwnerSerial.HasValue)
        {
            builder.AddHtml(20, 74, 500, 20, HtmlColor($"Showing only owner serial 0x{_filterOwnerSerial.Value:X8}", "#C0C0C0"));
        }
        else
        {
            builder.AddHtml(20, 74, 500, 20, HtmlColor("Showing global deleted template archive", "#C0C0C0"));
        }

        builder.AddHtml(20, 100, 770, 2, "<basefont color=#808080><hr></basefont>");

        var totalPages = GetTotalPages(entries.Count);
        var pageIndex = ClampIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, entries.Count);

        var y = 112;

        if (entries.Count == 0)
        {
            builder.AddHtml(30, y, 300, 20, HtmlColor("No deleted templates available.", "#C0C0C0"));
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var entry = entries[i];
                var rowIndex = i - start;

                builder.AddHtml(30, y, 210, 20, HtmlColor(entry.Template.Name, "#FFFFFF"));
                builder.AddHtml(245, y, 260, 20, HtmlColor($"Owner: {entry.OwnerName} (0x{entry.OwnerSerial:X8})", "#C0C0C0"));
                builder.AddHtml(30, y + 22, 260, 18, HtmlColor($"Deleted: {entry.DeletedAt:G}", "#C0C0C0"));
                builder.AddHtml(300, y + 22, 220, 18, HtmlColor($"By: {entry.DeletedBy}", "#C0C0C0"));
                builder.AddHtml(30, y + 42, 500, 20, BuildStatsHtml(entry.Template));
                builder.AddHtml(30, y + 64, 500, 18, HtmlColor(BuildSkillsPreview(entry.Template), "#C0C0C0"));

                builder.AddButton(560, y + 2, 4005, 4007, ButtonRestoreOrigBase + rowIndex);
                builder.AddHtml(595, y + 4, 70, 20, HtmlColor("To Orig", "#FFFFFF"));

                builder.AddButton(560, y + 24, 4005, 4007, ButtonRestoreTargetBase + rowIndex);
                builder.AddHtml(595, y + 26, 80, 20, HtmlColor("To Target", "#66FF66"));

                builder.AddButton(560, y + 46, 4005, 4007, ButtonSkillsBase + rowIndex);
                builder.AddHtml(595, y + 48, 60, 20, HtmlColor("Skills", "#66B2FF"));

                builder.AddButton(670, y + 2, 4017, 4019, ButtonPurgeBase + rowIndex);
                builder.AddHtml(705, y + 4, 60, 20, HtmlColor("Purge", "#FF0000"));

                // Outer frame (gives that defined boxed look)
                builder.AddImageTiled(20, y + 78, 740, 2, 5058); // dark line
                builder.AddImageTiled(20, y + 80, 740, 2, 2624); // highlight line

                y += 92;
            }
        }

        builder.AddHtml(20, 505, 220, 20, HtmlColor($"Page {pageIndex + 1}/{Math.Max(1, totalPages)}", "#FFFFFF"));

        if (pageIndex > 0)
        {
            builder.AddButton(650, 503, 4014, 4016, ButtonPrevEntry);
            builder.AddHtml(685, 505, 45, 20, HtmlColor("Prev", "#FFFFFF"));
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(710, 503, 4005, 4007, ButtonNextEntry);
            builder.AddHtml(745, 505, 45, 20, HtmlColor("Next", "#FFFFFF"));
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_from == null)
        {
            return;
        }

        List<DeletedTemplateArchiveEntry> entries = _filterOwnerSerial.HasValue
            ? TemplateSaverManager.GetDeletedArchiveEntriesForOwner(_filterOwnerSerial.Value)
            : new List<DeletedTemplateArchiveEntry>(TemplateSaverManager.GetDeletedArchiveEntries());

        switch (info.ButtonID)
        {
            case ButtonClose:
                return;

            case ButtonPrevEntry:
                Redisplay(Math.Max(0, _pageIndex - 1));
                return;

            case ButtonNextEntry:
                Redisplay(_pageIndex + 1);
                return;
        }

        var totalPages = GetTotalPages(entries.Count);
        var pageIndex = ClampIndex(_pageIndex, totalPages);
        var start = pageIndex * EntriesPerPage;

        if (info.ButtonID >= ButtonRestoreOrigBase && info.ButtonID < ButtonRestoreOrigBase + EntriesPerPage)
        {
            var entry = GetEntryForButton(entries, start, info.ButtonID - ButtonRestoreOrigBase);
            if (entry != null)
            {
                if (TemplateSaverManager.RestoreArchivedDeletedToOwnerAsStaff(_from, entry.DeletedId, false, out var message))
                {
                    _from.SendMessage(0x35, message);
                }
                else
                {
                    _from.SendMessage(0x22, message);
                }
            }

            Redisplay(pageIndex);
            return;
        }

        if (info.ButtonID >= ButtonRestoreTargetBase && info.ButtonID < ButtonRestoreTargetBase + EntriesPerPage)
        {
            var entry = GetEntryForButton(entries, start, info.ButtonID - ButtonRestoreTargetBase);
            if (entry != null)
            {
                _from.SendMessage("Target the player to restore the deleted template onto.");
                _from.Target = new RestoreDeletedTarget(_from, entry.DeletedId, _filterOwnerSerial, pageIndex);
            }

            return;
        }

        if (info.ButtonID >= ButtonSkillsBase && info.ButtonID < ButtonSkillsBase + EntriesPerPage)
        {
            var entry = GetEntryForButton(entries, start, info.ButtonID - ButtonSkillsBase);
            if (entry != null)
            {
                TemplateSkillListGump.DisplayTo(
                    _from,
                    $"Deleted Skills: {entry.Template.Name}",
                    entry.Template.Skills,
                    m => Redisplay(pageIndex)
                );
            }

            return;
        }

        if (info.ButtonID >= ButtonPurgeBase && info.ButtonID < ButtonPurgeBase + EntriesPerPage)
        {
            var entry = GetEntryForButton(entries, start, info.ButtonID - ButtonPurgeBase);
            if (entry != null)
            {
                if (TemplateSaverManager.PurgeArchivedDeletedTemplate(_from, entry.DeletedId, out var message))
                {
                    _from.SendMessage(0x35, message);
                }
                else
                {
                    _from.SendMessage(0x22, message);
                }
            }

            Redisplay(pageIndex);
        }
    }

    private void Redisplay(int pageIndex)
    {
        if (_filterOwnerSerial.HasValue)
        {
            DisplayTo(_from, _filterOwnerSerial.Value, pageIndex);
        }
        else
        {
            DisplayTo(_from, pageIndex);
        }
    }

    private sealed class RestoreDeletedTarget : Target
    {
        private readonly Mobile _actor;
        private readonly Guid _deletedId;
        private readonly uint? _filterOwnerSerial;
        private readonly int _pageIndex;

        public RestoreDeletedTarget(Mobile actor, Guid deletedId, uint? filterOwnerSerial, int pageIndex)
            : base(-1, false, TargetFlags.None)
        {
            _actor = actor;
            _deletedId = deletedId;
            _filterOwnerSerial = filterOwnerSerial;
            _pageIndex = pageIndex;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile mobile)
            {
                from.SendMessage(0x22, "That is not a valid mobile.");
                Redisplay();
                return;
            }

            if (TemplateSaverManager.RestoreArchivedDeletedAsStaff(_actor, _deletedId, mobile, false, out var message))
            {
                from.SendMessage(0x35, message);
            }
            else
            {
                from.SendMessage(0x22, message);
            }

            Redisplay();
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            Redisplay();
        }

        private void Redisplay()
        {
            if (_filterOwnerSerial.HasValue)
            {
                DisplayTo(_actor, _filterOwnerSerial.Value, _pageIndex);
            }
            else
            {
                DisplayTo(_actor, _pageIndex);
            }
        }
    }

    private static DeletedTemplateArchiveEntry GetEntryForButton(
        List<DeletedTemplateArchiveEntry> entries,
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
