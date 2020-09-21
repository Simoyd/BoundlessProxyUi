using System;

namespace BoundlessProxyUi.WsData
{
    /// <summary>
    /// Class to store a single message within a websocket frame
    /// </summary>
    class WsMessage
    {
        /// <summary>
        /// Creates a new instance of WsMessage
        /// </summary>
        /// <param name="messageLength">Total length of the message (including API byte)</param>
        /// <param name="apiId">The ID byte of the message</param>
        /// <param name="buffer">The contents of the message</param>
        public WsMessage(ushort messageLength, byte? apiId, byte[] buffer)
        {
            // Save the value
            ApiId = apiId;

            // Allocate the buffer
            Buffer = new byte[messageLength - 1];

            // Copy the data to the buffer
            Array.Copy(buffer, Buffer, messageLength - 1);
        }

        /// <summary>
        /// The message purpose
        /// </summary>
        public byte? ApiId { get; set; }

        /// <summary>
        /// The message contents
        /// </summary>
        public byte[] Buffer { get; set; }
    }
}
