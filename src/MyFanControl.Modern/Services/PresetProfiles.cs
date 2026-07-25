using System.Collections.ObjectModel;
using MyFanControl.Modern.Models;

namespace MyFanControl.Modern.Services;

public static class PresetProfiles
{
    public static IReadOnlyList<FanProfile> CreateDefaults()
    {
        return
        [
            Create(
                "quiet", "安静", "降低轻载噪声，适合办公和视频",
                [18, 20, 24, 32, 46, 68, 95],
                [18, 20, 25, 34, 50, 72, 95]),
            Create(
                "balanced", "均衡", "温度、噪声与响应速度的平衡方案",
                [22, 25, 31, 42, 58, 78, 100],
                [22, 26, 33, 46, 63, 82, 100]),
            Create(
                "performance", "性能", "更积极地提升转速，适合持续高负载",
                [30, 36, 45, 58, 74, 90, 100],
                [32, 38, 48, 62, 78, 92, 100])
        ];
    }

    public static FanProfile CreateCustom(FanProfile source) =>
        source.Clone("custom", "自定义", false);

    private static FanProfile Create(
        string id,
        string name,
        string description,
        IReadOnlyList<int> cpuDuty,
        IReadOnlyList<int> gpuDuty)
    {
        int[] temperatures = [40, 50, 60, 70, 80, 85, 90];
        return new FanProfile
        {
            Id = id,
            Name = name,
            Description = description,
            IsBuiltIn = true,
            CpuPoints = new ObservableCollection<FanCurvePoint>(
                temperatures.Select((temperature, index) => new FanCurvePoint(temperature, cpuDuty[index]))),
            GpuPoints = new ObservableCollection<FanCurvePoint>(
                temperatures.Select((temperature, index) => new FanCurvePoint(temperature, gpuDuty[index])))
        };
    }
}
