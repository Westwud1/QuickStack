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


	public static Dictionary<TileEntity, int> GetLockedTiles() {
		return Traverse.Create(GameManager.Instance).Field("lockedTileEntities").GetValue<Dictionary<TileEntity, int>>();
	}

	// Checks if a loot container is openable by a player
	// HOST OR SERVER ONLY
	public static bool IsContainerUnlocked(int _entityIdThatOpenedIt, TileEntity _tileEntity) {
		if (!ConnectionManager.Instance.IsServer) {
			return false;
		}

		if (_tileEntity == null) {
			return false;
		}

		// Handle locked containers
		if((_tileEntity is TileEntitySecureLootContainer lootContainer) && lootContainer.IsLocked()) {
			// Handle host
			if (!GameManager.IsDedicatedServer && _entityIdThatOpenedIt == GameManager.Instance.World.GetPrimaryPlayerId()) {
				if (!lootContainer.IsUserAllowed(GameManager.Instance.persistentLocalPlayer.PlatformUserIdentifier)) {
						return false;
				}
			}

			var cinfo = ConnectionManager.Instance.Clients.ForEntityId(_entityIdThatOpenedIt);

			if (cinfo != null && !lootContainer.IsUserAllowed(cinfo.PlatformId)) {
				return false;
			}
		}

		var lockedTileEntities = GetLockedTiles();

		// Handle in-use containers
		if (lockedTileEntities.ContainsKey(_tileEntity) &&
			(GameManager.Instance.World.GetEntity(lockedTileEntities[_tileEntity]) is EntityAlive entityAlive) &&
			!entityAlive.IsDead()) {
			return false;
		} else {
			return true;
		}
	}

  public static bool IsValidLoot(TileEntityLootContainer _tileEntity) {
		return (_tileEntity.GetTileEntityType() == TileEntityType.Loot ||
			_tileEntity.GetTileEntityType() == TileEntityType.SecureLoot ||
			_tileEntity.GetTileEntityType() == TileEntityType.SecureLootSigned);

	}

	// Yields all openable loot containers in a cubic radius about a point
	public static IEnumerable<ValueTuple<Vector3i,TileEntityLootContainer>> FindNearbyLootContainers(Vector3i _center, int _stackRadius, int _playerEntityId) {
		for (int i = -_stackRadius; i <= _stackRadius; i++) {
			for (int j = -_stackRadius; j <= _stackRadius; j++) {
				for (int k = -_stackRadius; k <= _stackRadius; k++) {
					var offset = new Vector3i(i, j, k);
					if (!(GameManager.Instance.World.GetTileEntity(0, _center + new Vector3i(i, j, k)) is TileEntityLootContainer tileEntity))
						continue;

					if (IsContainerUnlocked(_playerEntityId, tileEntity) && IsValidLoot(tileEntity)) {
						yield return new ValueTuple<Vector3i, TileEntityLootContainer>(offset, tileEntity);
					}
				}
			}
		}
	}

	//Quickstack functionality
	// SINGLEPLAYER ONLY
	public static void MoveQuickStack()
	{
		if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.entityId == -1)
			return;

		float unscaledTime = Time.unscaledTime;
		XUiM_LootContainer.EItemMoveKind moveKind = XUiM_LootContainer.EItemMoveKind.FillOnly;
		if (unscaledTime - lastClickTime < 2.0f)
			moveKind = XUiM_LootContainer.EItemMoveKind.FillAndCreate;
		lastClickTime = unscaledTime;

		EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
		int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();

		//returns tile entities opened by other players
		Dictionary<TileEntity, int> openedTileEntities = GetLockedTiles();
		
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
						IsValidLoot(tileEntity))
					{
						StashItems(playerBackpack, tileEntity, lockedSlots, moveKind, playerControls.MoveStartBottomRight);
						tileEntity.SetModified();
					}
				}
			}
		}
	}

	public static void ClientMoveQuickStack(Vector3i center, IEnumerable<Vector3i> _entityContainers) {
		if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.entityId == -1)
			return;

		// TODO: Decide whether we care about double-click and relying on low latency
		XUiM_LootContainer.EItemMoveKind moveKind = XUiM_LootContainer.EItemMoveKind.FillAndCreate;
		if(_entityContainers == null) {
			return;
    }
		int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();

		foreach (var offset in _entityContainers) {
			if (!(GameManager.Instance.World.GetTileEntity(0, center + offset) is TileEntityLootContainer tileEntity)) {
				continue;
			}

      StashItems(playerBackpack, tileEntity, lockedSlots, moveKind, playerControls.MoveStartBottomRight);
			tileEntity.SetModified();
		}
  }

	//Restock functionallity
	// SINGLEPLAYER ONLY
	public static void MoveQuickRestock()
    {
		if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.entityId == -1)
			return;

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
		Dictionary<TileEntity, int> openedTileEntities = GetLockedTiles();

		for (int i = -stackRadius; i <= stackRadius; i++)
		{
			for (int j = -stackRadius; j <= stackRadius; j++)
			{
				for (int k = -stackRadius; k <= stackRadius; k++)
				{
					Vector3i blockPos = new Vector3i((int)primaryPlayer.position.x + i, (int)primaryPlayer.position.y + j, (int)primaryPlayer.position.z + k);

          if (!(GameManager.Instance.World.GetTileEntity(0, blockPos) is TileEntityLootContainer tileEntity))
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

	public static void ClientMoveQuickRestock(Vector3i center, IEnumerable<Vector3i> _entityContainers) {
		if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.entityId == -1)
			return;

		XUiM_LootContainer.EItemMoveKind moveKind = XUiM_LootContainer.EItemMoveKind.FillAndCreate;

		if (_entityContainers == null) {
			return;
		}

		EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
		LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPlayer(primaryPlayer);
		int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();
		XUiC_LootWindowGroup lootWindowGroup = (XUiC_LootWindowGroup)((XUiWindowGroup)playerUI.windowManager.GetWindow("looting")).Controller;
		XUiC_LootWindow lootWindow = Traverse.Create(lootWindowGroup).Field("lootWindow").GetValue<XUiC_LootWindow>();
		XUiC_LootContainer lootContainer = Traverse.Create(lootWindow).Field("lootContainer").GetValue<XUiC_LootContainer>();

		foreach (var offset in _entityContainers) {
			if(!(GameManager.Instance.World.GetTileEntity(0, center + offset) is TileEntityLootContainer tileEntity)) {
				continue;
      }
			lootWindowGroup.SetTileEntityChest("QUICKSTACK", tileEntity);
			StashItems(lootContainer, primaryPlayer.bag, lockedSlots, moveKind, playerControls.MoveStartBottomRight);
			tileEntity.SetModified();
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
