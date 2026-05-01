using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorzCore.IO
{
    internal class Rom
    {
        private readonly byte[] _myData;
        private readonly BufferedStream _myStream;
        private int _size;
        // Tracks every byte position written via WriteTo so WriteRom only flushes
        // the actually-modified subset back to the underlying stream. This keeps
        // SparseRomBuffer manifests small and is functionally equivalent for
        // the original full-ROM stream pipeline.
        private readonly HashSet<int> _writtenPositions = new HashSet<int>();

        public Rom(Stream myRom)
        {
            _myStream = new BufferedStream(myRom);
            _myData = new byte[0x2000000];
            _size = _myStream.Read(_myData, 0, 0x2000000);
            _myStream.Position = 0;
        }

        public void WriteRom()
        {
            if (_writtenPositions.Count == 0)
            {
                _myStream.Flush();
                return;
            }

            var sorted = _writtenPositions.OrderBy(p => p).ToList();
            int runStart = sorted[0];
            int runEnd = runStart;
            for (int i = 1; i < sorted.Count; i++)
            {
                int pos = sorted[i];
                if (pos == runEnd + 1)
                {
                    runEnd = pos;
                }
                else
                {
                    _myStream.Position = runStart;
                    _myStream.Write(_myData, runStart, runEnd - runStart + 1);
                    runStart = pos;
                    runEnd = pos;
                }
            }
            _myStream.Position = runStart;
            _myStream.Write(_myData, runStart, runEnd - runStart + 1);
            _myStream.Flush();
        }

        public void WriteTo(int position, byte[] data)
        {
            Array.Copy(data, 0, _myData, position, data.Length);
            for (int i = 0; i < data.Length; i++)
                _writtenPositions.Add(position + i);
            if (data.Length + position > _size)
                _size = data.Length + position;
        }
    }
}
