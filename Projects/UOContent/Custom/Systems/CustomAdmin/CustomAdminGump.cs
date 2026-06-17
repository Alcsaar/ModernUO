using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;

namespace Server.Custom.Systems.CustomAdmin;

/* BEGIN CUSTOM ADMIN HUB: shared staff gump lists registered custom-system admin modules and opens their controls. */
public sealed class CustomAdminGump : DynamicGump
{
    public const int HueTitle = 1153;
    public const int HueHeader = 2213;
    public const int HueText = 2101;
    public const int HueMuted = 2401;
    public const int HueSubtle = 2413;
    public const int HueReady = 68;
    public const int HueWarn = 33;

    private const int ButtonOpenSelected = 1;
    private const int ButtonRefresh = 2;
    private const int ButtonPrevPage = 3;
    private const int ButtonNextPage = 4;
    private const int ButtonModuleBase = 100;
    private const int GumpWidth = 880;
    private const int GumpHeight = 560;
    private const int ModulesPerPage = 12;

    private readonly Mobile _from;
    private readonly string _selectedKey;
    private readonly int _pageIndex;
    private readonly List<ICustomAdminModule> _modules;

    public override bool Singleton => true;

    private CustomAdminGump(Mobile from, string selectedKey, int pageIndex) : base(55, 45)
    {
        _from = from;
        _selectedKey = selectedKey;
        _pageIndex = Math.Max(0, pageIndex);
        _modules = CustomAdminRegistry.GetVisibleModules(from);
    }

    public static void DisplayTo(Mobile from, string selectedKey = null, int pageIndex = 0)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        from.CloseGump<CustomAdminGump>();
        from.SendGump(new CustomAdminGump(from, selectedKey, pageIndex));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);
        builder.AddLabel(342, 20, HueTitle, "Custom Admin");
        builder.AddLabel(40, 52, HueText, $"{_modules.Count} module(s) available");
        DrawRule(ref builder, 34, 78, 812);

        BuildModuleList(ref builder);
        BuildDetailPanel(ref builder);

        DrawRule(ref builder, 34, 508, 812);
        DrawButton(ref builder, 48, 526, ButtonRefresh, "Refresh");
    }

    private void BuildModuleList(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(34, 96, 250, 394, 9270);
        builder.AddAlphaRegion(42, 104, 234, 378);
        builder.AddLabel(56, 116, HueHeader, "Modules");
        DrawRule(ref builder, 56, 142, 198);

        var totalPages = GetTotalPages(_modules.Count);
        var pageIndex = ClampPageIndex(_pageIndex, totalPages);
        var start = pageIndex * ModulesPerPage;
        var end = Math.Min(start + ModulesPerPage, _modules.Count);
        var y = 162;

        if (_modules.Count == 0)
        {
            builder.AddLabelCropped(56, y, 200, 44, HueMuted, "No custom admin modules are registered.");
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                DrawModuleRow(ref builder, _modules[i], i, y);
                y += 30;
            }
        }

        builder.AddLabel(112, 462, HueSubtle, $"Page {pageIndex + 1}/{Math.Max(1, totalPages)}");

        if (pageIndex > 0)
        {
            builder.AddButton(56, 460, 4014, 4016, ButtonPrevPage);
        }

        if (pageIndex + 1 < totalPages)
        {
            builder.AddButton(238, 460, 4005, 4007, ButtonNextPage);
        }
    }

    private void DrawModuleRow(ref DynamicGumpBuilder builder, ICustomAdminModule module, int index, int y)
    {
        var selected = IsSelected(module);

        builder.AddButton(56, y, selected ? 4006 : 4005, selected ? 4008 : 4007, ButtonModuleBase + index);
        builder.AddLabelCropped(90, y + 2, 172, 22, selected ? HueReady : HueText, module.DisplayName);
    }

    private void BuildDetailPanel(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(306, 96, 540, 394, 9270);
        builder.AddAlphaRegion(314, 104, 524, 378);

        var module = GetSelectedModule();
        if (module == null)
        {
            builder.AddLabel(334, 132, HueMuted, "Select a module to view its admin controls.");
            return;
        }

        module.BuildOverview(ref builder, _from, 334, 124, 470, 260);

        if (module.CanOpen)
        {
            DrawButton(ref builder, 334, 438, ButtonOpenSelected, $"Open {module.DisplayName}");
        }
        else
        {
            builder.AddLabel(334, 442, HueMuted, "This module only provides a hub summary.");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case ButtonOpenSelected:
                GetSelectedModule()?.Open(from);
                return;
            case ButtonRefresh:
                DisplayTo(from, _selectedKey, _pageIndex);
                return;
            case ButtonPrevPage:
                DisplayTo(from, _selectedKey, Math.Max(0, _pageIndex - 1));
                return;
            case ButtonNextPage:
                DisplayTo(from, _selectedKey, _pageIndex + 1);
                return;
        }

        var moduleIndex = info.ButtonID - ButtonModuleBase;
        if (moduleIndex >= 0 && moduleIndex < _modules.Count)
        {
            var pageIndex = moduleIndex / ModulesPerPage;
            DisplayTo(from, _modules[moduleIndex].Key, pageIndex);
        }
    }

    private ICustomAdminModule GetSelectedModule()
    {
        if (_modules.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_selectedKey))
        {
            for (var i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];

                if (module.Key.Equals(_selectedKey, StringComparison.OrdinalIgnoreCase))
                {
                    return module;
                }
            }
        }

        return _modules[0];
    }

    private bool IsSelected(ICustomAdminModule module)
    {
        var selected = GetSelectedModule();
        return selected != null && selected.Key.Equals(module.Key, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetTotalPages(int count) => Math.Max(1, (count + ModulesPerPage - 1) / ModulesPerPage);

    private static int ClampPageIndex(int pageIndex, int totalPages) =>
        Math.Max(0, Math.Min(pageIndex, Math.Max(0, totalPages - 1)));

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 36, y + 2, HueText, label);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }
}
/* END CUSTOM ADMIN HUB */
