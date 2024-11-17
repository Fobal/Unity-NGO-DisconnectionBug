using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public static class CustomNetMessages
{
    public class MsgTest : INetworkSerializable
    {
        public bool bool_value;
        public int int_value;
        public ulong ulong_value;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref bool_value);
            serializer.SerializeValue(ref int_value);
            serializer.SerializeValue(ref ulong_value);
        }
    }
}
