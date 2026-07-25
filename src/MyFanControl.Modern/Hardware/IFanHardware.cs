using MyFanControl.Modern.Models;

namespace MyFanControl.Modern.Hardware;

public interface IFanHardware : IDisposable
{
    string Name { get; }
    bool IsSimulation { get; }
    FanTelemetry Read(int channel);
    void SetDuty(int channel, int dutyPercent);
    void SetAutomatic(int channel);
}
