using System.Diagnostics;
using System.Text.Json;

public class ConfigManager
{
    // Define the event source and log name for the event log
    private const string EventSourceName = "PreventSleepServiceSource";
    private const string LogName = "PreventSleepServiceLog";

    // File path for the configuration file
    private static String ConfigFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PreventSleepService", "config.json");

    // Method to save the schedules to the config file
    public static void SaveSchedules(List<SleepPreventionPeriod> schedules)
    {
        try
        {
            // Ensure the directory exists
            String directory = Path.GetDirectoryName(ConfigFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Serialize the schedules list to JSON and write to the file
            String json = JsonSerializer.Serialize(schedules, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);

            // Log success message
            WriteToEventLog("Schedules saved successfully.", EventLogEntryType.Information);
        }
        catch (Exception ex)
        {
            // Log error to event log and console
            WriteToEventLog($"Failed to save config: {ex.Message}", EventLogEntryType.Error);
            Console.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    // Method to load the schedules from the config file
    public static List<SleepPreventionPeriod> LoadSchedules()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                // Read the file and deserialize the JSON to a list of SleepPreventionPeriod
                String json = File.ReadAllText(ConfigFilePath);
                var schedules = JsonSerializer.Deserialize<List<SleepPreventionPeriod>>(json);

                // Log success message
                WriteToEventLog("Schedules loaded successfully.", EventLogEntryType.Information);

                return schedules;
            }
        }
        catch (Exception ex)
        {
            // Log error to event log and console
            WriteToEventLog($"Failed to load config: {ex.Message}", EventLogEntryType.Error);
            Console.WriteLine($"Failed to load config: {ex.Message}");
        }

        // Return an empty list if no schedules are found or an error occurs
        WriteToEventLog("No schedules found or an error occurred while loading.", EventLogEntryType.Warning);
        return new List<SleepPreventionPeriod>();
    }

    // Method to log to the event log
    private static void WriteToEventLog(string message, EventLogEntryType entryType)
    {
        try
        {
            // Create event log source if it doesn't exist
            if (!EventLog.SourceExists(EventSourceName))
            {
                EventLog.CreateEventSource(EventSourceName, LogName);
            }

            // Write the log entry
            using (EventLog eventLog = new EventLog(LogName))
            {
                eventLog.Source = EventSourceName;
                eventLog.WriteEntry(message, entryType);
            }
        }
        catch (Exception ex)
        {
            // If the event log writing fails, fall back to console output
            Console.WriteLine($"Failed to write to event log: {ex.Message}");
        }
    }
}


public class SleepPreventionPeriod
{
    public DayOfWeek? Day { get; set; } // Day of week for recurring schedules
    public DateOnly? SpecificDate { get; set; } // Specific date for one-off schedules
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public Boolean KeepDisplayOn { get; set; }

    // Method to check if the current time is within the period
    public Boolean IsWithinPeriod(DateTime now)
    {
        TimeOnly currentTime = TimeOnly.FromDateTime(now);

        // Check for one-off schedules
        if (SpecificDate.HasValue)
        {
            if (DateOnly.FromDateTime(now) == SpecificDate.Value && currentTime >= StartTime && currentTime <= EndTime)
            {
                return true;
            }
        }

        // Check for recurring day-of-week schedules
        if (Day.HasValue && now.DayOfWeek == Day.Value)
        {
            if (currentTime >= StartTime && currentTime <= EndTime)
            {
                return true;
            }
        }

        return false;
    }
}

