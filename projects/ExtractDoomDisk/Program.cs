/* Copyright (c) 2019 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Gibbed.IO;
using NDesk.Options;

namespace ExtractDoomDisk
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        public static void Main(string[] args)
        {
            bool showHelp = false;
            bool overwriteFiles = false;
            bool verbose = false;

            var options = new OptionSet()
            {
                { "o|overwrite", "overwrite existing files", v => overwriteFiles = v != null },
                { "v|verbose", "be verbose", v => verbose = v != null },
                { "h|help", "show this message and exit", v => showHelp = v != null },
            };

            List<string> extras;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            if (extras.Count < 1 || extras.Count > 2 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ input_disk [output_dir]", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            var inputPath = Path.GetFullPath(extras[0]);
            var outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, null) + "_unpack";

            using (var input = File.OpenRead(inputPath))
            {
                const Endian endian = Endian.Big;

                var entryCount = input.ReadValueU32(endian);
                var entries = new DiskEntry[entryCount];
                for (uint i = 0; i < entryCount; i++)
                {
                    entries[i] = DiskEntry.Read(input, endian);
                }
                var totalDataSize = input.ReadValueU32(endian);
                var baseDataPosition = input.Position;

                long current = 0;
                long total = entries.Length;
                var padding = total.ToString(CultureInfo.InvariantCulture).Length;

                var directorySeparator = Path.DirectorySeparatorChar.ToString();
                foreach (var entry in entries)
                {
                    current++;

                    var entryPath = entry.Path;
                    entryPath = entryPath.Replace('\\', Path.DirectorySeparatorChar);

                    var rootIndex = entryPath.IndexOf(':');
                    if (rootIndex >= 0)
                    {
                        var rootPath = entryPath.Substring(0, rootIndex);
                        var relativePath = entryPath.Substring(rootIndex + 1);

                        if (relativePath.StartsWith(directorySeparator) == true)
                        {
                            relativePath = relativePath.Substring(directorySeparator.Length);
                        }

                        rootIndex = relativePath.IndexOf(':');
                        if (rootIndex >= 0)
                        {
                            throw new InvalidOperationException();
                        }

                        entryPath = Path.Combine($"[{rootPath}]", relativePath);
                    }

                    if (entryPath.StartsWith(directorySeparator) == true)
                    {
                        entryPath = entryPath.Substring(directorySeparator.Length);
                    }

                    entryPath = Path.Combine(outputPath, entryPath);
                    if (overwriteFiles == false && File.Exists(entryPath) == true)
                    {
                        continue;
                    }

                    if (verbose == true)
                    {
                        Console.WriteLine(
                            "[{0}/{1}] {2}",
                            current.ToString(CultureInfo.InvariantCulture).PadLeft(padding),
                            total,
                            entry.Path);
                    }

                    var entryDirectory = Path.GetDirectoryName(entryPath);
                    if (entryDirectory != null)
                    {
                        Directory.CreateDirectory(entryDirectory);
                    }

                    using (var output = File.Create(entryPath))
                    {
                        input.Seek(baseDataPosition + entry.DataOffset, SeekOrigin.Begin);
                        output.WriteFromStream(input, (int)entry.DataSize);
                    }
                }
            }
        }

        private struct DiskEntry
        {
            public string Path;
            public uint DataOffset;
            public uint DataSize;

            public static DiskEntry Read(Stream input, Endian endian)
            {
                DiskEntry instance;
                instance.Path = input.ReadString(64, true, Encoding.ASCII);
                instance.DataOffset = input.ReadValueU32(endian);
                instance.DataSize = input.ReadValueU32(endian);
                return instance;
            }
        }
    }
}
