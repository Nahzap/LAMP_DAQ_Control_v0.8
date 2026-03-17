using Microsoft.VisualStudio.TestTools.UnitTesting;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels.SignalManager;
using System;
using System.Linq;
using System.Collections.Generic;

namespace LAMP_DAQ_Control_v0_8.SignalManager.Tests
{
    /// <summary>
    /// Unit tests for Timeline ViewModel functionality
    /// Tests: DeleteEvent, DuplicateEvent, Event Layering, Conflict Detection
    /// </summary>
    [TestClass]
    public class TimelineViewModelTests
    {
        private TimelineChannelViewModel _channel;
        private const double TOTAL_DURATION = 10.0; // 10 seconds

        [TestInitialize]
        public void Setup()
        {
            _channel = new TimelineChannelViewModel("TestDevice", 0, 0, DeviceType.Analog);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _channel = null;
        }

        #region Z-Index and Layering Tests

        [TestMethod]
        public void TimelineEventViewModel_EarlierEventHasLowerZIndex()
        {
            // Arrange
            var earlyEvent = new SignalEvent
            {
                EventId = "early",
                Name = "Early Event",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            var lateEvent = new SignalEvent
            {
                EventId = "late",
                Name = "Late Event",
                StartTime = TimeSpan.FromSeconds(5.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            // Act
            var earlyVM = new TimelineEventViewModel(earlyEvent, TOTAL_DURATION);
            var lateVM = new TimelineEventViewModel(lateEvent, TOTAL_DURATION);

            // Assert
            Assert.IsTrue(earlyVM.ZIndex < lateVM.ZIndex, 
                $"Early event Z-Index ({earlyVM.ZIndex}) should be less than late event Z-Index ({lateVM.ZIndex})");
        }

        [TestMethod]
        public void TimelineEventViewModel_ZIndexRecalculatesOnPositionChange()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = "test",
                Name = "Test Event",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            var vm = new TimelineEventViewModel(evt, TOTAL_DURATION);
            int originalZIndex = vm.ZIndex;

            // Act - simulate moving event to later time
            evt.StartTime = TimeSpan.FromSeconds(8.0);
            vm.RecalculatePosition(TOTAL_DURATION);

            // Assert
            Assert.AreNotEqual(originalZIndex, vm.ZIndex, "Z-Index should change when event position changes");
            Assert.IsTrue(vm.ZIndex > originalZIndex, "Z-Index should be higher for later event");
        }

        #endregion

        #region Conflict Detection Tests

        [TestMethod]
        public void HasConflict_OverlappingEvents_ReturnsTrue()
        {
            // Arrange
            var existingEvent = new SignalEvent
            {
                EventId = "existing",
                Name = "Existing",
                StartTime = TimeSpan.FromSeconds(2.0),
                Duration = TimeSpan.FromSeconds(2.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            var overlappingEvent = new SignalEvent
            {
                EventId = "overlapping",
                Name = "Overlapping",
                StartTime = TimeSpan.FromSeconds(3.0),
                Duration = TimeSpan.FromSeconds(2.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            _channel.AddEvent(existingEvent, TOTAL_DURATION);

            // Act
            bool hasConflict = _channel.HasConflict(overlappingEvent);

            // Assert
            Assert.IsTrue(hasConflict, "Should detect overlap between events");
        }

        [TestMethod]
        public void HasConflict_NonOverlappingEvents_ReturnsFalse()
        {
            // Arrange
            var event1 = new SignalEvent
            {
                EventId = "event1",
                Name = "Event 1",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            var event2 = new SignalEvent
            {
                EventId = "event2",
                Name = "Event 2",
                StartTime = TimeSpan.FromSeconds(3.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            _channel.AddEvent(event1, TOTAL_DURATION);

            // Act
            bool hasConflict = _channel.HasConflict(event2);

            // Assert
            Assert.IsFalse(hasConflict, "Should not detect conflict for non-overlapping events");
        }

        [TestMethod]
        public void HasConflict_AdjacentEvents_ReturnsFalse()
        {
            // Arrange - Events end-to-end (no gap, no overlap)
            var event1 = new SignalEvent
            {
                EventId = "event1",
                Name = "Event 1",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.5),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            var event2 = new SignalEvent
            {
                EventId = "event2",
                Name = "Event 2",
                StartTime = TimeSpan.FromSeconds(2.5), // Exactly after event1
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            _channel.AddEvent(event1, TOTAL_DURATION);

            // Act
            bool hasConflict = _channel.HasConflict(event2);

            // Assert
            Assert.IsFalse(hasConflict, "Adjacent events (no gap) should not be considered conflicting");
        }

        #endregion

        #region Event Validation Tests

        [TestMethod]
        public void SignalEvent_ValidateAmplitudePlusOffset_ExceedsMax_ReturnsFalse()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = "test",
                Name = "Invalid Waveform",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.Waveform,
                Parameters = new Dictionary<string, double>
                {
                    { "frequency", 100.0 },
                    { "amplitude", 6.0 },  // 6V amplitude
                    { "offset", 5.0 }      // 5V offset -> peak = 11V (exceeds 10V max)
                }
            };

            // Act
            bool isValid = evt.Validate(out string errorMessage);

            // Assert
            Assert.IsFalse(isValid, "Should reject waveform with amplitude + offset > 10V");
            Assert.IsTrue(errorMessage.Contains("Peak voltage"), $"Error message should mention peak voltage: {errorMessage}");
            Assert.IsTrue(errorMessage.Contains("11"), $"Error message should show calculated peak of 11V: {errorMessage}");
        }

        [TestMethod]
        public void SignalEvent_ValidateAmplitudeMinusOffset_BelowMin_ReturnsFalse()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = "test",
                Name = "Invalid Waveform",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.Waveform,
                Parameters = new Dictionary<string, double>
                {
                    { "frequency", 100.0 },
                    { "amplitude", 3.0 },  // 3V amplitude
                    { "offset", 2.0 }      // 2V offset -> trough = -1V (below 0V min)
                }
            };

            // Act
            bool isValid = evt.Validate(out string errorMessage);

            // Assert
            Assert.IsFalse(isValid, "Should reject waveform with offset - amplitude < 0V");
            Assert.IsTrue(errorMessage.Contains("Trough voltage"), $"Error message should mention trough voltage: {errorMessage}");
        }

        [TestMethod]
        public void SignalEvent_ValidateValidWaveform_ReturnsTrue()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = "test",
                Name = "Valid Waveform",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.Waveform,
                Parameters = new Dictionary<string, double>
                {
                    { "frequency", 100.0 },
                    { "amplitude", 2.0 },  // 2V amplitude
                    { "offset", 5.0 }      // 5V offset -> peak = 7V, trough = 3V (both valid)
                }
            };

            // Act
            bool isValid = evt.Validate(out string errorMessage);

            // Assert
            Assert.IsTrue(isValid, $"Should accept valid waveform: {errorMessage}");
            Assert.IsNull(errorMessage, "Error message should be null for valid event");
        }

        [TestMethod]
        public void SignalEvent_ValidateDCVoltageInRange_ReturnsTrue()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = "test",
                Name = "Valid DC",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 7.5 } }
            };

            // Act
            bool isValid = evt.Validate(out string errorMessage);

            // Assert
            Assert.IsTrue(isValid, $"Should accept valid DC voltage: {errorMessage}");
        }

        [TestMethod]
        public void SignalEvent_ValidateDCVoltageOutOfRange_ReturnsFalse()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = "test",
                Name = "Invalid DC",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 12.0 } } // Exceeds 10V
            };

            // Act
            bool isValid = evt.Validate(out string errorMessage);

            // Assert
            Assert.IsFalse(isValid, "Should reject DC voltage > 10V");
            Assert.IsTrue(errorMessage.Contains("0-10V"), $"Error message should mention valid range: {errorMessage}");
        }

        #endregion

        #region Channel Operations Tests

        [TestMethod]
        public void AddEvent_ValidEvent_AddsToChannel()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = "test",
                Name = "Test Event",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            // Act
            _channel.AddEvent(evt, TOTAL_DURATION);

            // Assert
            Assert.AreEqual(1, _channel.Events.Count, "Should have 1 event in channel");
            Assert.AreEqual("test", _channel.Events[0].SignalEvent.EventId, "Event ID should match");
        }

        [TestMethod]
        public void ClearEvents_RemovesAllEvents()
        {
            // Arrange
            var evt1 = new SignalEvent
            {
                EventId = "event1",
                Name = "Event 1",
                StartTime = TimeSpan.FromSeconds(1.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            var evt2 = new SignalEvent
            {
                EventId = "event2",
                Name = "Event 2",
                StartTime = TimeSpan.FromSeconds(3.0),
                Duration = TimeSpan.FromSeconds(1.0),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                EventType = SignalEventType.DC,
                Parameters = new Dictionary<string, double> { { "voltage", 5.0 } }
            };

            _channel.AddEvent(evt1, TOTAL_DURATION);
            _channel.AddEvent(evt2, TOTAL_DURATION);

            // Act
            _channel.ClearEvents();

            // Assert
            Assert.AreEqual(0, _channel.Events.Count, "Should have 0 events after clear");
        }

        #endregion
    }
}
