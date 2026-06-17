using System;
using Server.Gumps;

namespace Server.Custom.Systems.CustomAdmin;

/* BEGIN CUSTOM ADMIN HUB: modules register one admin surface that the shared gump can list, summarize, and open. */
public interface ICustomAdminModule
{
    string Key { get; }
    string DisplayName { get; }
    string Category { get; }
    string Description { get; }
    AccessLevel AccessLevel { get; }
    int SortOrder { get; }

    bool CanOpen { get; }

    string GetStatus(Mobile from);
    void BuildOverview(ref DynamicGumpBuilder builder, Mobile from, int x, int y, int width, int height);
    void Open(Mobile from);
}

public sealed class CustomAdminLinkedModule : ICustomAdminModule
{
    private readonly Action<Mobile> _openAction;
    private readonly Func<Mobile, string> _statusProvider;
    private readonly Func<Mobile, string[]> _detailProvider;

    public CustomAdminLinkedModule(
        string key,
        string displayName,
        string category,
        string description,
        AccessLevel accessLevel,
        int sortOrder,
        Action<Mobile> openAction,
        Func<Mobile, string> statusProvider = null,
        Func<Mobile, string[]> detailProvider = null
    )
    {
        Key = key;
        DisplayName = displayName;
        Category = category;
        Description = description;
        AccessLevel = accessLevel;
        SortOrder = sortOrder;
        _openAction = openAction;
        _statusProvider = statusProvider;
        _detailProvider = detailProvider;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string Description { get; }
    public AccessLevel AccessLevel { get; }
    public int SortOrder { get; }
    public bool CanOpen => _openAction != null;

    public string GetStatus(Mobile from) => _statusProvider?.Invoke(from) ?? "Available";

    public void BuildOverview(ref DynamicGumpBuilder builder, Mobile from, int x, int y, int width, int height)
    {
        builder.AddLabel(x, y, CustomAdminGump.HueHeader, DisplayName);
        builder.AddLabel(x, y + 24, CustomAdminGump.HueSubtle, Category);
        builder.AddHtml(x, y + 56, width, 58, Description, "#D8D8D8", scrollbar: false);
        builder.AddLabel(x, y + 126, CustomAdminGump.HueText, $"Status: {GetStatus(from)}");

        var details = _detailProvider?.Invoke(from);
        if (details == null || details.Length == 0)
        {
            builder.AddLabelCropped(
                x,
                y + 164,
                width,
                22,
                CustomAdminGump.HueSubtle,
                "No additional module details are registered."
            );
            return;
        }

        var detailY = y + 164;
        var maxLines = Math.Min(details.Length, 6);

        for (var i = 0; i < maxLines; i++)
        {
            builder.AddLabelCropped(x, detailY, width, 22, CustomAdminGump.HueText, details[i]);
            detailY += 24;
        }
    }

    public void Open(Mobile from)
    {
        if (from?.NetState == null || from.AccessLevel < AccessLevel)
        {
            return;
        }

        _openAction?.Invoke(from);
    }
}
/* END CUSTOM ADMIN HUB */
