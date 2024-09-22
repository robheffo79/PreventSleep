using PreventSleep.Services;
using StreamJsonRpc;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;

namespace PreventSleep
{
    public class Program
    {
        private const String ServiceName = "PreventSleepService";
        private const String ServiceDescription = "Prevents the system from sleeping based on configured schedules. Keeps the system awake at specified times with the option to keep the display on.";

        public static async Task Main(String[] args)
        {
            // Check if running interactively (as a command-line tool) or as a service
            if (Environment.UserInteractive)
            {
                try
                {
                    if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
                    {
                        ShowHelp();
                        return;
                    }

                    // Command-line argument handling for installing/uninstalling service and interacting with the service
                    if (args[0].Equals("--install", StringComparison.OrdinalIgnoreCase))
                    {
                        InstallService();
                        return;
                    }
                    else if (args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        UninstallService();
                        return;
                    }

                    // Check if the service is installed and running
                    if (!CheckServiceStatus())
                    {
                        Console.WriteLine($"Please install the service using 'PreventSleep.exe --install'.");
                        return;  // Exit if the service is not installed or not running
                    }

                    // Otherwise, run as a command-line client to interact with the service via named pipes
                    using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", "PreventSleepPipe", PipeDirection.InOut, PipeOptions.Asynchronous))
                    {
                        try
                        {
                            await pipeClient.ConnectAsync();
                            JsonRpc jsonRpc = JsonRpc.Attach(pipeClient);

                            // Command handling
                            if (args[0] == "--add-schedule" && args.Length >= 4)
                            {
                                String dayOrDateRange = args[1];
                                String timeRange = args[2];
                                Boolean keepDisplayOn = false;

                                if (args[3].Equals("display-on", StringComparison.OrdinalIgnoreCase))
                                {
                                    keepDisplayOn = true;
                                }
                                else if (args[3].Equals("display-off", StringComparison.OrdinalIgnoreCase))
                                {
                                    keepDisplayOn = false;
                                }
                                else
                                {
                                    Console.WriteLine("Invalid value for display option. Use 'display-on' or 'display-off'.");
                                    return;
                                }

                                try
                                {
                                    await jsonRpc.InvokeAsync("AddSchedule", dayOrDateRange, timeRange, keepDisplayOn);
                                    Console.WriteLine("Schedule added successfully.");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to add schedule: {ex.Message}");
                                }
                            }
                            else if (args[0] == "--list-schedules")
                            {
                                try
                                {
                                    List<String> schedules = await jsonRpc.InvokeAsync<List<String>>("ListSchedules");
                                    foreach (String schedule in schedules)
                                    {
                                        Console.WriteLine(schedule);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to list schedules: {ex.Message}");
                                }
                            }
                            else if (args[0] == "--delete-schedule" && args.Length == 2)
                            {
                                if (!Int32.TryParse(args[1], out Int32 id))
                                {
                                    Console.WriteLine("Invalid schedule ID. Please provide a valid integer.");
                                    return;
                                }
                                try
                                {
                                    String result = await jsonRpc.InvokeAsync<String>("DeleteSchedule", id);
                                    Console.WriteLine(result);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to delete schedule: {ex.Message}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid command or arguments.");
                                ShowHelp();
                            }
                        }
                        catch (IOException)
                        {
                            Console.WriteLine("Failed to connect to the service. Make sure the PreventSleepService is running.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"An error occurred: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                }
            }
            else
            {
                // Running as a Windows service
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new PreventSleepService() };
                ServiceBase.Run(ServicesToRun);
            }
        }

        // Method to check if the service is installed and running
        private static Boolean CheckServiceStatus()
        {
            try
            {
                using (ServiceController sc = new ServiceController(ServiceName))
                {
                    // Check if the service is installed
                    ServiceControllerStatus status = sc.Status;
                    if (status != ServiceControllerStatus.Running)
                    {
                        Console.WriteLine($"Error: The {ServiceName} service is installed but not running.");
                        return false;
                    }
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine($"Error: The {ServiceName} service is not installed.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking service status: {ex.Message}");
                return false;
            }
        }

        // Method to show help information
        private static void ShowHelp()
        {
            Console.WriteLine("PreventSleep - A Tool to Prevent System Sleep Based on Configured Schedules");
            Console.WriteLine();
            Console.WriteLine("PreventSleep allows you to configure your Windows system to stay awake at specified times,");
            Console.WriteLine("either on recurring days or specific dates, with the option to keep the display on.");
            Console.WriteLine("This makes it ideal for preventing system sleep during periods of heavy workloads,");
            Console.WriteLine("remote access sessions, or other tasks that require the system to remain active.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  PreventSleep.exe [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  --add-schedule <day-or-date-range> <time-range> <display-on|display-off>");
            Console.WriteLine("      Adds a new schedule for sleep prevention.");
            Console.WriteLine("      Example:");
            Console.WriteLine("          PreventSleep.exe --add-schedule Mon-Fri 09:00-17:00 display-on");
            Console.WriteLine();
            Console.WriteLine("  --list-schedules");
            Console.WriteLine("      Lists all existing sleep prevention schedules.");
            Console.WriteLine("      Example:");
            Console.WriteLine("          PreventSleep.exe --list-schedules");
            Console.WriteLine();
            Console.WriteLine("  --delete-schedule <id>");
            Console.WriteLine("      Deletes a schedule by its ID.");
            Console.WriteLine("      Example:");
            Console.WriteLine("          PreventSleep.exe --delete-schedule 1");
            Console.WriteLine();
            Console.WriteLine("  --install");
            Console.WriteLine("      Installs the PreventSleep Windows Service.");
            Console.WriteLine();
            Console.WriteLine("  --uninstall");
            Console.WriteLine("      Uninstalls the PreventSleep Windows Service.");
            Console.WriteLine();
            Console.WriteLine("  --help, -h");
            Console.WriteLine("      Shows this help information.");
            Console.WriteLine();
        }

        // Method to install the service using sc.exe
        private static void InstallService()
        {
            if (!IsAdministrator())
            {
                Console.WriteLine("Administrator privileges are required to install the service.");
                RelaunchAsAdministrator();
                return;
            }

            try
            {
                String serviceExePath = Assembly.GetExecutingAssembly().Location;

                // Create and install the service using sc.exe
                ExecuteScCommand($"create {ServiceName} binPath= \"{serviceExePath}\" start= auto");
                ExecuteScCommand($"description {ServiceName} \"{ServiceDescription}\"");

                // Start the service after installation
                ExecuteScCommand($"start {ServiceName}");

                Console.WriteLine("Service installed and started successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service installation failed: {ex.Message}");
            }
        }

        // Method to uninstall the service using sc.exe
        private static void UninstallService()
        {
            if (!IsAdministrator())
            {
                Console.WriteLine("Administrator privileges are required to uninstall the service.");
                RelaunchAsAdministrator();
                return;
            }

            try
            {
                // Stop the service before uninstalling
                ExecuteScCommand($"stop {ServiceName}");

                // Uninstall the service
                ExecuteScCommand($"delete {ServiceName}");

                Console.WriteLine("Service uninstalled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service uninstallation failed: {ex.Message}");
            }
        }

        // Helper method to execute sc.exe commands
        private static void ExecuteScCommand(String arguments)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo("sc.exe", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(processInfo))
            {
                String output = process.StandardOutput.ReadToEnd();
                String error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"sc.exe exited with code {process.ExitCode}: {error}");
                }

                Console.WriteLine(output);
            }
        }

        // Method to check if the current process has administrator privileges
        private static Boolean IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        // Method to relaunch the application with administrator privileges
        private static void RelaunchAsAdministrator()
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Assembly.GetExecutingAssembly().Location,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = String.Join(" ", Environment.GetCommandLineArgs().Skip(1).ToArray())
            };

            try
            {
                Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User refused to allow privileges elevation.
                Console.WriteLine("Administrator privileges are required to perform this operation.");
            }
            Environment.Exit(0);
        }
    }
}
