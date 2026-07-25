namespace MyFanControl.Modern.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string SelectedProfileId { get; set; } = "balanced";
    public int UpdateIntervalSeconds { get; set; } = 2;
    public int HysteresisDegrees { get; set; } = 3;
    public int ForceCoolingTarget { get; set; } = 50;
    public bool LinearInterpolation { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public List<FanCurvePointData> CustomCpuPoints { get; set; } = [];
    public List<FanCurvePointData> CustomGpuPoints { get; set; } = [];
}

public sealed class FanCurvePointData
{
    public double Temperature { get; set; }
    public double Duty { get; set; }
}
