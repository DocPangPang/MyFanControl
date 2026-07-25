using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyFanControl.Modern.Models;

public sealed class FanCurvePoint : INotifyPropertyChanged
{
    private double _temperature;
    private double _duty;

    public FanCurvePoint(double temperature, double duty)
    {
        _temperature = temperature;
        _duty = duty;
    }

    public double Temperature
    {
        get => _temperature;
        set => SetField(ref _temperature, Math.Round(value));
    }

    public double Duty
    {
        get => _duty;
        set => SetField(ref _duty, Math.Round(value));
    }

    public FanCurvePoint Clone() => new(Temperature, Duty);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref double field, double value, [CallerMemberName] string? propertyName = null)
    {
        if (Math.Abs(field - value) < 0.01)
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
