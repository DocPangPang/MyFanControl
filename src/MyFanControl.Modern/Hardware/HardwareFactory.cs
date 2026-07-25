namespace MyFanControl.Modern.Hardware;

public sealed record HardwareResult(IFanHardware Hardware, string? Warning);

public static class HardwareFactory
{
    public static HardwareResult Create()
    {
        try
        {
            return new HardwareResult(new ClevoEcHardware(), null);
        }
        catch (Exception exception)
        {
            string warning =
                $"未能加载真实硬件接口，当前处于演示模式。{Environment.NewLine}" +
                $"原因：{exception.Message}";
            return new HardwareResult(new SimulationFanHardware(), warning);
        }
    }
}
