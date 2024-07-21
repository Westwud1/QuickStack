using System.Reflection;
using HarmonyLib;

//Harmony entry point.
public class QuickStackModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        QuickStack.configFilePath = modInstance.Path + "/QuickStackConfig.xml";
        QuickStack.LoadConfig();
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

