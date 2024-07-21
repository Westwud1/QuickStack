using System;
using System.Collections.Generic;

// Client => Server
// Requests a list of containers that are safe to modify
class NetPackageFindOpenableContainers : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public override bool AllowedBeforeAuth => false;
    protected int playerEntityId;
    protected QuickStackType type;

    public NetPackageFindOpenableContainers Setup(int _playerEntityId, QuickStackType _type)
    {
        try
        {
            playerEntityId = _playerEntityId;
            type = _type;
            return this;
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
            return null;
        }
    }

    public override int GetLength()
    {
        return 5;
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        try
        {
            if (_world == null)
            {
                return;
            }

            if (type >= QuickStackType.Count || type < QuickStackType.Stack)
            {
                return;
            }
            if (!_world.Players.dict.TryGetValue(playerEntityId, out var playerEntity) || playerEntity == null)
            {
                return;
            }

            var cinfo = ConnectionManager.Instance.Clients.ForEntityId(playerEntityId);
            if (cinfo == null)
            {
                return;
            }

            List<Vector3i> openableEntities = new List<Vector3i>(256);

            var center = new Vector3i(playerEntity.position);
            foreach (var centerEntityPair in QuickStack.FindNearbyLootContainers(center, playerEntityId))
            {
                if (centerEntityPair.Item2 == null)
                {
                    continue;
                }
                openableEntities.Add(centerEntityPair.Item1);
                GameManager.Instance.lockedTileEntities.Add(centerEntityPair.Item2, playerEntityId);
            }

            if (openableEntities.Count > 0)
            {
                cinfo.SendPackage(NetPackageManager.GetPackage<NetPackageDoQuickStack>().Setup(center, openableEntities, type));
            }
        }
        catch(Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        try
        {
            // ignore entity ID sent by client
            _ = _reader.ReadInt32();
            // use the NetPackage-provided one instead
            playerEntityId = Sender.entityId;
            type = (QuickStackType)_reader.ReadByte();
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }

    public override void write(PooledBinaryWriter _writer)
    {
        try
        {
            base.write(_writer);
            _writer.Write(playerEntityId);
            _writer.Write((byte)type);
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }
}
