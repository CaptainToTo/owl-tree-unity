using System;
using OwlTree;

namespace OwlTree.Unity
{
    /// <summary>
    /// Unique integer Id to instantiate the same prefab across clients.
    /// </summary>
    public struct PrefabId : IEncodable
    {
        public const UInt32 FirstPrefabId = 1;

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Get a GameObjectId using an existing id value.
        /// </summary>
        public PrefabId(uint id)
        {
            _id = id;
        }

        /// <summary>
        /// Get a PrefabId by decoding it from a span.
        /// </summary>
        public PrefabId(ReadOnlySpan<byte> bytes)
        {
            _id = 0;
            FromBytes(bytes);
        }

        /// <summary>
        /// The id value.
        /// </summary>
        public uint Id => _id;

        /// <summary>
        /// The prefab id used to signal that there is no prefab. Id value is 0.
        /// </summary>
        public static PrefabId None = new PrefabId(0);

        public int ByteLength() => 4;

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4)
                throw new ArgumentException("Span must have 4 bytes from ind to decode a PrefabId from.");
            _id = BitConverter.ToUInt32(bytes);
        }

        public void InsertBytes(Span<byte> bytes)
        {
            if (bytes.Length < 4)
                throw new ArgumentException("Not enough bytes to insert PrefabId.");
            BitConverter.TryWriteBytes(bytes, _id);
        }

        // Operators

        /// <summary>
        /// Returns the id number as a string.
        /// </summary>
        public override string ToString()
        {
            return "<PrefabId: " + (_id == 0 ? "None" : _id.ToString()) + ">";
        }

        public static bool operator ==(PrefabId a, PrefabId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(PrefabId a, PrefabId b)
        {
            return a._id != b._id;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(PrefabId) && ((PrefabId)obj)._id == _id;
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        public static int MaxLength()
        {
            return 4;
        }
    }
}
