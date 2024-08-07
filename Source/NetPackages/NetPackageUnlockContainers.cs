﻿using System;
using System.Collections.Generic;

// Client => Server
// Notifies server that containers are no longer in-use
class NetPackageUnlockContainers : NetPackageInvManageAction
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public new NetPackageUnlockContainers Setup(Vector3i _center, List<Vector3i> _containerEntities)
    {
        try
        {
            _ = base.Setup(_center, _containerEntities);
            return this;
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
            return null;
        }
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        try
        {
            if (containerEntities == null || _world == null)
            {
                return;
            }

            foreach (var offset in containerEntities)
            {
                var entity = _world.GetTileEntity(0, center + offset);
                if (entity != null)
                {
                    GameManager.Instance.lockedTileEntities.Remove(entity);
                }
            }
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }
}
