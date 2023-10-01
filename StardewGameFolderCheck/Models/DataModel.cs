using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StardewGameFolderCheck.Models
{
    /// <summary>The data model read from the <c>expected-files.json</c> file.</summary>
    internal class DataModel
    {
        /*********
        ** Accessors
        *********/
        /// <summary>The file paths that should be ignored when scanning the game folder.</summary>
        public HashSet<string> IgnoreRelativePaths { get; }

        /// <summary>The files that should be present in the game folder with their expected metadata.</summary>
        public Dictionary<string, FileData> ExpectedFiles { get; }


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="ignoreRelativePaths">The file paths that should be ignored when scanning the game folder.</param>
        /// <param name="expectedFiles">The files that should be present in the game folder with their expected metadata.</param>
        [JsonConstructor]
        public DataModel(HashSet<string> ignoreRelativePaths, Dictionary<string, FileData> expectedFiles)
        {
            this.IgnoreRelativePaths = ignoreRelativePaths;
            this.ExpectedFiles = expectedFiles;
        }
    }
}
