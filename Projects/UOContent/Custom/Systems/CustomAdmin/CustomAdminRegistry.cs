using System;
using System.Collections.Generic;

namespace Server.Custom.Systems.CustomAdmin;

/* BEGIN CUSTOM ADMIN HUB: central registry lets custom systems add admin modules without editing the hub gump. */
public static class CustomAdminRegistry
{
    private static readonly List<ICustomAdminModule> _modules = new();

    public static void Register(ICustomAdminModule module)
    {
        if (module == null || string.IsNullOrWhiteSpace(module.Key))
        {
            return;
        }

        for (var i = _modules.Count - 1; i >= 0; i--)
        {
            if (_modules[i].Key.Equals(module.Key, StringComparison.OrdinalIgnoreCase))
            {
                _modules.RemoveAt(i);
            }
        }

        _modules.Add(module);
        _modules.Sort(CompareModules);
    }

    public static List<ICustomAdminModule> GetVisibleModules(Mobile from)
    {
        var visible = new List<ICustomAdminModule>();

        if (from?.NetState == null)
        {
            return visible;
        }

        for (var i = 0; i < _modules.Count; i++)
        {
            var module = _modules[i];

            if (from.AccessLevel >= module.AccessLevel)
            {
                visible.Add(module);
            }
        }

        return visible;
    }

    public static ICustomAdminModule FindVisibleModule(Mobile from, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        for (var i = 0; i < _modules.Count; i++)
        {
            var module = _modules[i];

            if (from.AccessLevel >= module.AccessLevel && module.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return module;
            }
        }

        return null;
    }

    private static int CompareModules(ICustomAdminModule left, ICustomAdminModule right)
    {
        var sort = left.SortOrder.CompareTo(right.SortOrder);

        return sort != 0 ? sort : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
    }
}
/* END CUSTOM ADMIN HUB */
