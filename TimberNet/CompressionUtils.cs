using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Text;

namespace TimberNet
{
    public static class CompressionUtils
    {
        // Messages are a few hundred bytes to a few kilobytes; Stream.CopyTo's own buffer (80 KB) was made afresh for
        // each one, on the receive thread, ten times a second per player for cursor frames alone.
        private const int CopyBufferSize = 8192;

        public static byte[] Compress(string text)
        {
            return Compress(Encoding.UTF8.GetBytes(text));
        }

        /// <summary>Compresses text already encoded as UTF-8, for a sender that also hashes those same bytes.</summary>
        public static byte[] Compress(byte[] utf8)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                {
                    gzip.Write(utf8, 0, utf8.Length);
                }
                return output.ToArray();
            }
        }

        /// <summary>
        /// Decompresses data from another player, refusing to produce more than maxBytes, so a tiny
        /// crafted payload cannot expand into something huge.
        /// </summary>
        public static string Decompress(byte[] compressedData, int maxBytes)
        {
            using (var input = new MemoryStream(compressedData))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[CopyBufferSize];
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > maxBytes) throw new IOException("Compressed message is too large.");
                    output.Write(buffer, 0, read);
                }
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }

        public static string Decompress(byte[] compressedData)
        {
            return Encoding.UTF8.GetString(DecompressToBytes(compressedData));
        }

        /// <summary>The message's UTF-8 bytes, exactly as the sender encoded them.</summary>
        public static byte[] DecompressToBytes(byte[] compressedData)
        {
            using (var input = new MemoryStream(compressedData))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output, CopyBufferSize);
                return output.ToArray();
            }
        }
    }
}
