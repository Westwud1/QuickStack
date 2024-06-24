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
        //Load config from QuickstackConfig.xml
        try
        {
            string path = GamePrefs.GetString(EnumGamePrefs.UNUSED_UserDataFolder) + "/Mods/QuickStack";
            if (!Directory.Exists(path))
                path = Directory.GetCurrentDirectory() + "/Mods/QuickStack";

            XmlDocument xml = new XmlDocument();
            xml.Load(path + "/QuickStackConfig.xml");

            string[] quickLockButtons = xml.GetElementsByTagName("QuickLockButtons")[0].InnerText.Split(' ');
            QuickStack.quickLockHotkeys = new KeyCode[quickLockButtons.Length];
            for (int i = 0; i < quickLockButtons.Length; i++)
                QuickStack.quickLockHotkeys[i] = (KeyCode)int.Parse(quickLockButtons[i]);

            string[] quickStackButtons = xml.GetElementsByTagName("QuickStackButtons")[0].InnerText.Split(' ');
            QuickStack.quickStackHotkeys = new KeyCode[quickStackButtons.Length];
            for (int i = 0; i < quickStackButtons.Length; i++)
                QuickStack.quickStackHotkeys[i] = (KeyCode)int.Parse(quickStackButtons[i]);

            string[] quickRestockButtons = xml.GetElementsByTagName("QuickRestockButtons")[0].InnerText.Split(' ');
            QuickStack.quickRestockHotkeys = new KeyCode[quickRestockButtons.Length];
            for (int i = 0; i < quickRestockButtons.Length; i++)
                QuickStack.quickRestockHotkeys[i] = (KeyCode)int.Parse(quickRestockButtons[i]);

            QuickStack.stashDistanceX = Int32.Parse(xml.GetElementsByTagName("QuickStashDistanceX")[0].InnerText);
            if (QuickStack.stashDistanceX < 0 || QuickStack.stashDistanceX > 127)
                QuickStack.stashDistanceX = 7;

            QuickStack.stashDistanceY = Int32.Parse(xml.GetElementsByTagName("QuickStashDistanceY")[0].InnerText);
            if (QuickStack.stashDistanceY < 0 || QuickStack.stashDistanceY > 127)
                QuickStack.stashDistanceY = 7;

            QuickStack.stashDistanceZ = Int32.Parse(xml.GetElementsByTagName("QuickStashDistanceZ")[0].InnerText);
            if (QuickStack.stashDistanceZ < 0 || QuickStack.stashDistanceZ > 127)
                QuickStack.stashDistanceZ = 7;
        }
        catch
        {
            Log.Warning("[QuickStack] Failed to load or parse config");

            QuickStack.quickLockHotkeys = new KeyCode[1];
            QuickStack.quickLockHotkeys[0] = KeyCode.LeftAlt;

            QuickStack.quickStackHotkeys = new KeyCode[2];
            QuickStack.quickStackHotkeys[0] = KeyCode.LeftAlt;
            QuickStack.quickStackHotkeys[1] = KeyCode.X;

            QuickStack.quickRestockHotkeys = new KeyCode[2];
            QuickStack.quickRestockHotkeys[0] = KeyCode.LeftAlt;
            QuickStack.quickRestockHotkeys[1] = KeyCode.Z;

            QuickStack.stashDistanceX = 7;
            QuickStack.stashDistanceY = 7;
            QuickStack.stashDistanceZ = 7;
        }

        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

