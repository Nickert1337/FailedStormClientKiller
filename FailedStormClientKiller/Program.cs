using System.Diagnostics;

namespace FailedStormClientKiller;

class Program
{
    private static readonly Dictionary<int, DateTime> processTracker = new();
    private static readonly TimeSpan KillThreshold = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    static async Task Main(string[] args)
    {
        Console.WriteLine("FailedStormClientKiller started...");
        Console.WriteLine($"Monitoring for RuneLite processes with window title 'RuneLite' for more than {KillThreshold.TotalMinutes} minutes");
        Console.WriteLine($"Checking every {CheckInterval.TotalSeconds} seconds");
        Console.WriteLine("Press Ctrl+C to exit\n");

        while (true)
        {
            try
            {
                CheckRuneLiteProcesses();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Error during process check: {ex.Message}");
            }

            await Task.Delay(CheckInterval);
        }
    }

    private static void CheckRuneLiteProcesses()
    {
        var runeLiteProcesses = Process.GetProcessesByName("RuneLite");

        if (runeLiteProcesses.Length == 0)
        {
            if (processTracker.Count > 0)
            {
                Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - No RuneLite processes found, clearing tracker");
                processTracker.Clear();
            }
            return;
        }

        Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Found {runeLiteProcesses.Length} RuneLite process(es)");

        var currentProcessIds = new HashSet<int>();

        foreach (var process in runeLiteProcesses)
        {
            try
            {
                if (process.HasExited)
                    continue;

                currentProcessIds.Add(process.Id);

                string mainWindowTitle = process.MainWindowTitle;

                if (mainWindowTitle == "RuneLite")
                {
                    if (!processTracker.ContainsKey(process.Id))
                    {
                        processTracker[process.Id] = DateTime.Now;
                        Console.WriteLine($"[TRACKING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Started tracking process ID {process.Id} with title '{mainWindowTitle}'");
                    }
                    else
                    {
                        var trackingDuration = DateTime.Now - processTracker[process.Id];
                        Console.WriteLine($"[TRACKING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Process ID {process.Id} has had title 'RuneLite' for {trackingDuration.TotalSeconds:F0} seconds");

                        if (trackingDuration >= KillThreshold)
                        {
                            KillProcess(process);
                            processTracker.Remove(process.Id);
                        }
                    }
                }
                else
                {
                    if (processTracker.ContainsKey(process.Id))
                    {
                        Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Process ID {process.Id} title changed to '{mainWindowTitle}', removing from tracker");
                        processTracker.Remove(process.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Error processing RuneLite process ID {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        var staleProcessIds = processTracker.Keys.Where(id => !currentProcessIds.Contains(id)).ToList();
        foreach (var staleId in staleProcessIds)
        {
            Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Process ID {staleId} no longer exists, removing from tracker");
            processTracker.Remove(staleId);
        }
    }

    private static void KillProcess(Process process)
    {
        try
        {
            Console.WriteLine($"[KILL] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Attempting to kill process ID {process.Id}");
            process.Kill();

            if (process.WaitForExit(5000))
            {
                Console.WriteLine($"[SUCCESS] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Successfully killed process ID {process.Id}");
            }
            else
            {
                Console.WriteLine($"[WARNING] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Process ID {process.Id} did not exit within 5 seconds after kill attempt");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Failed to kill process ID {process.Id}: {ex.Message}");
        }
    }
}