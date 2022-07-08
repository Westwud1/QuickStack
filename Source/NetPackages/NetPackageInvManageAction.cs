using System.Collections.Generic;

// Net Package for sending a list of container entities
public abstract class NetPackageInvManageAction : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.Both;
    public override bool AllowedBeforeAuth => false;

    protected NetPackageInvManageAction Setup(Vector3i _center, List<Vector3i> _containerEntities)
    {
        center = _center;
        containerEntities = _containerEntities;
        return this;
    }

    // Requantizes Vector3i to a 3-bytes. Requires -128 < x, y, z <= 128 
    protected static void WriteOptimized(PooledBinaryWriter _writer, Vector3i ivec3)
    {
        _writer.Write((sbyte)ivec3.x);
        _writer.Write((sbyte)ivec3.y);
        _writer.Write((sbyte)ivec3.z);
    }

    protected static void ReadOptimized(PooledBinaryReader _reader, out Vector3i ivec3)
    {
        ivec3 = new Vector3i
        {
            x = _reader.ReadSByte(),
            y = _reader.ReadSByte(),
            z = _reader.ReadSByte()
        };
    }

    // Vector3i without any requantization. Full range, but takes up 4x more space
    protected static void Write(PooledBinaryWriter _writer, Vector3i ivec3)
    {
        _writer.Write(ivec3.x);
        _writer.Write(ivec3.y);
        _writer.Write(ivec3.z);
    }

    protected static void Read(PooledBinaryReader _reader, out Vector3i ivec3)
    {
        ivec3 = new Vector3i
        {
            x = _reader.ReadInt32(),
            y = _reader.ReadInt32(),
            z = _reader.ReadInt32()
        };
    }

    public override int GetLength()
    {
        return 3 * sizeof(int) + sizeof(ushort) + 3 * containerEntities.Count;
    }

    public abstract override void ProcessPackage(World _world, GameManager _callbacks);

    public override void read(PooledBinaryReader _reader)
    {

        Read(_reader, out center);

        int count = _reader.ReadInt16();
        containerEntities = new List<Vector3i>(count);
        for (int i = 0; i < count; ++i)
        {
            ReadOptimized(_reader, out var idx);
            containerEntities.Add(idx);
        }
    }

    public override void write(PooledBinaryWriter _writer)
    {
        base.write(_writer);

        Write(_writer, center);

        if (containerEntities == null)
        {
            _writer.Write((ushort)0);
            return;
        }

        _writer.Write((ushort)containerEntities.Count);
        foreach (var id in containerEntities)
        {
            WriteOptimized(_writer, id);
        }
    }

    protected Vector3i center;
    protected List<Vector3i> containerEntities = new List<Vector3i>();
}
