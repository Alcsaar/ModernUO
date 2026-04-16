using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.TemplateSaver;

public sealed class TemplateSkillListGump : DynamicGump
{
    private const int ButtonClose = 0;
    private const int ButtonBack = 1;
    private const int ButtonPrevPage = 2;
    private const int ButtonNextPage = 3;

    private const int SkillsPerPage = 14;

    private readonly Mobile _from;
    private readonly string _title;
    private readonly List<SkillTemplateSnapshot> _skills;
    private readonly int _pageIndex;
    private readonly Action<Mobile> _onBack;

    public override bool Singleton => true;

    private TemplateSkillListGump(
        Mobile from,
        string title,
        List<SkillTemplateSnapshot> skills,
        int pageIndex,
        Action<Mobile> onBack
    ) : base(80, 60)
    {
        _from = from;
        _title = string.IsNullOrWhiteSpace(title) ? "All Skills" : title;
        _skills = CreateSortedSkills(skills);
        _pageIndex = Math.Max(0, pageIndex);
        _onBack = onBack;
    }

    public static void DisplayTo(
        Mobile from,
        string title,
        List<SkillTemplateSnapshot> skills,
        Action<Mobile> onBack,
        int pageIndex = 0
    )
    {
        if (from?.NetState == null)
        {
            return;
        }

        from.SendGump(new TemplateSkillListGump(from, title, skills, pageIndex, onBack));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 520, 470, 9270);
        builder.AddAlphaRegion(15, 15, 490, 440);

        builder.AddHtml(20, 20, 300, 25, HtmlColor(_title, "#00FFFF"));
        builder.AddHtml(350, 20, 130, 25, HtmlColor($"Skills: {_skills.Count}", "#FFFFFF"));
        builder.AddButton(485, 20, 4017, 4019, ButtonClose);

        builder.AddHtml(25, 55, 180, 20, HtmlColor("Skill", "#FFFFFF"));
        builder.AddHtml(250, 55, 80, 20, HtmlColor("Value", "#FFFFFF"));
        builder.AddHtml(350, 55, 100, 20, HtmlColor("Lock", "#FFFFFF"));
        builder.AddHtml(20, 74, 480, 2, "<basefont color=#808080><hr></basefont>");

        var totalPages = GetTotalPages(_skills.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * SkillsPerPage;
        var end = Math.Min(start + SkillsPerPage, _skills.Count);
        var y = 88;

        if (_skills.Count == 0)
        {
            builder.AddHtml(30, y, 300, 20, HtmlColor("No saved skills.", "#C0C0C0"));
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var skill = _skills[i];
                var skillName = string.IsNullOrWhiteSpace(skill.SkillName) ? $"Skill {skill.SkillIndex}" : skill.SkillName;

                builder.AddHtml(30, y, 210, 20, HtmlColor(skillName, "#FFFFFF"));
                builder.AddHtml(250, y, 70, 20, HtmlColor($"{skill.Base:0.0}", "#C0C0C0"));
                builder.AddHtml(350, y, 100, 20, HtmlColor(SkillLockToText(skill.Lock), SkillLockToColor(skill.Lock)));
                builder.AddHtml(20, y + 20, 480, 2, "<basefont color=#404040><hr></basefont>");
                y += 26;
            }
        }

        builder.AddButton(20, 435, 4014, 4016, ButtonBack);
        builder.AddHtml(55, 437, 50, 20, HtmlColor("Back", "#FFFFFF"));

        builder.AddHtml(205, 437, 100, 20, HtmlColor($"Page {pageIndex + 1}/{Math.Max(1, totalPages)}", "#FFFFFF"));

        if (pageIndex > 0)
        {
            builder.AddButton(360, 435, 4014, 4016, ButtonPrevPage);
            builder.AddHtml(395, 437, 40, 20, HtmlColor("Prev", "#FFFFFF"));
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(435, 435, 4005, 4007, ButtonNextPage);
            builder.AddHtml(470, 437, 40, 20, HtmlColor("Next", "#FFFFFF"));
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        switch (info.ButtonID)
        {
            case ButtonClose:
                return;

            case ButtonBack:
                {
                    _onBack?.Invoke(_from);
                    return;
                }

            case ButtonPrevPage:
                {
                    DisplayTo(_from, _title, _skills, _onBack, Math.Max(0, _pageIndex - 1));
                    return;
                }

            case ButtonNextPage:
                {
                    DisplayTo(_from, _title, _skills, _onBack, _pageIndex + 1);
                    return;
                }
        }
    }

    private static List<SkillTemplateSnapshot> CreateSortedSkills(List<SkillTemplateSnapshot> skills)
    {
        var sortedSkills = skills != null ? new List<SkillTemplateSnapshot>(skills) : new List<SkillTemplateSnapshot>();
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

    private static int GetTotalPages(int count)
    {
        return Math.Max(1, (count + SkillsPerPage - 1) / SkillsPerPage);
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

    private static string SkillLockToText(SkillLock skillLock)
    {
        return skillLock switch
        {
            SkillLock.Up => "Up",
            SkillLock.Down => "Down",
            _ => "Locked"
        };
    }

    private static string SkillLockToColor(SkillLock skillLock)
    {
        return skillLock switch
        {
            SkillLock.Up => "#66FF66",
            SkillLock.Down => "#FF6666",
            _ => "#66B2FF"
        };
    }

    private static string HtmlColor(string text, string hex)
    {
        return $"<BASEFONT COLOR=\"{hex}\">{text}</BASEFONT>";
    }
}
