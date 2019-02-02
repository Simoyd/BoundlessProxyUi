using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace BoundlessProxyUi.SplitStream
{
    public class SplitStreamReader : Stream
    {
        private readonly ConcurrentQueue<StreamChunk> _queueChunks = new ConcurrentQueue<StreamChunk>();
        private readonly ManualResetEventSlim _queueWaiter = new ManualResetEventSlim(false, 10);

        private readonly ConcurrentQueue<StreamChunk> chunkStore = new ConcurrentQueue<StreamChunk>();

        private StreamChunk _chunk = null;
        private bool _finished = false;
        private int _position = 0;

        public Stream WriteBackStream { get; }
        public SplitStream Parent { get; }

        public SplitStreamReader(Stream writeBackStream, SplitStream parent)
        {
            WriteBackStream = writeBackStream;
            Parent = parent;
        }

        #region Overrides of Stream

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_finished) return 0;

            if (_chunk == null || _position == _chunk.Length)
            {
                _chunk = PopChunk(CancellationToken.None);
                if (_chunk == null || _chunk.Length == 0)
                {
                    _finished = true;
                    return 0;
                }
                _position = 0;
            }

            var bufferAvailable = _chunk.Length - _position;
            if (bufferAvailable >= count)
            {
                Array.Copy(_chunk.Buffer, _position, buffer, offset, count);
                _position += count;
            }
            else
            {
                count = bufferAvailable;

                Array.Copy(_chunk.Buffer, _position, buffer, offset, count);
                _position = 0;

                chunkStore.Enqueue(_chunk);
                _chunk = null;
            }

            return count;
        }

        public void PushChunk(byte[] buffer, int length)
        {
            if (buffer == null)
            {
                _queueChunks.Enqueue(null);
                _queueWaiter.Set();
                return;
            }

            if (!chunkStore.TryDequeue(out StreamChunk chunk))
            {
                chunk = new StreamChunk(new byte[buffer.Length]);
            }

            Array.Copy(buffer, 0, chunk.Buffer, 0, length);
            chunk.Length = length;

            _queueChunks.Enqueue(chunk);
            _queueWaiter.Set();
        }

        public StreamChunk PopChunk(CancellationToken cancellationToken)
        {
            StreamChunk chunk;
            while (_queueChunks.TryDequeue(out chunk) == false)
            {
                _queueWaiter.Wait(cancellationToken);
            }
            _queueWaiter.Reset();
            return chunk;
        }

        public override void Flush()
        {
            try
            {
                WriteBackStream.Flush();
            }
            catch
            {
                Parent.TerminateReader(this);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                WriteBackStream.Write(buffer, offset, count);
            }
            catch
            {
                Parent.TerminateReader(this);
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => -1;
        public override long Position { get; set; }

        public bool IsFinised => _finished;

        #endregion

        #region Overrides of Stream

        protected override void Dispose(bool disposing)
        {
            if (disposing && IsFinised == false)
            {
                PushChunk(null, 0);
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}