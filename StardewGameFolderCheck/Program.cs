using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewGameFolderCheck.Models;
using StardewModdingAPI.Toolkit;
using StardewModdingAPI.Toolkit.Framework.GameScanning;
using StardewModdingAPI.Toolkit.Utilities;

namespace StardewGameFolderCheck
{
    /// <summary>The mod entry class.</summary>
    public class Program
    {
        /*********
        ** Fields
        *********/
        /// <summary>The SMAPI toolkit utility for finding and analyzing installed game folders.</summary>
        private readonly GameScanner GameScanner = new();


        /*********
        ** Public methods
        *********/
        /// <summary>Launch the app.</summary>
        /// <param name="args">The command-line arguments.</param>
        static void Main(string[] args)
        {
            new Program().Test(args);
        }

        /// <summary>Interactively run the game folder tests.</summary>
        /// <param name="args">The command-line arguments.</param>
        public void Test(string[] args)
        {
            try
            {
                /*********
                ** Collect info
                *********/
                Platform platform = EnvironmentUtility.DetectPlatform();
                DirectoryInfo gameDir = this.InteractivelyGetInstallPath(new ModToolkit(), specifiedPath: args.FirstOrDefault(), platform);

                string? rawAppDir = this.GetExecutablePath();
                if (rawAppDir == null)
                    throw new InvalidOperationException("Can't find the app tool path.");

                DirectoryInfo appDir = new(rawAppDir);
                DataModel appData = this.ReadData(appDir);
                Dictionary<string, FileData> actualFiles = this.GetFilesToCheck(gameDir, appData.IgnoreRelativePaths);


                /*********
                ** Show system info
                *********/
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("System info");
                Console.WriteLine("-------------------------------------------------");
                Console.WriteLine($"Platform: {EnvironmentUtility.GetFriendlyPlatformName(platform)}");
                Console.WriteLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
                Console.WriteLine($"64-bit process: {Environment.Is64BitProcess}");
                Console.WriteLine($"Processor architecture: {Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")}");
                Console.WriteLine($"PATH value: {Environment.GetEnvironmentVariable("Path")}");
                Console.WriteLine();


                /*********
                ** Test A: 32-bit files
                *********/
                Console.WriteLine("32-bit files");
                Console.WriteLine("-------------------------------------------------");
                {
                    var matches = actualFiles
                        .Where(p => p.Value.Architecture is not (null or ProcessorArchitecture.None or ProcessorArchitecture.MSIL or ProcessorArchitecture.Amd64))
                        .Select(p => new { RelativePath = p.Key, Data = p.Value })
                        .ToArray();

                    if (matches.Any())
                    {
                        this.PrintError($"Found {matches.Length} files which are 32-bit. Is this Stardew Valley 1.5.4 or earlier?");
                        {
                            int nameColWidth = matches.Max(p => p.RelativePath.Length);
                            int archColWidth = "Architecture".Length;

                            this.PrintError("    " + "Name".PadRight(nameColWidth, ' ') + " | Architecture");
                            this.PrintError("    " + "----".PadRight(nameColWidth, '-') + " | ------------");

                            foreach (var match in matches)
                            {
                                this.PrintError($"    {match.RelativePath.PadRight(nameColWidth)} | {match.Data.Architecture?.ToString().PadRight(archColWidth)}");
                            }
                        }
                    }
                    else
                    {
                        this.PrintSuccess("No 32-bit files found.");
                    }
                }
                Console.WriteLine();


                /*********
                ** Test B: missing / unexpected files
                *********/
                Console.WriteLine("File integrity");
                Console.WriteLine("-------------------------------------------------");
                {
                    var issues = new Dictionary<string, List<string>>();
                    void TrackIssue(string relativePath, string issue)
                    {
                        if (!issues.TryGetValue(relativePath, out List<string>? curIssues))
                            issues[relativePath] = curIssues = new();

                        curIssues.Add(issue);
                    }

                    IDictionary<string, FileData> expectedFiles = appData.ExpectedFiles;

                    // missing files
                    foreach ((string relativePath, _) in expectedFiles.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!actualFiles.ContainsKey(relativePath))
                            TrackIssue(relativePath, "missing file");
                    }

                    // current files
                    foreach ((string relativePath, FileData entry) in actualFiles.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        // unexpected file
                        if (!expectedFiles.TryGetValue(relativePath, out FileData? expected))
                        {
                            TrackIssue(relativePath, "unexpected file");
                            continue;
                        }

                        // wrong architecture
                        if (entry.Architecture != expected.Architecture)
                            TrackIssue(relativePath, $"wrong processor architecture (found {entry.Architecture?.ToString() ?? "null"}, expected {expected.Architecture?.ToString() ?? "null"})");

                        // wrong version
                        if (entry.AssemblyVersion != expected.AssemblyVersion)
                            TrackIssue(relativePath, $"wrong version (found {entry.AssemblyVersion ?? "null"}, expected {expected.AssemblyVersion ?? "null"})");

                        // wrong hash
                        if (entry.Hash != expected.Hash)
                            TrackIssue(relativePath, $"modified file (file hash is {entry.Hash}, expected {expected.Hash})");
                    }

                    // show result
                    if (issues.Any())
                    {
                        int nameLength = issues.Keys.Max(p => p.Length);
                        int issueLength = issues.Values.Max(p => string.Join("; ", p).Length);

                        this.PrintError(string.Concat("   ", "file".PadRight(nameLength), " | ", "issues".PadRight(issueLength)));
                        this.PrintError(string.Concat("   ", "".PadRight(nameLength, '-'), " | ", "".PadRight(issueLength, '-')));

                        foreach ((string relativePath, List<string> curIssues) in issues.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                            this.PrintError(string.Concat("   ", relativePath.PadRight(nameLength), " | ", string.Join("; ", curIssues).PadRight(issueLength)));
                    }
                    else
                        this.PrintSuccess("No file issues detected.");
                }
                Console.WriteLine();


                /*********
                ** Dump current files
                *********/
                {
                    string dumpPath = Path.Combine(appDir.FullName, "actual-files.json");
                    var dumpModel = new DataModel(ignoreRelativePaths: appData.IgnoreRelativePaths, expectedFiles: actualFiles);

                    File.WriteAllText(dumpPath, JsonSerializer.Serialize(dumpModel, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault }));
                }
            }
            catch (Exception ex)
            {
                this.PrintError($"Unhandled exception:\n{ex}");
            }

            Console.WriteLine();
            Console.WriteLine("You can press enter to exit.");
            Console.ReadLine();
        }


        /*********
        ** Private methods
        *********/
        private void PrintInfo(string message)
        {
            Console.WriteLine(message);
        }

        private void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private void PrintSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>Read the data file from the app directory.</summary>
        /// <param name="appDir">The app directory.</param>
        private DataModel ReadData(DirectoryInfo appDir)
        {
            FileInfo dataFile = new(Path.Combine(appDir.FullName, "expected-files.json"));
            if (!dataFile.Exists)
                throw new FileNotFoundException($"Can't find required file at {dataFile.FullName}.");

            using Stream readStream = dataFile.OpenRead();
            DataModel? model = JsonSerializer.Deserialize<DataModel>(readStream, new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });
            if (model == null)
                throw new InvalidOperationException($"Can't read data from file at {dataFile.FullName}.");

            return model;
        }

        /// <summary>Get the actual game files to validate.</summary>
        /// <param name="gameDir">The game folder.</param>
        /// <param name="ignorePaths">The paths that should be ignored when scanning the game folder.</param>
        private Dictionary<string, FileData> GetFilesToCheck(DirectoryInfo gameDir, HashSet<string> ignorePaths)
        {
            return gameDir
                .EnumerateFiles()
                .Concat(gameDir.EnumerateFiles("smapi-internal/*", SearchOption.AllDirectories))
                .Concat(gameDir.EnumerateFiles("Content/*", SearchOption.AllDirectories))
                .Select(file => new { Data = new FileData(file), Path = Path.GetRelativePath(gameDir.FullName, file.FullName) })
                .Where(file => !ignorePaths.Contains(file.Path))
                .ToDictionary(p => p.Path, p => p.Data);
        }


        /*********
        ** Derived from SMAPI installer
        *********/
        /// <summary>Interactively locate the game install path to update.</summary>
        /// <param name="toolkit">The mod toolkit.</param>
        /// <param name="specifiedPath">The path specified as a command-line argument (if any), which should override automatic path detection.</param>
        /// <param name="platform">The current OS.</param>
        private DirectoryInfo InteractivelyGetInstallPath(ModToolkit toolkit, string? specifiedPath, Platform platform)
        {
            // try specified path
            if (!string.IsNullOrWhiteSpace(specifiedPath))
            {
                var dir = new DirectoryInfo(specifiedPath);
                if (!dir.Exists)
                {
                    this.PrintError("That folder doesn't exist.");
                }
                else
                {
                    switch (this.GameScanner.GetGameFolderType(dir))
                    {
                        case GameFolderType.Valid:
                            return dir;

                        case GameFolderType.Legacy154OrEarlier:
                            this.PrintError("That directory seems to have Stardew Valley 1.5.4 or earlier.");
                            this.PrintError("Please update your game to the latest version to use SMAPI.");
                            break;

                        case GameFolderType.LegacyCompatibilityBranch:
                            this.PrintError("That directory seems to have the Stardew Valley legacy 'compatibility' branch.");
                            this.PrintError("Unfortunately SMAPI is only compatible with the modern version of the game.");
                            this.PrintError("Please update your game to the main branch to use SMAPI.");
                            break;

                        case GameFolderType.NoGameFound:
                            this.PrintError("That directory doesn't contain a Stardew Valley executable.");
                            break;

                        default:
                            this.PrintError("That directory doesn't seem to contain a valid game install.");
                            break;
                    }
                }
            }

            // let user choose detected path
            DirectoryInfo[] defaultPaths = this.DetectGameFolders(toolkit).ToArray();
            if (defaultPaths.Any())
            {
                this.PrintInfo("Which Stardew Valley folder you want to test?");
                Console.WriteLine();
                for (int i = 0; i < defaultPaths.Length; i++)
                    this.PrintInfo($"[{i + 1}] {defaultPaths[i].FullName}");
                this.PrintInfo($"[{defaultPaths.Length + 1}] Enter a custom game path.");
                Console.WriteLine();

                string[] validOptions = Enumerable.Range(1, defaultPaths.Length + 1).Select(p => p.ToString(CultureInfo.InvariantCulture)).ToArray();
                string choice = this.InteractivelyChoose("Type the number next to your choice, then press enter.", validOptions);
                int index = int.Parse(choice, CultureInfo.InvariantCulture) - 1;

                if (index < defaultPaths.Length)
                    return defaultPaths[index];
            }
            else
                this.PrintInfo("Oops, couldn't find the game automatically.");

            // let user enter manual path
            while (true)
            {
                // get path from user
                Console.WriteLine();
                this.PrintInfo("Type the file path to the game directory (the one containing 'Stardew Valley.exe' or 'Stardew Valley.dll'), then press enter.");
                string? path = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    this.PrintError("You must specify a directory path to continue.");
                    continue;
                }

                // normalize path
                path = platform == Platform.Windows
                    ? path.Replace("\"", "") // in Windows, quotes are used to escape spaces and aren't part of the file path
                    : path.Replace("\\ ", " "); // in Linux/macOS, spaces in paths may be escaped if copied from the command line
                if (path.StartsWith("~/"))
                {
                    string home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetEnvironmentVariable("USERPROFILE")!;
                    path = Path.Combine(home, path.Substring(2));
                }

                // get directory
                if (File.Exists(path))
                    path = Path.GetDirectoryName(path)!;
                DirectoryInfo directory = new DirectoryInfo(path);

                // validate path
                if (!directory.Exists)
                {
                    this.PrintError("That directory doesn't seem to exist.");
                    continue;
                }

                switch (this.GameScanner.GetGameFolderType(directory))
                {
                    case GameFolderType.Valid:
                        this.PrintInfo("   OK!");
                        return directory;

                    case GameFolderType.Legacy154OrEarlier:
                        this.PrintError("That directory seems to have Stardew Valley 1.5.4 or earlier.");
                        this.PrintError("Please update your game to the latest version to use SMAPI.");
                        continue;

                    case GameFolderType.LegacyCompatibilityBranch:
                        this.PrintError("That directory seems to have the Stardew Valley legacy 'compatibility' branch.");
                        this.PrintError("Unfortunately SMAPI is only compatible with the modern version of the game.");
                        this.PrintError("Please update your game to the main branch to use SMAPI.");
                        continue;

                    case GameFolderType.NoGameFound:
                        this.PrintError("That directory doesn't contain a Stardew Valley executable.");
                        continue;

                    default:
                        this.PrintError("That directory doesn't seem to contain a valid game install.");
                        continue;
                }
            }
        }

        /// <summary>Get the possible game paths to update.</summary>
        /// <param name="toolkit">The mod toolkit.</param>
        private IEnumerable<DirectoryInfo> DetectGameFolders(ModToolkit toolkit)
        {
            HashSet<string> foundPaths = new HashSet<string>();

            // game folder which contains the installer, if any
            {
                DirectoryInfo curPath = new FileInfo(Environment.ProcessPath!).Directory!;
                while (curPath?.Parent != null) // must be in a folder (not at the root)
                {
                    if (this.GameScanner.LooksLikeGameFolder(curPath))
                    {
                        foundPaths.Add(curPath.FullName);
                        yield return curPath;
                        break;
                    }

                    curPath = curPath.Parent;
                }
            }

            // game paths detected by toolkit
            foreach (DirectoryInfo dir in toolkit.GetGameFolders())
            {
                if (foundPaths.Add(dir.FullName))
                    yield return dir;
            }
        }

        /// <summary>Interactively ask the user to choose a value.</summary>
        /// <param name="print">A callback which prints a message to the console.</param>
        /// <param name="message">The message to print.</param>
        /// <param name="options">The allowed options (not case sensitive).</param>
        /// <param name="indent">The indentation to prefix to output.</param>
        private string InteractivelyChoose(string message, string[] options, string indent = "", Action<string>? print = null)
        {
            print ??= this.PrintInfo;

            while (true)
            {
                print(indent + message);
                Console.Write(indent);
                string? input = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(input) || !options.Contains(input))
                {
                    print($"{indent}That's not a valid option.");
                    continue;
                }
                return input;
            }
        }

        /// <summary>Get the full path to the executing assembly.</summary>
        /// <remarks>This is a hack from <a href="https://github.com/dotnet/runtime/issues/13051#issuecomment-514774802" />.</remarks>
        private string? GetExecutablePath()
        {
            string? exePath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const int maxPathLength = 500;

                StringBuilder pathBuilder = new StringBuilder(maxPathLength);
                GetModuleFileName(IntPtr.Zero, pathBuilder, maxPathLength);
                exePath = pathBuilder.ToString();
            }
            else
                exePath = Process.GetCurrentProcess().MainModule?.FileName;

            return exePath != null
                ? Path.GetDirectoryName(exePath)
                : null;
        }

        [DllImport("kernel32.dll")]
        static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);
    }
}
