using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace BoundlessProxyUi.WsData
{
    /// <summary>
    /// Class to parse and store a single websocket frame
    /// </summary>
    class WsFrame
    {
        #region RFC 6455 5.2 Base Framing Protocol

        // 0                   1                   2                   3
        // 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        //+-+-+-+-+-------+-+-------------+-------------------------------+
        //|F|R|R|R| opcode|M| Payload len |    Extended payload length    |
        //|I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
        //|N|V|V|V|       |S|             |   (if payload len==126/127)   |
        //| |1|2|3|       |K|             |                               |
        //+-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
        //|     Extended payload length continued, if payload len == 127  |
        //+ - - - - - - - - - - - - - - - +-------------------------------+
        //|                               |Masking-key, if MASK set to 1  |
        //+-------------------------------+-------------------------------+
        //| Masking-key (continued)       |          Payload Data         |
        //+-------------------------------- - - - - - - - - - - - - - - - +
        //:                     Payload Data continued ...                :
        //+ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
        //|                     Payload Data continued ...                |
        //+---------------------------------------------------------------+

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a dummy frame with nothing in it
        /// </summary>
        public WsFrame() {}

        /// <summary>
        /// Creates a new instance of WsFrame
        /// </summary>
        /// <param name="buffer">Buffer to use to read data from the input stream. Can contain data already read</param>
        /// <param name="preBufferLength">Number of bytes already read</param>
        /// <param name="source">The source stream to read the frame bytes from</param>
        public WsFrame(byte[] buffer, int preBufferLength, Stream source)
        {
            // Some bytes have already be read before this parser is called. Save them.
            m_readStream.Write(buffer, 0, preBufferLength);

            // Populate members
            this.m_buffer = buffer;
            this.m_source = source;
            m_wsHeader = buffer[0];

            // Get final frame bit
            var isFinalFrame = (buffer[0] & 0b10000000) != 0;

            if (!isFinalFrame)
            {
                // TODO: This does rarely happen, but I'm not currently handling it... following frames will be wildly corrupted on the UI.
            }

            var rsv1 = (buffer[0] & 0b01000000) != 0;
            var rsv2 = (buffer[0] & 0b00100000) != 0;
            var rsv3 = (buffer[0] & 0b00010000) != 0;

            // TODO: are RSV bits ever used? I saw them a few times but it may be due to the flag above
            if (rsv1 || rsv2 || rsv3)
            {
                //throw new Exception("RSV1, RSV2, and RSV3 are not supported.");
            }

            // Get the opcode
            var opcode = buffer[0] & 0b00001111;

            if (opcode != 2 && opcode != 8 && opcode != 0)
            {
                throw new Exception("Opcodes other than 'binary' and 'close' are not supported.");
            }

            // Get the mask flag bit
            var isMasked = (buffer[1] & 0b10000000) != 0;

            // Get the first length byte
            ulong length = (ulong)(buffer[1] & 0b01111111);

            if (length == 126)
            {
                // Get the 16-bit length
                if (!ReadBytes(2))
                {
                    throw new Exception("Unexpected end of stream while reading length bytes");
                }

                length = BitConverter.ToUInt16(buffer.Take(2).Reverse().ToArray(), 0);

                if (length < 126)
                {
                    throw new Exception("Invalid length bytes in header");
                }
            }
            else if (length == 127)
            {
                // Get the 64-bit length
                if (!ReadBytes(8))
                {
                    throw new Exception("Unexpected end of stream while reading length bytes");
                }

                length = BitConverter.ToUInt64(buffer.Take(8).Reverse().ToArray(), 0);

                if (length <= ushort.MaxValue)
                {
                    throw new Exception("Invalid length bytes in header");
                }
            }

            // Get the mask value if the mask bit was set
            if (isMasked)
            {
                if (!ReadBytes(4))
                {
                    throw new Exception("Unexpected end of stream while reading mask bytes");
                }

                m_mask = buffer.Take(4).ToArray();
            }

            // Get the message count
            if (!ValidateLength(ref length, 2))
            {
                FinishPostBytes((int)length);
                return;
            }

            if (!ReadBytes(2))
            {
                throw new Exception("Unexpected end of stream while reading message count");
            }

            var messageCount = BitConverter.ToUInt16(buffer, 0);

            // This bit can sometimes be 1. Not sure what it means, but take it out so it doesn't mess with the count.
            m_frameBit = (messageCount & 0x8000) != 0;
            messageCount &= 0x7FFF;

            HeaderBytes = m_readStream.ToArray();
            m_readStream = null;

            // Read in all the messages
            for (int curMessageIndex = 0; curMessageIndex < messageCount; ++curMessageIndex)
            {
                // Get the message length
                if (!ValidateLength(ref length, 2))
                {
                    FinishPostBytes((int)length);
                    return;
                }

                if (!ReadBytes(2))
                {
                    throw new Exception("Unexpected end of stream while reading message length");
                }

                var messageLength = BitConverter.ToUInt16(buffer, 0);

                // Get the message API ID
                if (!ValidateLength(ref length, 1))
                {
                    m_postBytes.AddRange(BitConverter.GetBytes(messageLength));
                    FinishPostBytes((int)length);
                    return;
                }

                if (!ReadBytes(1))
                {
                    throw new Exception("Unexpected end of stream while reading Api Id");
                }

                var apiId = buffer[0];

                // Get the message payload
                if (!ValidateLength(ref length, (ulong)messageLength - 1))
                {
                    m_postBytes.AddRange(BitConverter.GetBytes(messageLength));
                    m_postBytes.Add(apiId);
                    FinishPostBytes((int)length);
                    return;
                }

                var myBuffer = new byte[messageLength - 1];

                if (!ReadBytesDump(myBuffer))
                {
                    throw new Exception("Unexpected end of stream while reading message content");
                }

                // Save the message on this frame
                Messages.Add(new WsMessage(messageLength, apiId, myBuffer));
            }
        }

        #endregion

        #region Public members

        /// <summary>
        /// Header bytes of the frame, for display on the UI
        /// </summary>
        public byte[] HeaderBytes { get; set; } = new byte[] { 0b10000000, 0 };

        /// <summary>
        /// List of messages in this frame
        /// </summary>
        public List<WsMessage> Messages { get; set; } = new List<WsMessage>();

        #endregion

        #region Private members

        /// <summary>
        /// First header byte
        /// </summary>
        private readonly byte m_wsHeader;

        /// <summary>
        /// Unknown bit mixed in with the frame message count
        /// </summary>
        private readonly bool m_frameBit;

        /// <summary>
        /// Used to store bytes when message parsing fails
        /// </summary>
        private readonly List<byte> m_postBytes = new List<byte>();

        /// <summary>
        /// Buffer used for reading
        /// </summary>
        private readonly byte[] m_buffer;

        /// <summary>
        /// The input stream for reading data to parse
        /// </summary>
        private readonly Stream m_source;

        /// <summary>
        /// Stream used to save bytes being read for saving the header
        /// </summary>
        private readonly MemoryStream m_readStream = new MemoryStream();

        /// <summary>
        /// Frame mask 4 bytes (if mask is true)
        /// </summary>
        private byte[] m_mask;

        /// <summary>
        /// Current read offset in the mask
        /// </summary>
        private ulong m_maskOffset = 0;

        #endregion

        #region Public members

        /// <summary>
        /// Serializes this frame and messages to the destination provided
        /// </summary>
        /// <param name="destination">Destination stream to serialize to</param>
        public void Send(Stream destination)
        {
            // Cache the mask locally during header output, so the header itself isn't masked
            byte[] mask = this.m_mask;
            this.m_mask = null;

            // Allocate space for the largest header
            byte[] header = new byte[10] { m_wsHeader, mask == null ? (byte)0 : (byte)0b10000000, 0, 0, 0, 0, 0, 0, 0, 0 };

            // Track the header length, start with smallest and work our way up
            int headerLength = 2;

            // Calculate the total length of the message
            ulong totalLength = 0;
            foreach (var msg in Messages)
            {
                // length + api id + buffer + postbytes
                totalLength += 2 + 1 + (ulong)msg.Buffer.Length + (ulong)m_postBytes.Count;
            }

            totalLength += 2;

            // Populate the correct number of length bytes
            if (totalLength > ushort.MaxValue)
            {
                header[1] |= 127;
                Array.Copy(BitConverter.GetBytes(totalLength).Reverse().ToArray(), 0, header, headerLength, 8);
                headerLength += 8;
            }
            else if (totalLength > 125)
            {
                header[1] |= 126;
                Array.Copy(BitConverter.GetBytes((ushort)totalLength).Reverse().ToArray(), 0, header, headerLength, 2);
                headerLength += 2;
            }
            else
            {
                header[1] |= (byte)totalLength;
            }

            // Update the header with the mask if set
            if (mask != null)
            {
                Array.Copy(mask, 0, header, headerLength, 4);
                headerLength += 4;
            }

            // Write header
            WriteBytes(destination, header, 0, headerLength);

            // Restore the mask now that the header is written, so the rest of the message can be masked
            this.m_mask = mask;
            m_maskOffset = 0;

            // Write the message count with the unknown bit
            WriteBytes(destination, BitConverter.GetBytes((ushort)Messages.Count | (m_frameBit ? 0x8000 : 0)), 0, 2);

            // Loop through all the messages
            foreach (var msg in Messages)
            {
                // Ensure length is valid (should never be a problem
                if ((msg.Buffer.Length + 1) > ushort.MaxValue)
                {
                    throw new OverflowException($"cannot send message over {ushort.MaxValue} bytes.");
                }

                // Write the lemgth, the API ID, and the message content
                WriteBytes(destination, BitConverter.GetBytes((ushort)(msg.Buffer.Length + 1)), 0, 2);
                WriteBytes(destination, new byte[] { msg.ApiId ?? 0 }, 0, 1);
                WriteBytes(destination, (byte[])msg.Buffer.Clone(), 0, msg.Buffer.Length);
            }

            // Output parse failure bytes
            if (m_postBytes.Count > 0)
            {
                WriteBytes(destination, m_postBytes.ToArray(), 0, m_postBytes.Count);
            }
        }

        #endregion

        #region Private members

        /// <summary>
        /// Writes bytes to the destination stream
        /// </summary>
        /// <param name="destination">Stream to write to</param>
        /// <param name="sendBytes">Bytes to write</param>
        /// <param name="offset">Offset in sendBytes to write from</param>
        /// <param name="count">Number of bytes to write</param>
        private void WriteBytes(Stream destination, byte[] sendBytes, int offset, int count)
        {
            // Apply the mask if applicable
            if (m_mask != null)
            {
                for (int i = 0; i < count; ++i)
                {
                    sendBytes[i + offset] ^= m_mask[m_maskOffset];
                    m_maskOffset = (m_maskOffset + 1) % 4;
                }
            }

            // Write the bytes
            destination.Write(sendBytes, 0, count);
        }

        /// <summary>
        /// Validates that the requested number of bytes are available to be read, and updates the remaining byte count
        /// </summary>
        /// <param name="length">Number of bytes available</param>
        /// <param name="count">Number of bytes requested</param>
        /// <returns>True if available, otherwise false</returns>
        private bool ValidateLength(ref ulong length, ulong count)
        {
            if (count > length)
            {
                return false;
            }

            length -= count;
            return true;
        }

        /// <summary>
        /// Reads the remainder of the frame into postBytes when parsing fails
        /// </summary>
        /// <param name="length">Number of bytes to read from the input stream</param>
        private void FinishPostBytes(int length)
        {
            ReadBytes(length);
            m_postBytes.AddRange(m_buffer.Take(length));
        }

        /// <summary>
        /// Fills the specified buffer from the source stream
        /// </summary>
        /// <param name="myBuffer">The buffer to fill</param>
        /// <returns>True on success, otherwise false</returns>
        private bool ReadBytesDump(byte[] myBuffer)
        {
            // Track the current offset
            int offset = 0;

            // Remaining number of bytes to read
            int count = myBuffer.Length;

            // Loop until no bytes left to read
            while (count > 0)
            {
                // Read as much as possible
                var readBytes = Math.Min(m_buffer.Length, count);
                if (!ReadBytes(readBytes))
                {
                    return false;
                }

                // Copy to the input buffer
                Array.Copy(m_buffer, 0, myBuffer, offset, readBytes);

                // Update tracking values
                offset += readBytes;
                count -= readBytes;
            }

            return true;
        }

        /// <summary>
        /// Reads bytes from the source stream into
        /// </summary>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>True on success, otherwise false</returns>
        private bool ReadBytes(int count)
        {
            // Track the offset in the buffer
            int offset = 0;

            // Loop until we're done reading the count bytes
            while (offset < count)
            {
                // Read the bytes
                int bytesRead = 0;
                try
                {
                    bytesRead = m_source.Read(m_buffer, offset, count - offset);
                }
                catch
                { }

                // Save the bytes for the header on the UI
                m_readStream?.Write(m_buffer, offset, bytesRead);

                // Unmask if applicable
                if (m_mask != null)
                {
                    for (int i = 0; i < bytesRead; ++i)
                    {
                        m_buffer[i + offset] ^= m_mask[m_maskOffset];
                        m_maskOffset = (m_maskOffset + 1) % 4;
                    }
                }

                Console.WriteLine($"BYTES: {{ {string.Join(", ", m_buffer.Take(bytesRead).Select(cur => cur.ToString("X2")))} }}");

                // If nothing was read, then fail
                if (bytesRead == 0)
                {
                    return false;
                }

                // Update the tracking offset
                offset += bytesRead;
            }

            return true;
        }

        #endregion
    }
}
