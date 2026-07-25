using MyFanControl.Modern.Models;

namespace MyFanControl.Modern.Hardware;

public sealed class SimulationFanHardware : IFanHardware
{
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly int[] _duty = [28, 30];
    private readonly bool[] _automatic = [true, true];

    public string Name => "界面演示模式 · 未连接 Clevo EC";
    public bool IsSimulation => true;

    public FanTelemetry Read(int channel)
    {
        int index = Validate(channel);
        double seconds = (DateTime.UtcNow - _startedAt).TotalSeconds;
        int baseTemperature = channel == 1 ? 57 : 53;
        int temperature = baseTemperature + (int)Math.Round(Math.Sin(seconds / 7 + channel) * 8);

        if (_automatic[index])
            _duty[index] = Math.Clamp(22 + (temperature - 45) * 2, 18, 100);

        int rpm = _duty[index] == 0 ? 0 : 900 + _duty[index] * 38;
        return new FanTelemetry(temperature, _duty[index], rpm);
    }

    public void SetDuty(int channel, int dutyPercent)
    {
        int index = Validate(channel);
        _automatic[index] = false;
        _duty[index] = Math.Clamp(dutyPercent, 0, 100);
    }

    public void SetAutomatic(int channel)
    {
        int index = Validate(channel);
        _automatic[index] = true;
    }

    public void Dispose()
    {
    }

    private static int Validate(int channel) =>
        channel is 1 or 2 ? channel - 1 : throw new ArgumentOutOfRangeException(nameof(channel));
}
