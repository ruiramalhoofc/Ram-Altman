using System.Drawing;
using System.Runtime.InteropServices;

namespace RamAltman;

public sealed class MainForm : Form
{
    private const long MiB = 1024L * 1024L;
    private const long GiB = 1024L * MiB;
    private const int ChunkMiB = 8;
    private const long ExtraHeadroom = 32L * MiB;

    private readonly Label _memoryLabel;
    private readonly Label _allocationLabel;
    private readonly Label _reserveLabel;
    private readonly Label _percentLabel;
    private readonly Label _statusLabel;
    private readonly ProgressBar _usageBar;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    private CancellationTokenSource? _cancellation;
    private Task? _workerTask;
    private long _allocatedBytes;
    private string _workerStatus = "Idle — the AI has not claimed your RAM yet.";
    private bool _closing;

    public MainForm()
    {
        Text = "Ram Altman";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(650, 430);
        MinimumSize = new Size(590, 400);
        BackColor = Color.FromArgb(18, 20, 29);
        ForeColor = Color.FromArgb(237, 240, 247);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var title = new Label
        {
            Text = "RAM ALTMAN",
            Font = new Font("Segoe UI", 25F, FontStyle.Bold),
            ForeColor = Color.FromArgb(232, 236, 245),
            AutoSize = true,
            Location = new Point(30, 23)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Your PC’s RAM belongs in an AI data centre.",
            Font = new Font("Segoe UI", 11F),
            ForeColor = Color.FromArgb(160, 170, 190),
            AutoSize = true,
            Location = new Point(33, 70)
        };
        Controls.Add(subtitle);

        var panel = new Panel
        {
            Location = new Point(30, 115),
            Size = new Size(590, 190),
            BackColor = Color.FromArgb(29, 33, 46),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(panel);

        _memoryLabel = MakeLabel("Physical RAM: measuring...", 20, 18, 550, 26, 11F, FontStyle.Bold);
        panel.Controls.Add(_memoryLabel);

        _usageBar = new ProgressBar
        {
            Location = new Point(20, 58),
            Size = new Size(548, 22),
            Minimum = 0,
            Maximum = 1000,
            Style = ProgressBarStyle.Continuous
        };
        panel.Controls.Add(_usageBar);

        _percentLabel = MakeLabel("Usage: --%", 20, 88, 250, 24, 10F);
        panel.Controls.Add(_percentLabel);

        _allocationLabel = MakeLabel("RAM used by Ram: 0 MB", 20, 119, 548, 23, 10F);
        panel.Controls.Add(_allocationLabel);

        _reserveLabel = MakeLabel("Reserved for Windows: calculating...", 20, 148, 548, 23, 10F);
        _reserveLabel.ForeColor = Color.FromArgb(160, 170, 190);
        panel.Controls.Add(_reserveLabel);

        _statusLabel = MakeLabel(_workerStatus, 32, 319, 580, 30, 9.5F);
        _statusLabel.ForeColor = Color.FromArgb(190, 199, 216);
        Controls.Add(_statusLabel);

        _startButton = new Button
        {
            Text = "GIVE RAM TO RAM ALTMAN",
            Location = new Point(30, 365),
            Size = new Size(280, 42),
            BackColor = Color.FromArgb(96, 74, 190),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _startButton.FlatAppearance.BorderSize = 0;
        _startButton.Click += StartButton_Click;
        Controls.Add(_startButton);

        _stopButton = new Button
        {
            Text = "RETURN MY RAM",
            Location = new Point(340, 365),
            Size = new Size(280, 42),
            BackColor = Color.FromArgb(55, 62, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _stopButton.FlatAppearance.BorderSize = 0;
        _stopButton.Click += StopButton_Click;
        Controls.Add(_stopButton);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        FormClosing += MainForm_FormClosing;
    }

    private static Label MakeLabel(string text, int x, int y, int width, int height,
        float fontSize, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            Font = new Font("Segoe UI", fontSize, style),
            ForeColor = Color.FromArgb(225, 230, 240),
            AutoEllipsis = true
        };
    }

    private void StartButton_Click(object? sender, EventArgs e)
    {
        if (_workerTask is { IsCompleted: false })
            return;

        _cancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        Volatile.Write(ref _workerStatus, "Claiming memory... (you can stop at any time)");
        _startButton.Enabled = false;
        _stopButton.Enabled = true;
        _workerTask = Task.Run(() => ConsumeMemory(cancellation.Token));
    }

    private void StopButton_Click(object? sender, EventArgs e)
    {
        Volatile.Write(ref _workerStatus, "Returning RAM to Windows...");
        _stopButton.Enabled = false;
        _cancellation?.Cancel();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            MemoryStatus status = MemoryStatus.Read();
            long total = checked((long)status.TotalPhysical);
            long available = checked((long)status.AvailablePhysical);
            long used = Math.Max(0, total - available);
            double percent = total > 0 ? used * 100.0 / total : 0;

            _memoryLabel.Text = $"Physical RAM: {FormatBytes(used)} used of {FormatBytes(total)}";
            _percentLabel.Text = $"System usage: {percent:0.0}%";
            _usageBar.Value = Math.Clamp((int)Math.Round(percent * 10), 0, 1000);
            _allocationLabel.Text = $"RAM used by Ram: {FormatBytes(Interlocked.Read(ref _allocatedBytes))}";
            _reserveLabel.Text = $"Headroom kept for Windows: {FormatBytes(GetReserve(total) + ExtraHeadroom)}";
            _statusLabel.Text = Volatile.Read(ref _workerStatus);
        }
        catch (Exception)
        {
            _memoryLabel.Text = "Unable to read memory status.";
        }

        if (_workerTask is { IsCompleted: true })
        {
            _workerTask = null;
            _cancellation?.Dispose();
            _cancellation = null;
            _startButton.Enabled = true;
            _stopButton.Enabled = false;
        }
    }

    private void ConsumeMemory(CancellationToken cancellationToken)
    {
        var blocks = new List<byte[]>();
        long allocated = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                MemoryStatus status = MemoryStatus.Read();
                long available = checked((long)status.AvailablePhysical);
                long reserve = GetReserve(checked((long)status.TotalPhysical));

                // If available memory drops below the total headroom, release blocks for Windows.
                long safetyThreshold = reserve + ExtraHeadroom;
                if (available < safetyThreshold)
                {
                    if (blocks.Count > 0)
                    {
                        byte[] released = blocks[^1];
                        blocks.RemoveAt(blocks.Count - 1);
                        allocated -= released.LongLength;
                        Interlocked.Exchange(ref _allocatedBytes, allocated);
                        released = Array.Empty<byte>();
                        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
                        Volatile.Write(ref _workerStatus, "Windows needs memory — Ram is releasing RAM.");
                    }
                    else
                    {
                        Volatile.Write(ref _workerStatus, "Paused: available RAM is below the safety reserve.");
                        Thread.Sleep(350);
                    }
                    continue;
                }

                long usable = available - safetyThreshold;
                if (usable < 4 * MiB)
                {
                    Volatile.Write(ref _workerStatus, "Maintaining the Windows safety reserve...");
                    Thread.Sleep(250);
                    continue;
                }

                int size = (int)Math.Min(ChunkMiB * MiB, usable);
                var block = new byte[size];

                // Writing to each page forces Windows to commit physical RAM for the block.
                const int pageSize = 4096;
                for (int offset = 0; offset < block.Length; offset += pageSize)
                    block[offset] = 0x5A;
                block[^1] = 0x5A;

                blocks.Add(block);
                allocated += block.LongLength;
                Interlocked.Exchange(ref _allocatedBytes, allocated);
                Volatile.Write(ref _workerStatus, "Using available RAM. Windows keeps a reserve.");
            }
        }
        catch (OutOfMemoryException)
        {
            Volatile.Write(ref _workerStatus, "Windows refused further memory allocation. Releasing reserved memory...");
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _workerStatus, $"Error: {ex.Message}");
        }
        finally
        {
            blocks.Clear();
            Interlocked.Exchange(ref _allocatedBytes, 0);
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
            if (cancellationToken.IsCancellationRequested)
                Volatile.Write(ref _workerStatus, "RAM released. Windows may take a moment to update the figures.");
        }
    }

    private static long GetReserve(long totalPhysicalBytes)
    {
        // Reserve 0.5% of physical RAM, with a minimum of 32 MiB.
        return Math.Max(32L * MiB, totalPhysicalBytes / 200);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_workerTask is { IsCompleted: false })
        {
            e.Cancel = true;
            if (_closing)
                return;

            _closing = true;
            Volatile.Write(ref _workerStatus, "Releasing memory before closing...");
            _cancellation?.Cancel();
            try
            {
                await _workerTask;
            }
            catch
            {
                // The process will close regardless; Windows will release its memory.
            }
            Close();
        }
    }
}

internal struct MemoryStatus
{
    private uint _length;
    private uint _memoryLoad;
    public ulong TotalPhysical;
    public ulong AvailablePhysical;
    private ulong _totalPageFile;
    private ulong _availablePageFile;
    private ulong _totalVirtual;
    private ulong _availableVirtual;
    private ulong _availableExtendedVirtual;

    public static MemoryStatus Read()
    {
        var status = new MemoryStatus
        {
            _length = (uint)Marshal.SizeOf<MemoryStatus>()
        };

        if (!GlobalMemoryStatusEx(ref status))
            throw new InvalidOperationException("GlobalMemoryStatusEx failed.");

        return status;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
}
