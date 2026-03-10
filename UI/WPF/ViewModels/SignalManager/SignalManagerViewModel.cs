using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Interfaces;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Services;
using LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels.SignalManager
{
    /// <summary>
    /// ViewModel for Signal Manager main window
    /// </summary>
    public class SignalManagerViewModel : ViewModelBase
    {
        private readonly ISequenceEngine _sequenceEngine;
        private readonly ISignalLibrary _signalLibrary;
        private readonly IExecutionEngine _executionEngine;
        private readonly DAQController _daqController;
        private readonly IEnumerable<UI.Models.DAQDevice> _allDevices;

        private SignalSequence _selectedSequence;
        private SignalEvent _selectedEvent;
        private TimelineEventViewModel _selectedTimelineEvent;
        private double _currentTimeSeconds;
        private SignalEvent _selectedEventFromList;
        private string _statusText;
        private string _executionStateText;
        private long _totalDurationNanoseconds;
        private double _zoomLevel;

        public SignalManagerViewModel(DAQController daqController, IEnumerable<UI.Models.DAQDevice> allDetectedDevices)
        {
            _daqController = daqController ?? throw new ArgumentNullException(nameof(daqController));
            _allDevices = allDetectedDevices ?? throw new ArgumentNullException(nameof(allDetectedDevices));
            _sequenceEngine = new SequenceEngine();
            _signalLibrary = new SignalLibrary();
            
            // CRITICAL: Create separate DAQController for each detected device
            var deviceControllers = CreateDeviceControllers(allDetectedDevices);
            _executionEngine = new ExecutionEngine(deviceControllers);

            // Initialize collections
            Sequences = new ObservableCollection<SignalSequence>();
            SignalCategories = new ObservableCollection<SignalCategoryViewModel>();
            TimelineChannels = new ObservableCollection<TimelineChannelViewModel>();
            EventsList = new ObservableCollection<SignalEvent>();

            // Initialize timeline channels from ALL detected devices
            InitializeTimelineChannels();

            // Initialize signal library UI
            InitializeSignalLibrary();

            // Subscribe to execution events
            _executionEngine.StateChanged += OnExecutionStateChanged;
            _executionEngine.EventExecuted += OnEventExecuted;
            _executionEngine.ExecutionError += OnExecutionError;

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

        public SignalSequence SelectedSequence
        {
            get => _selectedSequence;
            set
            {
                if (SetProperty(ref _selectedSequence, value))
                {
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
                        var realEvent = _sequenceEngine.GetEvent(SelectedSequence.SequenceId, value.EventId);
                        SelectedEvent = realEvent;
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
                    // CRITICAL FIX: Get real event from engine, not corrupted copy
                    if (value?.SignalEvent != null && SelectedSequence != null)
                    {
                        var realEvent = _sequenceEngine.GetEvent(SelectedSequence.SequenceId, value.SignalEvent.EventId);
                        SelectedEvent = realEvent;
                        System.Console.WriteLine($"[EVENT] SelectedTimelineEvent changed: {realEvent?.Name ?? "null"}, StartTime={realEvent?.StartTime.TotalSeconds:F6}s (Real event from engine)");
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
                
                var sequence = _sequenceEngine.CreateSequence(dialog.SequenceName, dialog.Description);
                
                // Store desired duration in metadata since TotalDuration is calculated from events
                sequence.Metadata["DesiredDuration"] = dialog.DurationSeconds;
                
                Sequences.Add(sequence);
                SelectedSequence = sequence;
                
                // Update TotalDurationSeconds for UI
                TotalDurationSeconds = dialog.DurationSeconds;
                
                System.Console.WriteLine($"[SEQUENCE SUCCESS] Sequence created and selected: {sequence.Name}");
                StatusText = $"Created new sequence: {dialog.SequenceName} ({dialog.DurationSeconds}s)";
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
                try
                {
                    var sequence = _sequenceEngine.LoadSequence(dialog.FileName);
                    Sequences.Add(sequence);
                    SelectedSequence = sequence;
                    StatusText = $"Loaded sequence: {sequence.Name}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load sequence: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
                var filePath = SelectedSequence.Metadata["FilePath"] as string;
                _sequenceEngine.SaveSequence(SelectedSequence.SequenceId, filePath);
                StatusText = $"Saved sequence: {SelectedSequence.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save sequence: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    _sequenceEngine.SaveSequence(SelectedSequence.SequenceId, dialog.FileName);
                    SelectedSequence.Metadata["FilePath"] = dialog.FileName;
                    StatusText = $"Saved sequence: {SelectedSequence.Name}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save sequence: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var newEvent = new SignalEvent
            {
                Name = $"Event_{SelectedSequence.Events.Count + 1}",
                EventType = SignalEventType.DC,
                Channel = 0,
                StartTime = TimeSpan.Zero,
                Duration = TimeSpan.FromSeconds(1),
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            _sequenceEngine.AddEvent(SelectedSequence.SequenceId, newEvent);
            UpdateTimeline();
            SelectedEvent = newEvent;
            StatusText = "Event added manually";
        }

        private void OnDeleteEvent()
        {
            if (SelectedEvent == null || SelectedTimelineEvent == null || SelectedSequence == null)
            {
                System.Console.WriteLine("[DELETE EVENT ERROR] Missing selection");
                return;
            }

            System.Console.WriteLine($"[DELETE EVENT] Deleting: {SelectedEvent.Name} from channel {SelectedEvent.Channel}");

            var result = MessageBox.Show(
                $"Delete event '{SelectedEvent.Name}' at {SelectedEvent.StartTime.TotalSeconds:F1}s?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Remove from sequence engine
                _sequenceEngine.RemoveEvent(SelectedSequence.SequenceId, SelectedEvent.EventId);

                // Remove from timeline channel
                var channel = TimelineChannels.FirstOrDefault(c => 
                    c.ChannelNumber == SelectedEvent.Channel &&
                    c.DeviceType == SelectedEvent.DeviceType);

                if (channel != null)
                {
                    var eventToRemove = channel.Events.FirstOrDefault(e => e.SignalEvent.EventId == SelectedEvent.EventId);
                    if (eventToRemove != null)
                    {
                        channel.Events.Remove(eventToRemove);
                        System.Console.WriteLine($"[DELETE EVENT SUCCESS] Event removed from timeline");
                    }
                }

                SelectedEvent = null;
                SelectedTimelineEvent = null;
                StatusText = $"Event deleted";
            }
            else
            {
                System.Console.WriteLine("[DELETE EVENT] Cancelled by user");
            }
        }

        private void OnDuplicateEvent()
        {
            if (SelectedEvent == null || SelectedSequence == null) return;

            var duplicate = new SignalEvent
            {
                Name = SelectedEvent.Name + "_Copy",
                EventType = SelectedEvent.EventType,
                Channel = SelectedEvent.Channel,
                DeviceType = SelectedEvent.DeviceType,
                StartTime = SelectedEvent.StartTime,
                Duration = SelectedEvent.Duration,
                Parameters = new Dictionary<string, double>(SelectedEvent.Parameters),
                Description = SelectedEvent.Description,
                Color = SelectedEvent.Color
            };

            _sequenceEngine.AddEvent(SelectedSequence.SequenceId, duplicate);
            UpdateTimeline();
            StatusText = $"Duplicated event: {SelectedEvent.Name}";
        }

        private void OnValidate()
        {
            if (SelectedSequence == null) return;

            if (_sequenceEngine.ValidateSequence(SelectedSequence.SequenceId, out var errors))
            {
                MessageBox.Show("Sequence is valid!", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "Validation passed";
            }
            else
            {
                var errorMessage = string.Join("\n", errors);
                MessageBox.Show($"Validation failed:\n\n{errorMessage}", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusText = "Validation failed";
            }
        }

        private void OnExecute()
        {
            OnPlay();
        }

        private async void OnPlay()
        {
            if (SelectedSequence == null) return;

            // Validate first
            if (!_sequenceEngine.ValidateSequence(SelectedSequence.SequenceId, out var errors))
            {
                MessageBox.Show($"Cannot execute invalid sequence:\n\n{string.Join("\n", errors)}",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                StatusText = "Executing sequence...";
                await _executionEngine.ExecuteSequenceAsync(SelectedSequence);
                StatusText = "Sequence completed";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Execution error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = $"Execution error: {ex.Message}";
            }
        }

        private void OnPause()
        {
            _executionEngine.Pause();
            StatusText = "Execution paused";
        }

        private void OnStop()
        {
            System.Console.WriteLine("[EXECUTION] Stop command invoked");
            try
            {
                _executionEngine.Stop();
                CurrentTimeSeconds = 0;
                System.Console.WriteLine("[EXECUTION] Execution stopped successfully");
                StatusText = "Execution stopped - Ready to edit";
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[EXECUTION ERROR] Failed to stop: {ex.Message}");
                StatusText = $"Error stopping execution: {ex.Message}";
            }
        }

        private void OnApplyEventChanges()
        {
            if (SelectedEvent == null || SelectedSequence == null) return;

            System.Console.WriteLine($"[APPLY CHANGES] Before update: StartTime={SelectedEvent.StartTime.TotalSeconds:F6}s, Duration={SelectedEvent.Duration.TotalSeconds:F6}s");
            _sequenceEngine.UpdateEvent(SelectedSequence.SequenceId, SelectedEvent);
            System.Console.WriteLine($"[APPLY CHANGES] After update: StartTime={SelectedEvent.StartTime.TotalSeconds:F6}s");
            UpdateTimeline();
            StatusText = "Event updated";
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
            
            if (SelectedSequence == null)
            {
                System.Console.WriteLine($"[UPDATE TIMELINE] No sequence selected");
                EventsList.Clear();
                return;
            }

            var events = SelectedSequence.Events;
            System.Console.WriteLine($"[UPDATE TIMELINE] Processing {events.Count} events for sequence '{SelectedSequence.Name}'");
            System.Console.WriteLine($"[UPDATE TIMELINE] Using grid duration: {TotalDurationSeconds}s ({TotalDurationNanoseconds}ns)");

            // Update EventsList with all events from sequence
            EventsList.Clear();
            foreach (var evt in events.OrderBy(e => e.StartTime))
            {
                EventsList.Add(evt);
            }

            // Clear all events from timeline
            foreach (var channel in TimelineChannels)
            {
                channel.ClearEvents();
            }

            // Add events to appropriate channels
            foreach (var evt in events)
            {
                var targetChannel = TimelineChannels.FirstOrDefault(ch => 
                    ch.ChannelNumber == evt.Channel && 
                    ch.DeviceType == evt.DeviceType &&
                    ch.DeviceModel == evt.DeviceModel); // CRITICAL: Match exact device

                if (targetChannel != null)
                {
                    System.Console.WriteLine($"[UPDATE TIMELINE] Adding event '{evt.Name}' to channel {targetChannel.ChannelName} (Type: {targetChannel.DeviceType})");
                    targetChannel.AddEvent(evt, TotalDurationSeconds);
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

            // Get real event from engine
            var realEvent = _sequenceEngine.GetEvent(SelectedSequence.SequenceId, existingEvent.EventId);
            if (realEvent == null)
            {
                System.Console.WriteLine($"[MOVE EVENT ERROR] Event not found in sequence");
                return false;
            }

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

            // Update event in engine
            _sequenceEngine.UpdateEvent(SelectedSequence.SequenceId, realEvent);
            
            // Refresh timeline to show in new position
            UpdateTimeline();
            
            System.Console.WriteLine($"[MOVE EVENT SUCCESS] Moved '{realEvent.Name}' from CH{oldChannel} to {targetChannel.ChannelName} @ {newStartTime.TotalSeconds:F3}s");
            StatusText = $"Moved {realEvent.Name} to {targetChannel.ChannelName} at {newStartTime.TotalSeconds:F1}s";
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
                    $"Cannot add event at {startTime.TotalSeconds:F1}s on {targetChannel.ChannelName}.\n\nThere is already an event in that time range.",
                    "Time Conflict",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            System.Console.WriteLine($"[ADD SIGNAL] No conflicts found");

            // Add to sequence and timeline
            System.Console.WriteLine($"[ADD SIGNAL] Adding to sequence engine...");
            _sequenceEngine.AddEvent(SelectedSequence.SequenceId, newEvent);
            System.Console.WriteLine($"[ADD SIGNAL] Adding to timeline channel (Grid: {TotalDurationSeconds}s)...");
            targetChannel.AddEvent(newEvent, TotalDurationSeconds);
            
            System.Console.WriteLine($"[ADD SIGNAL SUCCESS] Event added: {newEvent.Name} -> {targetChannel.ChannelName} @ {startTime.TotalSeconds:F2}s");
            StatusText = $"Added {newEvent.Name} to {targetChannel.ChannelName} at {startTime.TotalSeconds:F1}s";
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
