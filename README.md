# Ram Altman — RAM Greed Simulator

A small Windows satire: it dynamically attempts to use the available physical RAM instead of requesting a fixed number of gigabytes.

## Safety and behaviour

The application **does not attempt to use literally 100% of RAM**. Doing so could leave Windows without enough memory to operate, freeze the computer, or cause unsaved work to be lost.

Instead, it calculates an adaptive reserve equal to **0.5% of physical RAM, with a minimum of 32 MiB**, and keeps an additional 32 MiB of headroom. On a computer with 8 GiB of RAM, the total headroom is approximately 73 MiB, which may allow overall memory usage to approach 99%, depending on Windows and other applications. The application adapts to the installed memory and to the amount already used by other applications.

Memory is allocated in 8 MiB blocks and each page is written to so that the memory is actually committed. The application also monitors available memory: if free memory falls below the total headroom (the calculated reserve plus the additional margin), it releases blocks. The **RETURN MY RAM** button cancels the operation and releases the allocated blocks.

The interface displays the total headroom being maintained (the calculated reserve plus the additional 32 MiB margin). The percentage shown is the overall physical memory usage reported by Windows. It may differ slightly from Task Manager due to cached and compressed memory, as well as differences in accounting.

## Build requirements

- 64-bit Windows 10 or Windows 11.
- The **.NET 10 SDK** installed (installing only the Runtime is not sufficient).

## Build a single EXE

1. Install the .NET 10 SDK from the official page: https://dotnet.microsoft.com/download/dotnet/10.0
2. Extract the ZIP archive to a folder.
3. Open Terminal or PowerShell in the folder containing `RamAltman.csproj`.
4. Run:

   ```powershell
   dotnet publish -c Release
   ```

5. The executable will be created at:

   ```text
   bin\Release\net10.0-windows\win-x64\publish\Ram Altman.exe
   ```

The project is configured to publish a self-contained, single-file 64-bit executable. The target computer does not need .NET installed. Per-Monitor V2 DPI awareness is configured by the project itself, avoiding the Windows Forms WFO0003 warning.

## Usage

1. Run `Ram Altman.exe`.
2. Click **GIVE RAM TO RAM ALTMAN**.
3. Monitor usage in the application or in Windows Task Manager.
4. Click **RETURN MY RAM** to release the memory.

Save your work before testing software that consumes large amounts of memory. This configuration is aggressive: when little memory is available, Windows may use the paging file heavily, become slow, or close applications. The application releases memory dynamically when available memory falls below its defined threshold, but this does not eliminate the risk of instability. Actual behaviour also depends on other running applications and the Windows virtual memory configuration.

## Project structure

- `RamAltman.csproj` — project and publishing configuration.
- `Program.cs` — application entry point.
- `MainForm.cs` — user interface, memory monitoring, and memory management.
- `app.manifest` — Windows manifest (does not request administrator privileges).
