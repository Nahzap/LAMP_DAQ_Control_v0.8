using Microsoft.VisualStudio.TestTools.UnitTesting;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using System;

namespace LAMP_DAQ_Control_v0_8.SignalManager.Tests
{
    /// <summary>
    /// Unit tests for SignalTable - Column-oriented data structure
    /// Tests cover: Add, Remove, Update, Find operations (all O(1))
    /// </summary>
    [TestClass]
    public class SignalTableTests
    {
        private SignalTable _table;
        
        [TestInitialize]
        public void Setup()
        {
            _table = new SignalTable(64);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _table = null;
        }
        
        #region AddSignal Tests
        
        [TestMethod]
        public void AddSignal_ValidData_ReturnsIndexZero()
        {
            // Arrange & Act
            int index = _table.AddSignal(
                name: "Test Signal",
                startTimeNs: 1_000_000_000,
                durationNs: 500_000_000,
                channel: 0,
                deviceType: DeviceType.Analog,
                deviceModel: "PCIe-1824",
                eventType: SignalEventType.Ramp,
                color: "#FF0000"
            );
            
            // Assert
            Assert.AreEqual(0, index);
            Assert.AreEqual(1, _table.Count);
            Assert.AreEqual("Test Signal", _table.Names[0]);
        }
        
        [TestMethod]
        public void AddSignal_MultipleSignals_IncrementsCountCorrectly()
        {
            // Act
            for (int i = 0; i < 10; i++)
            {
                _table.AddSignal($"Signal_{i}", i * 1000, 500, i, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            }
            
            // Assert
            Assert.AreEqual(10, _table.Count);
        }
        
        [TestMethod]
        public void AddSignal_WithProvidedEventId_PreservesEventId()
        {
            // Arrange
            Guid expectedId = Guid.NewGuid();
            
            // Act
            int index = _table.AddSignal(
                "Test",
                1000,
                500,
                0,
                DeviceType.Analog,
                "PCIe-1824",
                SignalEventType.DC,
                "#FF0000",
                expectedId
            );
            
            // Assert
            Assert.AreEqual(expectedId, _table.EventIds[index]);
        }
        
        [TestMethod]
        public void AddSignal_ExceedingCapacity_ResizesAutomatically()
        {
            // Arrange
            var smallTable = new SignalTable(2); // Small capacity
            
            // Act - Add more than capacity
            smallTable.AddSignal("S1", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            smallTable.AddSignal("S2", 2000, 500, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            smallTable.AddSignal("S3", 3000, 500, 2, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Assert
            Assert.AreEqual(3, smallTable.Count);
            Assert.IsTrue(smallTable.Capacity >= 3);
        }
        
        [TestMethod]
        public void AddSignal_StoresAllColumnsCorrectly()
        {
            // Arrange
            long expectedStart = 2_500_000_000;
            long expectedDuration = 1_000_000_000;
            int expectedChannel = 5;
            
            // Act
            int index = _table.AddSignal(
                "Ramp_Test",
                expectedStart,
                expectedDuration,
                expectedChannel,
                DeviceType.Analog,
                "PCIe-1824",
                SignalEventType.Ramp,
                "#00FF00"
            );
            
            // Assert
            Assert.AreEqual("Ramp_Test", _table.Names[index]);
            Assert.AreEqual(expectedStart, _table.StartTimesNs[index]);
            Assert.AreEqual(expectedDuration, _table.DurationsNs[index]);
            Assert.AreEqual(expectedChannel, _table.Channels[index]);
            Assert.AreEqual(DeviceType.Analog, _table.DeviceTypes[index]);
            Assert.AreEqual("PCIe-1824", _table.DeviceModels[index]);
            Assert.AreEqual(SignalEventType.Ramp, _table.EventTypes[index]);
            Assert.AreEqual("#00FF00", _table.Colors[index]);
        }
        
        #endregion
        
        #region RemoveAt Tests
        
        [TestMethod]
        public void RemoveAt_ValidIndex_DecrementsCount()
        {
            // Arrange
            _table.AddSignal("S1", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("S2", 2000, 500, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            _table.RemoveAt(0);
            
            // Assert
            Assert.AreEqual(1, _table.Count);
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void RemoveAt_InvalidIndex_ThrowsException()
        {
            // Arrange
            _table.AddSignal("S1", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            _table.RemoveAt(10); // Invalid index
        }
        
        [TestMethod]
        public void RemoveAt_MiddleElement_SwapsWithLast()
        {
            // Arrange
            _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("S1", 2000, 500, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("S2", 3000, 500, 2, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act - Remove middle element
            _table.RemoveAt(1);
            
            // Assert - S2 should now be at index 1
            Assert.AreEqual(2, _table.Count);
            Assert.AreEqual("S2", _table.Names[1]);
            Assert.AreEqual(3000, _table.StartTimesNs[1]);
        }
        
        [TestMethod]
        public void RemoveAt_LastElement_DoesNotSwap()
        {
            // Arrange
            _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("S1", 2000, 500, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            _table.RemoveAt(1);
            
            // Assert
            Assert.AreEqual(1, _table.Count);
            Assert.AreEqual("S0", _table.Names[0]);
        }
        
        #endregion
        
        #region FindIndex Tests
        
        [TestMethod]
        public void FindIndex_ExistingEventId_ReturnsCorrectIndex()
        {
            // Arrange
            Guid targetId = Guid.NewGuid();
            _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            int expectedIndex = _table.AddSignal("S1", 2000, 500, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.DC, "#FF0000", targetId);
            
            // Act
            int foundIndex = _table.FindIndex(targetId);
            
            // Assert
            Assert.AreEqual(expectedIndex, foundIndex);
        }
        
        [TestMethod]
        public void FindIndex_NonExistingEventId_ReturnsNegative()
        {
            // Arrange
            _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            Guid nonExistentId = Guid.NewGuid();
            
            // Act
            int foundIndex = _table.FindIndex(nonExistentId);
            
            // Assert
            Assert.AreEqual(-1, foundIndex);
        }
        
        [TestMethod]
        public void FindIndex_AfterRemoval_ReturnsNegativeForRemovedId()
        {
            // Arrange
            Guid targetId = Guid.NewGuid();
            int index = _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC, "#FF0000", targetId);
            
            // Act
            _table.RemoveAt(index);
            int foundIndex = _table.FindIndex(targetId);
            
            // Assert
            Assert.AreEqual(-1, foundIndex);
        }
        
        #endregion
        
        #region UpdateTiming Tests
        
        [TestMethod]
        public void UpdateTiming_ValidIndex_UpdatesCorrectly()
        {
            // Arrange
            int index = _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            long newStart = 5_000_000_000;
            long newDuration = 2_000_000_000;
            
            // Act
            _table.UpdateTiming(index, newStart, newDuration);
            
            // Assert
            Assert.AreEqual(newStart, _table.StartTimesNs[index]);
            Assert.AreEqual(newDuration, _table.DurationsNs[index]);
        }
        
        [TestMethod]
        public void UpdateTiming_InvalidIndex_DoesNotCrash()
        {
            // Arrange
            _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act - Should not throw
            _table.UpdateTiming(10, 5000, 1000);
            
            // Assert - Original data unchanged
            Assert.AreEqual(1000, _table.StartTimesNs[0]);
        }
        
        #endregion
        
        #region UpdateChannel Tests
        
        [TestMethod]
        public void UpdateChannel_ValidIndex_UpdatesAllChannelInfo()
        {
            // Arrange
            int index = _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            _table.UpdateChannel(index, 5, DeviceType.Digital, "PCI-1735U");
            
            // Assert
            Assert.AreEqual(5, _table.Channels[index]);
            Assert.AreEqual(DeviceType.Digital, _table.DeviceTypes[index]);
            Assert.AreEqual("PCI-1735U", _table.DeviceModels[index]);
        }
        
        [TestMethod]
        public void UpdateChannel_InvalidIndex_DoesNotCrash()
        {
            // Arrange
            _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act - Should not throw
            _table.UpdateChannel(10, 5, DeviceType.Digital, "PCI-1735U");
            
            // Assert - Original data unchanged
            Assert.AreEqual(0, _table.Channels[0]);
            Assert.AreEqual(DeviceType.Analog, _table.DeviceTypes[0]);
        }
        
        #endregion
        
        #region GetTimeRange Tests
        
        [TestMethod]
        public void GetTimeRange_ValidIndex_ReturnsCorrectRange()
        {
            // Arrange
            long start = 1_000_000_000;
            long duration = 500_000_000;
            int index = _table.AddSignal("S0", start, duration, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            var (actualStart, actualEnd) = _table.GetTimeRange(index);
            
            // Assert
            Assert.AreEqual(start, actualStart);
            Assert.AreEqual(start + duration, actualEnd);
        }
        
        #endregion
        
        #region Clear Tests
        
        [TestMethod]
        public void Clear_AfterAddingElements_ResetsCountToZero()
        {
            // Arrange
            _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.AddSignal("S1", 2000, 500, 1, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Act
            _table.Clear();
            
            // Assert
            Assert.AreEqual(0, _table.Count);
        }
        
        [TestMethod]
        public void Clear_AfterClear_CanAddNewElements()
        {
            // Arrange
            _table.AddSignal("S0", 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            _table.Clear();
            
            // Act
            int index = _table.AddSignal("New", 5000, 1000, 2, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            
            // Assert
            Assert.AreEqual(0, index);
            Assert.AreEqual(1, _table.Count);
        }
        
        #endregion
        
        #region Capacity and Resize Tests
        
        [TestMethod]
        public void Capacity_AfterInitialization_MatchesConstructorParameter()
        {
            // Arrange & Act
            var customTable = new SignalTable(128);
            
            // Assert
            Assert.AreEqual(128, customTable.Capacity);
        }
        
        [TestMethod]
        public void Resize_WhenTriggered_DoublesCapacity()
        {
            // Arrange
            var smallTable = new SignalTable(4);
            
            // Act - Add beyond capacity
            for (int i = 0; i < 5; i++)
            {
                smallTable.AddSignal($"S{i}", i * 1000, 500, 0, DeviceType.Analog, "PCIe-1824", SignalEventType.DC);
            }
            
            // Assert
            Assert.AreEqual(5, smallTable.Count);
            Assert.IsTrue(smallTable.Capacity >= 8); // Should have doubled from 4 to 8
        }
        
        #endregion
        
        #region GetEventDebugString Tests
        
        [TestMethod]
        public void GetEventDebugString_ValidIndex_ReturnsFormattedString()
        {
            // Arrange
            int index = _table.AddSignal("TestSignal", 1_000_000_000, 500_000_000, 3, DeviceType.Analog, "PCIe-1824", SignalEventType.Ramp);
            
            // Act
            string debugStr = _table.GetEventDebugString(index);
            
            // Assert
            StringAssert.Contains(debugStr, "TestSignal");
            StringAssert.Contains(debugStr, "Ramp");
            StringAssert.Contains(debugStr, "PCIe-1824");
            StringAssert.Contains(debugStr, "CH3");
        }
        
        [TestMethod]
        public void GetEventDebugString_InvalidIndex_ReturnsErrorMessage()
        {
            // Act
            string debugStr = _table.GetEventDebugString(100);
            
            // Assert
            Assert.AreEqual("Invalid index", debugStr);
        }
        
        #endregion
    }
}
