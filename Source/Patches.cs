using System;
using Audio;
using HarmonyLib;
using UnityEngine;

internal class Patches
{
	//This patch is used to initialize the UI functionallity for the quickstack and restock buttons.
	[HarmonyPatch(typeof(XUiC_ContainerStandardControls), "Init")]
	private class QS_1
	{
		public static void Postfix(XUiC_ContainerStandardControls __instance)
		{
			XUiController childById = __instance.GetChildById("btnMoveQuickStack");
			if (childById != null)
			{
				childById.OnPress += delegate (XUiController _sender, int _args)
				{
					QuickStack.MoveQuickStack(__instance);
				};
			}

			childById = __instance.GetChildById("btnMoveQuickRestock");
			if (childById != null)
			{
				childById.OnPress += delegate (XUiController _sender, int _args)
				{
					QuickStack.MoveQuickRestock(__instance);
				};
			}
		}
	}

	//This patch overrides the original one to accommodate the locked slots.
	[HarmonyPatch(typeof(XUiC_ContainerStandardControls), "MoveAll")]
	/*private class QS_2
	{
		public static bool Prefix(XUiC_ContainerStandardControls __instance)
		{
			if (__instance.Parent.Parent.GetType() != typeof(XUiC_BackpackWindow))
				return true;

			XUiC_ItemStackGrid srcGrid;
			IInventory dstInventory;
			if (__instance.MoveAllowed(out srcGrid, out dstInventory))
			{
				ValueTuple<bool, bool> valueTuple = QuickStack.StashItems(GameManager.Instance.World.GetPrimaryPlayer().bag, dstInventory, XUiM_LootContainer.EItemMoveKind.All);

				if (__instance.MoveAllDone != null)
					__instance.MoveAllDone(valueTuple.Item1, valueTuple.Item2);
			}
			return false;
		}
	}

	//This patch overrides the original one to accommodate the locked slots. 
	[HarmonyPatch(typeof(XUiC_ContainerStandardControls), "MoveFillAndSmart")]
	private class QS_3
	{
		public static bool Prefix(XUiC_ContainerStandardControls __instance)
		{
			if (__instance.Parent.Parent.GetType() != typeof(XUiC_BackpackWindow))
				return true;

			XUiC_ItemStackGrid srcGrid;
			IInventory dstInventory;

			float unscaledTime = Time.unscaledTime;
			XUiM_LootContainer.EItemMoveKind moveKind = XUiM_LootContainer.EItemMoveKind.FillOnly;
			if (unscaledTime - QuickStack.lastClickTime < 2.0f)
				moveKind = XUiM_LootContainer.EItemMoveKind.FillAndCreate;
			QuickStack.lastClickTime = unscaledTime;

			if (__instance.MoveAllowed(out srcGrid, out dstInventory))
			{
				QuickStack.StashItems(GameManager.Instance.World.GetPrimaryPlayer().bag, dstInventory, moveKind);
			}

			return false;
		}
	}

	//This patch overrides the original one to accommodate the locked slots. 
	[HarmonyPatch(typeof(XUiC_ContainerStandardControls), "MoveFillStacks")]
	private class QS_4
	{
		public static bool Prefix(XUiC_ContainerStandardControls __instance)
		{
			if (__instance.Parent.Parent.GetType() != typeof(XUiC_BackpackWindow))
				return true;

			XUiC_ItemStackGrid srcGrid;
			IInventory dstInventory;
			if (__instance.MoveAllowed(out srcGrid, out dstInventory))
			{
				QuickStack.StashItems(GameManager.Instance.World.GetPrimaryPlayer().bag, dstInventory, XUiM_LootContainer.EItemMoveKind.FillOnly);
			}
			return false;
		}
	}

	//This patch overrides the original one to accommodate the locked slots. 
	[HarmonyPatch(typeof(XUiC_ContainerStandardControls), "MoveSmart")]
	private class QS_5
	{
		public static bool Prefix(XUiC_ContainerStandardControls __instance)
		{
			if (__instance.Parent.Parent.GetType() != typeof(XUiC_BackpackWindow))
				return true;

			XUiC_ItemStackGrid srcGrid;
			IInventory dstInventory;
			if (__instance.MoveAllowed(out srcGrid, out dstInventory))
			{
				QuickStack.StashItems(GameManager.Instance.World.GetPrimaryPlayer().bag, dstInventory, XUiM_LootContainer.EItemMoveKind.FillAndCreate);
			}

			return false;
		}
	}*/

	//This patch overrides the original one to accommodate the locked slots. 
	[HarmonyPatch(typeof(XUiC_ContainerStandardControls), "Sort")]
	private class QS_6
	{
		public static bool Prefix(XUiC_ContainerStandardControls __instance)
		{
			if (__instance.Parent.Parent.GetType() != typeof(XUiC_BackpackWindow))
				return true;

			XUiC_ItemStackGrid srcGrid;
			IInventory dstInventory;
			__instance.MoveAllowed(out srcGrid, out dstInventory);

			XUiController[] itemStackControllers = srcGrid.GetItemStackControllers();

			//Count the number of unlocked slots
			//We do this so we don't convert back and forth between List<ItemStack> and ItemStack[] since original code uses arrays.
			int numUnlockedSlots = 0;
			for (int i = 0; i < itemStackControllers.Length; ++i)
            {
				if (Traverse.Create(itemStackControllers[i]).Field("stackLockType").GetValue<XUiC_ItemStack.StackLockTypes>() == XUiC_ItemStack.StackLockTypes.None)
                {
					++numUnlockedSlots;
                }
			}

			//Create an empty array the size of the backpack minus the locked slots and add every unlocked itemstack
			ItemStack[] items = new ItemStack[numUnlockedSlots];
			int j = 0;
			for (int i = 0; i < itemStackControllers.Length; ++i)
			{
				if (Traverse.Create(itemStackControllers[i]).Field("stackLockType").GetValue<XUiC_ItemStack.StackLockTypes>() == XUiC_ItemStack.StackLockTypes.None)
				{
					items[j++] = ((XUiC_ItemStack)itemStackControllers[i]).ItemStack;
				}
			}

			//Combine and sort itemstacks using original code
			items = StackSortUtil.CombineAndSortStacks(items, 0);


			//Add back itemstack in sorted order, skipping through the lock slots.
			j = 0;
			for (int i = 0; i < itemStackControllers.Length; ++i)
			{
				if (Traverse.Create(itemStackControllers[i]).Field("stackLockType").GetValue<XUiC_ItemStack.StackLockTypes>() == XUiC_ItemStack.StackLockTypes.None)
				{
					((XUiC_ItemStack)itemStackControllers[i]).ItemStack = items[j++];
				}
			}

			return false;
		}
	}

	//This patch is used to initialize the functionallity for the slot locking mechanism.
	[HarmonyPatch(typeof(XUiC_BackpackWindow), "Init")]
	private class QS_7
	{
		public static void Postfix(XUiC_BackpackWindow __instance)
		{
			QuickStack.backpackWindow = __instance;
			QuickStack.playerBackpack = Traverse.Create(__instance).Field("backpackGrid").GetValue() as XUiC_Backpack;
			XUiController[] slots = QuickStack.playerBackpack.GetItemStackControllers();

			QuickStack.lastClickTime = 0;

			for (int i = 0; i < slots.Length; ++i)
			{
				int copy = i;
				slots[i].OnPress += (XUiController _sender, int _mouseButton) =>
				{
					XUiC_ItemStack itemStack = _sender as XUiC_ItemStack;

					if (UICamera.GetKey(KeyCode.LeftAlt))
					{
						if (Traverse.Create(itemStack).Field("stackLockType").GetValue<XUiC_ItemStack.StackLockTypes>() == XUiC_ItemStack.StackLockTypes.None)
						{
							Traverse.Create(itemStack).Field("stackLockType").SetValue(QuickStack.customLockEnum);
							itemStack.RefreshBindings();
						}
						else if (Traverse.Create(itemStack).Field("stackLockType").GetValue<int>() == QuickStack.customLockEnum)
						{
							Traverse.Create(itemStack).Field("stackLockType").SetValue(XUiC_ItemStack.StackLockTypes.None);
							itemStack.RefreshBindings();
						}

						//Manager.PlayInsidePlayerHead(StrAudioClip.UITab, -1, 0f, false);
						//Manager.PlayXUiSound(audio, 1);
						Manager.PlayButtonClick();
					}
				};
			}
		}
	}

	//This patch is used to add a binding to know whether the player is not accessing other loot container inventories with some exceptions like workstations.
	//This is used in the xml file to make the quickstack icon visible only when the player inventory is open.
	[HarmonyPatch(typeof(XUiC_BackpackWindow), "GetBindingValue")]
	private class QS_8
	{
		public static void Postfix(ref bool __result, XUiC_BackpackWindow __instance, ref string value, string bindingName)
		{
			if (__result == false)
			{
				if (bindingName != null)
				{
					if (bindingName == "notlootingorvehiclestorage")
					{
						bool flag1 = __instance.xui.vehicle != null && __instance.xui.vehicle.GetVehicle().HasStorage();
						bool flag2 = __instance.xui.lootContainer != null && __instance.xui.lootContainer.entityId == -1;
						bool flag3 = __instance.xui.lootContainer != null && GameManager.Instance.World.GetEntity(__instance.xui.lootContainer.entityId) is EntityDrone;
						value = (!flag1 && !flag2 && !flag3).ToString();
						__result = true;
					}
				}
			}
		}
	}

	//This patch is used to update the slot color in the backpack if the slot is locked by the player.
	[HarmonyPatch(typeof(XUiC_ItemStack), "updateBorderColor")]
	private class QS_9
	{
		[HarmonyPostfix]
		public static void Postfix(XUiC_ItemStack __instance)
		{
			if (Traverse.Create(__instance).Field("stackLockType").GetValue<int>() == QuickStack.customLockEnum)
				Traverse.Create(__instance).Field("selectionBorderColor").SetValue(new Color32(128, 0, 0, 255));
		}
	}

	//User QuickStack functionallity by pressing backslash (useful if other mods remove the QuickStack button)
	[HarmonyPatch(typeof(GameManager), "UpdateTick")]
	private class QS_10
	{
		public static void Postfix(EntityPlayerLocal __instance)
		{
			if (UICamera.GetKeyDown(KeyCode.Z) && UICamera.GetKey(KeyCode.LeftAlt))
			{
				//QuickStack.MoveQuickStackRestock(false);
			}
			else if (UICamera.GetKeyDown(KeyCode.X) && UICamera.GetKey(KeyCode.LeftAlt))
			{
				//QuickStack.MoveQuickStackRestock(true);
			}
		}
	}
}
