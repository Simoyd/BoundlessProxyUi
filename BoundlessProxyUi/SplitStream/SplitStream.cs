using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BoundlessProxyUi.SplitStream
{
    class SplitStream : Stream
    {
        private readonly Stream _sourceStream;
        private readonly List<SplitStreamReader> _splitStreams = new List<SplitStreamReader>();

        private volatile bool _finished = false;
        private volatile bool _started = false;

        private readonly byte[] buffer;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position { get => 0; set { } }

        public SplitStream(Stream stream, int chunkSize = 4096)
        {
            _sourceStream = stream;
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "> 0");
            buffer = new byte[chunkSize];
        }

        public SplitStreamReader GetReader()
        {
            if (_started) throw new InvalidOperationException("Data stream reading already started!");

            var stream = new SplitStreamReader(_sourceStream, this);
            lock (_splitStreams)
            {
                _splitStreams.Add(stream);
            }
            return stream;
        }

        public void TerminateReader(Stream stream)
        {
            var myStream = stream as SplitStreamReader;

            lock (_splitStreams)
            {
                _splitStreams.Remove(myStream);
            }
            myStream.PushChunk(null, 0);
        }

        public void StartReadAhead()
        {
            if (_sourceStream != null)
            {
                new Thread(() => ReadAheadChunks()).Start();
            }
        }

        public override void Write(byte[] writeBuffer, int offset, int count)
        {
            if (_sourceStream != null)
            {
                throw new Exception("nope");
            }

            int finalOffset = offset + count;

            while (offset < finalOffset)
            {
                int numBytes = Math.Min(buffer.Length, finalOffset - offset);

                Array.Copy(writeBuffer, offset, buffer, 0, numBytes);
                PushChunkToStreams(numBytes);

                offset += numBytes;
            }
        }

        private void ReadAheadChunks()
        {
            _started = true;

            do
            {
                if (_splitStreams.Count < 2)
                {
                    _splitStreams.ForEach(cur => cur.DoDirect());
                    return;
                }

                int length = 0;

                try
                {
                    length = _sourceStream.Read(buffer, 0, buffer.Length);
                }
                catch { }

                if (length > 0)
                {
                    PushChunkToStreams(length);
                }
                else
                {
                    PushChunkToStreams(-1);
                    _finished = true;
                }
            } while (_finished == false);
        }

        private void PushChunkToStreams(int length)
        {
            lock (_splitStreams)
            {
                if (length > 0)
                {
                    foreach (var stream in _splitStreams)
                    {
                        stream.PushChunk(buffer, length);
                    }
                }
                else
                {
                    foreach (var stream in _splitStreams)
                    {
                        stream.PushChunk(null, 0);
                    }
                }
            }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            PushChunkToStreams(-1);
        }
    }
}