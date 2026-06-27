using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

internal class Patches
{
    // ==================================================================================================
    // This patch is used to initialize the functionality for the quick slot locking for the player
    // backpack and the UI functionality for the QuickStack and QuickRestock buttons.
    // ==================================================================================================

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

                // Handle hotkey for locking slots
                QuickStack.InitializeQuickLock(QuickStack.playerBackpack, QuickStack.playerControls);

                // Handle clicking on QuickStack
                XUiController childById = QuickStack.playerControls.GetChildById("btnMoveQuickStack");
                if (childById != null)
                {
                    childById.OnPress += delegate (XUiController _sender, int _args)
                    {
                        QuickStack.RequestQuickStack();
                    };
                }

                // Handle clicking on QuickRestock
                childById = QuickStack.playerControls.GetChildById("btnMoveQuickRestock");
                if (childById != null)
                {
                    childById.OnPress += delegate (XUiController _sender, int _args)
                    {
                        QuickStack.RequestQuickRestock();
                    };
                }
            }
            catch (Exception e)
            {
                QuickStack.LogException(e);
            }
        }
    }

    // ==================================================================================================
    // This patch is used to initialize the functionality for the quick slot locking for the loot window.
    // ==================================================================================================

    [HarmonyPatch(typeof(XUiC_LootWindow), "Init")]
    private class QS_2
    {
        public static void Postfix(XUiC_LootWindow __instance)
        {
            try
            {
                QuickStack.InitializeQuickLock(__instance.lootContainer, __instance.standardControls);
            }
            catch (Exception e)
            {
                QuickStack.LogException(e);
            }
        }
    }

    // ==================================================================================================
    // This patch is used to initialize the functionality for the quick slot locking for the general
    // bags including vehicles and drones.
    // ==================================================================================================

    [HarmonyPatch(typeof(XUiC_BagContainer), "Init")]
    private class QS_3
    {
        public static void Postfix(XUiC_BagContainer __instance)
        {
            try
            {
                QuickStack.InitializeQuickLock(__instance, __instance.standardControls);
            }
            catch (Exception e)
            {
                QuickStack.LogException(e);
            }
        }
    }

    // ==================================================================================================
    // This patch is used to update the slot icon color in the backpack if the slot is locked.
    // ==================================================================================================

    [HarmonyPatch(typeof(XUiC_ItemStackGrid), "OnOpen")]
    private class QS_4
    {
        public static void Postfix(XUiC_ItemStackGrid __instance)
        {
            try
            {
                XUiController[] slots = __instance.GetItemStackControllers();

                for (int i = 0; i < slots.Length; ++i)
                    (slots[i].GetChildById("iconSlotLock").ViewComponent as XUiV_Sprite).Color = QuickStack.lockIconColor;
            }
            catch (Exception e)
            {
                QuickStack.LogException(e);
            }
        }
    }

    // ==================================================================================================
    //  This patch is used to update the slot border color in the backpack if the slot is locked.
    // ==================================================================================================

    [HarmonyPatch(typeof(XUiC_ItemStack), "updateBorderColor")]
    private class QS_5
    {
        public static void Postfix(XUiC_ItemStack __instance)
        {
            try
            {
                if (__instance.UserLockedSlot && QuickStack.lockBorderColor.a > 0)
                {
                    __instance.selectionBorderColor = QuickStack.lockBorderColor;
                }
            }
            catch (Exception e)
            {
                QuickStack.LogException(e);
            }
        }
    }

    // ==================================================================================================
    // This patch is used to add a binding to know whether the player is not accessing other loot
    // container inventories with some exceptions like workstations. This is used in the xml file to make
    // the QuickStack and QuickRestock icons visible only when the player inventory is open.
    // ==================================================================================================

    [HarmonyPatch(typeof(XUiC_BackpackWindow), "GetBindingValueInternal")]
    private class QS_6
    {
        public static void Postfix(ref bool __result, XUiC_BackpackWindow __instance, ref string value, string bindingName)
        {
            try
            {
                if (!__result && bindingName == "notlootingorvehiclestorage")
                {
                    IInventory inventory;
                    value = (!__instance.TryGetMoveDestinationInventory(out inventory)).ToString();
                    __result = true;
                }
            }
            catch (Exception e)
            {
                QuickStack.LogException(e);
            }
        }
    }

    // ==================================================================================================
    // This patch is used to add hotkeys for QuickStack and QuickRestock.
    // ==================================================================================================

    [HarmonyPatch(typeof(EntityPlayerLocal), "Update")]
    private class QS_7
    {
        public static void Postfix()
        {
            try
            {
                if (UICamera.GetKeyDown(QuickStack.quickStackHotkeys[QuickStack.quickStackHotkeys.Length - 1]))
                {
                    for (int i = 0; i < QuickStack.quickStackHotkeys.Length - 1; ++i)
                    {
                        if (!UICamera.GetKey(QuickStack.quickStackHotkeys[i]))
                            return;
                    }

                    QuickStack.RequestQuickStack();
                    QuickStack.PlayClickSound();
                }
                else if (UICamera.GetKeyDown(QuickStack.quickRestockHotkeys[QuickStack.quickRestockHotkeys.Length - 1]))
                {
                    for (int i = 0; i < QuickStack.quickRestockHotkeys.Length - 1; ++i)
                    {
                        if (!UICamera.GetKey(QuickStack.quickRestockHotkeys[i]))
                            return;
                    }

                    QuickStack.RequestQuickRestock();
                    QuickStack.PlayClickSound();
                }
            }
            catch (Exception e)
            {
                QuickStack.LogException(e);
            }
        }
    }

    // ==================================================================================================
    // This patch is used for processing the result after requesting a lock for the containers.
    // ==================================================================================================

    [HarmonyPatch(typeof(LockManager), "LockResponse")]
    private class QS_8
    {
        public static void Postfix(bool _success, string _errorMsg, ReadOnlySpan<ILockTarget> _targets, ILockContext _context, ushort _channel)
        {
            if (QuickStack.stackInProgress == StackType.None)
                return;

            try
            {
                if (_success)
                {
                    if (QuickStack.stackInProgress == StackType.QuickStack)
                        QuickStack.DoQuickStack(_targets);
                    else if (QuickStack.stackInProgress == StackType.QuickRestock)
                        QuickStack.DoQuickRestock(_targets);
                }
            }
            catch (Exception e)
            {
                QuickStack.LogException(e);
            }
        }
    }

    // ==================================================================================================
    // This patch is used for processing the result after requesting an unlock for the containers.
    // ==================================================================================================

    [HarmonyPatch(typeof(LockManager), "UnlockResponse")]
    private class QS_9
    {
        public static void Postfix(bool _success, string _errorMsg, bool _isForceUnlocked)
        {
            QuickStack.stackInProgress = StackType.None;
        }
    }

    // ==================================================================================================
    // This patch is used to not open container UI when requesting a lock for the containers.
    // ==================================================================================================

    [HarmonyPatch(typeof(TEFeatureStorage), "OnLockedLocal")]
    private class QS_10
    {
        public static bool Prefix(bool _success, ILockContext _context, ushort _channel)
        {
            return QuickStack.stackInProgress == StackType.None;
        }
    }

    // ==================================================================================================
    // This patch is used to not close the player UI when requesting a lock for the containers.
    // ==================================================================================================

    [HarmonyPatch(typeof(GUIWindowManager), "CloseAllOpenModalWindows", new Type[] { typeof(GUIWindow), typeof(bool) })]
    private class QS_11
    {
        public static bool Prefix(GUIWindow _exceptWindow, bool _fromEsc)
        {
            return QuickStack.stackInProgress == StackType.None;
        }
    }

    // ==================================================================================================
    // This patch is used to remove the limit of lock requests at a time.
    // ==================================================================================================

    [HarmonyPatch(typeof(LockManager), "LockRequestServer")]
    private class QS_12
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction code in instructions)
            {
                if (code.opcode == OpCodes.Ldc_I4_5)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4, int.MaxValue);
                }
                else
                {
                    yield return code;
                }
            }
        }
    }
}