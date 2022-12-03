using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

public enum QuickStackType : byte
{
    Stack = 0,
    Restock,
    Count
}

internal class QuickStack
{
    public static float[] lastClickTimes = new float[(int)QuickStackType.Count];
    public static int stackRadius = 7;
    public static XUiC_Backpack playerBackpack;
    public static XUiC_BackpackWindow backpackWindow;
    public static XUiC_ContainerStandardControls playerControls;
    public static int customLockEnum = (int)XUiC_ItemStack.LockTypes.Burning + 1; //XUiC_ItemStack.LockTypes - Last used is Burning with value 5, so we use 6 for our custom locked slots
    public static KeyCode[] quickLockHotkeys;
    public static KeyCode[] quickStackHotkeys;
    public static KeyCode[] quickRestockHotkeys;

    public static string lockedSlotsFile()
    {
        return Path.Combine(GameIO.GetPlayerDataDir(), GameManager.Instance.persistentLocalPlayer.UserIdentifier + ".qsls");
    }

    public static Dictionary<TileEntity, int> GetOpenedTiles()
    {
        return Traverse.Create(GameManager.Instance).Field("lockedTileEntities").GetValue<Dictionary<TileEntity, int>>();
    }

    // Checks if a loot container is openable by a player
    // HOST OR SERVER ONLY
    public static bool IsContainerUnlocked(int _entityIdThatOpenedIt, TileEntity _tileEntity)
    {
        if (!ConnectionManager.Instance.IsServer)
        {
            return false;
        }

        if (_tileEntity == null)
        {
            return false;
        }

        // Handle locked containers
        if ((_tileEntity is TileEntitySecureLootContainer lootContainer) && lootContainer.IsLocked())
        {
            // Handle Host
            if (!GameManager.IsDedicatedServer && _entityIdThatOpenedIt == GameManager.Instance.World.GetPrimaryPlayerId())
            {
                if (!lootContainer.IsUserAllowed(GameManager.Instance.persistentLocalPlayer.UserIdentifier))
                {
                    return false;
                }
            }
            else
            {
                // Handle Client
                var cinfo = ConnectionManager.Instance.Clients.ForEntityId(_entityIdThatOpenedIt);
                if (cinfo == null || !lootContainer.IsUserAllowed(cinfo.CrossplatformId))
                {
                    return false;
                }
            }
        }

        var openTileEntities = GetOpenedTiles();

        // Handle in-use containers
        if (openTileEntities.ContainsKey(_tileEntity) &&
            (GameManager.Instance.World.GetEntity(openTileEntities[_tileEntity]) is EntityAlive entityAlive) &&
            !entityAlive.IsDead())
        {
            return false;
        }

        return true;
    }

    public static bool IsValidLoot(TileEntityLootContainer _tileEntity)
    {
        return (_tileEntity.GetTileEntityType() == TileEntityType.Loot ||
                _tileEntity.GetTileEntityType() == TileEntityType.SecureLoot ||
                _tileEntity.GetTileEntityType() == TileEntityType.SecureLootSigned);
    }

    // Yields all openable loot containers in a cubic radius about a point
    public static IEnumerable<ValueTuple<Vector3i, TileEntityLootContainer>> FindNearbyLootContainers(Vector3i _center, int _playerEntityId)
    {
        if (stackRadius > 127 || stackRadius < -127)
        {
            stackRadius = 127;
        }

        for (int i = -stackRadius; i <= stackRadius; i++)
        {
            for (int j = -stackRadius; j <= stackRadius; j++)
            {
                for (int k = -stackRadius; k <= stackRadius; k++)
                {
                    var offset = new Vector3i(i, j, k);
                    if (!(GameManager.Instance.World.GetTileEntity(0, _center + offset) is TileEntityLootContainer tileEntity))
                        continue;

                    if (IsValidLoot(tileEntity) && IsContainerUnlocked(_playerEntityId, tileEntity))
                    {
                        yield return new ValueTuple<Vector3i, TileEntityLootContainer>(offset, tileEntity);
                    }
                }
            }
        }
    }

    // Gets the EItemMoveKind for the current move type based on the last time that move type was requested
    internal static XUiM_LootContainer.EItemMoveKind GetMoveKind(QuickStackType _type = QuickStackType.Stack)
    {
        float unscaledTime = Time.unscaledTime;
        float lastClickTime = lastClickTimes[(int)_type];
        lastClickTimes[(int)_type] = unscaledTime;

        if (unscaledTime - lastClickTime < 2.0f)
        {
            return XUiM_LootContainer.EItemMoveKind.FillAndCreate;
        }
        else
        {
            return XUiM_LootContainer.EItemMoveKind.FillOnly;
        }
    }

    //Quickstack functionality
    // SINGLEPLAYER ONLY
    public static void MoveQuickStack()
    {
        if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.entityId == -1)
            return;

        var moveKind = GetMoveKind();

        EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
        int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();

        //returns tile entities opened by other players
        Dictionary<TileEntity, int> openedTileEntities = GetOpenedTiles();

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
                    if (IsValidLoot(tileEntity) && !tileEntity.IsUserAccessing() && !openedTileEntities.ContainsKey(tileEntity))
                    {
                        StashItems(playerBackpack, tileEntity, lockedSlots, moveKind, playerControls.MoveStartBottomRight);
                        tileEntity.SetModified();
                    }
                }
            }
        }
    }

    public static void ClientMoveQuickStack(Vector3i center, IEnumerable<Vector3i> _entityContainers)
    {
        if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.entityId == -1)
            return;

        var moveKind = GetMoveKind();

        if (_entityContainers == null)
        {
            return;
        }
        int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();

        foreach (var offset in _entityContainers)
        {
            if (!(GameManager.Instance.World.GetTileEntity(0, center + offset) is TileEntityLootContainer tileEntity))
            {
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

        var moveKind = GetMoveKind(QuickStackType.Restock);

        EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
        LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPlayer(primaryPlayer);
        int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();
        XUiC_LootWindowGroup lootWindowGroup = (XUiC_LootWindowGroup)((XUiWindowGroup)playerUI.windowManager.GetWindow("looting")).Controller;
        XUiC_LootWindow lootWindow = Traverse.Create(lootWindowGroup).Field("lootWindow").GetValue<XUiC_LootWindow>();
        XUiC_LootContainer lootContainer = Traverse.Create(lootWindow).Field("lootContainer").GetValue<XUiC_LootContainer>();

        //returns tile entities opened by other players
        Dictionary<TileEntity, int> openedTileEntities = GetOpenedTiles();

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
                    if (IsValidLoot(tileEntity) && !tileEntity.IsUserAccessing() && !openedTileEntities.ContainsKey(tileEntity))
                    {
                        lootWindowGroup.SetTileEntityChest("QUICKSTACK", tileEntity);
                        StashItems(lootContainer, primaryPlayer.bag, lockedSlots, moveKind, playerControls.MoveStartBottomRight);
                        tileEntity.SetModified();
                    }
                }
            }
        }
    }

    public static void ClientMoveQuickRestock(Vector3i center, IEnumerable<Vector3i> _entityContainers)
    {
        if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.entityId == -1)
            return;

        var moveKind = GetMoveKind(QuickStackType.Restock);

        if (_entityContainers == null)
            return;

        EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
        LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPlayer(primaryPlayer);
        int lockedSlots = Traverse.Create(playerControls).Field("stashLockedSlots").GetValue<int>();
        XUiC_LootWindowGroup lootWindowGroup = (XUiC_LootWindowGroup)((XUiWindowGroup)playerUI.windowManager.GetWindow("looting")).Controller;
        XUiC_LootWindow lootWindow = Traverse.Create(lootWindowGroup).Field("lootWindow").GetValue<XUiC_LootWindow>();
        XUiC_LootContainer lootContainer = Traverse.Create(lootWindow).Field("lootContainer").GetValue<XUiC_LootContainer>();

        foreach (var offset in _entityContainers)
        {
            if (!(GameManager.Instance.World.GetTileEntity(0, center + offset) is TileEntityLootContainer tileEntity))
                continue;

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

    // UI Delegates
    public static void QuickStackOnClick()
    {
        // Singleplayer
        if (ConnectionManager.Instance.IsSinglePlayer)
        {
            MoveQuickStack();
            // Multiplayer (Client)
        }
        else if (!ConnectionManager.Instance.IsServer)
        {
            ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageFindOpenableContainers>().Setup(GameManager.Instance.World.GetPrimaryPlayerId(), QuickStackType.Stack));
            // Multiplayer (Host)
        }
        else if (!GameManager.IsDedicatedServer)
        {
            // But we do the steps of Multiplayer quick stack in-place because
            // The host has access to locking functions
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            var center = new Vector3i(player.position);
            List<Vector3i> offsets = new List<Vector3i>(1024);
            foreach (var pair in FindNearbyLootContainers(center, player.entityId))
            {
                offsets.Add(pair.Item1);
            }
            ClientMoveQuickStack(center, offsets);
        }
    }

    public static void QuickRestockOnClick()
    {
        // Singleplayer
        if (ConnectionManager.Instance.IsSinglePlayer)
        {
            MoveQuickRestock();
            // Multiplayer (Client)
        }
        else if (!ConnectionManager.Instance.IsServer)
        {
            ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageFindOpenableContainers>().Setup(GameManager.Instance.World.GetPrimaryPlayerId(), QuickStackType.Restock));
            // Multiplayer (Host)
        }
        else if (!GameManager.IsDedicatedServer)
        {
            // TODO: could be cleaned up a bit...
            // But we do the steps of Multiplayer quick stack in-place because
            // The host has access to locking functions
            var player = GameManager.Instance.World.GetPrimaryPlayer();
            var center = new Vector3i(player.position);
            List<Vector3i> offsets = new List<Vector3i>(1024);
            foreach (var pair in FindNearbyLootContainers(center, player.entityId))
            {
                offsets.Add(pair.Item1);
            }
            ClientMoveQuickRestock(center, offsets);
        }
    }
}
