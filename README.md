
# PreventSleep

**PreventSleep** is a simple C# tool that allows users to prevent their Windows system from sleeping for a specific duration or until a given time. This is especially useful when you need to keep your system awake for tasks like serving network traffic or remote desktop sessions.

## Features

- Prevent system sleep for a specific duration (`--prevent-for`).
- Prevent system sleep until a specific date and time (`--prevent-until`).
- Optionally prevent the display from turning off (`--prevent-display-off`).
- Cancel the sleep prevention by pressing `CTRL+C`.

## Usage

### Command Line Arguments

- **Prevent for a specific duration**:
  ```
  PreventSleep.exe --prevent-for <TimeSpan> [--prevent-display-off]
  ```

  Example:
  ```
  PreventSleep.exe --prevent-for 00:30:00 --prevent-display-off
  ```
  This prevents the system from sleeping for 30 minutes and keeps the display on.

- **Prevent until a specific date and time**:
  ```
  PreventSleep.exe --prevent-until <DateTime> [--prevent-display-off]
  ```

  Example:
  ```
  PreventSleep.exe --prevent-until 2024-09-20T10:30:00 --prevent-display-off
  ```
  This prevents the system from sleeping until 10:30 AM on September 20, 2024, while keeping the display on.

- **Prevent until a specific time (e.g., 5 PM)**:
  ```
  PreventSleep.exe --prevent-until <TimeOnly> [--prevent-display-off]
  ```

  Example:
  ```
  PreventSleep.exe --prevent-until 17:00
  ```
  This prevents the system from sleeping until 5 PM today (or tomorrow if 5 PM has already passed today), allowing the display to turn off.

### Cancelling Sleep Prevention

You can cancel the sleep prevention at any time by pressing \`CTRL+C\` in the terminal.

### Schedule with Task Scheduler

You can schedule the **PreventSleep** program to run automatically using the **Windows Task Scheduler**. This allows you to wake the PC from sleep and prevent it from sleeping again for the set period of time.

1. **Open Task Scheduler**:
   - Search for "Task Scheduler" in the Start menu and open it.

2. **Create a New Task**:
   - Click **Create Task** on the right side.

3. **Set General Information**:
   - Give the task a name like "Prevent Sleep Task".
   - Check the option **Run whether user is logged on or not**.

4. **Create a Trigger**:
   - Go to the **Triggers** tab.
   - Click **New** and set the time you want the task to start.
   - Check the box **Wake the computer to run this task**.

5. **Create an Action**:
   - Go to the **Actions** tab.
   - Click **New** and browse to the location of the `PreventSleep.exe`.
   - In the **Add arguments** field, you can provide the options like:
     ```
     --prevent-for 01:00:00 --prevent-display-off
     ```
     This will wake the computer and prevent it from sleeping for 1 hour.

6. **Set Conditions**:
   - Go to the **Conditions** tab.
   - Uncheck **Start the task only if the computer is on AC power** if you want it to run on battery.

7. **Save the Task**:
   - Click **OK** and enter your password to save the task.

Now, the task will wake your PC from sleep and prevent it from going back to sleep for the duration specified.

## Installation

1. **Clone the repository**:
   ```
   git clone https://github.com/robheffo79/PreventSleep.git
   ```

2. **Build the project**:
   Open the project in Visual Studio or build it from the command line:
   ```
   dotnet build
   ```

3. **Run the executable**:
   After building the project, you can run the generated executable from the `bin` folder.

## Examples

### Example 1: Prevent system sleep for 1 hour, allowing the display to turn off
```
PreventSleep.exe --prevent-for 01:00:00
```

### Example 2: Prevent system sleep until 10:30 AM on a specific day, keeping the display on
```
PreventSleep.exe --prevent-until 2024-09-20T10:30:00 --prevent-display-off
```

### Example 3: Prevent system sleep until 5 PM today, allowing the display to turn off
```
PreventSleep.exe --prevent-until 17:00
```

## Support

If you find this tool useful, please consider supporting me by buying me a coffee!  
[![Buy Me a Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-%23FFDD00.svg?style=flat&logo=buy-me-a-coffee&logoColor=black)](https://buymeacoffee.com/robheffo)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE.md) file for details.
