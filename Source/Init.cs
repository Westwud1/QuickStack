using System.Reflection;
using HarmonyLib;

public class QuickStackModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        QuickStack.version = modInstance.VersionString;
        QuickStack.configFilePath = modInstance.Path + "/QuickStackConfig.xml";
        QuickStack.LoadConfig();
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

