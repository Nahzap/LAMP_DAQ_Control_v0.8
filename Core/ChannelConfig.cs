using Automation.BDaq;

namespace LAMP_DAQ_Control_v0._8.Core
{
    public class ChannelConfig
    {
        public int Channel { get; set; }
        public ValueRange Range { get; set; }
        public double CurrentValue { get; set; }

        public ChannelConfig(int channel, ValueRange range)
        {
            Channel = channel;
            Range = range;
            CurrentValue = 0.0;
        }

        public double GetMinValue()
        {
            return Range == ValueRange.V_Neg10To10 ? -10 :
                   Range == ValueRange.mA_0To20 ? 0 : 4;
        }

        public double GetMaxValue()
        {
            return Range == ValueRange.V_Neg10To10 ? 10 : 20;
        }

        public string GetUnit()
        {
            return Range == ValueRange.V_Neg10To10 ? "V" : "mA";
        }
    }
}
