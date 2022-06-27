﻿using System.Collections.Generic;

// Client => Server
// Requests a list of containers that are safe to modify
class NetPackageFindOpenableContainers : NetPackage {
  public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
  public override bool AllowedBeforeAuth => false;

  public NetPackageFindOpenableContainers Setup(int _playerEntityId, QuickStackType _type) {
    playerEntityId = _playerEntityId;
    type = _type;
    return this;
  }

  public override int GetLength() {
    return 5;
  }

  public override void ProcessPackage(World _world, GameManager _callbacks) {
    if (_world == null) {
      return;
    }

    if(type >= QuickStackType.Count || type < QuickStackType.Stack) {
      return;
    }

    if (!_world.Players.dict.TryGetValue(playerEntityId, out var playerEntity) || playerEntity == null) {
      return;
    }

    var cinfo = ConnectionManager.Instance.Clients.ForEntityId(playerEntityId);
    if (cinfo == null) {
      return;
    }

    var lockedTileEntities = QuickStack.GetOpenedTiles();
    if (lockedTileEntities == null) {
      return;
    }

    List<Vector3i> openableEntities = new List<Vector3i>(256);

    var center = new Vector3i(playerEntity.position);
    foreach (var centerEntityPair in QuickStack.FindNearbyLootContainers(center, playerEntityId)) {
      if(centerEntityPair.Item2 == null) {
        continue;
      }
      openableEntities.Add(centerEntityPair.Item1);
      lockedTileEntities.Add(centerEntityPair.Item2, playerEntityId);
    }

    if(openableEntities.Count > 0) {
      cinfo.SendPackage(NetPackageManager.GetPackage<NetPackageDoQuickStack>().Setup(center, openableEntities, type));
    }
  }

  public override void read(PooledBinaryReader _reader) {
    playerEntityId = _reader.ReadInt32();
    type = (QuickStackType)_reader.ReadByte();
  }

  public override void write(PooledBinaryWriter _writer) {
    base.write(_writer);
    _writer.Write(playerEntityId);
    _writer.Write((byte)type);
  }

  protected int playerEntityId;
  protected QuickStackType type;
}
