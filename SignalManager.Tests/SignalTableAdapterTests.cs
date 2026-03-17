using Microsoft.VisualStudio.TestTools.UnitTesting;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using System;
using System.Linq;

namespace LAMP_DAQ_Control_v0_8.SignalManager.Tests
{
    /// <summary>
    /// Unit tests for SignalTableAdapter - OO to DO conversion bridge
    /// Tests cover: Event conversion, CRUD operations, filtering
    /// </summary>
    [TestClass]
    public class SignalTableAdapterTests
    {
        private DataOrientedSequenceManager _manager;
        private Guid _sequenceId;
        private SignalTableAdapter _adapter;
        
        [TestInitialize]
        public void Setup()
        {
            _manager = new DataOrientedSequenceManager();
            _sequenceId = _manager.CreateSequence("Test Sequence");
            _adapter = new SignalTableAdapter(_manager, _sequenceId);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _adapter = null;
            _manager = null;
        }
        
        #region Constructor Tests
        
        [TestMethod]
        public void Constructor_ValidSequence_CreatesAdapter()
        {
            // Assert
            Assert.IsNotNull(_adapter);
            Assert.IsNotNull(_adapter.Table);
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullManager_ThrowsException()
        {
            // Act
            var adapter = new SignalTableAdapter(null, Guid.NewGuid());
        }
        
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Constructor_NonExistentSequence_ThrowsException()
        {
            // Act
            var adapter = new SignalTableAdapter(_manager, Guid.NewGuid());
        }
        
        #endregion
        
        #region GetEvent Tests
        
        [TestMethod]
        public void GetEvent_ValidIndex_ReturnsSignalEvent()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Test Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            evt.Parameters["voltage"] = 5.0;
            int index = _adapter.AddEvent(evt);
            
            // Act
            var retrieved = _adapter.GetEvent(index);
            
            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("Test Event", retrieved.Name);
            Assert.AreEqual(TimeSpan.FromSeconds(1), retrieved.StartTime);
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), retrieved.Duration);
            Assert.AreEqual(0, retrieved.Channel);
            Assert.AreEqual(DeviceType.Analog, retrieved.DeviceType);
            Assert.AreEqual(SignalEventType.DC, retrieved.EventType);
        }
        
        [TestMethod]
        public void GetEvent_InvalidIndex_ReturnsNull()
        {
            // Act
            var result = _adapter.GetEvent(100);
            
            // Assert
            Assert.IsNull(result);
        }
        
        [TestMethod]
        public void GetEvent_NegativeIndex_ReturnsNull()
        {
            // Act
            var result = _adapter.GetEvent(-1);
            
            // Assert
            Assert.IsNull(result);
        }
        
        [TestMethod]
        public void GetEvent_RampEvent_LoadsVoltageAttributes()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Ramp Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromSeconds(2),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.Ramp
            };
            evt.Parameters["startVoltage"] = 0.0;
            evt.Parameters["endVoltage"] = 10.0;
            int index = _adapter.AddEvent(evt);
            
            // Act
            var retrieved = _adapter.GetEvent(index);
            
            // Assert
            Assert.IsNotNull(retrieved);
            Assert.IsTrue(retrieved.Parameters.ContainsKey("startVoltage"));
            Assert.IsTrue(retrieved.Parameters.ContainsKey("endVoltage"));
            Assert.AreEqual(0.0, retrieved.Parameters["startVoltage"]);
            Assert.AreEqual(10.0, retrieved.Parameters["endVoltage"]);
        }
        
        [TestMethod]
        public void GetEvent_WaveformEvent_LoadsWaveformAttributes()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Waveform Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromSeconds(5),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.Waveform
            };
            evt.Parameters["frequency"] = 100.0;
            evt.Parameters["amplitude"] = 5.0;
            evt.Parameters["offset"] = 2.5;
            int index = _adapter.AddEvent(evt);
            
            // Act
            var retrieved = _adapter.GetEvent(index);
            
            // Assert
            Assert.IsTrue(retrieved.Parameters.ContainsKey("frequency"));
            Assert.IsTrue(retrieved.Parameters.ContainsKey("amplitude"));
            Assert.IsTrue(retrieved.Parameters.ContainsKey("offset"));
            Assert.AreEqual(100.0, retrieved.Parameters["frequency"]);
            Assert.AreEqual(5.0, retrieved.Parameters["amplitude"]);
            Assert.AreEqual(2.5, retrieved.Parameters["offset"]);
        }
        
        #endregion
        
        #region GetAllEvents Tests
        
        [TestMethod]
        public void GetAllEvents_EmptyTable_ReturnsEmptyList()
        {
            // Act
            var events = _adapter.GetAllEvents();
            
            // Assert
            Assert.IsNotNull(events);
            Assert.AreEqual(0, events.Count);
        }
        
        [TestMethod]
        public void GetAllEvents_MultipleEvents_ReturnsAll()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                var evt = new SignalEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    Name = $"Event{i}",
                    StartTime = TimeSpan.FromSeconds(i),
                    Duration = TimeSpan.FromMilliseconds(500),
                    Channel = i % 4,
                    DeviceType = DeviceType.Analog,
                    DeviceModel = "PCIe-1824",
                    EventType = SignalEventType.DC
                };
                evt.Parameters["voltage"] = i * 2.0;
                _adapter.AddEvent(evt);
            }
            
            // Act
            var events = _adapter.GetAllEvents();
            
            // Assert
            Assert.AreEqual(5, events.Count);
            Assert.IsTrue(events.All(e => e.Name.StartsWith("Event")));
        }
        
        #endregion
        
        #region AsObservableCollection Tests
        
        [TestMethod]
        public void AsObservableCollection_MultipleEvents_ReturnsCollection()
        {
            // Arrange
            for (int i = 0; i < 3; i++)
            {
                var evt = new SignalEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    Name = $"Event{i}",
                    StartTime = TimeSpan.FromSeconds(i),
                    Duration = TimeSpan.FromMilliseconds(500),
                    Channel = 0,
                    DeviceType = DeviceType.Analog,
                    DeviceModel = "PCIe-1824",
                    EventType = SignalEventType.DC
                };
                _adapter.AddEvent(evt);
            }
            
            // Act
            var collection = _adapter.AsObservableCollection();
            
            // Assert
            Assert.IsNotNull(collection);
            Assert.AreEqual(3, collection.Count);
        }
        
        #endregion
        
        #region GetEventsForChannel Tests
        
        [TestMethod]
        public void GetEventsForChannel_MatchingEvents_ReturnsFiltered()
        {
            // Arrange
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "CH0_Event1",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "CH1_Event",
                StartTime = TimeSpan.FromSeconds(2),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 1,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "CH0_Event2",
                StartTime = TimeSpan.FromSeconds(3),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            var ch0Events = _adapter.GetEventsForChannel(0, DeviceType.Analog, "PCIe-1824");
            
            // Assert
            Assert.AreEqual(2, ch0Events.Count);
            Assert.IsTrue(ch0Events.All(e => e.Name.StartsWith("CH0")));
        }
        
        [TestMethod]
        public void GetEventsForChannel_NoMatches_ReturnsEmpty()
        {
            // Arrange
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            var ch5Events = _adapter.GetEventsForChannel(5, DeviceType.Analog, "PCIe-1824");
            
            // Assert
            Assert.AreEqual(0, ch5Events.Count);
        }
        
        #endregion
        
        #region AddEvent Tests
        
        [TestMethod]
        public void AddEvent_ValidEvent_AddsToTable()
        {
            // Arrange
            var evt = new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "New Event",
                StartTime = TimeSpan.FromSeconds(2),
                Duration = TimeSpan.FromSeconds(1),
                Channel = 3,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            evt.Parameters["voltage"] = 7.5;
            
            // Act
            int index = _adapter.AddEvent(evt);
            
            // Assert
            Assert.AreEqual(0, index);
            Assert.AreEqual(1, _adapter.Count);
        }
        
        #endregion
        
        #region UpdateEvent Tests
        
        [TestMethod]
        public void UpdateEvent_ExistingEvent_UpdatesCorrectly()
        {
            // Arrange
            Guid eventId = Guid.NewGuid();
            var originalEvent = new SignalEvent
            {
                EventId = eventId.ToString(),
                Name = "Original",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            originalEvent.Parameters["voltage"] = 5.0;
            _adapter.AddEvent(originalEvent);
            
            var updatedEvent = new SignalEvent
            {
                EventId = eventId.ToString(),
                Name = "Updated",
                StartTime = TimeSpan.FromSeconds(3),
                Duration = TimeSpan.FromSeconds(1),
                Channel = 1,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            updatedEvent.Parameters["voltage"] = 8.0;
            
            // Act
            _adapter.UpdateEvent(eventId.ToString(), updatedEvent);
            var retrieved = _adapter.FindEventById(eventId.ToString());
            
            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("Updated", retrieved.Name);
            Assert.AreEqual(TimeSpan.FromSeconds(3), retrieved.StartTime);
            Assert.AreEqual(1, retrieved.Channel);
        }
        
        [TestMethod]
        public void UpdateEvent_InvalidGuid_DoesNotThrow()
        {
            // Act - Should not throw
            _adapter.UpdateEvent("invalid-guid", new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Test",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Assert - No exception
        }
        
        #endregion
        
        #region RemoveEvent Tests
        
        [TestMethod]
        public void RemoveEvent_ExistingEvent_Removes()
        {
            // Arrange
            Guid eventId = Guid.NewGuid();
            var evt = new SignalEvent
            {
                EventId = eventId.ToString(),
                Name = "To Remove",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            _adapter.AddEvent(evt);
            
            // Act
            _adapter.RemoveEvent(eventId.ToString());
            
            // Assert
            Assert.AreEqual(0, _adapter.Count);
            Assert.IsNull(_adapter.FindEventById(eventId.ToString()));
        }
        
        [TestMethod]
        public void RemoveEvent_InvalidGuid_DoesNotThrow()
        {
            // Act - Should not throw
            _adapter.RemoveEvent("invalid-guid");
            
            // Assert - No exception
        }
        
        #endregion
        
        #region DetectConflicts Tests
        
        [TestMethod]
        public void DetectConflicts_OverlappingEvents_ReturnsConflicts()
        {
            // Arrange - Add overlapping events
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event1",
                StartTime = TimeSpan.FromSeconds(0),
                Duration = TimeSpan.FromSeconds(2),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event2",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromSeconds(2),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            var conflicts = _adapter.DetectConflicts();
            
            // Assert
            Assert.IsTrue(conflicts.Count > 0);
        }
        
        [TestMethod]
        public void DetectConflicts_NoOverlaps_ReturnsEmpty()
        {
            // Arrange - Add non-overlapping events
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event1",
                StartTime = TimeSpan.FromSeconds(0),
                Duration = TimeSpan.FromSeconds(1),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event2",
                StartTime = TimeSpan.FromSeconds(2),
                Duration = TimeSpan.FromSeconds(1),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            var conflicts = _adapter.DetectConflicts();
            
            // Assert
            Assert.AreEqual(0, conflicts.Count);
        }
        
        #endregion
        
        #region SortByStartTime Tests
        
        [TestMethod]
        public void SortByStartTime_UnsortedEvents_Sorts()
        {
            // Arrange - Add in reverse order
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event3",
                StartTime = TimeSpan.FromSeconds(3),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event1",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            _adapter.SortByStartTime();
            var events = _adapter.GetAllEvents();
            
            // Assert
            Assert.AreEqual("Event1", events[0].Name);
            Assert.AreEqual("Event3", events[1].Name);
        }
        
        #endregion
        
        #region ValidateAll Tests
        
        [TestMethod]
        public void ValidateAll_AllValid_ReturnsNoErrors()
        {
            // Arrange
            var validEvent = new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Valid Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.Ramp
            };
            validEvent.Parameters["startVoltage"] = 0.0;
            validEvent.Parameters["endVoltage"] = 5.0;
            _adapter.AddEvent(validEvent);
            
            // Act
            var errors = _adapter.ValidateAll();
            
            // Assert
            Assert.AreEqual(0, errors.Count);
        }
        
        [TestMethod]
        public void ValidateAll_InvalidEvents_ReturnsErrors()
        {
            // Arrange
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Invalid Event",
                StartTime = TimeSpan.FromSeconds(-1), // Negative time
                Duration = TimeSpan.FromMilliseconds(0), // Zero duration
                Channel = 50, // Invalid channel
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            var errors = _adapter.ValidateAll();
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
        }
        
        #endregion
        
        #region GetTotalDuration Tests
        
        [TestMethod]
        public void GetTotalDuration_EmptyTable_ReturnsZero()
        {
            // Act
            var duration = _adapter.GetTotalDuration();
            
            // Assert
            Assert.AreEqual(TimeSpan.Zero, duration);
        }
        
        [TestMethod]
        public void GetTotalDuration_MultipleEvents_ReturnsMaxEndTime()
        {
            // Arrange
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event1",
                StartTime = TimeSpan.FromSeconds(0),
                Duration = TimeSpan.FromSeconds(2),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event2",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromSeconds(3), // Ends at 4s
                Channel = 1,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            var duration = _adapter.GetTotalDuration();
            
            // Assert
            Assert.AreEqual(TimeSpan.FromSeconds(4), duration);
        }
        
        #endregion
        
        #region Count Tests
        
        [TestMethod]
        public void Count_EmptyTable_ReturnsZero()
        {
            // Assert
            Assert.AreEqual(0, _adapter.Count);
        }
        
        [TestMethod]
        public void Count_AfterAdding_ReturnsCorrectCount()
        {
            // Arrange
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event1",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event2",
                StartTime = TimeSpan.FromSeconds(2),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Assert
            Assert.AreEqual(2, _adapter.Count);
        }
        
        #endregion
        
        #region Clear Tests
        
        [TestMethod]
        public void Clear_WithEvents_RemovesAll()
        {
            // Arrange
            _adapter.AddEvent(new SignalEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Name = "Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            _adapter.Clear();
            
            // Assert
            Assert.AreEqual(0, _adapter.Count);
        }
        
        #endregion
        
        #region FindEventById Tests
        
        [TestMethod]
        public void FindEventById_ExistingId_ReturnsEvent()
        {
            // Arrange
            Guid eventId = Guid.NewGuid();
            _adapter.AddEvent(new SignalEvent
            {
                EventId = eventId.ToString(),
                Name = "Findable Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            });
            
            // Act
            var found = _adapter.FindEventById(eventId.ToString());
            
            // Assert
            Assert.IsNotNull(found);
            Assert.AreEqual("Findable Event", found.Name);
        }
        
        [TestMethod]
        public void FindEventById_NonExistentId_ReturnsNull()
        {
            // Act
            var found = _adapter.FindEventById(Guid.NewGuid().ToString());
            
            // Assert
            Assert.IsNull(found);
        }
        
        [TestMethod]
        public void FindEventById_InvalidGuid_ReturnsNull()
        {
            // Act
            var found = _adapter.FindEventById("not-a-guid");
            
            // Assert
            Assert.IsNull(found);
        }
        
        #endregion
    }
}
