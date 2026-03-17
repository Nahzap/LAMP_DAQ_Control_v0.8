using Microsoft.VisualStudio.TestTools.UnitTesting;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using System;
using System.IO;
using System.Linq;

namespace LAMP_DAQ_Control_v0_8.SignalManager.Tests
{
    /// <summary>
    /// Comprehensive unit tests for DataOrientedSequenceManager
    /// Tests: Sequence lifecycle, CRUD operations, Save/Load, Validation
    /// </summary>
    [TestClass]
    public class DataOrientedSequenceManagerTests
    {
        private DataOrientedSequenceManager _manager;
        private string _tempFolder;
        
        [TestInitialize]
        public void Setup()
        {
            _manager = new DataOrientedSequenceManager();
            _tempFolder = Path.Combine(Path.GetTempPath(), "DOManagerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _manager = null;
            
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, true);
            }
        }
        
        #region Sequence Creation Tests
        
        [TestMethod]
        public void CreateSequence_ValidName_ReturnsGuid()
        {
            // Act
            var id = _manager.CreateSequence("Test Sequence");
            
            // Assert
            Assert.AreNotEqual(Guid.Empty, id);
        }
        
        [TestMethod]
        public void CreateSequence_WithDescription_StoresCorrectly()
        {
            // Act
            var id = _manager.CreateSequence("Test Sequence", "Test Description");
            var metadata = _manager.GetMetadata(id);
            
            // Assert
            Assert.IsNotNull(metadata);
            Assert.AreEqual("Test Sequence", metadata.Name);
            Assert.AreEqual("Test Description", metadata.Description);
        }
        
        [TestMethod]
        public void CreateSequence_MultipleSequences_CreatesUnique()
        {
            // Act
            var id1 = _manager.CreateSequence("Sequence 1");
            var id2 = _manager.CreateSequence("Sequence 2");
            
            // Assert
            Assert.AreNotEqual(id1, id2);
        }
        
        #endregion
        
        #region GetSignalTable Tests
        
        [TestMethod]
        public void GetSignalTable_ExistingSequence_ReturnsTable()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            
            // Act
            var table = _manager.GetSignalTable(id);
            
            // Assert
            Assert.IsNotNull(table);
            Assert.AreEqual(0, table.Count);
        }
        
        [TestMethod]
        public void GetSignalTable_NonExistentSequence_ReturnsNull()
        {
            // Act
            var table = _manager.GetSignalTable(Guid.NewGuid());
            
            // Assert
            Assert.IsNull(table);
        }
        
        #endregion
        
        #region GetMetadata Tests
        
        [TestMethod]
        public void GetMetadata_ExistingSequence_ReturnsMetadata()
        {
            // Arrange
            var id = _manager.CreateSequence("Test", "Description");
            
            // Act
            var metadata = _manager.GetMetadata(id);
            
            // Assert
            Assert.IsNotNull(metadata);
            Assert.AreEqual(id, metadata.SequenceId);
            Assert.AreEqual("Test", metadata.Name);
            Assert.AreEqual("Description", metadata.Description);
            Assert.AreEqual(0, metadata.EventCount);
        }
        
        [TestMethod]
        public void GetMetadata_NonExistentSequence_ReturnsNull()
        {
            // Act
            var metadata = _manager.GetMetadata(Guid.NewGuid());
            
            // Assert
            Assert.IsNull(metadata);
        }
        
        #endregion
        
        #region AddSignal Tests
        
        [TestMethod]
        public void AddSignal_ValidEvent_ReturnsIndex()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent
            {
                Name = "Event1",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            evt.Parameters["voltage"] = 5.0;
            
            // Act
            int index = _manager.AddSignal(id, evt);
            
            // Assert
            Assert.AreEqual(0, index);
        }
        
        [TestMethod]
        public void AddSignal_MultipleEvents_IncrementsCount()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            
            // Act
            for (int i = 0; i < 5; i++)
            {
                var evt = new SignalEvent
                {
                    Name = $"Event{i}",
                    StartTime = TimeSpan.FromSeconds(i),
                    Duration = TimeSpan.FromMilliseconds(100),
                    Channel = 0,
                    DeviceType = DeviceType.Analog,
                    DeviceModel = "PCIe-1824",
                    EventType = SignalEventType.DC
                };
                evt.Parameters["voltage"] = 5.0;
                _manager.AddSignal(id, evt);
            }
            
            var metadata = _manager.GetMetadata(id);
            
            // Assert
            Assert.AreEqual(5, metadata.EventCount);
        }
        
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void AddSignal_NonExistentSequence_ThrowsException()
        {
            // Arrange
            var evt = new SignalEvent
            {
                Name = "Event1",
                EventType = SignalEventType.DC
            };
            
            // Act
            _manager.AddSignal(Guid.NewGuid(), evt);
        }
        
        [TestMethod]
        public void AddSignal_DCEvent_StoresVoltageAttribute()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent
            {
                Name = "DC Event",
                EventType = SignalEventType.DC,
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824"
            };
            evt.Parameters["voltage"] = 7.5;
            
            // Act
            _manager.AddSignal(id, evt);
            var table = _manager.GetSignalTable(id);
            
            // Assert
            Assert.AreEqual(7.5, table.Attributes.GetVoltage(0));
        }
        
        [TestMethod]
        public void AddSignal_RampEvent_StoresRampAttributes()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent
            {
                Name = "Ramp Event",
                EventType = SignalEventType.Ramp,
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824"
            };
            evt.Parameters["startVoltage"] = 0.0;
            evt.Parameters["endVoltage"] = 10.0;
            
            // Act
            _manager.AddSignal(id, evt);
            var table = _manager.GetSignalTable(id);
            
            // Assert
            Assert.AreEqual(0.0, table.Attributes.GetStartVoltage(0));
            Assert.AreEqual(10.0, table.Attributes.GetEndVoltage(0));
        }
        
        [TestMethod]
        public void AddSignal_WaveformEvent_StoresWaveformAttributes()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent
            {
                Name = "Waveform Event",
                EventType = SignalEventType.Waveform,
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824"
            };
            evt.Parameters["frequency"] = 100.0;
            evt.Parameters["amplitude"] = 5.0;
            evt.Parameters["offset"] = 2.5;
            
            // Act
            _manager.AddSignal(id, evt);
            var table = _manager.GetSignalTable(id);
            var (freq, amp, offset) = table.Attributes.GetWaveformParams(0);
            
            // Assert
            Assert.AreEqual(100.0, freq);
            Assert.AreEqual(5.0, amp);
            Assert.AreEqual(2.5, offset);
        }
        
        #endregion
        
        #region UpdateSignal Tests
        
        [TestMethod]
        public void UpdateSignal_ExistingEvent_UpdatesCorrectly()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var eventId = Guid.NewGuid();
            var original = new SignalEvent
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
            original.Parameters["voltage"] = 5.0;
            _manager.AddSignal(id, original);
            
            var updated = new SignalEvent
            {
                EventId = eventId.ToString(),
                Name = "Updated",
                StartTime = TimeSpan.FromSeconds(2),
                Duration = TimeSpan.FromSeconds(1),
                Channel = 1,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            updated.Parameters["voltage"] = 8.0;
            
            // Act
            _manager.UpdateSignal(id, eventId, updated);
            var table = _manager.GetSignalTable(id);
            
            // Assert
            Assert.AreEqual("Updated", table.Names[0]);
            Assert.AreEqual(1, table.Channels[0]);
            Assert.AreEqual(8.0, table.Attributes.GetVoltage(0));
        }
        
        [TestMethod]
        public void UpdateSignal_NonExistentEvent_DoesNotCrash()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent { Name = "Event", EventType = SignalEventType.DC };
            
            // Act (should not throw)
            _manager.UpdateSignal(id, Guid.NewGuid(), evt);
            
            // Assert
            Assert.IsTrue(true);
        }
        
        #endregion
        
        #region RemoveSignal Tests
        
        [TestMethod]
        public void RemoveSignal_ExistingEvent_Removes()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var eventId = Guid.NewGuid();
            var evt = new SignalEvent
            {
                EventId = eventId.ToString(),
                Name = "Event",
                EventType = SignalEventType.DC,
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824"
            };
            evt.Parameters["voltage"] = 5.0;
            _manager.AddSignal(id, evt);
            
            // Act
            _manager.RemoveSignal(id, eventId);
            var metadata = _manager.GetMetadata(id);
            
            // Assert
            Assert.AreEqual(0, metadata.EventCount);
        }
        
        [TestMethod]
        public void RemoveSignal_NonExistentEvent_DoesNotCrash()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            
            // Act (should not throw)
            _manager.RemoveSignal(id, Guid.NewGuid());
            
            // Assert
            Assert.IsTrue(true);
        }
        
        #endregion
        
        #region Conflict Detection Tests
        
        [TestMethod]
        public void DetectConflicts_NoOverlap_ReturnsEmpty()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt1 = CreateTestEvent("Event1", 0, 1000);
            var evt2 = CreateTestEvent("Event2", 1000, 1000);
            _manager.AddSignal(id, evt1);
            _manager.AddSignal(id, evt2);
            
            // Act
            var conflicts = _manager.DetectConflicts(id);
            
            // Assert
            Assert.AreEqual(0, conflicts.Count);
        }
        
        [TestMethod]
        public void DetectConflicts_Overlapping_DetectsConflict()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt1 = CreateTestEvent("Event1", 0, 1000);
            var evt2 = CreateTestEvent("Event2", 500, 1000);
            _manager.AddSignal(id, evt1);
            _manager.AddSignal(id, evt2);
            
            // Act
            var conflicts = _manager.DetectConflicts(id);
            
            // Assert
            Assert.AreEqual(1, conflicts.Count);
        }
        
        #endregion
        
        #region Sort Tests
        
        [TestMethod]
        public void SortSequence_UnsortedEvents_Sorts()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            _manager.AddSignal(id, CreateTestEvent("Event3", 3000, 100));
            _manager.AddSignal(id, CreateTestEvent("Event1", 1000, 100));
            _manager.AddSignal(id, CreateTestEvent("Event2", 2000, 100));
            
            // Act
            _manager.SortSequence(id);
            var table = _manager.GetSignalTable(id);
            
            // Assert
            Assert.AreEqual("Event1", table.Names[0]);
            Assert.AreEqual("Event2", table.Names[1]);
            Assert.AreEqual("Event3", table.Names[2]);
        }
        
        #endregion
        
        #region Validation Tests
        
        [TestMethod]
        public void ValidateSequence_AllValid_ReturnsNoErrors()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent
            {
                Name = "Valid Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            evt.Parameters["voltage"] = 5.0;
            _manager.AddSignal(id, evt);
            
            // Act
            var errors = _manager.ValidateSequence(id);
            
            // Assert
            Assert.AreEqual(0, errors.Count);
        }
        
        [TestMethod]
        public void ValidateSequence_InvalidChannel_ReturnsError()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var table = _manager.GetSignalTable(id);
            table.AddSignal("Invalid", 0, 1000000, 999, DeviceType.Analog, "PCIe-1824", SignalEventType.DC, "#FF0000");
            
            // Act
            var errors = _manager.ValidateSequence(id);
            
            // Assert
            Assert.IsTrue(errors.Count > 0);
        }
        
        #endregion
        
        #region Duration Calculation Tests
        
        [TestMethod]
        public void GetTotalDuration_EmptySequence_ReturnsZero()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            
            // Act
            var duration = _manager.GetTotalDuration(id);
            
            // Assert
            Assert.AreEqual(TimeSpan.Zero, duration);
        }
        
        [TestMethod]
        public void GetTotalDuration_MultipleEvents_ReturnsMaxEndTime()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            _manager.AddSignal(id, CreateTestEvent("Event1", 0, 1000));
            _manager.AddSignal(id, CreateTestEvent("Event2", 2000, 3000));
            
            // Act
            var duration = _manager.GetTotalDuration(id);
            
            // Assert (Event2 ends at 2000+3000=5000ms = 5 seconds)
            Assert.AreEqual(TimeSpan.FromMilliseconds(5000), duration);
        }
        
        [TestMethod]
        public void CalculateSequenceDuration_EmptySequence_ReturnsZero()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            
            // Act
            var duration = _manager.CalculateSequenceDuration(id);
            
            // Assert
            Assert.AreEqual(TimeSpan.Zero, duration);
        }
        
        #endregion
        
        #region Delete Tests
        
        [TestMethod]
        public void DeleteSequence_ExistingSequence_ReturnsTrue()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            
            // Act
            bool result = _manager.DeleteSequence(id);
            
            // Assert
            Assert.IsTrue(result);
            Assert.IsNull(_manager.GetMetadata(id));
        }
        
        [TestMethod]
        public void DeleteSequence_NonExistentSequence_ReturnsFalse()
        {
            // Act
            bool result = _manager.DeleteSequence(Guid.NewGuid());
            
            // Assert
            Assert.IsFalse(result);
        }
        
        #endregion
        
        #region GetAllSequenceIds Tests
        
        [TestMethod]
        public void GetAllSequenceIds_NoSequences_ReturnsEmpty()
        {
            // Act
            var ids = _manager.GetAllSequenceIds();
            
            // Assert
            Assert.AreEqual(0, ids.Count);
        }
        
        [TestMethod]
        public void GetAllSequenceIds_MultipleSequences_ReturnsAll()
        {
            // Arrange
            var id1 = _manager.CreateSequence("Seq1");
            var id2 = _manager.CreateSequence("Seq2");
            var id3 = _manager.CreateSequence("Seq3");
            
            // Act
            var ids = _manager.GetAllSequenceIds();
            
            // Assert
            Assert.AreEqual(3, ids.Count);
            Assert.IsTrue(ids.Contains(id1));
            Assert.IsTrue(ids.Contains(id2));
            Assert.IsTrue(ids.Contains(id3));
        }
        
        #endregion
        
        #region Save/Load Tests
        
        [TestMethod]
        public void SaveSequence_ValidSequence_CreatesFile()
        {
            // Arrange
            var id = _manager.CreateSequence("Test Sequence", "Test Description");
            _manager.AddSignal(id, CreateTestEvent("Event1", 1000, 500));
            string filePath = Path.Combine(_tempFolder, "test.json");
            
            // Act
            _manager.SaveSequence(id, filePath);
            
            // Assert
            Assert.IsTrue(File.Exists(filePath));
        }
        
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SaveSequence_NonExistentSequence_ThrowsException()
        {
            // Arrange
            string filePath = Path.Combine(_tempFolder, "test.json");
            
            // Act
            _manager.SaveSequence(Guid.NewGuid(), filePath);
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void SaveSequence_EmptyPath_ThrowsException()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            
            // Act
            _manager.SaveSequence(id, "");
        }
        
        [TestMethod]
        public void LoadSequence_ValidFile_LoadsSequence()
        {
            // Arrange
            var id = _manager.CreateSequence("Test Sequence", "Description");
            var evt = CreateTestEvent("Event1", 1000, 500);
            evt.Parameters["voltage"] = 7.5;
            _manager.AddSignal(id, evt);
            
            string filePath = Path.Combine(_tempFolder, "test.json");
            _manager.SaveSequence(id, filePath);
            
            // Create new manager
            var newManager = new DataOrientedSequenceManager();
            
            // Act
            var loadedId = newManager.LoadSequence(filePath);
            var metadata = newManager.GetMetadata(loadedId);
            
            // Assert
            Assert.IsNotNull(metadata);
            Assert.AreEqual("Test Sequence", metadata.Name);
            Assert.AreEqual("Description", metadata.Description);
            Assert.AreEqual(1, metadata.EventCount);
        }
        
        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void LoadSequence_NonExistentFile_ThrowsException()
        {
            // Act
            _manager.LoadSequence(Path.Combine(_tempFolder, "nonexistent.json"));
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void LoadSequence_EmptyPath_ThrowsException()
        {
            // Act
            _manager.LoadSequence("");
        }
        
        [TestMethod]
        public void SaveAndLoad_DCEvent_PreservesAttributes()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent
            {
                Name = "DC Event",
                StartTime = TimeSpan.FromSeconds(1),
                Duration = TimeSpan.FromMilliseconds(500),
                Channel = 5,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC,
                Color = "#FF0000"
            };
            evt.Parameters["voltage"] = 8.5;
            _manager.AddSignal(id, evt);
            
            string filePath = Path.Combine(_tempFolder, "dc_test.json");
            _manager.SaveSequence(id, filePath);
            
            var newManager = new DataOrientedSequenceManager();
            
            // Act
            var loadedId = newManager.LoadSequence(filePath);
            var table = newManager.GetSignalTable(loadedId);
            
            // Assert
            Assert.AreEqual("DC Event", table.Names[0]);
            Assert.AreEqual(5, table.Channels[0]);
            Assert.AreEqual(SignalEventType.DC, table.EventTypes[0]);
            Assert.AreEqual("#FF0000", table.Colors[0]);
            Assert.AreEqual(8.5, table.Attributes.GetVoltage(0));
        }
        
        [TestMethod]
        public void SaveAndLoad_RampEvent_PreservesAttributes()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent
            {
                Name = "Ramp Event",
                EventType = SignalEventType.Ramp,
                Channel = 3,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824"
            };
            evt.Parameters["startVoltage"] = 0.0;
            evt.Parameters["endVoltage"] = 10.0;
            _manager.AddSignal(id, evt);
            
            string filePath = Path.Combine(_tempFolder, "ramp_test.json");
            _manager.SaveSequence(id, filePath);
            
            var newManager = new DataOrientedSequenceManager();
            
            // Act
            var loadedId = newManager.LoadSequence(filePath);
            var table = newManager.GetSignalTable(loadedId);
            
            // Assert
            Assert.AreEqual(0.0, table.Attributes.GetStartVoltage(0));
            Assert.AreEqual(10.0, table.Attributes.GetEndVoltage(0));
        }
        
        [TestMethod]
        public void SaveAndLoad_WaveformEvent_PreservesAttributes()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var evt = new SignalEvent
            {
                Name = "Waveform Event",
                EventType = SignalEventType.Waveform,
                Channel = 7,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824"
            };
            evt.Parameters["frequency"] = 50.0;
            evt.Parameters["amplitude"] = 3.5;
            evt.Parameters["offset"] = 1.5;
            _manager.AddSignal(id, evt);
            
            string filePath = Path.Combine(_tempFolder, "waveform_test.json");
            _manager.SaveSequence(id, filePath);
            
            var newManager = new DataOrientedSequenceManager();
            
            // Act
            var loadedId = newManager.LoadSequence(filePath);
            var table = newManager.GetSignalTable(loadedId);
            var (freq, amp, offset) = table.Attributes.GetWaveformParams(0);
            
            // Assert
            Assert.AreEqual(50.0, freq);
            Assert.AreEqual(3.5, amp);
            Assert.AreEqual(1.5, offset);
        }
        
        [TestMethod]
        public void SaveAndLoad_MultipleEvents_PreservesOrder()
        {
            // Arrange
            var id = _manager.CreateSequence("Multi-Event Test");
            for (int i = 0; i < 5; i++)
            {
                _manager.AddSignal(id, CreateTestEvent($"Event{i}", i * 1000, 500));
            }
            
            string filePath = Path.Combine(_tempFolder, "multi_test.json");
            _manager.SaveSequence(id, filePath);
            
            var newManager = new DataOrientedSequenceManager();
            
            // Act
            var loadedId = newManager.LoadSequence(filePath);
            var table = newManager.GetSignalTable(loadedId);
            
            // Assert
            Assert.AreEqual(5, table.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual($"Event{i}", table.Names[i]);
            }
        }
        
        [TestMethod]
        public void SaveAndLoad_PreservesEventIds()
        {
            // Arrange
            var id = _manager.CreateSequence("Test");
            var eventId = Guid.NewGuid();
            var evt = new SignalEvent
            {
                EventId = eventId.ToString(),
                Name = "Test Event",
                EventType = SignalEventType.DC,
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824"
            };
            evt.Parameters["voltage"] = 5.0;
            _manager.AddSignal(id, evt);
            
            string filePath = Path.Combine(_tempFolder, "id_test.json");
            _manager.SaveSequence(id, filePath);
            
            var newManager = new DataOrientedSequenceManager();
            
            // Act
            var loadedId = newManager.LoadSequence(filePath);
            var table = newManager.GetSignalTable(loadedId);
            
            // Assert
            Assert.AreEqual(eventId, table.EventIds[0]);
        }
        
        #endregion
        
        #region Helper Methods
        
        private SignalEvent CreateTestEvent(string name, long startMs, long durationMs)
        {
            var evt = new SignalEvent
            {
                Name = name,
                StartTime = TimeSpan.FromMilliseconds(startMs),
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Channel = 0,
                DeviceType = DeviceType.Analog,
                DeviceModel = "PCIe-1824",
                EventType = SignalEventType.DC
            };
            evt.Parameters["voltage"] = 5.0;
            return evt;
        }
        
        #endregion
    }
}
