using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// The Program class contains the Main entry point for the application,
/// which accepts command-line options to prevent the system from sleeping.
/// You can use --prevent-for <TimeSpan> or --prevent-until <DateTime> or <TimeOnly>.
/// An optional --prevent-display-off flag can be passed to prevent the display from turning off as well.
/// </summary>
class Program
{
    /// <summary>
    /// Enum defining flags that specify system execution state to prevent sleep.
    /// </summary>
    [Flags]
    public enum EXECUTION_STATE : uint
    {
        /// <summary>
        /// Prevent the system from sleeping.
        /// </summary>
        ES_SYSTEM_REQUIRED = 0x00000001,

        /// <summary>
        /// Keep the display on.
        /// </summary>
        ES_DISPLAY_REQUIRED = 0x00000002,

        /// <summary>
        /// Used to reset previous flags and allow system sleep.
        /// </summary>
        ES_CONTINUOUS = 0x80000000
    }

    /// <summary>
    /// External method from kernel32.dll to change the execution state of the system.
    /// </summary>
    /// <param name="esFlags">Flags that determine the execution state.</param>
    /// <returns>Returns the previous execution state.</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

    /// <summary>
    /// Main method which acts as the entry point for the application.
    /// </summary>
    /// <param name="args">Command-line arguments that define the time period to prevent system sleep.</param>
    static void Main(string[] args)
    {
        // Variables to hold time-related information
        TimeSpan? duration = null;
        DateTime? endTime = null;
        bool preventDisplayOff = false;

        // Parsing command line arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--prevent-for" && i + 1 < args.Length)
            {
                if (TimeSpan.TryParse(args[i + 1], out TimeSpan parsedDuration))
                {
                    duration = parsedDuration;
                    i++;
                }
                else
                {
                    Console.WriteLine("Invalid TimeSpan format. Example: --prevent-for 00:30:00");
                    return;
                }
            }
            else if (args[i] == "--prevent-until" && i + 1 < args.Length)
            {
                if (DateTime.TryParse(args[i + 1], out DateTime parsedDateTime))
                {
                    // Handle full DateTime input
                    endTime = parsedDateTime;
                    i++;
                }
                else if (TimeOnly.TryParse(args[i + 1], out TimeOnly parsedTimeOnly))
                {
                    // Handle time-only input (e.g., 5pm)
                    endTime = CalculateDateTimeForTimeOnly(parsedTimeOnly);
                    i++;
                }
                else
                {
                    Console.WriteLine("Invalid DateTime or Time format. Example: --prevent-until 2024-09-20T10:30:00 or --prevent-until 17:00");
                    return;
                }
            }
            else if (args[i] == "--prevent-display-off")
            {
                preventDisplayOff = true;
            }
        }

        // Ensure either --prevent-for or --prevent-until is provided
        if (!duration.HasValue && !endTime.HasValue)
        {
            Console.WriteLine("Usage: PreventSleep.exe --prevent-for <TimeSpan> or --prevent-until <DateTime> [--prevent-display-off]");
            Console.WriteLine("Example: PreventSleep.exe --prevent-for 00:30:00 --prevent-display-off");
            Console.WriteLine("Example: PreventSleep.exe --prevent-until 2024-09-20T10:30:00 --prevent-display-off");
            Console.WriteLine("Example: PreventSleep.exe --prevent-until 17:00 --prevent-display-off");
            return;
        }

        // Calculate the duration based on the provided DateTime or TimeSpan
        if (endTime.HasValue)
        {
            duration = endTime.Value - DateTime.Now;
            if (duration <= TimeSpan.Zero)
            {
                Console.WriteLine("The --prevent-until time must be in the future.");
                return;
            }

            Console.WriteLine($"Preventing system sleep until {endTime.Value}. Press CTRL+C to cancel.");
        }
        else if (duration.HasValue)
        {
            Console.WriteLine($"Preventing system sleep for {duration.Value.TotalMinutes} minutes. Press CTRL+C to cancel.");
        }

        if (preventDisplayOff)
        {
            Console.WriteLine("Display will also be kept on.");
        }
        else
        {
            Console.WriteLine("Display can turn off.");
        }

        Console.WriteLine("\nIf you find this tool useful, please consider supporting me: https://buymeacoffee.com/robheffo");

        try
        {
            // Set the appropriate execution state based on the flag
            EXECUTION_STATE executionState = EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED;

            if (preventDisplayOff)
            {
                executionState |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;
            }

            // Prevent the system from sleeping (and possibly the display from turning off)
            SetThreadExecutionState(executionState);

            // Keep the application alive for the calculated duration
            Thread.Sleep(duration.Value);

            // Allow the system to sleep again after the specified time
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
            Console.WriteLine("System can now sleep again.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates the target DateTime based on the provided TimeOnly value.
    /// If the time has already passed for today, it returns the time for tomorrow.
    /// </summary>
    /// <param name="timeOnly">The time of day (as a TimeOnly) for which the system should prevent sleep.</param>
    /// <returns>A DateTime representing the next occurrence of the provided time of day.</returns>
    private static DateTime CalculateDateTimeForTimeOnly(TimeOnly timeOnly)
    {
        DateTime now = DateTime.Now;
        DateTime todayWithTime = DateTime.Today.Add(timeOnly.ToTimeSpan());

        if (todayWithTime > now)
        {
            // The time is later today
            return todayWithTime;
        }
        else
        {
            // The time has already passed today, so we use the same time tomorrow
            return todayWithTime.AddDays(1);
        }
    }
}
