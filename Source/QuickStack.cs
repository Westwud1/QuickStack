using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

internal class QuickStack
{
	public static float lastClickTime = 0;
	public static int stackRadius = 7;
	public static XUiC_ItemStack[] lockedSlots;
	public static int numLockedSlots = 0;
	public static XUiC_Backpack playerBackpackUi;

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
						if (quickStack)
							StashItems(primaryPlayer.bag, tileEntity, moveKind);
						else
							StashItems(tileEntity, primaryPlayer.bag, moveKind);
						tileEntity.SetModified();
					}
				}
			}
		}
	}

	//Refactored from the original code to check for custom locked slots, and same src/dst arguments
	public static ValueTuple<bool, bool> StashItems(IInventory _srcInventory, IInventory _dstInventory, XUiM_LootContainer.EItemMoveKind _moveKind)
	{
		if (_srcInventory == null || _dstInventory == null)
			return new ValueTuple<bool, bool>(false, false);

		bool item1 = true;
		bool item2 = false;

		ItemStack[] srcItems = Traverse.Create(_srcInventory).Field("items").GetValue<ItemStack[]>();
		ItemStack[] dstItems = Traverse.Create(_srcInventory).Field("items").GetValue<ItemStack[]>();
		if (srcItems == null)
			srcItems = Traverse.Create(_srcInventory).Field("itemsArr").GetValue<ItemStack[]>();
		if (dstItems == null)
			dstItems = Traverse.Create(_srcInventory).Field("itemsArr").GetValue<ItemStack[]>();


		//Check if _srcInventory is player inventory (so we can do locked slots checking)
		XUiController[] itemStackControllers = null;
		if (GameManager.Instance.World.GetPrimaryPlayer().bag == _srcInventory)
        {
			itemStackControllers = playerBackpackUi.GetItemStackControllers();
        }

		for (int i = srcItems.Length - 1; i >= 0; --i)
		{
			if (itemStackControllers != null && !((XUiC_ItemStack)itemStackControllers[i]).StackLock && lockedSlots[i] != null)
				continue;

			ItemStack itemStack = srcItems[i];
			if (!itemStack.IsEmpty())
			{
				int count = itemStack.count;
				_dstInventory.TryStackItem(0, itemStack);
				if (itemStack.count > 0 && (_moveKind == XUiM_LootContainer.EItemMoveKind.All || (_moveKind == XUiM_LootContainer.EItemMoveKind.FillAndCreate && _dstInventory.HasItem(itemStack.itemValue))) && _dstInventory.AddItem(itemStack))
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
					if (itemStackControllers != null)
						((XUiC_ItemStack)itemStackControllers[i]).ForceSetItemStack(itemStack);
					else
						dstItems[i] = itemStack;
					item2 = true;
				}
			}
		}

		return new ValueTuple<bool, bool>(item1, item2);
	}
}
