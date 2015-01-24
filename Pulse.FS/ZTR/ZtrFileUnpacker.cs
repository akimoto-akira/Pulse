﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Pulse.Core;

namespace Pulse.FS
{
    public sealed class ZtrFileUnpacker
    {
        private readonly Stream _input;
        private readonly BinaryReader _br;
        private readonly FFXIIITextEncoding _encoding;

        public ZtrFileUnpacker(Stream input, FFXIIITextEncoding encoding)
        {
            _encoding = encoding;
            _input = input;
            _br = new BinaryReader(_input);
        }

        public ZtrFileEntry[] Unpack()
        {
            if (_input.Length < 5)
                return new ZtrFileEntry[0];

            ZtrFileType type = (ZtrFileType)_br.ReadInt32();
            switch (type)
            {
                case ZtrFileType.LittleEndianUncompressedPair:
                    return ExtractLittleEndianUncompressedPair();
                case ZtrFileType.BigEndianCompressedDictionary:
                    return ExtractBigEndianCompressedDictionary();
                default:
                    return ExtractLittleEndianUncompressedDictionary((int)type);
            }
        }

        private ZtrFileEntry[] ExtractLittleEndianUncompressedDictionary(int count)
        {
            if (count < 0 || count > 10240)
                throw new ArgumentOutOfRangeException("count", count.ToString());

            ZtrFileEntry[] entries = new ZtrFileEntry[count];
            entries.InitializeElements();

            int[] offsets = new int[count * 2];
            for (int i = 0; i < count * 2; i++)
                offsets[i] = _br.ReadInt32();

            for (int i = 0; i < count; i++)
            {
                _input.SetReadPosition(offsets[i * 2]);
                entries[i].Key = ZtrFileHelper.ReadNullTerminatedString(_input, Encoding.ASCII);
                _input.SetReadPosition(offsets[i * 2 + 1]);
                entries[i].Value = ZtrFileHelper.ReadNullTerminatedString(_input, _encoding);
            }

            return entries;
        }

        private ZtrFileEntry[] ExtractLittleEndianUncompressedPair()
        {
            ZtrFileEntry result = new ZtrFileEntry();;

            int keyOffset = _br.ReadInt32();
            int textOffset = _br.ReadInt32();

            _input.SetReadPosition(keyOffset);
            result.Key = ZtrFileHelper.ReadNullTerminatedString(_input, Encoding.ASCII);

            _input.SetReadPosition(textOffset);
            result.Value = ZtrFileHelper.ReadNullTerminatedString(_input, _encoding);

            return new[] {result};
        }

        private ZtrFileEntry[] ExtractBigEndianCompressedDictionary()
        {
            ZtrFileHeader header = new ZtrFileHeader();
            header.ReadFromStream(_input);

            ZtrFileEntry[] result = new ZtrFileEntry[header.Count];
            result.InitializeElements();

            ZtrFileKeysUnpacker keysUnpacker = new ZtrFileKeysUnpacker(_input, result);
            keysUnpacker.Unpack(header.KeysUnpackedSize);

            ZtrFileTextUnpacker textUnpacker = new ZtrFileTextUnpacker(_input, result, header.TextLinesTable, _encoding);
            textUnpacker.Unpack(header.TextBlockTable[header.TextBlockTable.Length - 1]);

            return result;
        }
    }
}