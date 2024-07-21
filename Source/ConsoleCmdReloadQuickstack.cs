using System;
using System.Collections.Generic;

public class ConsoleCmdReloadQuickStack : ConsoleCmdAbstract
{
    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        try
        {
            if (_params.Count > 0)
                SingletonMonoBehaviour<SdtdConsole>.Instance.Output("[QuickStack] Ignoring extra parameters");

            QuickStack.LoadConfig();
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }

    public override string[] getCommands()
    {
        return new string[]
        {
            "reloadquickstack",
            "reloadqs"
        };
    }

    public override string getDescription()
    {
        return "Reloads QuickStack config";
    }
}