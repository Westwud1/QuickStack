using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
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
    public static KeyCode[] quickLockHotkeys;
    public static KeyCode[] quickStackHotkeys;
    public static KeyCode[] quickRestockHotkeys;
    public static Color32 lockIconColor = new Color32(128, 128, 128, 255);
    public static Color32 lockBorderColor = new Color32(0, 0, 0, 0);

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

    // Quickstack functionality
    // SINGLEPLAYER ONLY
    public static void MoveQuickStack()
    {
        try
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

                        XUiM_LootContainer.StashItems(backpackWindow, playerBackpack, val.Item1, 0, playerControls.LockedSlots, moveKind, playerControls.MoveStartBottomRight);
                        val.Item2.SetModified();
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning($"[QuickStack] {e.Message}");
            Log.Warning($"[QuickStack] {e.StackTrace}");
        }
    }

    public static void ClientMoveQuickStack(Vector3i center, IEnumerable<Vector3i> _entityContainers)
    {
        try
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

                XUiM_LootContainer.StashItems(backpackWindow, playerBackpack, val.Item1, 0, playerControls.LockedSlots, moveKind, playerControls.MoveStartBottomRight);
                val.Item2.SetModified();
            }
        }
        catch (Exception e)
        {
            Log.Warning($"[QuickStack] {e.Message}");
            Log.Warning($"[QuickStack] {e.StackTrace}");
        }
    }

    // Restock functionality
    // SINGLEPLAYER ONLY
    public static void MoveQuickRestock()
    {
        try
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
                        XUiM_LootContainer.StashItems(backpackWindow, lootWindowGroup.lootWindow.lootContainer, playerUI.mXUi.PlayerInventory, 0, playerControls.LockedSlots, moveKind, playerControls.MoveStartBottomRight);
                        val.Item2.SetModified();
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning($"[QuickStack] {e.Message}");
            Log.Warning($"[QuickStack] {e.StackTrace}");
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
            XUiM_LootContainer.StashItems(backpackWindow, lootWindowGroup.lootWindow.lootContainer, playerUI.mXUi.PlayerInventory, 0, playerControls.LockedSlots, moveKind, playerControls.MoveStartBottomRight);
            val.Item2.SetModified();
        }
    }

    // UI Delegates
    public static void QuickStackOnClick()
    {
        try
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
        catch (Exception e)
        {
            Log.Warning($"[QuickStack] {e.Message}");
            Log.Warning($"[QuickStack] {e.StackTrace}");
        }
    }

    public static void QuickRestockOnClick()
    {
        try
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
        catch (Exception e)
        {
            Log.Warning($"[QuickStack] {e.Message}");
            Log.Warning($"[QuickStack] {e.StackTrace}");
        }
    }

    public static void SetLockIconColor()
    {
        if (playerBackpack == null)
            return;

        XUiController[] slots = playerBackpack.GetItemStackControllers();

        for (int i = 0; i < slots.Length; ++i)
        {
            (slots[i].GetChildById("iconSlotLock").ViewComponent as XUiV_Sprite).Color = lockIconColor;
        }
    }

    public static void LoadConfig()
    {
        // Load config from QuickstackConfig.xml
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string path = GameIO.GetDefaultUserGameDataDir() + "/Mods/QuickStack/QuickStackConfig.xml";

            if (!File.Exists(path))
                path = Directory.GetCurrentDirectory() + "/Mods/QuickStack/QuickStackConfig.xml";

            if (!File.Exists(path))
                throw new Exception("Unable to find QuickStackConfig.xml");

            XmlDocument xml = new XmlDocument();
            xml.Load(path);

            string[] quickLockButtons = xml.GetElementsByTagName("QuickLockButtons")[0].InnerText.Trim().Split(' ');
            QuickStack.quickLockHotkeys = new KeyCode[quickLockButtons.Length];
            for (int i = 0; i < quickLockButtons.Length; i++)
                QuickStack.quickLockHotkeys[i] = (KeyCode)int.Parse(quickLockButtons[i]);

            string[] quickStackButtons = xml.GetElementsByTagName("QuickStackButtons")[0].InnerText.Trim().Split(' ');
            QuickStack.quickStackHotkeys = new KeyCode[quickStackButtons.Length];
            for (int i = 0; i < quickStackButtons.Length; i++)
                QuickStack.quickStackHotkeys[i] = (KeyCode)int.Parse(quickStackButtons[i]);

            string[] quickRestockButtons = xml.GetElementsByTagName("QuickRestockButtons")[0].InnerText.Trim().Split(' ');
            QuickStack.quickRestockHotkeys = new KeyCode[quickRestockButtons.Length];
            for (int i = 0; i < quickRestockButtons.Length; i++)
                QuickStack.quickRestockHotkeys[i] = (KeyCode)int.Parse(quickRestockButtons[i]);

            string[] stashDistance = xml.GetElementsByTagName("QuickStashDistance")[0].InnerText.Trim().Split(' ');
            QuickStack.stashDistanceX = Math.Min(Math.Max(QuickStack.stashDistanceX, 0), 127);
            QuickStack.stashDistanceY = Math.Min(Math.Max(QuickStack.stashDistanceY, 0), 127);
            QuickStack.stashDistanceZ = Math.Min(Math.Max(QuickStack.stashDistanceZ, 0), 127);

            string[] iconColor = xml.GetElementsByTagName("LockedSlotsIconColor")[0].InnerText.Trim().Split(' ');
            QuickStack.lockIconColor = new Color32(Byte.Parse(iconColor[0]), Byte.Parse(iconColor[1]), Byte.Parse(iconColor[2]), Byte.Parse(iconColor[3]));

            string[] borderColor = xml.GetElementsByTagName("LockedSlotsBorderColor")[0].InnerText.Trim().Split(' ');
            QuickStack.lockBorderColor = new Color32(Byte.Parse(borderColor[0]), Byte.Parse(borderColor[1]), Byte.Parse(borderColor[2]), Byte.Parse(borderColor[3]));

            SetLockIconColor();

            Log.Out($"[QuickStack] Loaded config in {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
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

            QuickStack.lockIconColor = new Color32(128, 128, 128, 255);
            QuickStack.lockBorderColor = new Color32(0, 0, 0, 0);

            Log.Warning($"[QuickStack] {e.Message}");
            Log.Warning($"[QuickStack] {e.StackTrace}");
            Log.Warning("[QuickStack] Failed to load or parse config");
        }
    }
}
