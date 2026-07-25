using MyFanControl.Modern.Hardware;
using MyFanControl.Modern.Models;

namespace MyFanControl.Modern.Services;

public sealed class FanControlEngine : IAsyncDisposable
{
    private readonly IFanHardware _hardware;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _stateLock = new();
    private readonly object _hardwareLock = new();
    private Task? _worker;
    private FanProfile _profile;
    private int[] _lastTemperature = [0, 0];
    private int[] _lastAppliedDuty = [-1, -1];
    private bool _wasControlling;
    private int _consecutiveErrors;

    public FanControlEngine(IFanHardware hardware, FanProfile profile)
    {
        _hardware = hardware;
        _profile = profile;
    }

    public event EventHandler<MonitoringSnapshot>? SnapshotUpdated;

    public bool IsTakeover { get; set; }
    public bool IsForcedCooling { get; private set; }
    public bool LinearInterpolation { get; set; } = true;
    public int HysteresisDegrees { get; set; } = 3;
    public int UpdateIntervalSeconds { get; set; } = 2;
    public int ForceCoolingTarget { get; set; } = 50;

    public void SetProfile(FanProfile profile)
    {
        lock (_stateLock)
            _profile = profile;
    }

    public void Start()
    {
        _worker ??= Task.Run(() => RunAsync(_cancellation.Token));
    }

    public void StartForcedCooling() => IsForcedCooling = true;
    public void StopForcedCooling() => IsForcedCooling = false;

    public void RestoreAutomatic()
    {
        IsTakeover = false;
        IsForcedCooling = false;
        TryRestoreAutomatic();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                FanTelemetry cpu;
                FanTelemetry gpu;
                lock (_hardwareLock)
                {
                    cpu = _hardware.Read(1);
                    gpu = _hardware.Read(2);
                }
                _consecutiveErrors = 0;

                if (IsForcedCooling && cpu.Temperature < ForceCoolingTarget && gpu.Temperature < ForceCoolingTarget)
                    IsForcedCooling = false;

                if (IsTakeover || IsForcedCooling)
                {
                    FanProfile profile;
                    lock (_stateLock)
                        profile = _profile;

                    int cpuDuty = IsForcedCooling
                        ? 95
                        : CalculateDuty(profile.CpuPoints, cpu.Temperature, _lastTemperature[0]);
                    int gpuDuty = IsForcedCooling
                        ? 95
                        : CalculateDuty(profile.GpuPoints, gpu.Temperature, _lastTemperature[1]);

                    ApplyDuty(1, cpuDuty);
                    ApplyDuty(2, gpuDuty);
                    _wasControlling = true;
                }
                else if (_wasControlling)
                {
                    RestoreAutomaticCore();
                }

                _lastTemperature[0] = cpu.Temperature;
                _lastTemperature[1] = gpu.Temperature;
                SnapshotUpdated?.Invoke(this, new MonitoringSnapshot(
                    cpu, gpu, IsTakeover, IsForcedCooling, _hardware.Name));
            }
            catch (Exception exception)
            {
                _consecutiveErrors++;
                if (_consecutiveErrors >= 3)
                    TryRestoreAutomatic();

                SnapshotUpdated?.Invoke(this, new MonitoringSnapshot(
                    new FanTelemetry(0, 0, 0),
                    new FanTelemetry(0, 0, 0),
                    false,
                    false,
                    _hardware.Name,
                    $"硬件读取失败（{_consecutiveErrors}/3）：{exception.Message}"));
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Clamp(UpdateIntervalSeconds, 1, 5)),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private int CalculateDuty(IReadOnlyList<FanCurvePoint> points, int temperature, int previousTemperature)
    {
        if (points.Count == 0)
            return 100;

        double effectiveTemperature = temperature < previousTemperature
            ? temperature + Math.Clamp(HysteresisDegrees, 0, 10)
            : temperature;

        FanCurvePoint[] ordered = points.OrderBy(point => point.Temperature).ToArray();
        if (effectiveTemperature <= ordered[0].Temperature)
            return (int)ordered[0].Duty;
        if (effectiveTemperature >= ordered[^1].Temperature)
            return (int)ordered[^1].Duty;

        for (int index = 1; index < ordered.Length; index++)
        {
            FanCurvePoint upper = ordered[index];
            if (effectiveTemperature > upper.Temperature)
                continue;

            FanCurvePoint lower = ordered[index - 1];
            if (!LinearInterpolation)
                return (int)lower.Duty;

            double range = upper.Temperature - lower.Temperature;
            double ratio = range <= 0 ? 0 : (effectiveTemperature - lower.Temperature) / range;
            return (int)Math.Round(lower.Duty + (upper.Duty - lower.Duty) * ratio);
        }

        return (int)ordered[^1].Duty;
    }

    private void ApplyDuty(int channel, int duty)
    {
        int index = channel - 1;
        duty = Math.Clamp(duty, 0, 100);
        if (_lastAppliedDuty[index] == duty)
            return;

        lock (_hardwareLock)
            _hardware.SetDuty(channel, duty);
        _lastAppliedDuty[index] = duty;
    }

    private void RestoreAutomaticCore()
    {
        try
        {
            lock (_hardwareLock)
            {
                _hardware.SetAutomatic(1);
                _hardware.SetAutomatic(2);
            }
        }
        finally
        {
            _wasControlling = false;
            _lastAppliedDuty = [-1, -1];
        }
    }

    private void TryRestoreAutomatic()
    {
        try
        {
            RestoreAutomaticCore();
        }
        catch
        {
            // The hardware interface is already failing. Do not let a failed best-effort
            // reset terminate the monitoring worker; a healthy EC retains its own fallback.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        if (_worker is not null)
            await _worker.ConfigureAwait(false);

        TryRestoreAutomatic();
        try
        {
            lock (_hardwareLock)
                _hardware.Dispose();
        }
        catch
        {
            // Shutdown must complete even if the legacy native component is unavailable.
        }
        _cancellation.Dispose();
    }
}
