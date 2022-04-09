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
	public static XUiC_ContainerStandardControls playerControls;
	public static int customLockEnum = (int)XUiC_ItemStack.LockTypes.Burning + 1; //XUiC_ItemStack.LockTypes - Last used is Burning with value 5, so we use 6 for our custom locked slots

	//Quickstack functionality
	public static void MoveQuickStack()
	{
		float unscaledTime = Time.unscaledTime;
		XUiM_LootContainer.EItemMoveKind moveKind = XUiM_LootContainer.EItemMoveKind.FillOnly;
		if (unscaledTime - lastClickTime < 2.0f)
			moveKind = XUiM_LootContainer.EItemMoveKind.FillAndCreate;
		lastClickTime = unscaledTime;

		EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
		int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();

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
						StashItems(playerBackpack, tileEntity, lockedSlots, moveKind, playerControls.MoveStartBottomRight);
						tileEntity.SetModified();
					}
				}
			}
		}
	}

	//Restock functionallity
	public static void MoveQuickRestock()
    {
		float unscaledTime = Time.unscaledTime;
		XUiM_LootContainer.EItemMoveKind moveKind = XUiM_LootContainer.EItemMoveKind.FillOnly;
		if (unscaledTime - lastClickTime < 2.0f)
			moveKind = XUiM_LootContainer.EItemMoveKind.FillAndCreate;
		lastClickTime = unscaledTime;

		EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
		LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPlayer(primaryPlayer);
		int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();
		XUiC_LootWindowGroup lootWindowGroup = (XUiC_LootWindowGroup)((XUiWindowGroup)playerUI.windowManager.GetWindow("looting")).Controller;
		XUiC_LootWindow lootWindow = Traverse.Create(lootWindowGroup).Field("lootWindow").GetValue<XUiC_LootWindow>();
		XUiC_LootContainer lootContainer = Traverse.Create(lootWindow).Field("lootContainer").GetValue<XUiC_LootContainer>();

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
						lootWindowGroup.SetTileEntityChest("QUICKSTACK", tileEntity);
						StashItems(lootContainer, primaryPlayer.bag, lockedSlots, moveKind, playerControls.MoveStartBottomRight);
						tileEntity.SetModified();
					}
				}
			}
		}
	}

	//Refactored from the original code to remove stash time due to quick stack/restock and check for custom locks
	public static ValueTuple<bool, bool> StashItems(XUiC_ItemStackGrid _srcGrid, IInventory _dstInventory, int _ignoredSlots, XUiM_LootContainer.EItemMoveKind _moveKind, bool _startBottomRight)
	{
		if (_srcGrid == null || _dstInventory == null)
		{
			return new ValueTuple<bool, bool>(false, false);
		}
		XUiController[] itemStackControllers = _srcGrid.GetItemStackControllers();
		
		bool item = true;
		bool item2 = false;
		int num = _startBottomRight ? (itemStackControllers.Length - 1) : _ignoredSlots;
		while (_startBottomRight ? (num >= _ignoredSlots) : (num < itemStackControllers.Length))
		{
			XUiC_ItemStack xuiC_ItemStack = (XUiC_ItemStack)itemStackControllers[num];
			if (!xuiC_ItemStack.StackLock && Traverse.Create(xuiC_ItemStack).Field("lockType").GetValue<int>() != QuickStack.customLockEnum)
			{
				ItemStack itemStack = xuiC_ItemStack.ItemStack;
				if (!xuiC_ItemStack.ItemStack.IsEmpty())
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
						item = false;
					}
					if (count != itemStack.count)
					{
						xuiC_ItemStack.ForceSetItemStack(itemStack);
						item2 = true;
					}
				}
			}
			num = (_startBottomRight ? (num - 1) : (num + 1));
		}

		return new ValueTuple<bool, bool>(item, item2);
	}
}
