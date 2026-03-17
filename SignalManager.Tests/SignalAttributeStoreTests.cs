using Microsoft.VisualStudio.TestTools.UnitTesting;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented;
using System;

namespace LAMP_DAQ_Control_v0_8.SignalManager.Tests
{
    /// <summary>
    /// Unit tests for SignalAttributeStore - Sparse attribute storage
    /// Tests cover: Set/Get operations, Swap, Clear
    /// </summary>
    [TestClass]
    public class SignalAttributeStoreTests
    {
        private SignalAttributeStore _store;
        
        [TestInitialize]
        public void Setup()
        {
            _store = new SignalAttributeStore(64);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _store = null;
        }
        
        #region Ramp Attributes Tests
        
        [TestMethod]
        public void SetStartVoltage_ValidValue_StoresCorrectly()
        {
            // Arrange
            int index = 5;
            double voltage = 3.5;
            
            // Act
            _store.SetStartVoltage(index, voltage);
            double retrieved = _store.GetStartVoltage(index);
            
            // Assert
            Assert.AreEqual(voltage, retrieved);
        }
        
        [TestMethod]
        public void GetStartVoltage_NonExistentIndex_ReturnsDefaultValue()
        {
            // Arrange
            int index = 10;
            double defaultValue = -1.0;
            
            // Act
            double retrieved = _store.GetStartVoltage(index, defaultValue);
            
            // Assert
            Assert.AreEqual(defaultValue, retrieved);
        }
        
        [TestMethod]
        public void SetEndVoltage_ValidValue_StoresCorrectly()
        {
            // Arrange
            int index = 3;
            double voltage = 7.8;
            
            // Act
            _store.SetEndVoltage(index, voltage);
            double retrieved = _store.GetEndVoltage(index);
            
            // Assert
            Assert.AreEqual(voltage, retrieved);
        }
        
        [TestMethod]
        public void GetEndVoltage_NonExistentIndex_ReturnsDefaultValue()
        {
            // Arrange
            int index = 20;
            
            // Act
            double retrieved = _store.GetEndVoltage(index, 99.9);
            
            // Assert
            Assert.AreEqual(99.9, retrieved);
        }
        
        [TestMethod]
        public void RampAttributes_MultipleIndices_StoresIndependently()
        {
            // Arrange & Act
            _store.SetStartVoltage(0, 0.0);
            _store.SetEndVoltage(0, 5.0);
            
            _store.SetStartVoltage(1, 2.5);
            _store.SetEndVoltage(1, 7.5);
            
            // Assert
            Assert.AreEqual(0.0, _store.GetStartVoltage(0));
            Assert.AreEqual(5.0, _store.GetEndVoltage(0));
            Assert.AreEqual(2.5, _store.GetStartVoltage(1));
            Assert.AreEqual(7.5, _store.GetEndVoltage(1));
        }
        
        #endregion
        
        #region DC Attributes Tests
        
        [TestMethod]
        public void SetVoltage_ValidValue_StoresCorrectly()
        {
            // Arrange
            int index = 7;
            double voltage = 4.2;
            
            // Act
            _store.SetVoltage(index, voltage);
            double retrieved = _store.GetVoltage(index);
            
            // Assert
            Assert.AreEqual(voltage, retrieved);
        }
        
        [TestMethod]
        public void GetVoltage_NonExistentIndex_ReturnsDefaultValue()
        {
            // Arrange
            int index = 15;
            
            // Act
            double retrieved = _store.GetVoltage(index, 10.0);
            
            // Assert
            Assert.AreEqual(10.0, retrieved);
        }
        
        [TestMethod]
        public void Voltage_OverwriteExisting_UpdatesValue()
        {
            // Arrange
            int index = 2;
            _store.SetVoltage(index, 3.3);
            
            // Act
            _store.SetVoltage(index, 6.6);
            double retrieved = _store.GetVoltage(index);
            
            // Assert
            Assert.AreEqual(6.6, retrieved);
        }
        
        #endregion
        
        #region Waveform Attributes Tests
        
        [TestMethod]
        public void SetWaveformParams_ValidValues_StoresAllCorrectly()
        {
            // Arrange
            int index = 4;
            double freq = 100.0;
            double amp = 5.0;
            double offset = 2.5;
            
            // Act
            _store.SetWaveformParams(index, freq, amp, offset);
            var (retrievedFreq, retrievedAmp, retrievedOffset) = _store.GetWaveformParams(index);
            
            // Assert
            Assert.AreEqual(freq, retrievedFreq);
            Assert.AreEqual(amp, retrievedAmp);
            Assert.AreEqual(offset, retrievedOffset);
        }
        
        [TestMethod]
        public void GetWaveformParams_NonExistentIndex_ReturnsZeros()
        {
            // Arrange
            int index = 25;
            
            // Act
            var (freq, amp, offset) = _store.GetWaveformParams(index);
            
            // Assert
            Assert.AreEqual(0.0, freq);
            Assert.AreEqual(0.0, amp);
            Assert.AreEqual(0.0, offset);
        }
        
        [TestMethod]
        public void SetWaveformParams_MultipleIndices_StoresIndependently()
        {
            // Arrange & Act
            _store.SetWaveformParams(0, 50.0, 3.0, 1.0);
            _store.SetWaveformParams(1, 200.0, 7.0, 2.0);
            
            var (f0, a0, o0) = _store.GetWaveformParams(0);
            var (f1, a1, o1) = _store.GetWaveformParams(1);
            
            // Assert
            Assert.AreEqual(50.0, f0);
            Assert.AreEqual(3.0, a0);
            Assert.AreEqual(1.0, o0);
            
            Assert.AreEqual(200.0, f1);
            Assert.AreEqual(7.0, a1);
            Assert.AreEqual(2.0, o1);
        }
        
        #endregion
        
        #region Swap Tests
        
        [TestMethod]
        public void Swap_BothIndicesHaveData_SwapsCorrectly()
        {
            // Arrange
            _store.SetStartVoltage(0, 1.0);
            _store.SetEndVoltage(0, 2.0);
            
            _store.SetStartVoltage(1, 3.0);
            _store.SetEndVoltage(1, 4.0);
            
            // Act
            _store.Swap(0, 1);
            
            // Assert
            Assert.AreEqual(3.0, _store.GetStartVoltage(0));
            Assert.AreEqual(4.0, _store.GetEndVoltage(0));
            Assert.AreEqual(1.0, _store.GetStartVoltage(1));
            Assert.AreEqual(2.0, _store.GetEndVoltage(1));
        }
        
        [TestMethod]
        public void Swap_OneIndexHasData_SwapsCorrectly()
        {
            // Arrange
            _store.SetVoltage(5, 5.5);
            // Index 10 has no data
            
            // Act
            _store.Swap(5, 10);
            
            // Assert
            Assert.AreEqual(0.0, _store.GetVoltage(5, 0.0)); // Now empty
            Assert.AreEqual(5.5, _store.GetVoltage(10));      // Now has data
        }
        
        [TestMethod]
        public void Swap_NeitherIndexHasData_DoesNotCrash()
        {
            // Act - Should not throw
            _store.Swap(20, 30);
            
            // Assert - Both still empty
            Assert.AreEqual(0.0, _store.GetVoltage(20, 0.0));
            Assert.AreEqual(0.0, _store.GetVoltage(30, 0.0));
        }
        
        [TestMethod]
        public void Swap_AllAttributeTypes_SwapsAll()
        {
            // Arrange - Set all attribute types for index 0
            _store.SetStartVoltage(0, 1.0);
            _store.SetEndVoltage(0, 2.0);
            _store.SetVoltage(0, 3.0);
            _store.SetWaveformParams(0, 100.0, 4.0, 5.0);
            
            // Set different values for index 1
            _store.SetStartVoltage(1, 10.0);
            _store.SetEndVoltage(1, 20.0);
            _store.SetVoltage(1, 30.0);
            _store.SetWaveformParams(1, 200.0, 40.0, 50.0);
            
            // Act
            _store.Swap(0, 1);
            
            // Assert - All attributes swapped
            Assert.AreEqual(10.0, _store.GetStartVoltage(0));
            Assert.AreEqual(20.0, _store.GetEndVoltage(0));
            Assert.AreEqual(30.0, _store.GetVoltage(0));
            var (f0, a0, o0) = _store.GetWaveformParams(0);
            Assert.AreEqual(200.0, f0);
            Assert.AreEqual(40.0, a0);
            Assert.AreEqual(50.0, o0);
            
            Assert.AreEqual(1.0, _store.GetStartVoltage(1));
            Assert.AreEqual(2.0, _store.GetEndVoltage(1));
            Assert.AreEqual(3.0, _store.GetVoltage(1));
            var (f1, a1, o1) = _store.GetWaveformParams(1);
            Assert.AreEqual(100.0, f1);
            Assert.AreEqual(4.0, a1);
            Assert.AreEqual(5.0, o1);
        }
        
        #endregion
        
        #region Clear Tests
        
        [TestMethod]
        public void Clear_ExistingData_RemovesAllAttributes()
        {
            // Arrange
            int index = 3;
            _store.SetStartVoltage(index, 1.0);
            _store.SetEndVoltage(index, 2.0);
            _store.SetVoltage(index, 3.0);
            _store.SetWaveformParams(index, 100.0, 4.0, 5.0);
            
            // Act
            _store.Clear(index);
            
            // Assert - All should return default values
            Assert.AreEqual(-99.0, _store.GetStartVoltage(index, -99.0));
            Assert.AreEqual(-99.0, _store.GetEndVoltage(index, -99.0));
            Assert.AreEqual(-99.0, _store.GetVoltage(index, -99.0));
            var (f, a, o) = _store.GetWaveformParams(index);
            Assert.AreEqual(0.0, f);
            Assert.AreEqual(0.0, a);
            Assert.AreEqual(0.0, o);
        }
        
        [TestMethod]
        public void Clear_NonExistentData_DoesNotCrash()
        {
            // Act - Should not throw
            _store.Clear(100);
            
            // Assert - Still empty
            Assert.AreEqual(0.0, _store.GetVoltage(100, 0.0));
        }
        
        [TestMethod]
        public void Clear_ThenSet_WorksCorrectly()
        {
            // Arrange
            int index = 7;
            _store.SetVoltage(index, 5.0);
            
            // Act
            _store.Clear(index);
            _store.SetVoltage(index, 8.0);
            
            // Assert
            Assert.AreEqual(8.0, _store.GetVoltage(index));
        }
        
        #endregion
        
        #region Resize Tests
        
        [TestMethod]
        public void Resize_LargerCapacity_DoesNotLoseData()
        {
            // Arrange
            _store.SetVoltage(5, 5.5);
            _store.SetVoltage(10, 10.5);
            
            // Act
            _store.Resize(128); // Dictionaries auto-resize
            
            // Assert - Data still accessible
            Assert.AreEqual(5.5, _store.GetVoltage(5));
            Assert.AreEqual(10.5, _store.GetVoltage(10));
        }
        
        #endregion
        
        #region Mixed Scenarios Tests
        
        [TestMethod]
        public void MixedScenario_RampAndWaveformOnSameIndex_BothStored()
        {
            // Arrange & Act - Store both Ramp and Waveform attributes on same index
            int index = 12;
            _store.SetStartVoltage(index, 0.0);
            _store.SetEndVoltage(index, 10.0);
            _store.SetWaveformParams(index, 50.0, 5.0, 2.5);
            
            // Assert - Both types accessible (even though semantically weird)
            Assert.AreEqual(0.0, _store.GetStartVoltage(index));
            Assert.AreEqual(10.0, _store.GetEndVoltage(index));
            var (f, a, o) = _store.GetWaveformParams(index);
            Assert.AreEqual(50.0, f);
            Assert.AreEqual(5.0, a);
            Assert.AreEqual(2.5, o);
        }
        
        [TestMethod]
        public void SparseStorage_ManyIndices_OnlyStoresNonZero()
        {
            // Arrange & Act - Set attributes on sparse indices
            _store.SetVoltage(0, 1.0);
            _store.SetVoltage(50, 2.0);
            _store.SetVoltage(100, 3.0);
            
            // Assert - No memory waste on empty indices 1-49, 51-99
            Assert.AreEqual(1.0, _store.GetVoltage(0));
            Assert.AreEqual(0.0, _store.GetVoltage(25, 0.0)); // Empty
            Assert.AreEqual(2.0, _store.GetVoltage(50));
            Assert.AreEqual(0.0, _store.GetVoltage(75, 0.0)); // Empty
            Assert.AreEqual(3.0, _store.GetVoltage(100));
        }
        
        #endregion
    }
}
