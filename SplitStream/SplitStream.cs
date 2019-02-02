using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BoundlessProxyUi.SplitStream
{
    public class SplitStream
    {
        private readonly Stream _sourceStream;
        private readonly List<SplitStreamReader> _splitStreams = new List<SplitStreamReader>();

        private volatile bool _finished = false;
        private volatile bool _started = false;

        private byte[] buffer;

        public SplitStream(Stream stream, int chunkSize = ushort.MaxValue)
        {
            _sourceStream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "> 0");
            buffer = new byte[chunkSize];
        }

        public Stream GetReader()
        {
            if (_started) throw new InvalidOperationException("Data stream reading already started!");

            var stream = new SplitStreamReader(_sourceStream, this);
            _splitStreams.Add(stream);
            return stream;
        }

        public void TerminateReader(Stream stream)
        {
            var myStream = stream as SplitStreamReader;

            _splitStreams.Remove(myStream);
            myStream.PushChunk(null, 0);
        }

        public Task StartReadAhead()
        {
            return Task.Run(() => ReadAheadChunks());
        }

        private void ReadAheadChunks()
        {
            try
            {
                _started = true;

                do
                {
                    int length = _sourceStream.Read(buffer, 0, buffer.Length);
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
            catch { }
        }

        private void PushChunkToStreams(int length)
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
}