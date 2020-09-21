using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace BoundlessProxyUi.SplitStream
{
    class SplitStreamReader : Stream
    {
        public SplitStreamReader(Stream writeBackStream, SplitStream parent)
        {
            WriteBackStream = writeBackStream;
            Parent = parent;
        }

        public Stream WriteBackStream { get; }

        public SplitStream Parent { get; }

        public bool IsMaster { get; set; }

        private readonly ConcurrentQueue<StreamChunk> _queueChunks = new ConcurrentQueue<StreamChunk>();
        private readonly ManualResetEventSlim _queueWaiter = new ManualResetEventSlim(false, 10);

        private readonly ConcurrentQueue<StreamChunk> chunkStore = new ConcurrentQueue<StreamChunk>();

        private StreamChunk _chunk = null;
        private int _position = 0;

        public int GetBufferAvailable()
        {
            if (IsFinised) return 0;

            if (_chunk == null || _position == _chunk.Length)
            {
                if (_queueChunks.IsEmpty)
                {
                    if (IsDirect)
                    {
                        throw new Exception("not supported for direct");
                    }

                    return 0;
                }

                _queueChunks.TryDequeue(out var peekResult);
                return peekResult?.Length ?? 0;
            }

            return _chunk.Length - _position;
        }
        
        #region Stream overrides

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (IsFinised) return 0;

            if (_chunk == null || _position == _chunk.Length)
            {
                if (IsDirect && _queueChunks.IsEmpty)
                {
                    try
                    {
                        return WriteBackStream.Read(buffer, offset, count);
                    }
                    catch
                    {
                        return 0;
                    }
                }

                _chunk = PopChunk(CancellationToken.None);
                if (_chunk == null || _chunk.Length == 0)
                {
                    if (IsDirect)
                    {
                        try
                        {
                            return WriteBackStream.Read(buffer, offset, count);
                        }
                        catch
                        {
                            return 0;
                        }
                    }

                    IsFinised = true;
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

            Buffer.BlockCopy(buffer, 0, chunk.Buffer, 0, length);

            chunk.Length = length;

            _queueChunks.Enqueue(chunk);
            _queueWaiter.Set();
        }

        public StreamChunk PopChunk(CancellationToken cancellationToken)
        {
            if (IsDirect && _queueChunks.IsEmpty)
            {
                return null;
            }

            StreamChunk chunk;
            while (_queueChunks.TryDequeue(out chunk) == false)
            {
                _queueWaiter.Wait(cancellationToken);

                if (IsDirect && _queueChunks.IsEmpty)
                {
                    return null;
                }
            }
            _queueWaiter.Reset();

            if (_queueChunks.IsEmpty && IsDirect)
            {
                _queueWaiter.Set();
            }

            return chunk;
        }

        bool IsDirect = false;

        public void DoDirect()
        {
            IsDirect = true;
            _queueWaiter.Set();
        }

        public override void Flush()
        {
            try
            {
                WriteBackStream?.Flush();
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
                WriteBackStream?.Write(buffer, offset, count);
            }
            catch
            {
                Parent.TerminateReader(this);
            }
        }

        public override void Close()
        {
            Parent.TerminateReader(this);
            PushChunk(null, 0);
            if (IsMaster)
            {
                WriteBackStream?.Flush();
                WriteBackStream?.Close();
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => -1;

        public override long Position { get; set; }

        #endregion

        #region Disposable overrides

        public bool IsFinised { get; private set; } = false;

        protected override void Dispose(bool disposing)
        {
            if (disposing && IsFinised == false)
            {
                Close();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}