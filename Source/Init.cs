using System.Reflection;
using HarmonyLib;

//Harmony entry point.
public class QuickStackModApi : IModApi
{
	public void InitMod(Mod modInstance)
	{
		Harmony harmony = new Harmony(GetType().ToString());
		harmony.PatchAll(Assembly.GetExecutingAssembly());
	}
}

