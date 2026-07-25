using System.Collections.ObjectModel;

namespace MyFanControl.Modern.Models;

public sealed class FanProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool IsBuiltIn { get; init; }
    public ObservableCollection<FanCurvePoint> CpuPoints { get; set; } = [];
    public ObservableCollection<FanCurvePoint> GpuPoints { get; set; } = [];

    public FanProfile Clone(string? id = null, string? name = null, bool? isBuiltIn = null) => new()
    {
        Id = id ?? Id,
        Name = name ?? Name,
        Description = Description,
        IsBuiltIn = isBuiltIn ?? IsBuiltIn,
        CpuPoints = new ObservableCollection<FanCurvePoint>(CpuPoints.Select(point => point.Clone())),
        GpuPoints = new ObservableCollection<FanCurvePoint>(GpuPoints.Select(point => point.Clone()))
    };

    public override string ToString() => Name;
}
