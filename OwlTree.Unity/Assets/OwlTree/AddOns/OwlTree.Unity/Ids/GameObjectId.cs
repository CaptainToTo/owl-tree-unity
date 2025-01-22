using System;
using OwlTree;

namespace OwlTree.Unity
{
    /// <summary>
    /// Unique integer Id for each GameObject that needs to be synchronized.
    /// </summary>
    public struct GameObjectId : IEncodable
    {
        public const UInt32 FirstGameObjectId = 1;

        // the actual id
        private UInt32 _id;

        /// <summary>
        /// Get a GameObjectId using an existing id value.
        /// </summary>
        public GameObjectId(uint id)
        {
            _id = id;
        }

        /// <summary>
        /// Get a GameObjectId by decoding it from a span.
        /// </summary>
        public GameObjectId(ReadOnlySpan<byte> bytes)
        {
            _id = 0;
            FromBytes(bytes);
        }

        /// <summary>
        /// The id value.
        /// </summary>
        public uint Id => _id;

        /// <summary>
        /// The game object id used to signal that there is no object. Id value is 0.
        /// </summary>
        public static GameObjectId None = new GameObjectId(0);

        public int ByteLength() => 4;

        public void FromBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 4)
                throw new ArgumentException("Span must have 4 bytes from ind to decode a GameObjectId from.");
            _id = BitConverter.ToUInt32(bytes);
        }

        public void InsertBytes(Span<byte> bytes)
        {
            if (bytes.Length < 4)
                throw new ArgumentException("Not enough bytes to insert GameObjectId.");
            BitConverter.TryWriteBytes(bytes, _id);
        }

        // Operators

        /// <summary>
        /// Returns the id number as a string.
        /// </summary>
        public override string ToString()
        {
            return "<GameObjectId: " + (_id == 0 ? "None" : _id.ToString()) + ">";
        }

        public static bool operator ==(GameObjectId a, GameObjectId b)
        {
            return a._id == b._id;
        }

        public static bool operator !=(GameObjectId a, GameObjectId b)
        {
            return a._id != b._id;
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj.GetType() == typeof(GameObjectId) && ((GameObjectId)obj)._id == _id;
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

