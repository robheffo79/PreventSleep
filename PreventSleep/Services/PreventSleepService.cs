using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace PreventSleep.Services
{
    public class PreventSleepService : ServiceBase
    {
        private const string EventSourceName = "PreventSleepServiceSource";  // Source for the event log
        private const string LogName = "PreventSleepServiceLog";  // Custom event log name

        private CancellationTokenSource _cancellationTokenSource;
        private List<SleepPreventionPeriod> _sleepPeriods;
        private Task _serverTasks;
        private EventLog _eventLog;

        public override EventLog EventLog { get => _eventLog; }

        public PreventSleepService()
        {
            this.ServiceName = "PreventSleepService";
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = false;  // Disable automatic logging, use our custom log

            // Initialize the custom event log
            if (!EventLog.SourceExists(EventSourceName))
            {
                EventLog.CreateEventSource(EventSourceName, LogName);
            }

            _eventLog = new EventLog
            {
                Source = EventSourceName,
                Log = LogName
            };

            // Log a diagnostic message indicating service initialization
            EventLog.WriteEntry("Service initialized successfully.", EventLogEntryType.Information);

            // Load the saved schedules from the config file
            try
            {
                _sleepPeriods = ConfigManager.LoadSchedules();
                EventLog.WriteEntry("Loaded schedules successfully.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                // Handle exception, initialize to empty list
                _sleepPeriods = new List<SleepPreventionPeriod>();
                EventLog.WriteEntry("Failed to load schedules: " + ex.Message, EventLogEntryType.Error);
            }
        }

        protected override void OnStart(String[] args)
        {
            try
            {
                // Log diagnostic message for service start
                EventLog.WriteEntry("Service is starting...", EventLogEntryType.Information);

                // Create a cancellation token source to manage the server and schedule checker shutdown
                _cancellationTokenSource = new CancellationTokenSource();

                // Start both the RPC server and the schedule checker tasks
                Task rpcServerTask = RunRpcServerAsync(_cancellationTokenSource.Token);
                Task scheduleCheckerTask = RunScheduleCheckerAsync(_cancellationTokenSource.Token);

                // Store the tasks so they can be awaited in OnStop
                _serverTasks = Task.WhenAll(rpcServerTask, scheduleCheckerTask);

                EventLog.WriteEntry("Service started successfully.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                // Log exception and stop the service
                EventLog.WriteEntry("Service failed to start: " + ex.Message, EventLogEntryType.Error);
                this.Stop();
            }
        }

        protected override void OnStop()
        {
            try
            {
                // Log diagnostic message for service stop
                EventLog.WriteEntry("Service is stopping...", EventLogEntryType.Information);

                // Cancel the token to stop the server and the schedule checker
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Cancel();
                }

                // Wait for both tasks to complete before shutting down the service
                _serverTasks?.Wait();

                EventLog.WriteEntry("Service stopped successfully.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                // Log the exception
                EventLog.WriteEntry("Error stopping service: " + ex.Message, EventLogEntryType.Error);
            }
        }

        private async Task RunRpcServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipeServer = null;
                try
                {
                    // Log diagnostic message for RPC server connection
                    EventLog.WriteEntry("RPC server is accepting connections...", EventLogEntryType.Information);

                    pipeServer = new NamedPipeServerStream(
                        "PreventSleepPipe",
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous
                    );

                    await pipeServer.WaitForConnectionAsync(cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        pipeServer.Dispose();
                        break;
                    }

                    _ = Task.Run(async () =>
                    {
                        using (pipeServer)
                        {
                            JsonRpc jsonRpc = null;
                            try
                            {
                                jsonRpc = JsonRpc.Attach(pipeServer, this);
                                await jsonRpc.Completion;
                            }
                            catch (Exception ex)
                            {
                                // Handle RPC session exceptions and log them
                                EventLog.WriteEntry("Error in RPC connection: " + ex.Message, EventLogEntryType.Error);
                            }
                            finally
                            {
                                await pipeServer.DisposeAsync();
                            }
                        }
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log RPC server errors
                    EventLog.WriteEntry("Error in RPC server: " + ex.Message, EventLogEntryType.Error);

                    if (pipeServer != null)
                    {
                        pipeServer.Dispose();
                    }

                    // Optionally wait before retrying
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task RunScheduleCheckerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    DateTime now = DateTime.Now;

                    foreach (SleepPreventionPeriod period in _sleepPeriods)
                    {
                        if (period.IsWithinPeriod(now))
                        {
                            EXECUTION_STATE esFlags = EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED;
                            if (period.KeepDisplayOn)
                            {
                                esFlags |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;
                            }
                            SetThreadExecutionState(esFlags);
                        }
                    }

                    // Wait for 1 minute or until the cancellation token is triggered
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break; // Task was canceled, exit the loop
                }
                catch (Exception ex)
                {
                    // Log schedule checking errors
                    EventLog.WriteEntry("Error in schedule checker: " + ex.Message, EventLogEntryType.Error);

                    // Optionally wait before retrying
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        // Example RPC Methods with logging
        [JsonRpcMethod]
        public void AddSchedule(string dayOrDateRange, string timeRange, bool keepDisplayOn)
        {
            try
            {
                // Logic for adding a schedule
                // ...

                // Log success message for schedule addition
                EventLog.WriteEntry($"Schedule added: {dayOrDateRange} {timeRange}, Display: {keepDisplayOn}", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                // Log error during schedule addition
                EventLog.WriteEntry("Error adding schedule: " + ex.Message, EventLogEntryType.Error);
                throw;
            }
        }

        [JsonRpcMethod]
        public List<String> ListSchedules()
        {
            try
            {
                // List schedules logic
                List<String> scheduleList = new List<String>();

                if (_sleepPeriods.Count == 0)
                {
                    scheduleList.Add("No schedules available.");
                    return scheduleList;
                }

                for (Int32 i = 0; i < _sleepPeriods.Count; i++)
                {
                    SleepPreventionPeriod period = _sleepPeriods[i];
                    if (period.SpecificDate.HasValue)
                    {
                        scheduleList.Add($"[{i}] Date: {period.SpecificDate}, Time: {period.StartTime} - {period.EndTime}, Display: {(period.KeepDisplayOn ? "On" : "Off")}");
                    }
                    else
                    {
                        scheduleList.Add($"[{i}] Day: {period.Day}, Time: {period.StartTime} - {period.EndTime}, Display: {(period.KeepDisplayOn ? "On" : "Off")}");
                    }
                }

                // Log schedule listing success
                EventLog.WriteEntry("Schedules listed successfully.", EventLogEntryType.Information);

                return scheduleList;
            }
            catch (Exception ex)
            {
                // Log error during schedule listing
                EventLog.WriteEntry("Error listing schedules: " + ex.Message, EventLogEntryType.Error);
                throw;
            }
        }

        [JsonRpcMethod]
        public String DeleteSchedule(Int32 id)
        {
            try
            {
                if (id >= 0 && id < _sleepPeriods.Count)
                {
                    _sleepPeriods.RemoveAt(id);
                    ConfigManager.SaveSchedules(_sleepPeriods);

                    // Log success message for schedule deletion
                    EventLog.WriteEntry($"Deleted schedule [{id}].", EventLogEntryType.Information);

                    return $"Deleted schedule [{id}].";
                }
                else
                {
                    throw new ArgumentException($"Invalid schedule ID: {id}");
                }
            }
            catch (Exception ex)
            {
                // Log error during schedule deletion
                EventLog.WriteEntry("Error deleting schedule: " + ex.Message, EventLogEntryType.Error);
                throw;
            }
        }

        private DayOfWeek ParseDayOfWeek(String day)
        {
            try
            {
                return day.ToLower() switch
                {
                    "mon" => DayOfWeek.Monday,
                    "tue" => DayOfWeek.Tuesday,
                    "wed" => DayOfWeek.Wednesday,
                    "thu" => DayOfWeek.Thursday,
                    "fri" => DayOfWeek.Friday,
                    "sat" => DayOfWeek.Saturday,
                    "sun" => DayOfWeek.Sunday,
                    "monday" => DayOfWeek.Monday,
                    "tuesday" => DayOfWeek.Tuesday,
                    "wednesday" => DayOfWeek.Wednesday,
                    "thursday" => DayOfWeek.Thursday,
                    "friday" => DayOfWeek.Friday,
                    "saturday" => DayOfWeek.Saturday,
                    "sunday" => DayOfWeek.Sunday,
                    _ => throw new ArgumentException("Invalid day of the week: " + day)
                };
            }
            catch (Exception ex)
            {
                // Log error during day parsing
                EventLog.WriteEntry("Error parsing day of week: " + ex.Message, EventLogEntryType.Error);
                throw;
            }
        }

        // Native method to prevent system sleep
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_CONTINUOUS = 0x80000000
        }
    }
}