namespace BoundlessProxyUi.SplitStream
{
    public class StreamChunk
    {
        public StreamChunk(byte[] buffer)
        {
            Buffer = buffer;
        }

        public byte[] Buffer;
        public int Length;
    }
}