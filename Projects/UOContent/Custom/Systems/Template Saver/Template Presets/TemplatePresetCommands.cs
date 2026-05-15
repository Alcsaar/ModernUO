using Server.Commands;

namespace Server.Custom.Systems.TemplateSaver;

public static class TemplatePresetCommands
{
    public static void Configure()
    {
        CommandSystem.Register("TemplatePresets", AccessLevel.GameMaster, TemplatePresets_OnCommand);
        CommandSystem.Register("TemplatePresetCapture", AccessLevel.Owner, TemplatePresetCapture_OnCommand);
        CommandSystem.Register("TemplatePresetDelete", AccessLevel.Owner, TemplatePresetDelete_OnCommand);
        CommandSystem.Register("TemplatePresetLoad", AccessLevel.GameMaster, TemplatePresetLoad_OnCommand);
    }

    [Usage("TemplatePresets")]
    [Description("Opens the staff template preset library.")]
    private static void TemplatePresets_OnCommand(CommandEventArgs e)
    {
        if (!TemplatePresetManager.CanUse(e.Mobile, out var message))
        {
            e.Mobile.SendMessage(0x22, message);
            return;
        }

        TemplatePresetGump.DisplayTo(e.Mobile);
    }

    [Usage("TemplatePresetCapture <PresetName> <Tier>")]
    [Description("Owner only. Captures your current stats and skills into a preset tier.")]
    private static void TemplatePresetCapture_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 2)
        {
            e.Mobile.SendMessage("Usage: [TemplatePresetCapture <PresetName> <Tier>");
            return;
        }

        var presetName = e.GetString(0);
        var tier = e.GetString(1);

        if (TemplatePresetManager.CreateOrUpdateFromSelf(e.Mobile, presetName, tier, out var message))
        {
            e.Mobile.SendMessage(0x35, message);
        }
        else
        {
            e.Mobile.SendMessage(0x22, message);
        }

        TemplatePresetGump.DisplayTo(e.Mobile);
    }

    [Usage("TemplatePresetDelete <PresetName> <Tier>")]
    [Description("Owner only. Deletes a preset tier from the library.")]
    private static void TemplatePresetDelete_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 2)
        {
            e.Mobile.SendMessage("Usage: [TemplatePresetDelete <PresetName> <Tier>");
            return;
        }

        var presetName = e.GetString(0);
        var tier = e.GetString(1);

        if (TemplatePresetManager.DeletePresetTier(e.Mobile, presetName, tier, out var message))
        {
            e.Mobile.SendMessage(0x35, message);
        }
        else
        {
            e.Mobile.SendMessage(0x22, message);
        }

        TemplatePresetGump.DisplayTo(e.Mobile);
    }

    [Usage("TemplatePresetLoad <PresetName> <Tier>")]
    [Description("Loads a preset tier onto yourself.")]
    private static void TemplatePresetLoad_OnCommand(CommandEventArgs e)
    {
        if (e.Length < 2)
        {
            e.Mobile.SendMessage("Usage: [TemplatePresetLoad <PresetName> <Tier>");
            return;
        }

        var presetName = e.GetString(0);
        var tier = e.GetString(1);

        if (TemplatePresetManager.LoadPresetOntoSelf(e.Mobile, presetName, tier, out var message))
        {
            e.Mobile.SendMessage(0x35, message);
        }
        else
        {
            e.Mobile.SendMessage(0x22, message);
        }

        TemplatePresetGump.DisplayTo(e.Mobile);
    }
}
