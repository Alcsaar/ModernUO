using Server.Commands;
using Server.Targeting;

namespace Server.Custom.Systems.TemplateSaver;

public static class TemplateSaverCommands
{
    public static void Configure()
    {
        CommandSystem.Register("Templates", AccessLevel.Player, Templates_OnCommand);
        CommandSystem.Register("Tpl", AccessLevel.Player, Templates_OnCommand);
        CommandSystem.Register("Tmpl", AccessLevel.Player, Templates_OnCommand);

        CommandSystem.Register("TemplateUndoDelete", AccessLevel.Player, TemplateUndoDelete_OnCommand);
        CommandSystem.Register("TplUndo", AccessLevel.Player, TemplateUndoDelete_OnCommand);
        CommandSystem.Register("TmplUndo", AccessLevel.Player, TemplateUndoDelete_OnCommand);

        CommandSystem.Register("TemplateRestore", AccessLevel.GameMaster, TemplateRestore_OnCommand);
        CommandSystem.Register("TplRestore", AccessLevel.GameMaster, TemplateRestore_OnCommand);
        CommandSystem.Register("TmplRestore", AccessLevel.GameMaster, TemplateRestore_OnCommand);

        CommandSystem.Register("TemplateRestoreTarget", AccessLevel.GameMaster, TemplateRestoreTarget_OnCommand);
        CommandSystem.Register("TplRestoreTarget", AccessLevel.GameMaster, TemplateRestoreTarget_OnCommand);
        CommandSystem.Register("TmplRestoreTarget", AccessLevel.GameMaster, TemplateRestoreTarget_OnCommand);

        CommandSystem.Register("TemplateInspect", AccessLevel.GameMaster, TemplateInspect_OnCommand);
        CommandSystem.Register("TplInspect", AccessLevel.GameMaster, TemplateInspect_OnCommand);
        CommandSystem.Register("TmplInspect", AccessLevel.GameMaster, TemplateInspect_OnCommand);

        CommandSystem.Register("TemplateSlots", AccessLevel.GameMaster, TemplateSlots_OnCommand);
        CommandSystem.Register("TplSlots", AccessLevel.GameMaster, TemplateSlots_OnCommand);
        CommandSystem.Register("TmplSlots", AccessLevel.GameMaster, TemplateSlots_OnCommand);

        CommandSystem.Register("TemplateExportJson", AccessLevel.Administrator, TemplateExportJson_OnCommand);
        CommandSystem.Register("TplExportJson", AccessLevel.Administrator, TemplateExportJson_OnCommand);
        CommandSystem.Register("TmplExportJson", AccessLevel.Administrator, TemplateExportJson_OnCommand);
    }

    [Usage("Templates")]
    [Description("Opens the skill/stat template saver gump.")]
    private static void Templates_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (from == null)
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

        TemplateSaverGump.DisplayTo(from);
    }

    [Usage("TemplateUndoDelete")]
    [Description("Restores your most recently deleted template if you have room.")]
    private static void TemplateUndoDelete_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (from == null)
        {
            return;
        }

        if (TemplateSaverManager.RestoreMostRecentDeleted(from, out var message))
        {
            from.SendMessage(0x35, message);
        }
        else
        {
            from.SendMessage(0x22, message);
        }
    }

    [Usage("TemplateRestore")]
    [Description("Opens the staff deleted-template restore gump.")]
    private static void TemplateRestore_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (from == null || from.AccessLevel <= AccessLevel.Player)
        {
            return;
        }

        TemplateRestoreAdminGump.DisplayTo(from);
    }

    [Usage("TemplateRestoreTarget")]
    [Description("Target a player to view only that character's deleted template archive entries.")]
    private static void TemplateRestoreTarget_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (from == null || from.AccessLevel <= AccessLevel.Player)
        {
            return;
        }

        from.SendMessage("Target a player to inspect their deleted template archive.");
        from.Target = new TemplateRestoreTarget();
    }

    [Usage("TemplateInspect")]
    [Description("Target a player to inspect their saved templates.")]
    private static void TemplateInspect_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (from == null || from.AccessLevel <= AccessLevel.Player)
        {
            return;
        }

        from.SendMessage("Target a player to inspect their saved templates.");
        from.Target = new TemplateInspectTarget();
    }

    private sealed class TemplateInspectTarget : Target
    {
        public TemplateInspectTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile mobile)
            {
                from.SendMessage(0x22, "That is not a valid player.");
                return;
            }

            TemplateInspectAdminGump.DisplayTo(from, mobile.Serial.Value);
        }
    }

    private sealed class TemplateRestoreTarget : Target
    {
        public TemplateRestoreTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile mobile)
            {
                from.SendMessage(0x22, "That is not a valid player.");
                return;
            }

            TemplateRestoreAdminGump.DisplayTo(from, mobile.Serial.Value);
        }
    }

    [Usage("TemplateSlots add|remove|set <amount>")]
    [Description("Targets a character and adds, removes, or sets extra template slots.")]
    private static void TemplateSlots_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (from == null)
        {
            return;
        }

        if (e.Arguments.Length == 0)
        {
            var extra = TemplateSaverManager.GetExtraSlots(from);
            var total = TemplateSaverManager.GetTemplateSlotLimit(from);

            from.SendMessage(0x35, $"Extra template slots: {extra}");
            from.SendMessage(0x35, $"Total template slots: {total}");
            SendTemplateSlotsUsage(from);
            return;
        }

        if (e.Arguments.Length == 2 &&
            TryParseTemplateSlotAction(e.Arguments[0], out var action) &&
            int.TryParse(e.Arguments[1], out var amount))
        {
            if (amount < 0)
            {
                from.SendMessage(0x22, "Slot amount cannot be negative.");
                return;
            }

            from.SendMessage("Target the character whose template slots you want to change.");
            from.Target = new TemplateSlotsTarget(action, amount);
            return;
        }

        if (e.Arguments.Length == 2 &&
            e.Arguments[0].Equals("target", System.StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(e.Arguments[1], out var targetAmount))
        {
            if (targetAmount < 0)
            {
                from.SendMessage(0x22, "Slot amount cannot be negative.");
                return;
            }

            from.SendMessage("Target the character whose template slots you want to change.");
            from.Target = new TemplateSlotsTarget(TemplateSlotAction.Set, targetAmount);
            return;
        }

        SendTemplateSlotsUsage(from);
    }

    private sealed class TemplateSlotsTarget : Target
    {
        private readonly TemplateSlotAction _action;
        private readonly int _amount;

        public TemplateSlotsTarget(TemplateSlotAction action, int amount) : base(-1, false, TargetFlags.None)
        {
            _action = action;
            _amount = amount;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile mobile)
            {
                from.SendMessage(0x22, "That is not a valid player.");
                return;
            }

            var currentExtraSlots = TemplateSaverManager.GetExtraSlots(mobile.Serial.Value);
            var newExtraSlots = _action switch
            {
                TemplateSlotAction.Add => currentExtraSlots + _amount,
                TemplateSlotAction.Remove => currentExtraSlots - _amount,
                _ => _amount
            };

            if (newExtraSlots < 0)
            {
                from.SendMessage(0x22, $"{mobile.Name} only has {currentExtraSlots} extra template slot(s).");
                return;
            }

            if (TemplateSaverManager.SetExtraSlots(from, mobile.Serial.Value, newExtraSlots, out var message))
            {
                from.SendMessage(0x35, $"{mobile.Name}: {message}");
            }
            else
            {
                from.SendMessage(0x22, message);
            }
        }
    }

    private enum TemplateSlotAction
    {
        Add,
        Remove,
        Set
    }

    private static bool TryParseTemplateSlotAction(string value, out TemplateSlotAction action)
    {
        if (value.Equals("add", System.StringComparison.OrdinalIgnoreCase) ||
            value.Equals("+", System.StringComparison.OrdinalIgnoreCase))
        {
            action = TemplateSlotAction.Add;
            return true;
        }

        if (value.Equals("remove", System.StringComparison.OrdinalIgnoreCase) ||
            value.Equals("rem", System.StringComparison.OrdinalIgnoreCase) ||
            value.Equals("-", System.StringComparison.OrdinalIgnoreCase))
        {
            action = TemplateSlotAction.Remove;
            return true;
        }

        if (value.Equals("set", System.StringComparison.OrdinalIgnoreCase))
        {
            action = TemplateSlotAction.Set;
            return true;
        }

        action = TemplateSlotAction.Set;
        return false;
    }

    private static void SendTemplateSlotsUsage(Mobile from)
    {
        from.SendMessage(0x22, "Usage: [TemplateSlots add <amount>");
        from.SendMessage(0x22, "Usage: [TemplateSlots remove <amount>");
        from.SendMessage(0x22, "Usage: [TemplateSlots set <amount>");
    }

    [Usage("TemplateExportJson")]
    [Description("Exports the current binary Template Saver state to timestamped JSON backup files.")]
    private static void TemplateExportJson_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (from == null || from.AccessLevel < AccessLevel.Administrator)
        {
            return;
        }

        var path = TemplateSaverManager.ExportJsonBackup();
        from.SendMessage(0x35, "Template Saver JSON backup exported.");
        from.SendMessage(path);
    }
}
