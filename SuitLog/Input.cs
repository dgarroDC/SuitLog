using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace SuitLog;

public static class Input
{
    public enum Action
    {
        OpenSuitLog,
        CloseSuitLog,
        ViewEntries,
        CloseEntries,
        MarkEntryOnHUD,
        ListUp,
        ListDown,
        ScrollFactsKbm,
        ScrollFactsGamepad
    }

    private static List<IInputCommands> GetInputCommands(Action action)
    {
        switch (action)
        {
            case Action.OpenSuitLog:
                return new List<IInputCommands>{InputLibrary.autopilot};
            // The rest of actions use the same input commands as the ship log
            case Action.CloseSuitLog:
                return new List<IInputCommands>{InputLibrary.cancel};
            case Action.ViewEntries:
                return new List<IInputCommands>{InputLibrary.interact};
            case Action.CloseEntries:
                // The prompt will show the cancel command but interact is also possible
                return new List<IInputCommands>{InputLibrary.cancel, InputLibrary.interact};
            case Action.MarkEntryOnHUD:
                return new List<IInputCommands>{InputLibrary.markEntryOnHUD};
            case Action.ListUp:
                return new List<IInputCommands>{InputLibrary.up, InputLibrary.up2};
            case Action.ListDown:
                return new List<IInputCommands>{InputLibrary.down, InputLibrary.down2};
            case Action.ScrollFactsKbm:
                return new List<IInputCommands>{InputLibrary.toolOptionY};
            case Action.ScrollFactsGamepad:
                return new List<IInputCommands>{InputLibrary.scrollLogText};
        }

        return null;
    }

    private static bool CheckAction(Action action, Func<IInputCommands, bool> checker)
    {
        foreach (IInputCommands commands in GetInputCommands(action))
        {
            if (checker.Invoke(commands))
            {
                return true;
            }
        }

        return false;
    }

    public static IInputCommands PromptCommands(Action action)
    {
        return GetInputCommands(action)[0];
    }

    public static bool IsNewlyPressed(Action action)
    {
        return CheckAction(action, commands => OWInput.IsNewlyPressed(commands));
    }

    public static bool IsPressed(Action action)
    {
        return CheckAction(action, commands => OWInput.IsPressed(commands));
    }

    public static float GetValue(Action action)
    {
        List<IInputCommands> commands = GetInputCommands(action);
        return OWInput.GetValue(commands[0]);
    }
}