using Server.Commands;

namespace Server.Custom.Systems.CustomFeatureFlags;

public static class CustomFeatureFlagCommands
{
    public static void Configure()
    {
        CommandSystem.Register("CFF", AccessLevel.Administrator, CFF_OnCommand);
        CommandSystem.Register("CF", AccessLevel.Administrator, CFF_OnCommand);
        CommandSystem.Register("CFFG", AccessLevel.Administrator, CFFG_OnCommand);
        CommandSystem.Register("CFFL", AccessLevel.GameMaster, CFFL_OnCommand);
        CommandSystem.Register("CFFT", AccessLevel.Administrator, CFFT_OnCommand);
    }

    [Usage("CFF [key] [on|off|toggle|info]")]
    [Aliases("CF")]
    [Description("Manage custom feature flags. No args opens the admin gump.")]
    private static void CFF_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (e.Length == 0)
        {
            CustomFeatureFlagAdminGump.DisplayTo(from);
            return;
        }

        var key = e.GetString(0);
        var action = e.Length > 1 ? e.GetString(1).ToLowerInvariant() : "info";

        switch (action)
        {
            case "on":
            case "enable":
            case "true":
            case "1":
                {
                    if (CustomFeatureFlagManager.SetEnabled(key, true, from.Name, out var reason))
                    {
                        from.SendMessage(0x35, $"Custom flag '{key}' enabled.");
                    }
                    else
                    {
                        from.SendMessage(0x22, reason ?? "Unable to enable flag.");
                    }

                    break;
                }
            case "off":
            case "disable":
            case "false":
            case "0":
                {
                    if (CustomFeatureFlagManager.SetEnabled(key, false, from.Name, out var reason))
                    {
                        from.SendMessage(0x35, $"Custom flag '{key}' disabled.");
                    }
                    else
                    {
                        from.SendMessage(0x22, reason ?? "Unable to disable flag.");
                    }

                    break;
                }
            case "toggle":
                {
                    if (CustomFeatureFlagManager.Toggle(key, from.Name, out var reason))
                    {
                        var status = CustomFeatureFlagManager.GetStatus(key);
                        from.SendMessage(
                            0x35,
                            $"Custom flag '{key}' stored state is now {(status?.StoredEnabled == true ? "ON" : "OFF")}."
                        );
                    }
                    else
                    {
                        from.SendMessage(0x22, reason ?? "Unable to toggle flag.");
                    }

                    break;
                }
            default:
                {
                    var status = CustomFeatureFlagManager.GetStatus(key);

                    if (status == null)
                    {
                        from.SendMessage(0x22, $"Unknown custom flag '{key}'.");
                        return;
                    }

                    from.SendMessage(0x35, $"=== {status.DisplayName} ===");
                    from.SendMessage($"Key: {status.Key}");
                    from.SendMessage($"Category: {status.Category}");
                    from.SendMessage($"Stored Enabled: {(status.StoredEnabled ? "Yes" : "No")}");
                    from.SendMessage($"Effective Enabled: {(status.EffectiveEnabled ? "Yes" : "No")}");
                    from.SendMessage($"Default Enabled: {(status.DefaultEnabled ? "Yes" : "No")}");
                    from.SendMessage(
                        $"Dependencies: {(status.Dependencies.Length > 0 ? string.Join(", ", status.Dependencies) : "None")}"
                    );
                    from.SendMessage($"Description: {status.Description}");

                    if (status.BlockingDependencies.Length > 0)
                    {
                        from.SendMessage(0x22, $"Blocked By: {string.Join(", ", status.BlockingDependencies)}");
                    }

                    break;
                }
        }
    }

    [Usage("CFFL")]
    [Description("List all custom feature flags.")]
    private static void CFFL_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var list = CustomFeatureFlagManager.GetAllStatuses(includeHidden: true);

        from.SendMessage(0x35, $"=== Custom Feature Flags ({list.Count}) ===");

        for (var i = 0; i < list.Count; i++)
        {
            var status = list[i];
            from.SendMessage(
                $"{status.Key} [Stored:{(status.StoredEnabled ? "ON" : "OFF")}] [Effective:{(status.EffectiveEnabled ? "ON" : "OFF")}] ({status.Category})"
            );
        }
    }

    [Usage("CFFG")]
    [Description("Open the custom feature flag gump.")]
    private static void CFFG_OnCommand(CommandEventArgs e)
    {
        CustomFeatureFlagAdminGump.DisplayTo(e.Mobile);
    }

    [Usage("CFFT")]
    [Description("Write the current custom feature flag config to disk.")]
    private static void CFFT_OnCommand(CommandEventArgs e)
    {
        CustomFeatureFlagManager.Save();
        e.Mobile.SendMessage(0x35, "Custom feature flag config saved.");
    }
}
