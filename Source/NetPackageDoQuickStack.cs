using System;
using System.Collections.Generic;

// Server => Client
// Informs client it is safe to quick stack/restock
// To a list of containers
class NetPackageDoQuickStack : NetPackageInvManageAction
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    protected QuickStackType type;

    public NetPackageDoQuickStack Setup(Vector3i _center, List<Vector3i> _containerEntities, QuickStackType _type)
    {
        try
        {
            Setup(_center, _containerEntities);
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
        try
        {
            return base.GetLength() + 1;
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
            return 0;
        }
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        try
        {
            if (containerEntities == null || _world == null || containerEntities.Count == 0)
            {
                return;
            }

            switch (type)
            {
                case QuickStackType.Stack:
                    QuickStack.ClientMoveQuickStack(center, containerEntities);
                    break;

                case QuickStackType.Restock:
                    QuickStack.ClientMoveQuickRestock(center, containerEntities);
                    break;

                default:
                    break;
            }

            ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageUnlockContainers>().Setup(center, containerEntities));
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        try
        {
            base.read(_reader);
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
            _writer.Write((byte)type);
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }
}
