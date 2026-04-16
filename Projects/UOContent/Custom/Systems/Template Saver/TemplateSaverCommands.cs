using Server.Commands;
using Server.Targeting;

namespace Server.Custom.Systems.TemplateSaver;

public static class TemplateSaverCommands
{
    public static void Configure()
    {
        CommandSystem.Register("Templates", AccessLevel.Player, Templates_OnCommand);
        CommandSystem.Register("TemplateUndoDelete", AccessLevel.Player, TemplateUndoDelete_OnCommand);
        CommandSystem.Register("TemplateRestore", AccessLevel.GameMaster, TemplateRestore_OnCommand);
        CommandSystem.Register("TemplateRestoreTarget", AccessLevel.GameMaster, TemplateRestoreTarget_OnCommand);
        CommandSystem.Register("TemplateInspect", AccessLevel.GameMaster, TemplateInspect_OnCommand);
        CommandSystem.Register("TemplateSlots", AccessLevel.GameMaster, TemplateSlots_OnCommand);
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

    [Usage("TemplateSlots [amount] | TemplateSlots target <amount>")]
    [Description("Shows or sets extra template slots. Use 'target' to apply the slot count to another character.")]
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
            return;
        }

        if (e.Arguments.Length >= 2 && e.Arguments[0].Equals("target", System.StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(e.Arguments[1], out var targetAmount))
            {
                from.SendMessage(0x22, "Usage: [TemplateSlots target <amount>");
                return;
            }

            if (targetAmount < 0)
            {
                from.SendMessage(0x22, "Slot amount cannot be negative.");
                return;
            }

            from.SendMessage("Target the character whose template slots you want to change.");
            from.Target = new TemplateSlotsTarget(targetAmount);
            return;
        }

        if (!int.TryParse(e.Arguments[0], out var amount))
        {
            from.SendMessage(0x22, "Usage: [TemplateSlots [amount] | [TemplateSlots target <amount>");
            return;
        }

        if (amount < 0)
        {
            from.SendMessage(0x22, "Slot amount cannot be negative.");
            return;
        }

        if (TemplateSaverManager.SetExtraSlots(from, from.Serial.Value, amount, out var message))
        {
            from.SendMessage(0x35, message);
        }
        else
        {
            from.SendMessage(0x22, message);
        }
    }

    private sealed class TemplateSlotsTarget : Target
    {
        private readonly int _amount;

        public TemplateSlotsTarget(int amount) : base(-1, false, TargetFlags.None)
        {
            _amount = amount;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not Mobile mobile)
            {
                from.SendMessage(0x22, "That is not a valid player.");
                return;
            }

            if (TemplateSaverManager.SetExtraSlots(from, mobile.Serial.Value, _amount, out var message))
            {
                from.SendMessage(0x35, $"{mobile.Name}: {message}");
            }
            else
            {
                from.SendMessage(0x22, message);
            }
        }
    }
}
