using System;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server.Custom.Systems.RareSpawns;

/* BEGIN RARE SPAWN ADMIN CONTROLS: staff gumps manage rare spawn points and possible item pools. */
public sealed class RareSpawnAdminGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueHeader = 2213;
    private const int HueText = 2101;
    private const int HueMuted = 2401;
    private const int HueReady = 68;
    private const int HueWarn = 33;
    private const int EntryDisplayName = 1;
    private const int EntrySpawnTypes = 2;
    private const int EntryMinMinutes = 4;
    private const int EntryMaxMinutes = 5;
    private const int ButtonRefresh = 1;
    private const int ButtonCreate = 2;
    private const int ButtonSaveEdit = 3;
    private const int ButtonClearEdit = 4;
    private const int ButtonPrevPage = 5;
    private const int ButtonNextPage = 6;
    private const int ButtonProfilePrev = 7;
    private const int ButtonProfileNext = 8;
    private const int ButtonEditBase = 100;
    private const int ButtonForceBase = 200;
    private const int ButtonDeleteBase = 300;
    private const int ButtonToggleBase = 400;
    private const int ButtonGoBase = 500;
    private const int GumpWidth = 820;
    private const int GumpHeight = 640;
    private const int EntriesPerPage = 7;

    private readonly Mobile _from;
    private readonly int _pageIndex;
    private readonly RareSpawnPoint _editPoint;
    private readonly RareSpawnPoint[] _points;
    private readonly RareSpawnForm? _draft;

    public override bool Singleton => true;

    private RareSpawnAdminGump(Mobile from, int pageIndex, RareSpawnPoint editPoint, RareSpawnForm? draft) : base(60, 45)
    {
        _from = from;
        _pageIndex = Math.Max(0, pageIndex);
        _editPoint = editPoint?.Deleted == false ? editPoint : null;
        _draft = draft;
        _points = RareSpawnManager.GetSpawnPoints();
    }

    public static void DisplayTo(Mobile from, int pageIndex = 0, RareSpawnPoint editPoint = null)
    {
        DisplayTo(from, pageIndex, editPoint, null);
    }

    private static void DisplayTo(Mobile from, int pageIndex, RareSpawnPoint editPoint, RareSpawnForm? draft)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        from.CloseGump<RareSpawnAdminGump>();
        from.SendGump(new RareSpawnAdminGump(from, pageIndex, editPoint, draft));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);
        builder.AddLabel(330, 20, HueTitle, "Rare Spawn Admin");
        builder.AddLabel(34, 50, RareSpawnManager.IsEnabled() ? HueReady : HueWarn, RareSpawnManager.IsEnabled() ? "System: enabled" : "System: disabled");
        builder.AddLabel(650, 50, HueMuted, $"{_points.Length} spawn points");
        DrawRule(ref builder, 30, 78, 760);

        BuildEditor(ref builder);
        BuildList(ref builder);
    }

    private void BuildEditor(ref DynamicGumpBuilder builder)
    {
        var editing = _editPoint != null;
        var form = GetEditorForm();
        builder.AddBackground(34, 94, 560, 138, 9270);
        builder.AddAlphaRegion(42, 102, 544, 122);
        builder.AddLabel(52, 106, HueHeader, editing ? $"Editing: {Truncate(_editPoint.DisplayName, 30)}" : "Create Rare Spawn");

        builder.AddLabel(52, 138, HueText, "Name");
        AddTextField(ref builder, 148, 134, 210, 26, EntryDisplayName, form.DisplayName);

        builder.AddLabel(378, 138, HueText, "Profile");
        builder.AddButton(450, 136, 4014, 4016, ButtonProfilePrev);
        builder.AddBackground(486, 132, 70, 30, 0x2486);
        builder.AddLabel(496, 138, HueTitle, GetProfileShortName(form.Profile));
        builder.AddButton(562, 136, 4005, 4007, ButtonProfileNext);

        builder.AddLabel(52, 174, HueText, "Item types");
        AddTextField(
            ref builder,
            148,
            170,
            410,
            28,
            EntrySpawnTypes,
            form.SpawnTypeNames
        );

        if (form.Profile == RareRespawnProfile.Custom)
        {
            builder.AddLabel(52, 208, HueText, "Min");
            AddTextField(ref builder, 148, 204, 80, 26, EntryMinMinutes, form.MinRespawnMinutes.ToString());
            builder.AddLabel(246, 208, HueText, "Max");
            AddTextField(ref builder, 300, 204, 80, 26, EntryMaxMinutes, form.MaxRespawnMinutes.ToString());
        }
        else
        {
            GetDefaultWindow(form.Profile, out var min, out var max);
            builder.AddLabel(52, 208, HueMuted, $"Window: {FormatWindow(min, max)}");
        }

        builder.AddBackground(614, 94, 172, 138, 9270);
        builder.AddAlphaRegion(622, 102, 156, 122);
        DrawButton(ref builder, 636, 116, editing ? ButtonSaveEdit : ButtonCreate, editing ? "Save" : "Add at Target");
        DrawButton(ref builder, 636, 154, ButtonRefresh, "Refresh");

        if (editing)
        {
            DrawButton(ref builder, 636, 192, ButtonClearEdit, "Clear Edit");
        }
    }

    private void BuildList(ref DynamicGumpBuilder builder)
    {
        DrawRule(ref builder, 30, 250, 760);
        builder.AddLabel(42, 270, HueHeader, "Spawn Points");
        builder.AddLabel(374, 270, HueMuted, "Next");
        builder.AddLabel(516, 270, HueMuted, "State");
        builder.AddLabel(604, 270, HueMuted, "Edit");
        builder.AddLabel(646, 270, HueMuted, "Spawn");
        builder.AddLabel(696, 270, HueMuted, "On");
        builder.AddLabel(732, 270, HueMuted, "Go");
        builder.AddLabel(764, 270, HueMuted, "Del");

        var totalPages = GetTotalPages(_points.Length);
        var pageIndex = Math.Min(_pageIndex, Math.Max(0, totalPages - 1));
        var start = pageIndex * EntriesPerPage;
        var end = Math.Min(start + EntriesPerPage, _points.Length);
        var y = 300;

        if (_points.Length == 0)
        {
            builder.AddLabel(42, y, HueMuted, "No rare spawn points are registered.");
        }
        else
        {
            for (var i = start; i < end; i++)
            {
                var point = _points[i];
                point.CheckRespawn();
                DrawEntry(ref builder, point, i, y);
                y += 38;
            }
        }

        DrawRule(ref builder, 30, 578, 760);
        builder.AddLabel(350, 596, HueText, $"Page {pageIndex + 1}/{Math.Max(1, totalPages)}");

        if (pageIndex > 0)
        {
            DrawButton(ref builder, 42, 592, ButtonPrevPage, "Prev");
        }

        if (pageIndex + 1 < totalPages)
        {
            DrawButton(ref builder, 680, 592, ButtonNextPage, "Next");
        }
    }

    private static void DrawEntry(ref DynamicGumpBuilder builder, RareSpawnPoint point, int index, int y)
    {
        var spawned = point.HasActiveSpawn;
        builder.AddImageTiled(42, y + 34, 720, 1, 2624);
        builder.AddLabel(52, y, point.Enabled ? HueText : HueMuted, Truncate(point.DisplayName, 28));
        builder.AddLabel(52, y + 17, HueMuted, $"{Truncate(point.GetSpawnTypeDisplay(), 22)} | {GetProfileShortName(point.RespawnProfile)}");
        builder.AddLabel(374, y + 8, HueMuted, FormatNextShort(point.NextSpawnTime));
        builder.AddLabel(516, y + 8, spawned ? HueReady : HueMuted, spawned ? "Spawned" : "Waiting");

        builder.AddButton(606, y + 8, 4005, 4007, ButtonEditBase + index);
        builder.AddButton(654, y + 8, 4005, 4007, ButtonForceBase + index);
        builder.AddButton(706, y + 8, point.Enabled ? 4017 : 4005, point.Enabled ? 4019 : 4007, ButtonToggleBase + index);
        builder.AddButton(738, y + 8, 4005, 4007, ButtonGoBase + index);
        builder.AddButton(772, y + 8, 4017, 4019, ButtonDeleteBase + index);
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
            case ButtonRefresh:
                RareSpawnManager.CheckAll();
                DisplayTo(from, _pageIndex, _editPoint);
                return;
            case ButtonCreate:
                BeginCreate(from, info);
                return;
            case ButtonSaveEdit:
                SaveEdit(from, info);
                return;
            case ButtonClearEdit:
                DisplayTo(from, _pageIndex);
                return;
            case ButtonPrevPage:
                DisplayTo(from, Math.Max(0, _pageIndex - 1), _editPoint);
                return;
            case ButtonNextPage:
                DisplayTo(from, _pageIndex + 1, _editPoint);
                return;
            case ButtonProfilePrev:
                CycleProfile(from, info, -1);
                return;
            case ButtonProfileNext:
                CycleProfile(from, info, 1);
                return;
        }

        if (TryGetPointButton(info.ButtonID, ButtonEditBase, out var editPoint))
        {
            DisplayTo(from, _pageIndex, editPoint);
            return;
        }

        if (TryGetPointButton(info.ButtonID, ButtonForceBase, out var forcePoint))
        {
            if (forcePoint.ForceRespawn(out var reason))
            {
                from.SendMessage(0x35, "Rare force-respawned.");
            }
            else
            {
                from.SendMessage(0x22, reason ?? "Rare could not be spawned.");
            }

            DisplayTo(from, _pageIndex, forcePoint);
            return;
        }

        if (TryGetPointButton(info.ButtonID, ButtonToggleBase, out var togglePoint))
        {
            togglePoint.Enabled = !togglePoint.Enabled;
            from.SendMessage(0x35, $"Rare spawn point enabled: {togglePoint.Enabled}");
            DisplayTo(from, _pageIndex, togglePoint);
            return;
        }

        if (TryGetPointButton(info.ButtonID, ButtonGoBase, out var goPoint))
        {
            TeleportToSpawnPoint(from, goPoint);
            DisplayTo(from, _pageIndex, goPoint);
            return;
        }

        if (TryGetPointButton(info.ButtonID, ButtonDeleteBase, out var deletePoint))
        {
            RareSpawnDeleteConfirmGump.DisplayTo(from, deletePoint, _pageIndex);
        }
    }

    private static void TeleportToSpawnPoint(Mobile from, RareSpawnPoint point)
    {
        if (point == null || point.Deleted || point.Map == null || point.Map == Map.Internal)
        {
            from.SendMessage(0x22, "That rare spawn point is not in a valid world location.");
            return;
        }

        /* BEGIN RARE SPAWN ADMIN CONTROLS: hide staff before teleporting to avoid exposing rare locations. */
        from.Hidden = true;
        from.MoveToWorld(point.Location, point.Map);
        from.SendMessage(0x35, $"Teleported hidden to rare spawn point '{point.DisplayName}'.");
        /* END RARE SPAWN ADMIN CONTROLS */
    }

    private void BeginCreate(Mobile from, in RelayInfo info)
    {
        if (!TryReadForm(from, info, out var form))
        {
            DisplayTo(from, _pageIndex, _editPoint);
            return;
        }

        from.SendMessage("Target the world location where this rare spawn point should be placed.");
        from.Target = new AddRareSpawnTarget(form);
    }

    private void CycleProfile(Mobile from, in RelayInfo info, int direction)
    {
        var form = ReadLooseForm(info);
        form = form with { Profile = GetNextProfile(form.Profile, direction) };

        if (form.Profile != RareRespawnProfile.Custom)
        {
            GetDefaultWindow(form.Profile, out var min, out var max);
            form = form with { MinRespawnMinutes = min, MaxRespawnMinutes = max };
        }

        DisplayTo(from, _pageIndex, _editPoint, form);
    }

    private void SaveEdit(Mobile from, in RelayInfo info)
    {
        if (_editPoint == null || _editPoint.Deleted)
        {
            from.SendMessage(0x22, "No rare spawn point is selected.");
            DisplayTo(from, _pageIndex);
            return;
        }

        if (!TryReadForm(from, info, out var form))
        {
            DisplayTo(from, _pageIndex, _editPoint);
            return;
        }

        ApplyForm(_editPoint, form);
        _editPoint.CheckRespawn();
        from.SendMessage(0x35, "Rare spawn point updated.");
        DisplayTo(from, _pageIndex, _editPoint);
    }

    private bool TryReadForm(Mobile from, in RelayInfo info, out RareSpawnForm form)
    {
        form = default;

        var displayName = info.GetTextEntry(EntryDisplayName)?.Trim() ?? string.Empty;
        var spawnTypes = RareSpawnPoint.NormalizeSpawnTypeList(info.GetTextEntry(EntrySpawnTypes));
        var profile = GetEditorForm().Profile;

        if (displayName.Length == 0)
        {
            from.SendMessage(0x22, "Display name is required.");
            return false;
        }

        if (!ValidateSpawnTypes(from, spawnTypes))
        {
            return false;
        }

        var min = 0;
        var max = 0;

        if (profile == RareRespawnProfile.Custom)
        {
            var minText = info.GetTextEntry(EntryMinMinutes);
            var maxText = info.GetTextEntry(EntryMaxMinutes);
            min = Utility.ToInt32(minText);
            max = Utility.ToInt32(maxText);
        }
        else
        {
            GetDefaultWindow(profile, out min, out max);
        }

        if (min < 0 || max < 0 || max < min)
        {
            from.SendMessage(0x22, "Respawn minutes must be non-negative and max must be at least min.");
            return false;
        }

        form = new RareSpawnForm(displayName, spawnTypes, profile, min, max);
        return true;
    }

    private RareSpawnForm ReadLooseForm(in RelayInfo info)
    {
        var current = GetEditorForm();
        var displayName = info.GetTextEntry(EntryDisplayName)?.Trim() ?? current.DisplayName;
        var spawnTypes = RareSpawnPoint.NormalizeSpawnTypeList(info.GetTextEntry(EntrySpawnTypes) ?? current.SpawnTypeNames);
        var min = current.MinRespawnMinutes;
        var max = current.MaxRespawnMinutes;

        if (current.Profile == RareRespawnProfile.Custom)
        {
            min = Utility.ToInt32(info.GetTextEntry(EntryMinMinutes));
            max = Utility.ToInt32(info.GetTextEntry(EntryMaxMinutes));
        }

        return new RareSpawnForm(displayName, spawnTypes, current.Profile, min, max);
    }

    private RareSpawnForm GetEditorForm()
    {
        if (_draft.HasValue)
        {
            return _draft.Value;
        }

        if (_editPoint != null)
        {
            return new RareSpawnForm(
                _editPoint.DisplayName,
                string.Join(", ", _editPoint.GetSpawnTypeList()),
                _editPoint.RespawnProfile,
                _editPoint.MinRespawnMinutes,
                _editPoint.MaxRespawnMinutes
            );
        }

        GetDefaultWindow(RareRespawnProfile.Daily, out var min, out var max);
        return new RareSpawnForm(string.Empty, string.Empty, RareRespawnProfile.Daily, min, max);
    }

    private static RareRespawnProfile GetNextProfile(RareRespawnProfile profile, int direction)
    {
        var values = Enum.GetValues<RareRespawnProfile>();
        var index = Array.IndexOf(values, profile);
        if (index < 0)
        {
            index = 0;
        }

        index = (index + direction + values.Length) % values.Length;
        return values[index];
    }

    private static void GetDefaultWindow(RareRespawnProfile profile, out int min, out int max)
    {
        switch (profile)
        {
            case RareRespawnProfile.ServerBirth:
                min = 0;
                max = 0;
                break;
            case RareRespawnProfile.Weekly:
                min = 6 * 24 * 60;
                max = 8 * 24 * 60;
                break;
            case RareRespawnProfile.Monthly:
                min = 25 * 24 * 60;
                max = 35 * 24 * 60;
                break;
            case RareRespawnProfile.Custom:
                min = 60;
                max = 120;
                break;
            default:
                min = 20 * 60;
                max = 24 * 60;
                break;
        }
    }

    private static string FormatWindow(int min, int max)
    {
        if (min == 0 && max == 0)
        {
            return "once";
        }

        return min == max ? $"{min} minutes" : $"{min}-{max} minutes";
    }

    private static string GetProfileShortName(RareRespawnProfile profile)
    {
        return profile switch
        {
            RareRespawnProfile.ServerBirth => "Birth",
            RareRespawnProfile.Monthly     => "Month",
            RareRespawnProfile.Weekly      => "Week",
            RareRespawnProfile.Custom      => "Custom",
            _                              => "Daily"
        };
    }

    private static string FormatNextShort(DateTime next)
    {
        if (next == DateTime.MinValue)
        {
            return "unscheduled";
        }

        if (next == DateTime.MaxValue)
        {
            return "never";
        }

        return RareSpawnPoint.FormatSpawnTime(next, includeDateForToday: false);
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(unset)";
        }

        return value.Length <= maxLength ? value : value[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static bool ValidateSpawnTypes(Mobile from, string spawnTypes)
    {
        var names = RareSpawnPoint.ParseSpawnTypeNames(spawnTypes);
        if (names.Length == 0)
        {
            from.SendMessage(0x22, "At least one item type is required.");
            return false;
        }

        for (var i = 0; i < names.Length; i++)
        {
            if (!RareSpawnManager.TryResolveItemType(names[i], out _))
            {
                from.SendMessage(0x22, $"'{names[i]}' is not a valid item type.");
                return false;
            }
        }

        return true;
    }

    private static void ApplyForm(RareSpawnPoint point, RareSpawnForm form)
    {
        point.DisplayName = form.DisplayName;
        point.SpawnTypeName = RareSpawnPoint.ParseSpawnTypeNames(form.SpawnTypeNames)[0];
        point.PossibleSpawnTypeNames = form.SpawnTypeNames;
        point.RespawnProfile = form.Profile;
        point.MinRespawnMinutes = form.MinRespawnMinutes;
        point.MaxRespawnMinutes = form.MaxRespawnMinutes;
        point.Enabled = true;
    }

    private bool TryGetPointButton(int buttonId, int buttonBase, out RareSpawnPoint point)
    {
        point = null;

        var index = buttonId - buttonBase;
        if (index < 0 || index >= _points.Length)
        {
            return false;
        }

        point = _points[index];
        return point?.Deleted == false;
    }

    private static int GetTotalPages(int count) => Math.Max(1, (count + EntriesPerPage - 1) / EntriesPerPage);

    private static void DrawButton(ref DynamicGumpBuilder builder, int x, int y, int buttonId, string label)
    {
        builder.AddButton(x, y, 4005, 4007, buttonId);
        builder.AddLabel(x + 36, y + 2, HueText, label);
    }

    private static void AddTextField(ref DynamicGumpBuilder builder, int x, int y, int width, int height, int entryId, string value)
    {
        builder.AddBackground(x - 2, y - 2, width + 4, height + 4, 0x2486);
        builder.AddTextEntry(x + 4, y + 4, width - 8, height - 8, HueTitle, entryId, value ?? string.Empty);
    }

    private static void DrawRule(ref DynamicGumpBuilder builder, int x, int y, int width)
    {
        builder.AddImageTiled(x, y, width, 2, 5058);
        builder.AddImageTiled(x, y + 2, width, 2, 2624);
    }

    private readonly record struct RareSpawnForm(
        string DisplayName,
        string SpawnTypeNames,
        RareRespawnProfile Profile,
        int MinRespawnMinutes,
        int MaxRespawnMinutes
    );

    private sealed class AddRareSpawnTarget : Target
    {
        private readonly RareSpawnForm _form;

        public AddRareSpawnTarget(RareSpawnForm form) : base(-1, true, TargetFlags.None)
        {
            _form = form;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!TryGetTargetLocation(from, targeted, out var point, out var map))
            {
                from.SendMessage(0x22, "That is not a valid world location.");
                DisplayTo(from);
                return;
            }

            var spawnPoint = new RareSpawnPoint();
            ApplyForm(spawnPoint, _form);
            spawnPoint.MoveToWorld(point, map);
            spawnPoint.ScheduleNextSpawn(true);
            spawnPoint.CheckRespawn();
            from.SendMessage(0x35, $"Rare spawn point '{_form.DisplayName}' created.");
            DisplayTo(from, 0, spawnPoint);
        }
    }

    private static bool TryGetTargetLocation(Mobile from, object targeted, out Point3D point, out Map map)
    {
        point = Point3D.Zero;
        map = null;

        if (targeted is not IPoint3D ip)
        {
            return false;
        }

        point = new Point3D(ip);
        map = from.Map;

        if (targeted is Item item)
        {
            point = item.GetWorldLocation();
            map = item.Map;
        }
        else if (targeted is Mobile mobile)
        {
            point = mobile.Location;
            map = mobile.Map;
        }
        else if (targeted is LandTarget land)
        {
            point = land.Location;
        }
        else if (targeted is StaticTarget st)
        {
            point = st.Location;
        }

        return map != null && map != Map.Internal;
    }
}

public sealed class RareSpawnDeleteConfirmGump : DynamicGump
{
    private const int HueTitle = 1153;
    private const int HueText = 2101;
    private const int HueWarn = 33;
    private const int ButtonDelete = 1;
    private const int ButtonCancel = 2;
    private const int GumpWidth = 430;
    private const int GumpHeight = 190;

    private readonly RareSpawnPoint _point;
    private readonly int _returnPage;

    public override bool Singleton => true;

    private RareSpawnDeleteConfirmGump(RareSpawnPoint point, int returnPage) : base(160, 140)
    {
        _point = point;
        _returnPage = Math.Max(0, returnPage);
    }

    public static void DisplayTo(Mobile from, RareSpawnPoint point, int returnPage)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel.GameMaster || point == null || point.Deleted)
        {
            return;
        }

        from.CloseGump<RareSpawnDeleteConfirmGump>();
        from.SendGump(new RareSpawnDeleteConfirmGump(point, returnPage));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, GumpWidth, GumpHeight, 9270);
        builder.AddAlphaRegion(15, 15, GumpWidth - 30, GumpHeight - 30);
        builder.AddLabel(132, 22, HueTitle, "Delete Rare Spawn");
        builder.AddLabel(36, 58, HueText, $"Spawn: {RareSpawnAdminGump.Truncate(_point.DisplayName, 36)}");
        builder.AddLabel(36, 84, HueWarn, "This deletes the spawn point and any active spawned rare.");

        builder.AddButton(70, 130, 4017, 4019, ButtonDelete);
        builder.AddLabel(106, 132, HueWarn, "Delete");
        builder.AddButton(260, 130, 4005, 4007, ButtonCancel);
        builder.AddLabel(296, 132, HueText, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.GameMaster)
        {
            return;
        }

        if (info.ButtonID == ButtonDelete && _point?.Deleted == false)
        {
            DeleteActiveSpawn(_point);
            var name = _point.DisplayName;
            _point.Delete();
            from.SendMessage(0x35, $"Rare spawn point '{name}' deleted.");
            RareSpawnAdminGump.DisplayTo(from, _returnPage);
            return;
        }

        RareSpawnAdminGump.DisplayTo(from, _returnPage, _point?.Deleted == false ? _point : null);
    }

    private static void DeleteActiveSpawn(RareSpawnPoint point)
    {
        if (point.SpawnedItem is not { Deleted: false } spawned)
        {
            return;
        }

        if (spawned.Spawner == point)
        {
            spawned.Spawner = null;
        }

        spawned.Delete();
    }
}
