using System.Collections.Generic;

// Server => Client
// Informs client it is safe to quick stack/restock
// To a list of containers
class NetPackageDoQuickStack : NetPackageInvManageAction {
  public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

  public NetPackageDoQuickStack Setup(Vector3i _center, List<Vector3i> _containerEntities, QuickStackType _type) {
    Setup(_center,_containerEntities);
    type = _type;
    return this;
  }

  public override int GetLength() {
    return base.GetLength() + 1;
  }

  public override void ProcessPackage(World _world, GameManager _callbacks) {
    if(containerEntities == null || _world == null || containerEntities.Count == 0) {
      return;
    }

    switch (type) {
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

  public override void read(PooledBinaryReader _reader) {
    base.read(_reader);
    type = (QuickStackType)_reader.ReadByte();
  }

  public override void write(PooledBinaryWriter _writer) {
    base.write(_writer);
    _writer.Write((byte)type);
  }

  protected QuickStackType type;
}
