using System.Collections.Generic;

// Server => Client
// Informs client it is safe to quick stack/restock
// To a list of containers
class NetPackageDoQuickStack : NetPackageInvManageAction {

  public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

  public NetPackageDoQuickStack Setup(Vector3i _center, List<Vector3i> _containerEntities, bool _forStacking) {
    Setup(_center,_containerEntities);
    forStacking = _forStacking;
    return this;
  }

  public override int GetLength() {
    return base.GetLength() + 1;
  }

  public override void ProcessPackage(World _world, GameManager _callbacks) {
    if(containerEntities == null || _world == null) {
      return;
    }

    if (forStacking) {
      QuickStack.ClientMoveQuickStack(center, containerEntities);
    } else {
      QuickStack.ClientMoveQuickRestock(center, containerEntities);
    }

    ConnectionManager.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageUnlockContainers>().Setup(center, containerEntities));
  }

  public override void read(PooledBinaryReader _reader) {
    base.read(_reader);
    forStacking = _reader.ReadBoolean();
  }

  public override void write(PooledBinaryWriter _writer) {
    base.write(_writer);
    _writer.Write(forStacking);
  }

  protected bool forStacking;
}
