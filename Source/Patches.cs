using System;
using Audio;
using HarmonyLib;

internal class Patches
{
    // This patch is used to initialize the functionality for the quick slot locking and the UI functionality for the QuickStack and QuickRestock buttons
    [HarmonyPatch(typeof(XUiC_BackpackWindow), "Init")]
    private class QS_1
    {
        public static void Postfix(XUiC_BackpackWindow __instance)
        {
            try
            {
                QuickStack.backpackWindow = __instance;
                QuickStack.playerControls = __instance.GetChildByType<XUiC_ContainerStandardControls>();
                QuickStack.playerBackpack = __instance.backpackGrid;
                QuickStack.lastClickTimes.Fill(0.0f);

                XUiController[] slots = QuickStack.playerBackpack.GetItemStackControllers();

                // Handle hotkey for locking slots
                for (int i = 0; i < slots.Length; ++i)
                {
                    slots[i].OnPress += (XUiController _sender, int _mouseButton) =>
                    {
                        for (int j = 0; j < QuickStack.quickLockHotkeys.Length; j++)
                        {
                            if (!UICamera.GetKey(QuickStack.quickLockHotkeys[j]))
                                return;
                        }

                        XUiC_ItemStack itemStack = _sender as XUiC_ItemStack;

                        itemStack.UserLockedSlot = !itemStack.UserLockedSlot;
                        QuickStack.backpackWindow.UpdateLockedSlots(QuickStack.playerControls);
                        itemStack.xui.PlayMenuClickSound();
                    };
                }

                // Handle clicking on QuickStack and QuickRestock
                XUiController childById = QuickStack.playerControls.GetChildById("btnMoveQuickStack");
                if (childById != null)
                {
                    childById.OnPress += delegate (XUiController _sender, int _args)
                    {
                        QuickStack.QuickStackOnClick();
                    };
                }

                childById = QuickStack.playerControls.GetChildById("btnMoveQuickRestock");
                if (childById != null)
                {
                    childById.OnPress += delegate (XUiController _sender, int _args)
                    {
                        QuickStack.QuickRestockOnClick();
                    };
                }
            }
            catch (Exception e)
            {
                QuickStack.printExceptionInfo(e);
            }
        }
    }

    // This patch is used to update the UI whenever the backpack is opened
    [HarmonyPatch(typeof(XUiC_BackpackWindow), "OnOpen")]
    private class QS_2
    {
        public static void Postfix(XUiC_BackpackWindow __instance)
        {
            QuickStack.UpdateUI();
        }
    }

   // This patch is used to add a binding to know whether the player is not accessing other loot container inventories with some exceptions like workstations.
   // This is used in the xml file to make the quickstack icon visible only when the player inventory is open.
   [HarmonyPatch(typeof(XUiC_BackpackWindow), "GetBindingValueInternal")]
    private class QS_3
    {
        public static void Postfix(ref bool __result, XUiC_BackpackWindow __instance, ref string value, string bindingName)
        {
            try
            {
                if (__result == false)
                {
                    if (bindingName != null)
                    {
                        if (bindingName == "notlootingorvehiclestorage")
                        {
                            bool flag1 = __instance.xui.vehicle != null && __instance.xui.vehicle.GetVehicle().HasStorage();
                            bool flag2 = __instance.xui.lootContainer != null && __instance.xui.lootContainer.EntityId == -1;
                            bool flag3 = __instance.xui.lootContainer != null && GameManager.Instance.World.GetEntity(__instance.xui.lootContainer.EntityId) is EntityDrone;
                            value = (!flag1 && !flag2 && !flag3).ToString();
                            __result = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                QuickStack.printExceptionInfo(e);
            }
        }
    }

    // QuickStack and Restock functionality by pressing hotkeys (useful if other mods remove the UI buttons)
    [HarmonyPatch(typeof(GameManager), "UpdateTick")]
    private class QS_4
    {
        public static void Postfix(EntityPlayerLocal __instance)
        {
            try
            {
                if (UICamera.GetKeyDown(QuickStack.quickStackHotkeys[QuickStack.quickStackHotkeys.Length - 1]))
                {
                    for (int i = 0; i < QuickStack.quickStackHotkeys.Length - 1; i++)
                    {
                        if (!UICamera.GetKey(QuickStack.quickStackHotkeys[i]))
                            return;
                    }

                    QuickStack.QuickStackOnClick();
                    Manager.PlayButtonClick();
                }
                else if (UICamera.GetKeyDown(QuickStack.quickRestockHotkeys[QuickStack.quickRestockHotkeys.Length - 1]))
                {
                    for (int i = 0; i < QuickStack.quickRestockHotkeys.Length - 1; i++)
                    {
                        if (!UICamera.GetKey(QuickStack.quickRestockHotkeys[i]))
                            return;
                    }

                    QuickStack.QuickRestockOnClick();
                    Manager.PlayButtonClick();
                }
            }
            catch (Exception e)
            {
                QuickStack.printExceptionInfo(e);
            }
        }
    }

    // This patch is used to update the slot color in the backpack if the slot is locked by the player.
    [HarmonyPatch(typeof(XUiC_ItemStack), "updateBorderColor")]
    private class QS_5
    {
        [HarmonyPostfix]
        public static void Postfix(XUiC_ItemStack __instance)
        {
            try
            {
                if (__instance.UserLockedSlot && QuickStack.lockBorderColor.a > 0)
                    __instance.selectionBorderColor = QuickStack.lockBorderColor;
            }
            catch (Exception e)
            {
                QuickStack.printExceptionInfo(e);
            }
        }
    }
}