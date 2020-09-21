
namespace BoundlessProxyUi.SplitStream
{
    /// <summary>
    /// Object to hold a buffer array and indicate how much data is in it
    /// </summary>
    class StreamChunk
    {
        /// <summary>
        /// Creates a new instance of StreamChunk
        /// </summary>
        /// <param name="buffer">The buffer to store data</param>
        public StreamChunk(byte[] buffer)
        {
            Buffer = buffer;
        }

        /// <summary>
        /// The buffer to store data
        /// </summary>
        public byte[] Buffer { get; set; }

        /// <summary>
        /// The length of the data contained in Buffer
        /// </summary>
        public int Length { get; set; }
    }
}