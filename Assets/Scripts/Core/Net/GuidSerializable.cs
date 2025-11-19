// Assets/Scripts/Core/Net/GuidSerializable.cs
using System;
using Unity.Collections;
using Unity.Netcode;

namespace CardGameBuilder.Core.Net
{
    /// <summary>
    /// Lightweight Guid wrapper that serializes as a FixedString32Bytes (N format "D32").
    /// </summary>
    public struct GuidSerializable : INetworkSerializable, IEquatable<GuidSerializable>
    {
        public FixedString32Bytes Value; // stores "N" (32 hex chars, no dashes)

        public static GuidSerializable From(Guid g)
        {
            // "N" = 32 digits, no hyphens; fits in FixedString32Bytes
            return new GuidSerializable { Value = g.ToString("N") };
        }

        public Guid ToGuid()
        {
            // Guid.Parse is fine here since Value length is bounded (32)
            return Guid.ParseExact(Value.ToString(), "N");
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Value);
        }

        public bool Equals(GuidSerializable other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is GuidSerializable other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }
}
