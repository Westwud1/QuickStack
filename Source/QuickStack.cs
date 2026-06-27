using Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using UnityEngine;

public enum StackType
{
    QuickStack,
    QuickRestock,
    None
}

internal class QuickStack
{
    public static string configFilePath;
    public static float[] lastClickTimes = new float[(int)StackType.None];
    public static bool lockModeIconVisible = true;
    public static Vector3i stashDistance = new Vector3i(7, 7, 7);
    public static XUiC_Backpack playerBackpack;
    public static XUiC_BackpackWindow backpackWindow;
    public static XUiC_ContainerStandardControls playerControls;
    public static KeyCode[] quickLockHotkeys;
    public static KeyCode[] quickStackHotkeys;
    public static KeyCode[] quickRestockHotkeys;
    public static Color32 lockIconColor = new Color32(255, 0, 0, 255);
    public static Color32 lockBorderColor = new Color32(128, 0, 0, 0);
    public static StackType stackInProgress = StackType.None;

    public static void LogInfo(string msg)
    {
        Log.Out($"[QuickStack] {msg}");
    }

    public static void LogWarning(string msg)
    {
        Log.Warning($"[QuickStack] {msg}");
    }

    public static void LogException(Exception e)
    {
        LogWarning(e.Message);
        LogWarning(e.StackTrace);
    }

    public static void PlayClickSound()
    {
        LocalPlayerUI.mPrimaryUI.mCursorController.PlayPagingSound();
    }

    private static XUiM_LootContainer.EItemMoveKind GetMoveKind(StackType type)
    {
        float unscaledTime = Time.unscaledTime;
        float lastClickTime = lastClickTimes[(int)type];
        lastClickTimes[(int)type] = unscaledTime;

        if (unscaledTime - lastClickTime < 2.0f)
            return XUiM_LootContainer.EItemMoveKind.FillAndCreate;
        else
            return XUiM_LootContainer.EItemMoveKind.FillOnly;
    }

    private static TEFeatureStorage[] GetNearbyContainers()
    {
        List<TEFeatureStorage> containers = new List<TEFeatureStorage>();

        EntityPlayerLocal primaryPlayer = GameManager.Instance.World.GetPrimaryPlayer();

        Vector3i playerPosition = new Vector3i(primaryPlayer.position.x, primaryPlayer.position.y, primaryPlayer.position.z);

        for (int x = -stashDistance.x; x <= stashDistance.x; ++x)
        {
            for (int y = -stashDistance.y; y <= stashDistance.y; ++y)
            {
                for (int z = -stashDistance.z; z <= stashDistance.z; ++z)
                {
                    Vector3i offset = new Vector3i(x, y, z);
                    Vector3i tilePosition = playerPosition + offset;

                    TileEntity tileEntity = GameManager.Instance.World.GetTileEntity(tilePosition);

                    if (tileEntity == null)
                        continue;

                    TEFeatureStorage storage = tileEntity.GetSelfOrFeature<TEFeatureStorage>();

                    if (storage == null)
                        continue;

                    if (storage.IsUserAccessing())
                    {
                        containers.Clear();
                        LogInfo("Unable to quick stack/restock while having a container opened");
                        return containers.ToArray();
                    }

                    if (storage.lockpickFeature != null && storage.lockpickFeature.NeedsLockpicking())
                        continue;

                    if (storage.lockFeature != null && storage.lockFeature.IsLocked() && !storage.lockFeature.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier))
                        continue;

                    if (storage.isJammed || storage.isQuestLoot || !storage.bTouched)
                        continue;

                    LockEntry lockEntry = new LockEntry(tileEntity);

                    if (LockManager.Instance.singleLocks.ContainsValue(lockEntry) || LockManager.Instance.sharedLocks.ContainsValue(lockEntry))
                        continue;

                    containers.Add(storage);
                }
            }
        }

        LogInfo($"Found {containers.Count} nearby suitable containers");

        return containers.ToArray();
    }

    public static void RequestQuickStack()
    {
        if (stackInProgress != StackType.None)
            return;

        TEFeatureStorage[] containers = GetNearbyContainers();

        if (containers.Length == 0)
            return;

        stackInProgress = StackType.QuickStack;
        LockManager.Instance.LockRequestLocal(containers);
    }

    public static void RequestQuickRestock()
    {
        if (stackInProgress != StackType.None)
            return;

        TEFeatureStorage[] containers = GetNearbyContainers();

        if (containers.Length == 0)
            return;

        stackInProgress = StackType.QuickRestock;
        LockManager.Instance.LockRequestLocal(containers);
    }

    public static void DoQuickStack(ReadOnlySpan<ILockTarget> containers)
    {
        XUiM_LootContainer.EItemMoveKind moveKind = GetMoveKind(StackType.QuickStack);

        foreach (TEFeatureStorage container in containers)
        {
            XUiM_LootContainer.StashItems(backpackWindow, playerBackpack, container, 0, playerControls.LockedSlots, moveKind, playerControls.MoveStartBottomRight);
            container.SetModified();
        }

        LockManager.Instance.UnlockRequestLocal();
    }

    public static void DoQuickRestock(ReadOnlySpan<ILockTarget> containers)
    {
        XUiM_LootContainer.EItemMoveKind moveKind = GetMoveKind(StackType.QuickRestock);
        LocalPlayerUI localPlayerUI = LocalPlayerUI.GetUIForPrimaryPlayer();
        XUiC_LootWindow lootWindow = ((XUiC_LootWindowGroup)((XUiWindowGroup)localPlayerUI.windowManager.GetWindow("looting")).Controller).lootWindow;

        ITileEntityLootable previousTileEntity = lootWindow.te;
        string previousName = lootWindow.lootContainerName;

        foreach (TEFeatureStorage container in containers)
        {
            lootWindow.SetTileEntityChest("QuickRestock", container);
            XUiM_LootContainer.StashItems(backpackWindow, lootWindow.lootContainer, localPlayerUI.mXUi.PlayerInventory, 0, lootWindow.standardControls.LockedSlots, moveKind, playerControls.MoveStartBottomRight);
            container.SetModified();
        }

        lootWindow.SetTileEntityChest(previousName, previousTileEntity);
        LockManager.Instance.UnlockRequestLocal();
    }

    public static void InitializeQuickLock(XUiC_ItemStackGrid grid, XUiC_ContainerStandardControls controls)
    {
        controls.GetChildById("btnToggleLockMode").ViewComponent.IsVisible = lockModeIconVisible;

        XUiC_ItemStack[] slots = grid.GetItemStackControllers();

        for (int i = 0; i < slots.Length; ++i)
        {
            int index = i;
            slots[i].OnPress += (XUiController _sender, int _mouseButton) =>
            {
                for (int j = 0; j < quickLockHotkeys.Length; ++j)
                {
                    if (!UICamera.GetKey(quickLockHotkeys[j]))
                        return;
                }
                
                XUiC_ItemStack itemStack = _sender as XUiC_ItemStack;
                itemStack.UserLockedSlot = !itemStack.UserLockedSlot;
                controls.LockedSlots[index] = itemStack.userLockedSlot;
                PlayClickSound();
            };
        }
    }

    public static void LoadConfig()
    {
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (!File.Exists(configFilePath))
                throw new Exception($"Unable to find config at: {configFilePath}");

            XmlDocument xml = new XmlDocument();
            xml.Load(configFilePath);

            string[] quickLockButtons = xml.GetElementsByTagName("QuickLockButtons")[0].InnerText.Trim().Split(' ');
            if (quickLockButtons.Length == 0)
                throw new Exception("Must have at least one value for tag QuickLockButtons");
            quickLockHotkeys = new KeyCode[quickLockButtons.Length];
            for (int i = 0; i < quickLockButtons.Length; ++i)
                quickLockHotkeys[i] = (KeyCode)int.Parse(quickLockButtons[i]);

            string[] quickStackButtons = xml.GetElementsByTagName("QuickStackButtons")[0].InnerText.Trim().Split(' ');
            if (quickStackButtons.Length == 0)
                throw new Exception("Must have at least one value for tag QuickStackButtons");
            quickStackHotkeys = new KeyCode[quickStackButtons.Length];
            for (int i = 0; i < quickStackButtons.Length; ++i)
                quickStackHotkeys[i] = (KeyCode)int.Parse(quickStackButtons[i]);

            string[] quickRestockButtons = xml.GetElementsByTagName("QuickRestockButtons")[0].InnerText.Trim().Split(' ');
            if (quickRestockButtons.Length == 0)
                throw new Exception("Must have at least one value for tag QuickRestockButtons");
            quickRestockHotkeys = new KeyCode[quickRestockButtons.Length];
            for (int i = 0; i < quickRestockButtons.Length; ++i)
                quickRestockHotkeys[i] = (KeyCode)int.Parse(quickRestockButtons[i]);

            lockModeIconVisible = bool.Parse(xml.GetElementsByTagName("LockModeIconVisible")[0].InnerText);

            string[] stashDistanceStr = xml.GetElementsByTagName("QuickStashDistance")[0].InnerText.Trim().Split(' ');
            if (stashDistanceStr.Length != 3)
                throw new Exception("Must have exactly three values for tag QuickStashDistance");
            stashDistance.x = Math.Min(Math.Max(int.Parse(stashDistanceStr[0]), 0), 127);
            stashDistance.y = Math.Min(Math.Max(int.Parse(stashDistanceStr[1]), 0), 127);
            stashDistance.z = Math.Min(Math.Max(int.Parse(stashDistanceStr[2]), 0), 127);

            string[] iconColor = xml.GetElementsByTagName("LockedSlotsIconColor")[0].InnerText.Trim().Split(' ');
            if (iconColor.Length != 4)
                throw new Exception("Must have exactly four values for tag LockedSlotsIconColor");
            lockIconColor = new Color32(byte.Parse(iconColor[0]), byte.Parse(iconColor[1]), byte.Parse(iconColor[2]), byte.Parse(iconColor[3]));

            string[] borderColor = xml.GetElementsByTagName("LockedSlotsBorderColor")[0].InnerText.Trim().Split(' ');
            if (borderColor.Length != 4)
                throw new Exception("Must have exactly four values for tag LockedSlotsBorderColor");
            lockBorderColor = new Color32(byte.Parse(borderColor[0]), byte.Parse(borderColor[1]), byte.Parse(borderColor[2]), byte.Parse(borderColor[3]));

            LogInfo($"Loaded config in {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (Exception e)
        {
            quickLockHotkeys = new KeyCode[1];
            quickLockHotkeys[0] = KeyCode.LeftAlt;

            quickStackHotkeys = new KeyCode[2];
            quickStackHotkeys[0] = KeyCode.LeftAlt;
            quickStackHotkeys[1] = KeyCode.X;

            quickRestockHotkeys = new KeyCode[2];
            quickRestockHotkeys[0] = KeyCode.LeftAlt;
            quickRestockHotkeys[1] = KeyCode.Z;

            lockModeIconVisible = true;

            stashDistance = new Vector3i(7, 7, 7);

            lockIconColor = new Color32(255, 0, 0, 255);
            lockBorderColor = new Color32(128, 0, 0, 0);

            LogWarning("Failed to load or parse config");
            LogException(e);
        }
    }
}
