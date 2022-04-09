using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

internal class QuickStack
{
	public static float lastClickTime = 0;
	public static int stackRadius = 7;
	public static XUiC_Backpack playerBackpack;
	public static XUiC_BackpackWindow backpackWindow;
	public static int customLockEnum = (int)XUiC_ItemStack.StackLockTypes.Hidden + 1; //XUiC_ItemStack.StackLockTypes - Last used is Hidden with value 4, so we use 5 for our custom locked slots

	//Quickstack functionality
	public static void MoveQuickStackRestock(bool quickStack) //true = quickstack; false = quickrestock
	{
		float unscaledTime = Time.unscaledTime;
		XUiM_LootContainer.EItemMoveKind moveKind = XUiM_LootContainer.EItemMoveKind.FillOnly;
		if (unscaledTime - lastClickTime < 2.0f)
		{
			moveKind = XUiM_LootContainer.EItemMoveKind.FillAndCreate;
		}
		lastClickTime = unscaledTime;
		EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
		LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPlayer(primaryPlayer);

		//returns tile entities opened by other players
		Dictionary<TileEntity, int> openedTileEntities = Traverse.Create(GameManager.Instance).Field("lockedTileEntities").GetValue<Dictionary<TileEntity, int>>();

		for (int i = -stackRadius; i <= stackRadius; i++)
		{
			for (int j = -stackRadius; j <= stackRadius; j++)
			{
				for (int k = -stackRadius; k <= stackRadius; k++)
				{
					Vector3i blockPos = new Vector3i((int)primaryPlayer.position.x + i, (int)primaryPlayer.position.y + j, (int)primaryPlayer.position.z + k);
					TileEntityLootContainer tileEntity = GameManager.Instance.World.GetTileEntity(0, blockPos) as TileEntityLootContainer;

					if (tileEntity == null)
						continue;

					//TODO: !tileEntity.IsUserAccessing() && !openedTileEntities.ContainsKey(tileEntity) does not work on multiplayer
					if (!tileEntity.IsUserAccessing() && !openedTileEntities.ContainsKey(tileEntity) &&
						(tileEntity.GetTileEntityType() == TileEntityType.Loot || tileEntity.GetTileEntityType() == TileEntityType.SecureLoot || tileEntity.GetTileEntityType() == TileEntityType.SecureLootSigned))
					{
						XUiC_LootWindowGroup lootWindowGroup = (XUiC_LootWindowGroup)((XUiWindowGroup)playerUI.windowManager.GetWindow("looting")).Controller;
						lootWindowGroup.SetTileEntityChest("QUICKSTACK", tileEntity);

						XUiC_LootWindow lootWindow = Traverse.Create(lootWindowGroup).Field("lootWindow").GetValue<XUiC_LootWindow>();
						XUiC_LootContainer lootContainer = Traverse.Create(lootWindow).Field("lootContainer").GetValue<XUiC_LootContainer>();

						if (quickStack)
							StashItems(playerBackpack, lootContainer, moveKind);
						else
							StashItems(lootContainer, playerBackpack, moveKind);
						tileEntity.SetModified();
					}
				}
			}
		}
	}

	//Refactored from the original code to check for custom locked slots, and same src/dst arguments
	public static ValueTuple<bool, bool> StashItems(XUiC_ItemStackGrid srcGrid, XUiC_ItemStackGrid dstGrid, XUiM_LootContainer.EItemMoveKind _moveKind)
	{
		if (srcGrid == null || dstGrid == null)
			return new ValueTuple<bool, bool>(false, false);

		bool item1 = true;
		bool item2 = false;

		XUiController[] srcSlots = srcGrid.GetItemStackControllers();
		XUiController[] dstSlots = dstGrid.GetItemStackControllers();

		int[] locks = new int[dstSlots.Length];
		for (int i = 0; i < dstSlots.Length; ++i)
			locks[i] = Traverse.Create(dstSlots[i]).Field("stackLockType").GetValue<int>();

		ItemStack[] dstItems = dstGrid.GetSlots();

		for (int i = srcSlots.Length - 1; i >= 0; --i)
		{
			XUiC_ItemStack itemStackSlot = (XUiC_ItemStack)srcSlots[i];

			if (itemStackSlot.StackLock)
				continue;

			ItemStack itemStack = itemStackSlot.ItemStack;

			if (itemStack.IsEmpty())
				continue;

			int count = itemStack.count;
			TryStackItem(dstItems, itemStack);

			if (itemStack.count > 0 && (_moveKind == XUiM_LootContainer.EItemMoveKind.All || (_moveKind == XUiM_LootContainer.EItemMoveKind.FillAndCreate && HasItem(dstItems, itemStack.itemValue))) && AddItem(dstItems, itemStack))
			{
				itemStack = ItemStack.Empty.Clone();
			}
			if (itemStack.count == 0)
			{
				itemStack = ItemStack.Empty.Clone();
			}
			else
			{
				item1 = false;
			}
			if (itemStack.count != count)
			{
				itemStackSlot.ForceSetItemStack(itemStack);
				item2 = true;
			}
		}

		for (int i = 0; i < srcSlots.Length; ++i)
			srcSlots[i].RefreshBindings();

		for (int i = 0; i < dstSlots.Length; ++i)
			dstSlots[i].RefreshBindings();

		for (int i = 0; i < dstSlots.Length; ++i)
			Traverse.Create(dstSlots[i]).Field("stackLockType").SetValue(locks[i]);

		return new ValueTuple<bool, bool>(item1, item2);
	}

	//Taken from Bag.TryStackItem
	private static bool TryStackItem(ItemStack[] slots, ItemStack itemStack)
	{
		int num = 0;
		for (int i = 0; i < slots.Length; ++i)
		{
			num = itemStack.count;
			if (itemStack.itemValue.type == slots[i].itemValue.type && slots[i].CanStackPartly(ref num))
			{
				slots[i].count += num;
				itemStack.count -= num;
				if (itemStack.count == 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	//Taken from Bag.HasItem and Bag.GetItemCount
	private static bool HasItem(ItemStack[] slots, ItemValue itemValue)
    {
		int num = 0;
		for (int i = 0; i < slots.Length; i++)
		{
			if ((!slots[i].itemValue.HasModSlots || !slots[i].itemValue.HasMods()) && slots[i].itemValue.type == itemValue.type && ((int)slots[i].itemValue.Seed == -1) && (slots[i].itemValue.Meta == -1))
			{
				num += slots[i].count;
			}
		}
		return num > 0;
	}

	//Taken from Bag.AddItem
	private static bool AddItem(ItemStack[] slots, ItemStack itemStack)
    {
		return ItemStack.AddToItemStackArray(slots, itemStack, -1) >= 0;
	}
}
