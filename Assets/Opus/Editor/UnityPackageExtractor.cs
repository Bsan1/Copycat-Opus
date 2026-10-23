using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Opus.Editor
{
    /// <summary>
    /// Unpacks a .unitypackage (a gzipped tar) straight into the project folder. Every entry
    /// consists of the files "&lt;guid&gt;/pathname", "&lt;guid&gt;/asset" and "&lt;guid&gt;/asset.meta";
    /// since the .meta files are written too, the GUIDs and the references between them are kept.
    /// </summary>
    public static class UnityPackageExtractor
    {
        const int BlockSize = 512;

        /// <returns>Number of files written (.meta included).</returns>
        public static int Extract(string packagePath, string projectRoot)
        {
            var entries = new Dictionary<string, Dictionary<string, byte[]>>();
            using (FileStream file = File.OpenRead(packagePath))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                foreach ((string path, byte[] data) in ReadTar(gzip))
                {
                    string[] parts = path.TrimStart('.', '/').Split('/');
                    if (parts.Length != 2 || parts[1].Length == 0)
                        continue;
                    if (!entries.TryGetValue(parts[0], out var files))
                        entries[parts[0]] = files = new Dictionary<string, byte[]>();
                    files[parts[1]] = data;
                }
            }

            int written = 0;
            foreach (Dictionary<string, byte[]> files in entries.Values)
            {
                if (!files.TryGetValue("pathname", out byte[] pathBytes))
                    continue;
                string relative = Encoding.UTF8.GetString(pathBytes).Split('\n')[0].Trim();
                if (!relative.StartsWith("Assets/", StringComparison.Ordinal) || relative.Contains(".."))
                    continue;

                string target = Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (files.TryGetValue("asset", out byte[] asset))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, asset);
                    written++;
                }
                else
                {
                    Directory.CreateDirectory(target);
                }

                if (files.TryGetValue("asset.meta", out byte[] meta))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target + ".meta", meta);
                    written++;
                }
            }
            return written;
        }

        static IEnumerable<(string path, byte[] data)> ReadTar(Stream stream)
        {
            var header = new byte[BlockSize];
            string paxPath = null;
            while (ReadExactly(stream, header, BlockSize))
            {
                if (IsZeroBlock(header))
                    yield break;

                string name = ReadField(header, 0, 100);
                string prefix = ReadField(header, 345, 155);
                string sizeField = ReadField(header, 124, 12).Trim();
                long size = sizeField.Length == 0 ? 0 : Convert.ToInt64(sizeField, 8);
                char type = (char)header[156];

                var data = new byte[size];
                if (!ReadExactly(stream, data, size))
                    throw new EndOfStreamException("The package ended earlier than expected.");
                long padding = (BlockSize - size % BlockSize) % BlockSize;
                if (padding > 0)
                    ReadExactly(stream, new byte[padding], padding);

                switch (type)
                {
                    case 'x': // PAX extended header: can carry the long name of the next entry
                        paxPath = ParsePaxPath(data);
                        continue;
                    case 'g': // global PAX header
                        continue;
                    case '0':
                    case '\0':
                        string path = paxPath ?? (prefix.Length > 0 ? prefix + "/" + name : name);
                        paxPath = null;
                        yield return (path, data);
                        break;
                    default: // folder etc.
                        paxPath = null;
                        break;
                }
            }
        }

        static string ParsePaxPath(byte[] data)
        {
            // Lines: "<length> <key>=<value>\n"
            foreach (string line in Encoding.UTF8.GetString(data).Split('\n'))
            {
                int space = line.IndexOf(' ');
                if (space < 0) continue;
                string record = line.Substring(space + 1);
                if (record.StartsWith("path=", StringComparison.Ordinal))
                    return record.Substring(5);
            }
            return null;
        }

        static string ReadField(byte[] block, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && block[end] != 0) end++;
            return Encoding.ASCII.GetString(block, offset, end - offset);
        }

        static bool IsZeroBlock(byte[] block)
        {
            foreach (byte b in block)
                if (b != 0) return false;
            return true;
        }

        static bool ReadExactly(Stream stream, byte[] buffer, long count)
        {
            long total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, (int)total, (int)(count - total));
                if (read <= 0) return false;
                total += read;
            }
            return true;
        }
    }
}
