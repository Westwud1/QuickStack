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
    public static int stashDistanceX = 7;
    public static int stashDistanceY = 7;
    public static int stashDistanceZ = 7;
    public static XUiC_Backpack playerBackpack;
    public static XUiC_BackpackWindow backpackWindow;
    public static XUiC_ContainerStandardControls playerControls;
    public static XUiC_ItemStack.LockTypes customLockEnum = XUiC_ItemStack.LockTypes.Burning + 1; //XUiC_ItemStack.LockTypes - Last used is Burning with value 5, so we use 6 for our custom locked slots
    public static KeyCode[] quickLockHotkeys;
    public static KeyCode[] quickStackHotkeys;
    public static KeyCode[] quickRestockHotkeys;

    public static int stashLockedSlots()
    {
        return playerControls.xui.playerUI.entityPlayer.bag.LockedSlots;
    }

    public static string lockedSlotsFile()
    {
        if (ConnectionManager.Instance.IsSinglePlayer)
            return Path.Combine(GameIO.GetPlayerDataDir(), GameManager.Instance.persistentLocalPlayer.PrimaryId + ".qsls");
        return Path.Combine(GameIO.GetPlayerDataLocalDir(), GameManager.Instance.persistentLocalPlayer.PrimaryId + ".qsls");
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
                if (!lootContainer.IsUserAllowed(GameManager.Instance.persistentLocalPlayer.PrimaryId))
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
        // Handle locked composite storages
        else if ((_tileEntity is TileEntityComposite compositeContainer) && compositeContainer.teData.GetFeatureIndex<TEFeatureLockable>() > 0)
        {
            TEFeatureLockable fLockable = compositeContainer.GetFeature<TEFeatureLockable>();
            TEFeatureStorage fStorage = compositeContainer.GetFeature<TEFeatureStorage>();

            // Handle Host
            if (!GameManager.IsDedicatedServer && _entityIdThatOpenedIt == GameManager.Instance.World.GetPrimaryPlayerId())
            {
                if (!fLockable.IsUserAllowed(GameManager.Instance.persistentLocalPlayer.PrimaryId))
                {
                    return false;
                }
            }
            else
            {
                // Handle Client
                var cinfo = ConnectionManager.Instance.Clients.ForEntityId(_entityIdThatOpenedIt);
                if (cinfo == null || !fLockable.IsUserAllowed(cinfo.CrossplatformId))
                {
                    return false;
                }
            }
        }

        // Handle in-use containers
        if (GameManager.Instance.lockedTileEntities.ContainsKey(_tileEntity) &&
           (GameManager.Instance.World.GetEntity(GameManager.Instance.lockedTileEntities[_tileEntity]) is EntityAlive entityAlive) &&
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

    public static (ITileEntityLootable, TileEntity) GetInventoryFromBlockPosition(Vector3i position)
    {
        TileEntityLootContainer lootContainer = GameManager.Instance.World.GetTileEntity(0, position) as TileEntityLootContainer;

        if (lootContainer != null)
        {
            if (IsValidLoot(lootContainer) && !lootContainer.IsUserAccessing())
                return (lootContainer, lootContainer);

            return (null, lootContainer);
        }

        TileEntityComposite compositeContainer = GameManager.Instance.World.GetTileEntity(0, position) as TileEntityComposite;

        if (compositeContainer == null)
            return (null, null);

        if (compositeContainer.teData.GetFeatureIndex<TEFeatureStorage>() == 0)
            return (null, compositeContainer);

        if (compositeContainer.IsUserAccessing())
            return (null, compositeContainer);

        return (compositeContainer.GetFeature<TEFeatureStorage>(), compositeContainer);
    }

    // Yields all openable loot containers in a cubic radius about a point
    public static IEnumerable<ValueTuple<Vector3i, TileEntity>> FindNearbyLootContainers(Vector3i _center, int _playerEntityId)
    {
        for (int i = -stashDistanceX; i <= stashDistanceX; i++)
        {
            for (int j = -stashDistanceY; j <= stashDistanceY; j++)
            {
                for (int k = -stashDistanceZ; k <= stashDistanceZ; k++)
                {
                    var offset = new Vector3i(i, j, k);

                    var val = GetInventoryFromBlockPosition(_center + offset);

                    if (val.Item1 == null)
                        continue;

                    if (!IsContainerUnlocked(_playerEntityId, val.Item2))
                        continue;

                    yield return new ValueTuple<Vector3i, TileEntity>(offset, val.Item2);
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
        if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.EntityId == -1)
            return;

        var moveKind = GetMoveKind();

        EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();

        for (int i = -stashDistanceX; i <= stashDistanceX; i++)
        {
            for (int j = -stashDistanceY; j <= stashDistanceY; j++)
            {
                for (int k = -stashDistanceZ; k <= stashDistanceZ; k++)
                {
                    Vector3i blockPos = new Vector3i((int)primaryPlayer.position.x + i, (int)primaryPlayer.position.y + j, (int)primaryPlayer.position.z + k);

                    var val = GetInventoryFromBlockPosition(blockPos);

                    if (val.Item1 == null)
                        continue;

                    StashItems(backpackWindow, playerBackpack, val.Item1, moveKind);
                    val.Item2.SetModified();
                }
            }
        }
    }

    public static void ClientMoveQuickStack(Vector3i center, IEnumerable<Vector3i> _entityContainers)
    {
        if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.EntityId == -1)
            return;

        var moveKind = GetMoveKind();

        if (_entityContainers == null)
            return;

        foreach (var offset in _entityContainers)
        {
            var val = GetInventoryFromBlockPosition(center + offset);

            if (val.Item1 == null)
                continue;

            StashItems(backpackWindow, playerBackpack, val.Item1, moveKind);
            val.Item2.SetModified();
        }
    }

    //Restock functionallity
    // SINGLEPLAYER ONLY
    public static void MoveQuickRestock()
    {
        if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.EntityId == -1)
            return;

        var moveKind = GetMoveKind(QuickStackType.Restock);

        EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
        LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPlayer(primaryPlayer);
        XUiC_LootWindowGroup lootWindowGroup = (XUiC_LootWindowGroup)((XUiWindowGroup)playerUI.windowManager.GetWindow("looting")).Controller;

        for (int i = -stashDistanceX; i <= stashDistanceX; i++)
        {
            for (int j = -stashDistanceY; j <= stashDistanceY; j++)
            {
                for (int k = -stashDistanceZ; k <= stashDistanceZ; k++)
                {
                    Vector3i blockPos = new Vector3i((int)primaryPlayer.position.x + i, (int)primaryPlayer.position.y + j, (int)primaryPlayer.position.z + k);

                    var val = GetInventoryFromBlockPosition(blockPos);

                    if (val.Item1 == null)
                        continue;

                    lootWindowGroup.SetTileEntityChest("QUICKSTACK", val.Item1);
                    StashItems(backpackWindow, lootWindowGroup.lootWindow.lootContainer, primaryPlayer.bag, moveKind);
                    val.Item2.SetModified();
                }
            }
        }
    }

    public static void ClientMoveQuickRestock(Vector3i center, IEnumerable<Vector3i> _entityContainers)
    {
        if (backpackWindow.xui.lootContainer != null && backpackWindow.xui.lootContainer.EntityId == -1)
            return;

        var moveKind = GetMoveKind(QuickStackType.Restock);

        if (_entityContainers == null)
            return;

        EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();
        LocalPlayerUI playerUI = LocalPlayerUI.GetUIForPlayer(primaryPlayer);
        XUiC_LootWindowGroup lootWindowGroup = (XUiC_LootWindowGroup)((XUiWindowGroup)playerUI.windowManager.GetWindow("looting")).Controller;

        foreach (var offset in _entityContainers)
        {
            var val = GetInventoryFromBlockPosition(center + offset);

            if (val.Item1 == null)
                continue;

            lootWindowGroup.SetTileEntityChest("QUICKSTACK", val.Item1);
            StashItems(backpackWindow, lootWindowGroup.lootWindow.lootContainer, primaryPlayer.bag, moveKind);
            val.Item2.SetModified();
        }
    }

    //Refactored from the original code to remove stash time due to quick stack/restock and check for custom locks
    public static ValueTuple<bool, bool> StashItems(XUiController _srcWindow, XUiC_ItemStackGrid _srcGrid, IInventory _dstInventory, XUiM_LootContainer.EItemMoveKind _moveKind)
    {
        if (_srcGrid == null || _dstInventory == null)
        {
            return new ValueTuple<bool, bool>(false, false);
        }
        XUiController[] itemStackControllers = _srcGrid.GetItemStackControllers();

        bool item = true;
        bool item2 = false;

        PreferenceTracker preferenceTracker = null;
        XUiC_LootWindow xuiC_LootWindow = _srcWindow as XUiC_LootWindow;
        if (xuiC_LootWindow != null)
        {
            preferenceTracker = xuiC_LootWindow.GetPreferenceTrackerFromTileEntity();
        }
        if (preferenceTracker != null && preferenceTracker.AnyPreferences)
        {
            XUiM_PlayerInventory xuiM_PlayerInventory = _dstInventory as XUiM_PlayerInventory;
            if (xuiM_PlayerInventory != null)
            {
                item2 = xuiM_PlayerInventory.AddItemsUsingPreferenceTracker(_srcGrid, preferenceTracker);
            }
        }

        for (int i=0;i<itemStackControllers.Length;++i)
        {
            XUiC_ItemStack xuiC_ItemStack = (XUiC_ItemStack)itemStackControllers[i];

            if (xuiC_ItemStack.StackLock)
                continue;

            if (xuiC_ItemStack.lockType == QuickStack.customLockEnum)
                continue;

            ItemStack itemStack = xuiC_ItemStack.ItemStack;
            if (xuiC_ItemStack.ItemStack.IsEmpty())
                continue;

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

    /* 
     * Binary format:
     * [int32] locked slots - built-in locking mechanism compatibility
     * [int32] array count (N) of locked slots by us
     * [N bytes] boolean array indicating locked slots
     */
    public static void SaveLockedSlots()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            XUiController[] slots = playerBackpack.GetItemStackControllers();

            using (BinaryWriter binWriter = new BinaryWriter(File.Open(lockedSlotsFile(), FileMode.Create)))
            {
                binWriter.Write(playerControls.stashLockedSlots);

                binWriter.Write(slots.Length);
                for (int i = 0; i < slots.Length; i++)
                    binWriter.Write((slots[i] as XUiC_ItemStack).lockType == customLockEnum);
            }
            Log.Out($"[QuickStack] Saved locked slots config in {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            Log.Warning($"[QuickStack] Failed to write locked slots file: {e.Message}. Slot states will not be saved!");
        }
    }

    public static void LoadLockedSlots()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            string path = lockedSlotsFile();
            if (!File.Exists(path))
            {
                Log.Warning("[QuickStack] No locked slots config detected. Slots will default to unlocked");
                return;
            }

            // reported number of locked slots
            long reportedLength = new FileInfo(path).Length - sizeof(int) * 2;
            if (reportedLength < 0)
            {
                // file is too small to process
                Log.Warning("[QuickStack] locked slots config appears corrupted. Slots will be defaulted to unlocked");
                return;
            }

            using (BinaryReader binReader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                // locked slots saved by the unused combobox some mods may enable
                int comboLockedSlots = Math.Max(0, binReader.ReadInt32());

                // locked slots saved by us
                int quickStackLockedSlots = binReader.ReadInt32();
                if (reportedLength != quickStackLockedSlots * sizeof(bool))
                {
                    Log.Warning("[QuickStack] locked slots config appears corrupted. Slots will be defaulted to unlocked");
                    return;
                }

                // Built-in locking mechanism compatibility
                if (playerControls.GetChildById("cbxLockedSlots") is XUiC_ComboBoxInt comboBox)
                {
                    comboBox.Value = comboLockedSlots;
                    playerControls.ChangeLockedSlots(comboLockedSlots);
                }

                XUiController[] slots = playerBackpack.GetItemStackControllers();
                for (int i = 0; i < Math.Min(quickStackLockedSlots, slots.Length); i++)
                {
                    if (binReader.ReadBoolean())
                        (slots[i] as XUiC_ItemStack).lockType = (XUiC_ItemStack.LockTypes)customLockEnum;
                }
            }
            Log.Out($"[QuickStack] Loaded locked slots config in {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            Log.Warning($"[QuickStack] Failed to read locked slots config:  {e.Message}. Slots will default to unlocked");
        }
    }
}
