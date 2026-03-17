using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Services;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented;
using LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels.SignalManager
{
    /// <summary>
    /// ViewModel for Signal Manager main window
    /// </summary>
    public class SignalManagerViewModel : ViewModelBase
    {
        private readonly ISignalLibrary _signalLibrary;
        private readonly DAQController _daqController;
        private readonly IEnumerable<UI.Models.DAQDevice> _allDevices;
        
        // Data-Oriented Architecture (ONLY DO - OO removed)
        private readonly DataOrientedSequenceManager _doManager;
        private Guid _currentSequenceId;
        private SignalTableAdapter _currentAdapter;
        private readonly DataOrientedExecutionEngine _doExecutionEngine;

        private SignalSequence _selectedSequence;
        private SignalEvent _selectedEvent;
        private TimelineEventViewModel _selectedTimelineEvent;
        private double _currentTimeSeconds;
        private SignalEvent _selectedEventFromList;
        private string _statusText;
        private string _executionStateText;
        private long _totalDurationNanoseconds;
        private double _zoomLevel;
        private bool _isLoopEnabled;
        private string _executionMode;

        public SignalManagerViewModel(DAQController daqController, IEnumerable<UI.Models.DAQDevice> allDetectedDevices)
        {
            _daqController = daqController ?? throw new ArgumentNullException(nameof(daqController));
            _allDevices = allDetectedDevices ?? throw new ArgumentNullException(nameof(allDetectedDevices));
            _signalLibrary = new SignalLibrary();
            
            // DATA-ORIENTED ARCHITECTURE ONLY (OO code removed)
            _doManager = new DataOrientedSequenceManager();
            System.Console.WriteLine("[VM] Data-Oriented Architecture ONLY - OO removed");
            
            // CRITICAL: Create separate DAQController for each detected device
            var deviceControllers = CreateDeviceControllers(allDetectedDevices);
            _doExecutionEngine = new DataOrientedExecutionEngine(deviceControllers);
            
            _executionMode = "DO (Data-Oriented)";
            System.Console.WriteLine($"[VM] Execution mode: {_executionMode} - EXCLUSIVE");

            // Initialize collections
            Sequences = new ObservableCollection<SignalSequence>();
            SignalCategories = new ObservableCollection<SignalCategoryViewModel>();
            TimelineChannels = new ObservableCollection<TimelineChannelViewModel>();
            EventsList = new ObservableCollection<SignalEvent>();

            // Initialize timeline channels from ALL detected devices
            InitializeTimelineChannels();

            // Initialize signal library UI
            InitializeSignalLibrary();

            // Subscribe to execution events (DO engine ONLY)
            _doExecutionEngine.StateChanged += OnExecutionStateChanged;
            _doExecutionEngine.EventExecuted += OnEventExecuted;
            _doExecutionEngine.ExecutionError += OnExecutionError;

            // Initialize commands
            InitializeCommands();

            StatusText = "Ready";
            ExecutionStateText = "Idle";
            ZoomLevel = 1.0;
            TotalDurationNanoseconds = 10_000_000_000; // 10 segundos por defecto
        }

        /// <summary>
        /// Creates a DAQController for each detected device
        /// </summary>
        private Dictionary<string, DAQController> CreateDeviceControllers(IEnumerable<UI.Models.DAQDevice> devices)
        {
            var controllers = new Dictionary<string, DAQController>();
            
            System.Console.WriteLine($"[SIGNAL MANAGER] Creating DAQControllers for {devices.Count()} devices...");
            
            foreach (var device in devices)
            {
                try
                {
                    // Extract device model (e.g., "PCI-1735U" from "PCI-1735U,BID#3")
                    string deviceModel = device.Name.Contains(",") 
                        ? device.Name.Split(',')[0] 
                        : device.Name;
                    
                    // Create a new DAQController for this device
                    var controller = new DAQController();
                    
                    // Initialize the controller with the device's profile
                    if (!string.IsNullOrEmpty(device.ConfigFile))
                    {
                        System.Console.WriteLine($"[SIGNAL MANAGER] Initializing {deviceModel} (BID#{device.BoardId}) with profile: {device.ConfigFile}");
                        controller.Initialize(device.ConfigFile, device.BoardId);
                        controllers[deviceModel] = controller;
                        System.Console.WriteLine($"[SIGNAL MANAGER] ✓ Controller created for {deviceModel}");
                    }
                    else
                    {
                        System.Console.WriteLine($"[SIGNAL MANAGER WARNING] No config file for {deviceModel}, skipping");
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[SIGNAL MANAGER ERROR] Failed to create controller for {device.Name}: {ex.Message}");
                }
            }
            
            System.Console.WriteLine($"[SIGNAL MANAGER] Created {controllers.Count} device controllers");
            return controllers;
        }

        #region Properties

        public ObservableCollection<SignalSequence> Sequences { get; }
        public ObservableCollection<SignalCategoryViewModel> SignalCategories { get; }
        public ObservableCollection<TimelineChannelViewModel> TimelineChannels { get; }
        public ObservableCollection<SignalEvent> EventsList { get; private set; }

        public bool IsSequenceSelected => SelectedSequence != null;

        public SignalSequence SelectedSequence
        {
            get => _selectedSequence;
            set
            {
                if (SetProperty(ref _selectedSequence, value))
                {
                    OnPropertyChanged(nameof(IsSequenceSelected));
                    UpdateTimeline();
                    if (value != null)
                    {
                        // Use desired duration from metadata, or calculated duration if not set
                        if (value.Metadata.ContainsKey("DesiredDuration"))
                        {
                            TotalDurationSeconds = Convert.ToDouble(value.Metadata["DesiredDuration"]);
                        }
                        else
                        {
                            TotalDurationSeconds = value.TotalDuration.TotalSeconds;
                        }
                    }
                }
            }
        }

        public SignalEvent SelectedEvent
        {
            get => _selectedEvent;
            set
            {
                if (SetProperty(ref _selectedEvent, value))
                {
                    System.Console.WriteLine($"[EVENT] SelectedEvent changed: {value?.Name ?? "null"}");
                    ((RelayCommand)DeleteEventCommand)?.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(SelectedEventStartSeconds));
                    OnPropertyChanged(nameof(SelectedEventDurationMs));
                    OnPropertyChanged(nameof(SelectedEventStartVoltage));
                    OnPropertyChanged(nameof(SelectedEventEndVoltage));
                    OnPropertyChanged(nameof(SelectedEventHasRampVoltages));
                    OnPropertyChanged(nameof(SelectedEventFrequency));
                    OnPropertyChanged(nameof(SelectedEventAmplitude));
                    OnPropertyChanged(nameof(SelectedEventOffset));
                    OnPropertyChanged(nameof(SelectedEventHasWaveformParams));
                }
            }
        }

        public SignalEvent SelectedEventFromList
        {
            get => _selectedEventFromList;
            set
            {
                if (SetProperty(ref _selectedEventFromList, value))
                {
                    if (value != null && SelectedSequence != null)
                    {
                        // DO: Get event directly from current adapter
                        SelectedEvent = value;
                    }
                }
            }
        }

        public TimelineEventViewModel SelectedTimelineEvent
        {
            get => _selectedTimelineEvent;
            set
            {
                if (SetProperty(ref _selectedTimelineEvent, value))
                {
                    // DO: Use event directly from timeline
                    if (value?.SignalEvent != null && SelectedSequence != null)
                    {
                        SelectedEvent = value.SignalEvent;
                        System.Console.WriteLine($"[EVENT] SelectedTimelineEvent changed: {value.SignalEvent?.Name ?? "null"}, StartTime={value.SignalEvent?.StartTime.TotalSeconds:F6}s");
                    }
                    else
                    {
                        SelectedEvent = null;
                        System.Console.WriteLine($"[EVENT] SelectedTimelineEvent changed: null");
                    }
                }
            }
        }

        public double SelectedEventStartSeconds
        {
            get => SelectedEvent?.StartTime.TotalSeconds ?? 0;
            set
            {
                if (SelectedEvent != null)
                {
                    SelectedEvent.StartTime = TimeSpan.FromSeconds(value);
                    OnPropertyChanged();
                }
            }
        }

        public double SelectedEventDurationMs
        {
            get => SelectedEvent?.Duration.TotalMilliseconds ?? 0;
            set
            {
                if (SelectedEvent != null)
                {
                    SelectedEvent.Duration = TimeSpan.FromMilliseconds(value);
                    OnPropertyChanged();
                }
            }
        }

        public double SelectedEventStartVoltage
        {
            get => SelectedEvent?.Parameters.ContainsKey("startVoltage") == true 
                   ? SelectedEvent.Parameters["startVoltage"] 
                   : 0;
            set
            {
                if (SelectedEvent?.Parameters != null)
                {
                    SelectedEvent.Parameters["startVoltage"] = value;
                    OnPropertyChanged();
                    System.Console.WriteLine($"[PARAM CHANGE] startVoltage updated to {value}V for event '{SelectedEvent.Name}'");
                }
            }
        }

        public double SelectedEventEndVoltage
        {
            get => SelectedEvent?.Parameters.ContainsKey("endVoltage") == true 
                   ? SelectedEvent.Parameters["endVoltage"] 
                   : 0;
            set
            {
                if (SelectedEvent?.Parameters != null)
                {
                    SelectedEvent.Parameters["endVoltage"] = value;
                    OnPropertyChanged();
                    System.Console.WriteLine($"[PARAM CHANGE] endVoltage updated to {value}V for event '{SelectedEvent.Name}'");
                }
            }
        }

        public bool SelectedEventHasRampVoltages => SelectedEvent?.EventType == SignalEventType.Ramp;

        // Waveform parameters (frequency, amplitude, offset)
        public double SelectedEventFrequency
        {
            get => SelectedEvent?.Parameters.ContainsKey("frequency") == true 
                   ? SelectedEvent.Parameters["frequency"] 
                   : 1000;
            set
            {
                if (SelectedEvent?.Parameters != null)
                {
                    SelectedEvent.Parameters["frequency"] = value;
                    OnPropertyChanged();
                    System.Console.WriteLine($"[PARAM CHANGE] frequency updated to {value}Hz for event '{SelectedEvent.Name}'");
                }
            }
        }

        public double SelectedEventAmplitude
        {
            get => SelectedEvent?.Parameters.ContainsKey("amplitude") == true 
                   ? SelectedEvent.Parameters["amplitude"] 
                   : 2.0;
            set
            {
                if (SelectedEvent?.Parameters != null)
                {
                    SelectedEvent.Parameters["amplitude"] = value;
                    OnPropertyChanged();
                    System.Console.WriteLine($"[PARAM CHANGE] amplitude updated to {value}V for event '{SelectedEvent.Name}'");
                }
            }
        }

        public double SelectedEventOffset
        {
            get => SelectedEvent?.Parameters.ContainsKey("offset") == true 
                   ? SelectedEvent.Parameters["offset"] 
                   : 5.0;
            set
            {
                if (SelectedEvent?.Parameters != null)
                {
                    SelectedEvent.Parameters["offset"] = value;
                    OnPropertyChanged();
                    System.Console.WriteLine($"[PARAM CHANGE] offset updated to {value}V for event '{SelectedEvent.Name}'");
                }
            }
        }

        public bool SelectedEventHasWaveformParams => SelectedEvent?.EventType == SignalEventType.Waveform;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ExecutionStateText
        {
            get => _executionStateText;
            set => SetProperty(ref _executionStateText, value);
        }

        public double CurrentTimeSeconds
        {
            get => _currentTimeSeconds;
            set => SetProperty(ref _currentTimeSeconds, value);
        }

        public long TotalDurationNanoseconds
        {
            get => _totalDurationNanoseconds;
            set
            {
                if (SetProperty(ref _totalDurationNanoseconds, value))
                {
                    OnPropertyChanged(nameof(TotalDurationSeconds));
                }
            }
        }

        public double TotalDurationSeconds
        {
            get => _totalDurationNanoseconds / 1e9;
            set => TotalDurationNanoseconds = (long)(value * 1e9);
        }

        public string CurrentTimeText => TimeSpan.FromSeconds(CurrentTimeSeconds).ToString(@"mm\:ss\.fff");

        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (SetProperty(ref _zoomLevel, value))
                {
                    OnPropertyChanged(nameof(ZoomLevelText));
                    OnPropertyChanged(nameof(TimelineWidth));
                }
            }
        }

        public string ZoomLevelText => $"{_zoomLevel:F1}";
        public double TimelineWidth => 800 * _zoomLevel;

        // Playback speed locked at 1X (real-time: 1,000,000,000 ns/s)
        public string PlaybackSpeedText => "1X";

        public bool IsLoopEnabled
        {
            get => _isLoopEnabled;
            set
            {
                if (SetProperty(ref _isLoopEnabled, value))
                {
                    System.Console.WriteLine($"[LOOP CONTROL] Loop enabled: {value}");
                    _doExecutionEngine.IsLoopEnabled = value;
                }
            }
        }

        public string ExecutionMode
        {
            get => _executionMode;
            set => SetProperty(ref _executionMode, value);
        }

        // DO mode is now the only mode (property kept for backward compat)
        public bool UseDataOrientedExecution => true;

        #endregion

        #region Commands

        public ICommand NewSequenceCommand { get; private set; }
        public ICommand OpenSequenceCommand { get; private set; }
        public ICommand SaveSequenceCommand { get; private set; }
        public ICommand SaveSequenceAsCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }
        public ICommand AddEventCommand { get; private set; }
        public RelayCommand DeleteEventCommand { get; private set; }
        public ICommand DuplicateEventCommand { get; private set; }
        public ICommand ValidateCommand { get; private set; }
        public ICommand ExecuteCommand { get; private set; }
        public ICommand PlayCommand { get; private set; }
        public ICommand PauseCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand ApplyEventChangesCommand { get; private set; }
        public ICommand ZoomInCommand { get; private set; }
        public ICommand ZoomOutCommand { get; private set; }

        private void InitializeCommands()
        {
            NewSequenceCommand = new RelayCommand(OnNewSequence);
            OpenSequenceCommand = new RelayCommand(OnOpenSequence);
            SaveSequenceCommand = new RelayCommand(OnSaveSequence, () => SelectedSequence != null);
            SaveSequenceAsCommand = new RelayCommand(OnSaveSequenceAs, () => SelectedSequence != null);
            CloseCommand = new RelayCommand(OnClose);
            AddEventCommand = new RelayCommand(OnAddEvent, () => SelectedSequence != null);
            DeleteEventCommand = new RelayCommand(OnDeleteEvent, () => SelectedEvent != null && SelectedTimelineEvent != null);
            DuplicateEventCommand = new RelayCommand(OnDuplicateEvent, () => SelectedEvent != null);
            ValidateCommand = new RelayCommand(OnValidate, () => SelectedSequence != null);
            ExecuteCommand = new RelayCommand(OnExecute, () => SelectedSequence != null);
            PlayCommand = new RelayCommand(OnPlay, () => SelectedSequence != null);
            PauseCommand = new RelayCommand(OnPause);
            StopCommand = new RelayCommand(OnStop);
            ApplyEventChangesCommand = new RelayCommand(OnApplyEventChanges, () => SelectedEvent != null);
            ZoomInCommand = new RelayCommand(OnZoomIn);
            ZoomOutCommand = new RelayCommand(OnZoomOut);
        }

        #endregion

        #region Command Handlers

        private void OnNewSequence()
        {
            System.Console.WriteLine("[SEQUENCE] Opening New Sequence dialog...");
            
            var dialog = new UI.WPF.Views.SignalManager.NewSequenceDialog();
            if (dialog.ShowDialog() == true)
            {
                System.Console.WriteLine($"[SEQUENCE] Creating sequence: {dialog.SequenceName}, Duration: {dialog.DurationSeconds}s");
                
                // DATA-ORIENTED ONLY: Create DO sequence
                _currentSequenceId = _doManager.CreateSequence(dialog.SequenceName, dialog.Description);
                _currentAdapter = new SignalTableAdapter(_doManager, _currentSequenceId);
                System.Console.WriteLine($"[DO SEQUENCE] Created DO sequence with ID: {_currentSequenceId}");
                
                // Create SignalSequence for UI binding
                var sequence = new SignalSequence
                {
                    SequenceId = _currentSequenceId.ToString(),
                    Name = dialog.SequenceName,
                    Description = dialog.Description,
                    Metadata = new Dictionary<string, object> { ["DesiredDuration"] = dialog.DurationSeconds }
                };
                
                Sequences.Add(sequence);
                SelectedSequence = sequence;
                
                // Update TotalDurationSeconds for UI
                TotalDurationSeconds = dialog.DurationSeconds;
                
                System.Console.WriteLine($"[SEQUENCE SUCCESS] Sequence created (OO + DO hybrid): {sequence.Name}");
                StatusText = $"Created new sequence: {dialog.SequenceName} ({dialog.DurationSeconds}s) [Data-Oriented]";
            }
            else
            {
                System.Console.WriteLine("[SEQUENCE] New sequence cancelled by user");
            }
        }

        private void OnOpenSequence()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Sequence Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Open Sequence"
            };

            if (dialog.ShowDialog() == true)
            {
                // TODO: Implement DO-based Load
                MessageBox.Show("Load sequence not yet implemented in DO mode", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "Load feature pending DO implementation";
            }
        }

        private void OnSaveSequence()
        {
            if (SelectedSequence == null) return;

            // If never saved, prompt for location
            if (!SelectedSequence.Metadata.ContainsKey("FilePath") || string.IsNullOrEmpty(SelectedSequence.Metadata["FilePath"] as string))
            {
                OnSaveSequenceAs();
                return;
            }

            try
            {
                string filePath = SelectedSequence.Metadata["FilePath"] as string;
                Guid sequenceId = Guid.Parse(SelectedSequence.SequenceId);
                _doManager.SaveSequence(sequenceId, filePath);
                StatusText = $"Saved sequence '{SelectedSequence.Name}' to {filePath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save sequence: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "Save failed";
            }
        }

        private void OnSaveSequenceAs()
        {
            if (SelectedSequence == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "Sequence Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Save Sequence As",
                FileName = SelectedSequence.Name + ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Guid sequenceId = Guid.Parse(SelectedSequence.SequenceId);
                    _doManager.SaveSequence(sequenceId, dialog.FileName);
                    SelectedSequence.Metadata["FilePath"] = dialog.FileName;
                    StatusText = $"Saved sequence '{SelectedSequence.Name}' to {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save sequence: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText = "Save failed";
                }
            }
        }

        private void OnClose()
        {
            Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this)?.Close();
        }

        private void OnAddEvent()
        {
            if (SelectedSequence == null) return;

            // TODO: Implement manual event add in DO mode
            StatusText = "Manual event add not yet implemented in DO mode";
        }

        private void OnDeleteEvent()
        {
            if (SelectedEvent == null || SelectedTimelineEvent == null || SelectedSequence == null)
            {
                System.Console.WriteLine("[DELETE EVENT ERROR] Missing selection");
                return;
            }

            System.Console.WriteLine($"[DELETE EVENT CMD] OnDeleteEvent called for: {SelectedEvent.Name} (ID: {SelectedEvent.EventId})");

            var result = MessageBox.Show(
                $"Delete event '{SelectedEvent.Name}' at {SelectedEvent.StartTime.TotalSeconds:F1}s?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                System.Console.WriteLine($"[DELETE EVENT CMD] User confirmed deletion, calling DeleteEvent...");
                
                // Use the proper DeleteEvent method that removes from DO system
                bool success = DeleteEvent(SelectedEvent.EventId);
                
                if (!success)
                {
                    MessageBox.Show(
                        "Failed to delete event. Please check the logs.",
                        "Delete Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                System.Console.WriteLine("[DELETE EVENT CMD] Cancelled by user");
            }
        }

        private void OnDuplicateEvent()
        {
            if (SelectedEvent == null || SelectedSequence == null) return;

            // TODO: Implement duplicate in DO mode
            StatusText = "Duplicate not yet implemented in DO mode";
        }

        private void OnValidate()
        {
            if (SelectedSequence == null) return;

            // DO mode: Basic validation
            if (_currentAdapter != null && _currentAdapter.Table.Count > 0)
            {
                MessageBox.Show($"Sequence valid: {_currentAdapter.Table.Count} events", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "Validation passed";
            }
            else
            {
                MessageBox.Show("No events in sequence", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusText = "Validation: empty sequence";
            }
        }

        private void OnExecute()
        {
            OnPlay();
        }

        private async void OnPlay()
        {
            if (SelectedSequence == null) return;

            // DO mode: Check if table has events
            if (_currentAdapter == null || _currentAdapter.Table.Count == 0)
            {
                MessageBox.Show("Cannot execute empty sequence",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var startTime = System.Diagnostics.Stopwatch.StartNew();
                
                // DATA-ORIENTED EXECUTION ONLY
                System.Console.WriteLine($"[EXEC] Using DO execution engine for sequence: {SelectedSequence.Name}");
            System.Console.WriteLine($"[EXEC] Configured sequence duration: {TotalDurationNanoseconds}ns ({TotalDurationSeconds:F1}s)");
            
            var table = _doManager.GetSignalTable(_currentSequenceId);
            if (table != null)
            {
                // CRITICAL: Pass configured duration to execution engine
                await _doExecutionEngine.ExecuteTableAsync(table, TotalDurationNanoseconds, CancellationToken.None);
            }    
                startTime.Stop();
                System.Console.WriteLine($"[EXEC PERF] DO execution completed in {startTime.ElapsedMilliseconds}ms");
                StatusText = $"Sequence completed (DO: {startTime.ElapsedMilliseconds}ms)";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Execution error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = $"Execution error: {ex.Message}";
                System.Console.WriteLine($"[EXEC ERROR] {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnStop()
        {
            _doExecutionEngine.Stop();
            StatusText = "Execution stopped";
        }

        private void OnPause()
        {
            // TODO: Implement pause in DO engine
            StatusText = "Pause not yet implemented in DO mode";
        }

        private void OnApplyEventChanges()
        {
            if (SelectedEvent == null || SelectedSequence == null || _currentAdapter == null) return;

            System.Console.WriteLine($"[APPLY CHANGES] Updating event ID={SelectedEvent.EventId}: StartTime={SelectedEvent.StartTime.TotalSeconds:F6}s, Duration={SelectedEvent.Duration.TotalSeconds:F6}s");
            
            // Find event index in DO table by matching EventId
            var table = _currentAdapter.Table;
            bool found = false;
            for (int i = 0; i < table.Count; i++)
            {
                if (table.EventIds[i].ToString() == SelectedEvent.EventId)
                {
                    System.Console.WriteLine($"[APPLY CHANGES] Found event at index {i}, updating...");
                    
                    // Update DO table directly
                    table.StartTimesNs[i] = (long)(SelectedEvent.StartTime.TotalSeconds * 1e9);
                    table.DurationsNs[i] = (long)(SelectedEvent.Duration.TotalSeconds * 1e9);
                    
                    if (SelectedEvent.EventType == SignalEventType.Ramp)
                    {
                        double startV = SelectedEvent.Parameters.ContainsKey("startVoltage") ? SelectedEvent.Parameters["startVoltage"] : 0;
                        double endV = SelectedEvent.Parameters.ContainsKey("endVoltage") ? SelectedEvent.Parameters["endVoltage"] : 0;
                        table.Attributes.SetStartVoltage(i, startV);
                        table.Attributes.SetEndVoltage(i, endV);
                        System.Console.WriteLine($"[APPLY CHANGES] Updated voltages: {startV}V → {endV}V");
                    }
                    else if (SelectedEvent.EventType == SignalEventType.Waveform)
                    {
                        double freq = SelectedEvent.Parameters.ContainsKey("frequency") ? SelectedEvent.Parameters["frequency"] : 1000;
                        double amp = SelectedEvent.Parameters.ContainsKey("amplitude") ? SelectedEvent.Parameters["amplitude"] : 2.0;
                        double offset = SelectedEvent.Parameters.ContainsKey("offset") ? SelectedEvent.Parameters["offset"] : 5.0;
                        table.Attributes.SetWaveformParams(i, freq, amp, offset);
                        System.Console.WriteLine($"[APPLY CHANGES] Updated waveform: {freq}Hz, {amp}V amp, {offset}V offset");
                    }
                    
                    System.Console.WriteLine($"[APPLY CHANGES] Successfully updated DO table at index {i}");
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                System.Console.WriteLine($"[APPLY CHANGES ERROR] Event ID {SelectedEvent.EventId} not found in DO table (Count={table.Count})");
            }
            
            UpdateTimeline();
            StatusText = found ? "Event updated in DO table" : "Error: Event not found";
        }

        private void OnZoomIn()
        {
            // Zoom gradual: +20% por click
            if (ZoomLevel < 100)
            {
                ZoomLevel = Math.Min(100, ZoomLevel * 1.2);
                StatusText = $"Zoom: {ZoomLevelText}X";
                System.Console.WriteLine($"[ZOOM BTN+] Level: {ZoomLevel:F2}X");
            }
        }

        private void OnZoomOut()
        {
            // Zoom gradual: -20% por click
            if (ZoomLevel > 0.1)
            {
                ZoomLevel = Math.Max(0.1, ZoomLevel / 1.2);
                StatusText = $"Zoom: {ZoomLevelText}X";
                System.Console.WriteLine($"[ZOOM BTN-] Level: {ZoomLevel:F2}X");
            }
        }

        #endregion

        #region Private Methods

        private void InitializeSignalLibrary()
        {
            System.Console.WriteLine($"[SIGNAL LIBRARY] Initializing signal library...");
            var categories = _signalLibrary.GetCategories();
            System.Console.WriteLine($"[SIGNAL LIBRARY] Found {categories.Count} categories: {string.Join(", ", categories)}");
            
            foreach (var category in categories)
            {
                var signals = _signalLibrary.GetSignalsByCategory(category);
                System.Console.WriteLine($"[SIGNAL LIBRARY] Category '{category}': {signals.Count} signals");
                foreach (var signal in signals.Take(2))
                {
                    System.Console.WriteLine($"[SIGNAL LIBRARY]   - {signal.Name} (Type: {signal.EventType}, Device: {signal.DeviceType})");
                }
                
                SignalCategories.Add(new SignalCategoryViewModel
                {
                    CategoryName = category,
                    Signals = new ObservableCollection<SignalEvent>(signals)
                });
            }
            System.Console.WriteLine($"[SIGNAL LIBRARY] Initialization complete. Total categories in UI: {SignalCategories.Count}");
        }

        private void UpdateTimeline()
        {
            System.Console.WriteLine($"[UPDATE TIMELINE] Starting timeline update...");
            
            if (SelectedSequence == null || _currentAdapter == null)
            {
                System.Console.WriteLine($"[UPDATE TIMELINE] No sequence or adapter");
                EventsList.Clear();
                return;
            }

            // DO MODE: Read events from DO table via adapter
            var events = _currentAdapter.GetAllEvents();
            System.Console.WriteLine($"[UPDATE TIMELINE] Processing {events.Count} events from DO table for sequence '{SelectedSequence.Name}'");
            System.Console.WriteLine($"[UPDATE TIMELINE] Using grid duration: {TotalDurationSeconds}s ({TotalDurationNanoseconds}ns)");

            // CRITICAL: Save current selection EventId BEFORE clearing ViewModels
            string selectedEventId = SelectedTimelineEvent?.SignalEvent?.EventId;
            System.Console.WriteLine($"[UPDATE TIMELINE] Current selection EventId: {selectedEventId ?? "null"}");

            // Update EventsList with all events from DO table
            EventsList.Clear();
            foreach (var evt in events.OrderBy(e => e.StartTime))
            {
                EventsList.Add(evt);
            }

            // CRITICAL: Clear all events from timeline and force UI refresh
            // This destroys old ViewModels - any references become STALE
            foreach (var channel in TimelineChannels)
            {
                channel.ClearEvents();
            }
            
            // CRITICAL: Clear stale selections BEFORE recreating ViewModels
            System.Console.WriteLine($"[UPDATE TIMELINE] Clearing stale ViewModel selections");
            SelectedTimelineEvent = null;
            SelectedEvent = null;
            
            // Force property changed to refresh binding
            OnPropertyChanged(nameof(TimelineChannels));

            // Add events to appropriate channels (creates NEW ViewModels)
            foreach (var evt in events)
            {
                var targetChannel = TimelineChannels.FirstOrDefault(ch => 
                    ch.ChannelNumber == evt.Channel && 
                    ch.DeviceType == evt.DeviceType &&
                    ch.DeviceModel == evt.DeviceModel);

                if (targetChannel != null)
                {
                    System.Console.WriteLine($"[UPDATE TIMELINE] Adding event '{evt.Name}' to channel {targetChannel.ChannelName} (Type: {targetChannel.DeviceType})");
                    targetChannel.AddEvent(evt, TotalDurationSeconds);
                }
            }

            // OPTIONAL: Restore selection if event still exists in DO system
            if (!string.IsNullOrEmpty(selectedEventId))
            {
                var stillExists = _currentAdapter.FindEventById(selectedEventId);
                if (stillExists != null)
                {
                    System.Console.WriteLine($"[UPDATE TIMELINE] Re-selecting event {selectedEventId} after refresh");
                    SelectedEvent = stillExists;
                    
                    // Find NEW ViewModel for this event
                    foreach (var channel in TimelineChannels)
                    {
                        var eventVm = channel.Events.FirstOrDefault(e => e.SignalEvent.EventId == selectedEventId);
                        if (eventVm != null)
                        {
                            SelectedTimelineEvent = eventVm;
                            System.Console.WriteLine($"[UPDATE TIMELINE] Re-selected NEW ViewModel for {stillExists.Name}");
                            break;
                        }
                    }
                }
                else
                {
                    System.Console.WriteLine($"[UPDATE TIMELINE] Previous selection {selectedEventId} no longer exists in DO system");
                }
            }

            System.Console.WriteLine($"[UPDATE TIMELINE] Timeline update complete");
        }

        /// <summary>
        /// Initializes timeline channels from ALL detected DAQ devices
        /// </summary>
        private void InitializeTimelineChannels()
        {
            System.Console.WriteLine("[TIMELINE INIT] Starting timeline initialization...");
            TimelineChannels.Clear();

            if (_allDevices == null || !_allDevices.Any())
            {
                System.Console.WriteLine("[TIMELINE INIT ERROR] No devices detected");
                StatusText = "No devices detected. Please detect devices first.";
                return;
            }

            System.Console.WriteLine($"[TIMELINE INIT] Found {_allDevices.Count()} devices to process");

            // Iterate through ALL detected devices and create channels for each
            foreach (var device in _allDevices.OrderBy(d => d.Name))
            {
                // Extract clean model name (e.g., "PCI-1735U" from "PCI-1735U,BID#3")
                string deviceModel = device.Name.Contains(",") 
                    ? device.Name.Split(',')[0] 
                    : device.Name;

                System.Console.WriteLine($"[TIMELINE INIT] Processing device: {deviceModel}, Type: {device.DeviceType}, BoardId: {device.BoardId}");

                // Determine channel count based on device type
                // PCI-1735U: 32 digital channels (4 ports x 8 bits)
                // PCIe-1824: 32 analog channels
                int channelCount = 32; // Standard for both devices

                // Create a channel entry for each channel of this device
                for (int ch = 0; ch < channelCount; ch++)
                {
                    var channelVM = new TimelineChannelViewModel(
                        deviceModel,
                        device.BoardId,
                        ch,
                        device.DeviceType
                    );
                    TimelineChannels.Add(channelVM);
                    
                    if (ch < 3) // Log first 3 channels
                    {
                        System.Console.WriteLine($"[TIMELINE INIT] Created channel: {channelVM.ChannelName}, Type: {channelVM.DeviceType}");
                    }
                }
                System.Console.WriteLine($"[TIMELINE INIT] Added {channelCount} channels for {deviceModel}");
            }

            System.Console.WriteLine($"[TIMELINE INIT] COMPLETE: {TimelineChannels.Count} total channels created");
            System.Console.WriteLine($"[TIMELINE INIT] Digital channels: {TimelineChannels.Count(c => c.DeviceType == Core.DAQ.Models.DeviceType.Digital)}");
            System.Console.WriteLine($"[TIMELINE INIT] Analog channels: {TimelineChannels.Count(c => c.DeviceType == Core.DAQ.Models.DeviceType.Analog)}");
            
            StatusText = $"Initialized {TimelineChannels.Count} channels from {_allDevices.Count()} devices";
        }

        /// <summary>
        /// Adds a signal from library to a specific channel
        /// </summary>
        public bool MoveEventToChannel(SignalEvent existingEvent, TimelineChannelViewModel targetChannel, TimeSpan newStartTime)
        {
            System.Console.WriteLine($"[MOVE EVENT] MoveEventToChannel called: Event={existingEvent.Name}, From CH{existingEvent.Channel} to {targetChannel.ChannelName}, NewStart={newStartTime.TotalSeconds:F3}s");
            
            if (SelectedSequence == null)
            {
                System.Console.WriteLine($"[MOVE EVENT ERROR] No sequence selected");
                StatusText = "No sequence selected";
                return false;
            }

            // Validate signal type matches channel type
            if (existingEvent.DeviceType != targetChannel.DeviceType)
            {
                System.Console.WriteLine($"[MOVE EVENT ERROR] Type mismatch: Event={existingEvent.DeviceType}, Channel={targetChannel.DeviceType}");
                StatusText = $"Cannot move {existingEvent.DeviceType} event to {targetChannel.DeviceType} channel";
                MessageBox.Show(
                    $"Cannot move {existingEvent.DeviceType} event '{existingEvent.Name}' to {targetChannel.DeviceType} channel '{targetChannel.ChannelName}'.\n\nEvent type must match channel type.",
                    "Type Mismatch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Use event directly (DO mode)
            var realEvent = existingEvent;

            // Store old channel for removal
            int oldChannel = realEvent.Channel;
            string oldDeviceModel = realEvent.DeviceModel;

            // Update event properties
            realEvent.Channel = targetChannel.ChannelNumber;
            realEvent.DeviceModel = targetChannel.DeviceModel;
            realEvent.StartTime = newStartTime;

            // Check for conflicts in target channel
            if (targetChannel.HasConflict(realEvent))
            {
                System.Console.WriteLine($"[MOVE EVENT ERROR] Time conflict on target channel");
                // Restore original values
                realEvent.Channel = oldChannel;
                realEvent.DeviceModel = oldDeviceModel;
                StatusText = $"Cannot move: Time conflict on {targetChannel.ChannelName}";
                MessageBox.Show(
                    $"Cannot move event to {targetChannel.ChannelName} at {newStartTime.TotalSeconds:F1}s.\n\nThere is already an event in that time range.",
                    "Time Conflict",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // DATA-ORIENTED: Update in DO system
            if (_currentAdapter != null)
            {
                System.Console.WriteLine($"[MOVE EVENT] Updating in DO system...");
                _currentAdapter.UpdateEvent(realEvent.EventId, realEvent);
            }
            
            // Refresh timeline to show in new position
            UpdateTimeline();
            
            System.Console.WriteLine($"[MOVE EVENT SUCCESS] Moved '{realEvent.Name}' from CH{oldChannel} to {targetChannel.ChannelName} @ {newStartTime.TotalSeconds:F3}s");
            StatusText = $"Moved {realEvent.Name} to {targetChannel.ChannelName} at {newStartTime.TotalSeconds:F1}s [DO]";
            return true;
        }

        public bool AddSignalToChannel(SignalEvent templateEvent, TimelineChannelViewModel targetChannel, TimeSpan startTime)
        {
            System.Console.WriteLine($"[ADD SIGNAL] AddSignalToChannel called: Signal={templateEvent.Name}, Channel={targetChannel.ChannelName}, StartTime={startTime.TotalSeconds:F2}s");
            
            if (SelectedSequence == null)
            {
                System.Console.WriteLine($"[ADD SIGNAL ERROR] No sequence selected");
                StatusText = "No sequence selected";
                return false;
            }
            System.Console.WriteLine($"[ADD SIGNAL] Sequence: {SelectedSequence.Name}, Duration: {SelectedSequence.TotalDuration.TotalSeconds:F0}s");
            System.Console.WriteLine($"[ADD SIGNAL] Target channel: {targetChannel.ChannelName}, DeviceType={targetChannel.DeviceType}, DeviceModel={targetChannel.DeviceModel}");
            System.Console.WriteLine($"[ADD SIGNAL] Signal DeviceType: {templateEvent.DeviceType}");
            
            // CRITICAL FIX: Validate signal type matches channel type
            if (templateEvent.DeviceType != targetChannel.DeviceType)
            {
                System.Console.WriteLine($"[ADD SIGNAL ERROR] Signal type mismatch: Signal={templateEvent.DeviceType}, Channel={targetChannel.DeviceType}");
                StatusText = $"Cannot add {templateEvent.DeviceType} signal to {targetChannel.DeviceType} channel";
                MessageBox.Show(
                    $"Cannot add {templateEvent.DeviceType} signal '{templateEvent.Name}' to {targetChannel.DeviceType} channel '{targetChannel.ChannelName}'.\n\nPlease use signals matching the channel type.",
                    "Signal Type Mismatch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            System.Console.WriteLine($"[ADD SIGNAL] Signal type matches channel type: {targetChannel.DeviceType}");

            // Create new event from template
            // CRITICAL: Must include DeviceModel to distinguish between devices
            var newEvent = new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = templateEvent.Name,
                StartTime = startTime,
                Duration = templateEvent.Duration,
                Channel = targetChannel.ChannelNumber,
                DeviceType = targetChannel.DeviceType,
                DeviceModel = targetChannel.DeviceModel, // CRITICAL for separation
                EventType = templateEvent.EventType,
                Parameters = new Dictionary<string, double>(templateEvent.Parameters),
                Description = templateEvent.Description,
                Color = templateEvent.Color
            };

            // Check for conflicts
            System.Console.WriteLine($"[ADD SIGNAL] Checking for time conflicts...");
            if (targetChannel.HasConflict(newEvent))
            {
                System.Console.WriteLine($"[ADD SIGNAL ERROR] Time conflict detected on channel {targetChannel.ChannelName}");
                System.Console.WriteLine($"[ADD SIGNAL ERROR] Attempted: {startTime.TotalSeconds:F2}s - {(startTime + templateEvent.Duration).TotalSeconds:F2}s");
                StatusText = $"Cannot add event: Time conflict on {targetChannel.ChannelName}";
                MessageBox.Show(
                    $"Cannot add event at {startTime.TotalSeconds:F1}s on {targetChannel.ChannelName}.\n\nThere is already an event in that time range.\n\nLa señal se sobrepondría con otra existente.",
                    "Time Conflict",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            System.Console.WriteLine($"[ADD SIGNAL] No conflicts found");
            
            // WARNING: Validate voltage continuity for analog ramps (but allow override)
            if (targetChannel.DeviceType == Core.DAQ.Models.DeviceType.Analog && newEvent.EventType == SignalEventType.Ramp)
            {
                var continuityCheck = ValidateVoltageContinuity(targetChannel, newEvent);
                if (!continuityCheck.IsValid)
                {
                    System.Console.WriteLine($"[ADD SIGNAL WARNING] Voltage discontinuity detected");
                    System.Console.WriteLine($"[ADD SIGNAL WARNING] {continuityCheck.ErrorMessage}");
                    StatusText = "WARNING: Voltage discontinuity detected";
                    
                    var result = MessageBox.Show(
                        continuityCheck.ErrorMessage + "\n\n¿Desea agregar la señal de todos modos?",
                        "⚠️ Advertencia: Discontinuidad de Voltaje",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.No)
                    {
                        System.Console.WriteLine($"[ADD SIGNAL] User cancelled due to voltage discontinuity");
                        return false;
                    }
                    
                    System.Console.WriteLine($"[ADD SIGNAL] User confirmed - proceeding despite voltage discontinuity");
                }
                else
                {
                    System.Console.WriteLine($"[ADD SIGNAL] Voltage continuity validated");
                }
            }

            // DATA-ORIENTED: Add to DO system
            if (_currentAdapter != null)
            {
                System.Console.WriteLine($"[ADD SIGNAL] Adding to DO system...");
                _currentAdapter.AddEvent(newEvent);
                System.Console.WriteLine($"[ADD SIGNAL] DO Count: {_currentAdapter.Count}");
            }
            
            System.Console.WriteLine($"[ADD SIGNAL] Adding to timeline channel (Grid: {TotalDurationSeconds}s)...");
            targetChannel.AddEvent(newEvent, TotalDurationSeconds);
            
            System.Console.WriteLine($"[ADD SIGNAL SUCCESS] Event added: {newEvent.Name} -> {targetChannel.ChannelName} @ {startTime.TotalSeconds:F2}s");
            StatusText = $"Added {newEvent.Name} to {targetChannel.ChannelName} at {startTime.TotalSeconds:F1}s [DO]";
            return true;
        }

        private void OnExecutionStateChanged(object sender, ExecutionStateChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ExecutionStateText = e.NewState.ToString();
            });
        }

        private void OnEventExecuted(object sender, EventExecutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentTimeSeconds = e.ActualTime.TotalSeconds;
                OnPropertyChanged(nameof(CurrentTimeText));
            });
        }

        private void OnExecutionError(object sender, ExecutionErrorEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = $"Error executing {e.Event.Name}: {e.Error.Message}";
            });
        }
        
        /// <summary>
        /// Gets the final voltage of an event (for continuity validation)
        /// </summary>
        private double? GetEventFinalVoltage(SignalEvent evt)
        {
            if (evt.EventType == SignalEventType.Ramp)
            {
                if (evt.Parameters.ContainsKey("endVoltage"))
                    return evt.Parameters["endVoltage"];
            }
            else if (evt.EventType == SignalEventType.DC)
            {
                if (evt.Parameters.ContainsKey("voltage"))
                    return evt.Parameters["voltage"];
            }
            return null;
        }
        
        /// <summary>
        /// Gets the initial voltage of an event (for continuity validation)
        /// </summary>
        private double? GetEventInitialVoltage(SignalEvent evt)
        {
            if (evt.EventType == SignalEventType.Ramp)
            {
                if (evt.Parameters.ContainsKey("startVoltage"))
                    return evt.Parameters["startVoltage"];
            }
            else if (evt.EventType == SignalEventType.DC)
            {
                if (evt.Parameters.ContainsKey("voltage"))
                    return evt.Parameters["voltage"];
            }
            return null;
        }
        
        /// <summary>
        /// Validates voltage continuity when adding/moving events
        /// </summary>
        private (bool IsValid, string ErrorMessage) ValidateVoltageContinuity(TimelineChannelViewModel channel, SignalEvent newEvent)
        {
            const double VOLTAGE_TOLERANCE = 0.01; // 10mV tolerance
            
            double? newEventStartV = GetEventInitialVoltage(newEvent);
            if (!newEventStartV.HasValue)
                return (true, ""); // Not a voltage event, skip validation
            
            // Find events on this channel from DO table
            var channelEvents = _currentAdapter.GetAllEvents()
                .Where(e => e.Channel == channel.ChannelNumber && 
                           e.DeviceModel == channel.DeviceModel &&
                           e.EventType == SignalEventType.Ramp)
                .OrderBy(e => e.StartTime)
                .ToList();
            
            // Find the event immediately before the new event
            var previousEvent = channelEvents
                .Where(e => (e.StartTime + e.Duration) <= newEvent.StartTime)
                .OrderByDescending(e => e.StartTime + e.Duration)
                .FirstOrDefault();
            
            if (previousEvent != null)
            {
                double? prevEventEndV = GetEventFinalVoltage(previousEvent);
                if (prevEventEndV.HasValue)
                {
                    double gap = Math.Abs(prevEventEndV.Value - newEventStartV.Value);
                    if (gap > VOLTAGE_TOLERANCE)
                    {
                        return (false, 
                            $"DISCONTINUIDAD DE VOLTAJE DETECTADA:\n\n" +
                            $"La señal anterior '{previousEvent.Name}' termina en {prevEventEndV.Value:F2}V a t={previousEvent.StartTime.TotalSeconds + previousEvent.Duration.TotalSeconds:F2}s\n\n" +
                            $"La nueva señal '{newEvent.Name}' comienza en {newEventStartV.Value:F2}V a t={newEvent.StartTime.TotalSeconds:F2}s\n\n" +
                            $"Salto discontinuo: {gap:F2}V\n\n" +
                            $"SOLUCIÓN: La rampa debe comenzar desde {prevEventEndV.Value:F2}V para mantener continuidad.");
                    }
                }
            }
            
            // Find the event immediately after the new event
            var nextEvent = channelEvents
                .Where(e => e.StartTime >= (newEvent.StartTime + newEvent.Duration))
                .OrderBy(e => e.StartTime)
                .FirstOrDefault();
            
            if (nextEvent != null)
            {
                double? newEventEndV = GetEventFinalVoltage(newEvent);
                double? nextEventStartV = GetEventInitialVoltage(nextEvent);
                
                if (newEventEndV.HasValue && nextEventStartV.HasValue)
                {
                    double gap = Math.Abs(newEventEndV.Value - nextEventStartV.Value);
                    if (gap > VOLTAGE_TOLERANCE)
                    {
                        return (false,
                            $"DISCONTINUIDAD DE VOLTAJE DETECTADA:\n\n" +
                            $"La nueva señal '{newEvent.Name}' termina en {newEventEndV.Value:F2}V a t={newEvent.StartTime.TotalSeconds + newEvent.Duration.TotalSeconds:F2}s\n\n" +
                            $"La señal siguiente '{nextEvent.Name}' comienza en {nextEventStartV.Value:F2}V a t={nextEvent.StartTime.TotalSeconds:F2}s\n\n" +
                            $"Salto discontinuo: {gap:F2}V\n\n" +
                            $"SOLUCIÓN: La rampa debe terminar en {nextEventStartV.Value:F2}V para mantener continuidad.");
                    }
                }
            }
            
            return (true, "");
        }

        /// <summary>
        /// Deletes an event from the current sequence
        /// </summary>
        public bool DeleteEvent(string eventId)
        {
            System.Console.WriteLine($"[DELETE EVENT] DeleteEvent called for EventId: {eventId}");
            
            if (SelectedSequence == null || _currentAdapter == null)
            {
                System.Console.WriteLine($"[DELETE EVENT ERROR] No sequence or adapter");
                return false;
            }

            try
            {
                // Verify event exists before deletion
                System.Console.WriteLine($"[DELETE EVENT] Count before: {_currentAdapter.Count}");
                var eventToDelete = _currentAdapter.FindEventById(eventId);
                if (eventToDelete == null)
                {
                    System.Console.WriteLine($"[DELETE EVENT ERROR] Event {eventId} not found in adapter");
                    StatusText = $"Event not found for deletion";
                    return false;
                }
                System.Console.WriteLine($"[DELETE EVENT] Found event: {eventToDelete.Name}");
                
                // Remove from DO system
                System.Console.WriteLine($"[DELETE EVENT] Calling RemoveEvent on adapter...");
                _currentAdapter.RemoveEvent(eventId);
                System.Console.WriteLine($"[DELETE EVENT] Count after: {_currentAdapter.Count}");
                
                // Verify removal
                var stillExists = _currentAdapter.FindEventById(eventId);
                if (stillExists != null)
                {
                    System.Console.WriteLine($"[DELETE EVENT ERROR] Event still exists after RemoveEvent!");
                    StatusText = $"Failed to delete event from DO system";
                    return false;
                }
                System.Console.WriteLine($"[DELETE EVENT] Verified: event removed from DO system");
                
                // Clear selection
                SelectedTimelineEvent = null;
                SelectedEvent = null;
                
                // Refresh timeline to show changes
                System.Console.WriteLine($"[DELETE EVENT] Calling UpdateTimeline to refresh UI...");
                UpdateTimeline();
                
                StatusText = $"Event '{eventToDelete.Name}' deleted successfully";
                System.Console.WriteLine($"[DELETE EVENT SUCCESS] Event completely removed");
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[DELETE EVENT ERROR] Exception: {ex.Message}");
                System.Console.WriteLine($"[DELETE EVENT ERROR] Stack: {ex.StackTrace}");
                StatusText = $"Failed to delete event: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Duplicates an event by creating a copy offset by 100ms
        /// </summary>
        public bool DuplicateEvent(SignalEvent sourceEvent)
        {
            System.Console.WriteLine($"[DUPLICATE EVENT] DuplicateEvent called for: {sourceEvent.Name}");
            
            if (SelectedSequence == null || _currentAdapter == null)
            {
                System.Console.WriteLine($"[DUPLICATE EVENT ERROR] No sequence or adapter");
                return false;
            }

            try
            {
                // Find target channel
                var targetChannel = TimelineChannels.FirstOrDefault(ch =>
                    ch.ChannelNumber == sourceEvent.Channel &&
                    ch.DeviceType == sourceEvent.DeviceType &&
                    ch.DeviceModel == sourceEvent.DeviceModel);

                if (targetChannel == null)
                {
                    System.Console.WriteLine($"[DUPLICATE EVENT ERROR] Target channel not found");
                    return false;
                }

                // Create duplicate with offset (100ms after original ends)
                var duplicateEvent = new SignalEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    Name = sourceEvent.Name + " (copy)",
                    StartTime = sourceEvent.EndTime + TimeSpan.FromMilliseconds(100),
                    Duration = sourceEvent.Duration,
                    Channel = sourceEvent.Channel,
                    DeviceType = sourceEvent.DeviceType,
                    DeviceModel = sourceEvent.DeviceModel,
                    EventType = sourceEvent.EventType,
                    Parameters = new Dictionary<string, double>(sourceEvent.Parameters),
                    Description = sourceEvent.Description,
                    Color = sourceEvent.Color
                };

                System.Console.WriteLine($"[DUPLICATE EVENT] Created duplicate at {duplicateEvent.StartTime.TotalSeconds:F3}s");

                // Check for conflicts
                if (targetChannel.HasConflict(duplicateEvent))
                {
                    System.Console.WriteLine($"[DUPLICATE EVENT ERROR] Time conflict at proposed position");
                    StatusText = "Cannot duplicate: Time conflict";
                    return false;
                }

                // Validate event
                if (!duplicateEvent.Validate(out string error))
                {
                    System.Console.WriteLine($"[DUPLICATE EVENT ERROR] Validation failed: {error}");
                    StatusText = $"Cannot duplicate: {error}";
                    return false;
                }

                // Add to DO system
                System.Console.WriteLine($"[DUPLICATE EVENT] Adding to DO system...");
                _currentAdapter.AddEvent(duplicateEvent);
                System.Console.WriteLine($"[DUPLICATE EVENT] DO Count: {_currentAdapter.Count}");
                
                // Refresh timeline
                UpdateTimeline();
                
                StatusText = $"Event duplicated successfully";
                System.Console.WriteLine($"[DUPLICATE EVENT SUCCESS]");
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[DUPLICATE EVENT ERROR] Exception: {ex.Message}");
                StatusText = $"Failed to duplicate event: {ex.Message}";
                return false;
            }
        }

        #endregion
    }

    #region Helper Classes

    public class SignalCategoryViewModel
    {
        public string CategoryName { get; set; }
        public ObservableCollection<SignalEvent> Signals { get; set; }
    }

    #endregion
}
