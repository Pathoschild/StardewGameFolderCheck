using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using ApiChange.Api.Introspection;

namespace StardewGameFolderCheck.Models
{
    /// <summary>The metadata for a file.</summary>
    internal class FileData
    {
        /*********
        ** Accessors
        *********/
        /// <summary>The processor architecture, if applicable.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ProcessorArchitecture? Architecture { get; }

        /// <summary>The assembly file version, if applicable.</summary>
        public string? AssemblyVersion { get; }

        /// <summary>The MD5 file hash.</summary>
        public string Hash { get; }


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="file">The raw file data.</param>
        public FileData(FileInfo file)
        {
            CorFlagsReader? assemblyData = CorFlagsReader.ReadAssemblyMetadata(file.FullName);

            this.Architecture = assemblyData?.ProcessorArchitecture;
            this.AssemblyVersion = this.GetAssemblyVersion(file)?.ToString(4);
            this.Hash = FileData.CalculateHash(file);
        }

        /// <summary>Construct an instance.</summary>
        /// <param name="architecture">The processor architecture, if applicable.</param>
        /// <param name="assemblyVersion">The assembly file version, if applicable.</param>
        /// <param name="hash">The MD5 file hash.</param>
        [JsonConstructor]
        public FileData(ProcessorArchitecture? architecture, string? assemblyVersion, string hash)
        {
            this.Architecture = architecture;
            this.AssemblyVersion = assemblyVersion;
            this.Hash = hash;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get the assembly file version, if available.</summary>
        /// <param name="file">The file version.</param>
        private Version? GetAssemblyVersion(FileInfo file)
        {
            try
            {
                return AssemblyName.GetAssemblyName(file.FullName).Version;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Get an MD5 checksum for a file.</summary>
        /// <param name="file">The file to check.</param>
        private static string CalculateHash(FileInfo file)
        {
            using MD5 md5 = MD5.Create();
            using FileStream stream = file.OpenRead();
            byte[] hash = md5.ComputeHash(stream);

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
