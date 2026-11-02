#region license

// Copyright (c) 2024, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using ClassicUO.IO;
using ClassicUO.Utility;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;

namespace ClassicUO.Assets
{
    public class GumpsLoader : UOFileLoader
    {
        private static GumpsLoader _instance;
        private UOFile _file;

        public const int MAX_GUMP_DATA_INDEX_COUNT = 0x10000;

        private GumpsLoader(int count) { }

        public static GumpsLoader Instance =>
            _instance ?? (_instance = new GumpsLoader(MAX_GUMP_DATA_INDEX_COUNT));

        public bool UseUOPGumps = false;

        public override Task Load()
        {
            return Task.Run(() =>
            {
                string path = UOFileManager.GetUOFilePath("gumpartLegacyMUL.uop");

                if (UOFileManager.IsUOPInstallation && File.Exists(path))
                {
                    _file = new UOFileUop(path, "build/gumpartlegacymul/{0:D8}.tga", true);
                    Entries = new UOFileIndex[
                        Math.Max(((UOFileUop)_file).TotalEntriesCount, MAX_GUMP_DATA_INDEX_COUNT + 0x4000)
                    ];
                    UseUOPGumps = true;
                }
                else
                {
                    path = UOFileManager.GetUOFilePath("gumpart.mul");
                    string pathidx = UOFileManager.GetUOFilePath("gumpidx.mul");

                    if (!File.Exists(path))
                    {
                        path = UOFileManager.GetUOFilePath("Gumpart.mul");
                    }

                    if (!File.Exists(pathidx))
                    {
                        pathidx = UOFileManager.GetUOFilePath("Gumpidx.mul");
                    }

                    _file = new UOFileMul(path, pathidx);

                    UseUOPGumps = false;
                }

                _file.FillEntries(ref Entries);

                string pathdef = UOFileManager.GetUOFilePath("gump.def");

                if (!File.Exists(pathdef))
                {
                    return;
                }

                using (DefReader defReader = new DefReader(pathdef, 3))
                {
                    while (defReader.Next())
                    {
                        int ingump = defReader.ReadInt();

                        if (
                            ingump < 0
                            || ingump >= MAX_GUMP_DATA_INDEX_COUNT
                            || ingump >= Entries.Length
                            || Entries[ingump].Length > 0
                        )
                        {
                            continue;
                        }

                        int[] group = defReader.ReadGroup();

                        if (group == null)
                        {
                            continue;
                        }

                        for (int i = 0; i < group.Length; i++)
                        {
                            int checkIndex = group[i];

                            if (
                                checkIndex < 0
                                || checkIndex >= MAX_GUMP_DATA_INDEX_COUNT
                                || checkIndex >= Entries.Length
                                || Entries[checkIndex].Length <= 0
                            )
                            {
                                continue;
                            }

                            Entries[ingump] = Entries[checkIndex];

                            Entries[ingump].Hue = (ushort)defReader.ReadInt();

                            break;
                        }
                    }
                }
            });
        }

        public unsafe GumpInfo GetGump(uint index)
        {
            ref UOFileIndex entry = ref GetValidRefEntry((int)index);

            if (_file == null || entry.Address == IntPtr.Zero || entry.Length <= 0
                || entry.Offset < 0 || entry.Offset > entry.FileSize
                || entry.Length > entry.FileSize - entry.Offset
                || (entry.Width <= 0 && entry.Height <= 0))
            {
                return default;
            }

            ushort color = entry.Hue;

            _file.SetData(entry.Address, entry.FileSize);
            _file.Seek(entry.Offset);


            ReadOnlySpan<byte> output;
            var newFileFormat = UOFileManager.Version >= ClientVersion.CV_7010400;
            if (newFileFormat)
            {
                if (entry.DecompressedLength < 5)
                    return default;
                var cbuf = _file.ReadArray(entry.Length);
                var dbuf = new byte[entry.DecompressedLength];
                var result = ZLib.Decompress(cbuf, 0, dbuf, dbuf.Length);
                if (result != ZLib.ZLibError.Okay)
                {
                    return default;
                }

                output = BwtDecompress.Decompress(dbuf);
            }
            else
            {
                output = new ReadOnlySpan<byte>(_file.PositionAddress.ToPointer(), entry.Length);
            }

            var reader = new StackDataReader(output);
            var w = newFileFormat ? reader.ReadUInt32LE() : (uint)entry.Width;
            var h = newFileFormat ? reader.ReadUInt32LE() : (uint)entry.Height;

            if (!TryDecodeGump(output.Slice(reader.Position), w, h, color, out uint[] pixels))
                return default;

            return new GumpInfo { Pixels = pixels, Width = (int)w, Height = (int)h };
        }

        // The decoded UOP buffer can move during GC. Span-based reads keep the
        // buffer tracked and validate offsets before accessing row data.
        internal static bool TryDecodeGump(ReadOnlySpan<byte> data, uint width, uint height, ushort color, out uint[] pixels)
        {
            pixels = null;
            if (width == 0 || height == 0 || width > 4096 || height > 4096
                || height > (uint)(data.Length / 4))
                return false;

            int rowCount = (int)height;
            int rowWidth = (int)width;
            int blockCount = data.Length / 4;
            var decoded = new uint[rowWidth * rowCount];
            for (int y = 0; y < rowCount; y++)
            {
                int start = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(y * 4, 4));
                int end = y + 1 < rowCount
                    ? BinaryPrimitives.ReadInt32LittleEndian(data.Slice((y + 1) * 4, 4))
                    : blockCount;
                if (start < rowCount || end < start || end > blockCount)
                    return false;

                int column = 0;
                for (int i = start; i < end; i++)
                {
                    int offset = i * 4;
                    ushort value = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
                    int count = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset + 2, 2));
                    if (count > rowWidth - column) return false;
                    uint pixel = value;
                    if (color != 0 && pixel != 0)
                        pixel = HuesLoader.Instance.ApplyHueRgba5551(value, color);
                    if (pixel != 0)
                        pixel = HuesHelper.Color16To32(value) | 0xFF_00_00_00;
                    decoded.AsSpan(y * rowWidth + column, count).Fill(pixel);
                    column += count;
                }
            }
            pixels = decoded;
            return true;
        }


    }

    public ref struct GumpInfo
    {
        public Span<uint> Pixels;
        public int Width;
        public int Height;
    }
}
