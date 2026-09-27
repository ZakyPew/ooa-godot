# Portable, dependency-free PNG output for importer-generated graphics.
if ($null -eq ('Epoch.Importing.PortablePngWriter' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Epoch.Importing
{
    public static class PortablePngWriter
    {
        private static readonly uint[] CrcTable = CreateCrcTable();
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static void WriteRgba(string path, int width, int height, byte[] pixels)
        {
            if (width <= 0 || height <= 0 || pixels == null ||
                pixels.LongLength != (long)width * height * 4)
                throw new ArgumentException("Invalid RGBA image dimensions or buffer length.");

            int stride = checked(width * 4);
            using var imageData = new MemoryStream();
            using (var compressor = new ZLibStream(imageData, CompressionLevel.Optimal, true))
            {
                var row = new byte[checked(stride + 1)];
                for (int y = 0; y < height; y++)
                {
                    row[0] = 0; // PNG filter None.
                    Buffer.BlockCopy(pixels, y * stride, row, 1, stride);
                    compressor.Write(row, 0, row.Length);
                }
            }

            using var png = new MemoryStream();
            png.Write(Signature, 0, Signature.Length);
            var header = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)height);
            header[8] = 8; // bit depth
            header[9] = 6; // RGBA
            WriteChunk(png, "IHDR", header);
            WriteChunk(png, "IDAT", imageData.ToArray());
            WriteChunk(png, "IEND", Array.Empty<byte>());
            File.WriteAllBytes(path, png.ToArray());
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
            output.Write(length);
            output.Write(typeBytes, 0, typeBytes.Length);
            output.Write(data, 0, data.Length);

            uint crc = 0xffffffff;
            foreach (byte value in typeBytes) crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 0xff];
            foreach (byte value in data) crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 0xff];
            Span<byte> checksum = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(checksum, ~crc);
            output.Write(checksum);
        }

        private static uint[] CreateCrcTable()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                uint value = index;
                for (int bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xedb88320 ^ (value >> 1) : value >> 1;
                table[index] = value;
            }
            return table;
        }
    }
}
'@
}

function Write-RgbaPng([string]$Path, [int]$Width, [int]$Height, [byte[]]$Pixels) {
    [Epoch.Importing.PortablePngWriter]::WriteRgba($Path, $Width, $Height, $Pixels)
}
