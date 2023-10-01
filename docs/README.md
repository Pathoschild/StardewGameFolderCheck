**StardewGameFolderCheck** is a little tool to scan a Stardew Valley game folder for file integrity
issues (see [example report](screenshot.png)).

This detects common issues [when troubleshooting](https://smapi.io/troubleshoot) like...
* 32-bit OS;
* old game version;
* compatibility branch;
* missing/unexpected/modified game files.

## Contents
* [Usage](#usage)
* [For contributors](#for-contributors)
  * [Compile and run the code](#compile-and-run-the-code)
  * [Update the file list](#update-the-file-list)
  * [Prepare a release](#prepare-a-release)

## Usage
**This is for Windows players only.**

1. Download the latest version from the [releases tab](https://github.com/Pathoschild/StardewGameFolderCheck/releases).  
   _(Look for the file named `StardewGameFolderCheck-<version>.zip` under "Assets".)_
2. Unzip the download somewhere (**not** in your game folder!).
3. Double-click the `GameFolderTestApp.exe` file to launch it. (It may be shown as
   `GameFolderTestApp` with no extension.)
4. Follow the on-screen instructions.

## For contributors
### Compile and run the code
1. Install:
   * [Visual Studio](https://visualstudio.microsoft.com/) (the Community edition is free and has
     all the features you'll need).
   * Stardew Valley.
   * [SMAPI](https://smapi.io/).
2. Download the source code.
3. Double-click `StardewGameFolderCheck.sln` to open it in Visual Studio.
4. Click _Build > Rebuild Solution_ to build the tool.
5. Click _Debug > Start Without Debugging_ to launch the tool.

### Update the file list
1. [Run the tool](#compile-from-source).
2. This will create an `actual-files.json` file in the tool folder (`bin/Debug/net6.0` when run
   locally).
3. Copy the contents of that file into `expected-files.json`.

### Prepare a release
1. [Open the solution in Visual Studio](#compile-from-source).
2. Edit the project and update the `<Version>` number.
3. Right-click the project and choose _Publish_.
4. Click the _Publish_ button in the view that opens. (There's no need to edit the preconfigured
   settings.)
5. When it's done, click _open folder_ to view the files.
6. Rename the folder to `StardewGameFolderCheck <version>.zip`.
7. Zip and release the zip file.
