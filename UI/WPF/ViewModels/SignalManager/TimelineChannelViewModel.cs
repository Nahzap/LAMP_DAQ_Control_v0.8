using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels.SignalManager
{
    /// <summary>
    /// ViewModel for a single timeline channel row
    /// </summary>
    public class TimelineChannelViewModel : ViewModelBase
    {
        private int _channelNumber;
        private string _channelName;
        private DeviceType _deviceType;
        private string _deviceModel;
        private int _deviceId;
        private ObservableCollection<TimelineEventViewModel> _events;

        public TimelineChannelViewModel(string deviceModel, int deviceId, int channelNumber, DeviceType deviceType)
        {
            _deviceModel = deviceModel;
            _deviceId = deviceId;
            _channelNumber = channelNumber;
            _deviceType = deviceType;
            _channelName = $"{deviceModel} CH{channelNumber}";
            _events = new ObservableCollection<TimelineEventViewModel>();
        }

        public int ChannelNumber
        {
            get => _channelNumber;
            set => SetProperty(ref _channelNumber, value);
        }

        public string ChannelName
        {
            get => _channelName;
            set => SetProperty(ref _channelName, value);
        }

        public DeviceType DeviceType
        {
            get => _deviceType;
            set => SetProperty(ref _deviceType, value);
        }

        public string DeviceModel
        {
            get => _deviceModel;
            set => SetProperty(ref _deviceModel, value);
        }

        public int DeviceId
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        public ObservableCollection<TimelineEventViewModel> Events
        {
            get => _events;
            set => SetProperty(ref _events, value);
        }

        /// <summary>
        /// Adds an event to this channel at the specified time
        /// </summary>
        public bool AddEvent(SignalEvent signalEvent, double totalDurationSeconds)
        {
            // Check for conflicts
            if (HasConflict(signalEvent))
            {
                return false;
            }

            System.Console.WriteLine($"[RENDER] Creating TimelineEventViewModel: TotalDuration={totalDurationSeconds}s");
            var eventVm = new TimelineEventViewModel(signalEvent, totalDurationSeconds);
            System.Console.WriteLine($"[RENDER] Event created: Left={eventVm.LeftPosition:F2}%, Width={eventVm.Width:F2}%, Color={eventVm.Color}");
            System.Console.WriteLine($"[RENDER] Adding to Events collection. Current count: {Events.Count}");
            Events.Add(eventVm);
            System.Console.WriteLine($"[RENDER] Event added. New count: {Events.Count}");
            return true;
        }

        /// <summary>
        /// Removes an event from this channel
        /// </summary>
        public void RemoveEvent(TimelineEventViewModel eventVm)
        {
            Events.Remove(eventVm);
        }

        /// <summary>
        /// Checks if adding this event would create a time conflict
        /// </summary>
        public bool HasConflict(SignalEvent newEvent)
        {
            var newStart = newEvent.StartTime.TotalSeconds;
            var newEnd = (newEvent.StartTime + newEvent.Duration).TotalSeconds;

            foreach (var existingEvent in Events)
            {
                // CRITICAL: Skip checking against itself (for move operations)
                if (existingEvent.SignalEvent.EventId == newEvent.EventId)
                {
                    continue;
                }

                var existingStart = existingEvent.SignalEvent.StartTime.TotalSeconds;
                var existingEnd = (existingEvent.SignalEvent.StartTime + existingEvent.SignalEvent.Duration).TotalSeconds;

                // Check for overlap with tolerance (1ms)
                if (newStart < existingEnd - 0.001 && newEnd > existingStart + 0.001)
                {
                    System.Console.WriteLine($"[CONFLICT] Event '{newEvent.Name}' ({newStart:F3}-{newEnd:F3}s) conflicts with '{existingEvent.SignalEvent.Name}' ({existingStart:F3}-{existingEnd:F3}s)");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Clears all events from this channel
        /// </summary>
        public void ClearEvents()
        {
            Events.Clear();
        }
    }

    /// <summary>
    /// ViewModel for a single event in the timeline
    /// </summary>
    public class TimelineEventViewModel : ViewModelBase
    {
        private SignalEvent _signalEvent;
        private double _leftPosition;
        private double _width;
        private string _displayText;

        public TimelineEventViewModel(SignalEvent signalEvent, double totalDurationSeconds)
        {
            _signalEvent = signalEvent;
            CalculatePosition(totalDurationSeconds);
            UpdateDisplayText();
        }

        public SignalEvent SignalEvent
        {
            get => _signalEvent;
            set
            {
                if (SetProperty(ref _signalEvent, value))
                {
                    UpdateDisplayText();
                }
            }
        }

        public double LeftPosition
        {
            get => _leftPosition;
            set => SetProperty(ref _leftPosition, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public string DisplayText
        {
            get => _displayText;
            private set => SetProperty(ref _displayText, value);
        }

        public string Color => _signalEvent.Color ?? "#4A90E2";

        private void CalculatePosition(double totalDurationSeconds)
        {
            if (totalDurationSeconds <= 0) totalDurationSeconds = 1;

            // Calculate percentage positions (0-100)
            LeftPosition = (_signalEvent.StartTime.TotalSeconds / totalDurationSeconds) * 100.0;
            Width = (_signalEvent.Duration.TotalSeconds / totalDurationSeconds) * 100.0;
            
            System.Console.WriteLine($"[CALC POS] Event '{_signalEvent.Name}': StartTime={_signalEvent.StartTime.TotalSeconds:F6}s, Duration={_signalEvent.Duration.TotalSeconds:F6}s, TotalGrid={totalDurationSeconds}s → Left={LeftPosition:F4}%, Width={Width:F4}%");
        }

        private void UpdateDisplayText()
        {
            // Mostrar nombre y tiempo de inicio
            DisplayText = $"{_signalEvent.Name} @ {_signalEvent.StartTime.TotalSeconds:F3}s";
        }

        public void RecalculatePosition(double totalDurationSeconds)
        {
            CalculatePosition(totalDurationSeconds);
        }
    }
}
