using System;
using System.IO;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using UnityEngine;

//Harmony entry point.
public class QuickStackModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        QuickStack.LoadConfig();
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

