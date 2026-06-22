using System;
using System.Collections.Generic;

// Net Package for sending a list of container entities
public abstract class NetPackageInvManageAction : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.Both;
    public override bool AllowedBeforeAuth => false;
    public abstract override void ProcessPackage(World _world, GameManager _callbacks);
    protected Vector3i center;
    protected List<Vector3i> containerEntities = new List<Vector3i>();

    protected NetPackageInvManageAction Setup(Vector3i _center, List<Vector3i> _containerEntities)
    {
        try
        {
            center = _center;
            containerEntities = _containerEntities;
            return this;
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
            return null;
        }
    }

    // Requantizes Vector3i to a 3-bytes. Requires -128 < x, y, z <= 128 
    protected static void WriteOptimized(PooledBinaryWriter _writer, Vector3i ivec3)
    {
        try
        {
            _writer.Write((sbyte)ivec3.x);
            _writer.Write((sbyte)ivec3.y);
            _writer.Write((sbyte)ivec3.z);
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }

    protected static void ReadOptimized(PooledBinaryReader _reader, out Vector3i ivec3)
    {
        try
        {
            ivec3 = new Vector3i
            {
                x = _reader.ReadSByte(),
                y = _reader.ReadSByte(),
                z = _reader.ReadSByte()
            };
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
            ivec3 = new Vector3i(0, 0, 0);
        }
    }

    // Vector3i without any requantization. Full range, but takes up 4x more space
    protected static void Write(PooledBinaryWriter _writer, Vector3i ivec3)
    {
        try
        {
            _writer.Write(ivec3.x);
            _writer.Write(ivec3.y);
            _writer.Write(ivec3.z);
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }

    protected static void Read(PooledBinaryReader _reader, out Vector3i ivec3)
    {
        try
        {
            ivec3 = new Vector3i
            {
                x = _reader.ReadInt32(),
                y = _reader.ReadInt32(),
                z = _reader.ReadInt32()
            };
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
            ivec3 = new Vector3i(0, 0, 0);
        }
    }

    public override int GetLength()
    {
        try
        {
            return 3 * sizeof(int) + sizeof(ushort) + 3 * containerEntities.Count;
        }
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
            return 0;
        }
    }

    public override void read(PooledBinaryReader _reader)
    {
        try
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
        catch (Exception e)
        {
            QuickStack.printExceptionInfo(e);
        }
    }
}
