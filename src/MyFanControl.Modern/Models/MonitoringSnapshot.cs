namespace MyFanControl.Modern.Models;

public sealed record FanTelemetry(int Temperature, int Duty, int Rpm);

public sealed record MonitoringSnapshot(
    FanTelemetry Cpu,
    FanTelemetry Gpu,
    bool IsTakeover,
    bool IsForcedCooling,
    string HardwareName,
    string? Warning = null);
