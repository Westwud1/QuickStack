using System.Collections.Generic;

class NetPackageFindOpenableContainers : NetPackage {
  public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

  public NetPackageFindOpenableContainers Setup(int _playerEntityId, bool _forStacking) {
    playerEntityId = _playerEntityId;
    forStacking = _forStacking;
    return this;
  }

  public override int GetLength() {
    return 5;
  }

  public override void ProcessPackage(World _world, GameManager _callbacks) {
    if (_world == null) {
      return;
    }

    if (!_world.Players.dict.TryGetValue(playerEntityId, out var playerEntity) || playerEntity == null) {
      return;
    }

    var cinfo = ConnectionManager.Instance.Clients.ForEntityId(playerEntityId);
    if (cinfo == null) {
      return;
    }

    var lockedTileEntities = QuickStack.GetLockedTiles();
    if (lockedTileEntities == null) {
      return;
    }

    List<Vector3i> openableEntities = new List<Vector3i>(1024);

    var center = new Vector3i(playerEntity.position);
    foreach (var centerEntityPair in QuickStack.FindNearbyLootContainers(center, QuickStack.stackRadius, playerEntityId)) {
      if(centerEntityPair.Item2 == null) {
        continue;
      }
      openableEntities.Add(centerEntityPair.Item1);
      lockedTileEntities.Add(centerEntityPair.Item2, playerEntityId);
    }

    cinfo.SendPackage(NetPackageManager.GetPackage<NetPackageDoQuickStack>().Setup(center, openableEntities, forStacking));
  }

  public override void read(PooledBinaryReader _reader) {
    playerEntityId = _reader.ReadInt32();
    forStacking = _reader.ReadBoolean();
  }

  public override void write(PooledBinaryWriter _writer) {
    base.write(_writer);
    _writer.Write(playerEntityId);
    _writer.Write(forStacking);
  }

  protected int playerEntityId;
  protected bool forStacking;
}
