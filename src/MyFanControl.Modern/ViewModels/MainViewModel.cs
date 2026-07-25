using System.Collections.ObjectModel;
using System.Windows;
using MyFanControl.Modern.Hardware;
using MyFanControl.Modern.Models;
using MyFanControl.Modern.Services;

namespace MyFanControl.Modern.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;
    private readonly FanControlEngine _engine;
    private FanProfile _selectedProfile;
    private bool _isTakeover;
    private bool _isForcedCooling;
    private bool _linearInterpolation;
    private bool _startWithWindows;
    private int _hysteresisDegrees;
    private int _updateIntervalSeconds;
    private int _forceCoolingTarget;
    private int _cpuTemperature;
    private int _gpuTemperature;
    private int _cpuDuty;
    private int _gpuDuty;
    private int _cpuRpm;
    private int _gpuRpm;
    private string _hardwareName;
    private string _statusMessage;
    private bool _isSimulation;

    public MainViewModel()
    {
        _settings = _settingsService.Load();
        Profiles = new ObservableCollection<FanProfile>(PresetProfiles.CreateDefaults());
        FanProfile balanced = Profiles.First(profile => profile.Id == "balanced");
        FanProfile custom = PresetProfiles.CreateCustom(balanced);

        if (_settings.CustomCpuPoints.Count >= 2 && _settings.CustomGpuPoints.Count >= 2)
        {
            custom.CpuPoints = SettingsService.ToPoints(_settings.CustomCpuPoints);
            custom.GpuPoints = SettingsService.ToPoints(_settings.CustomGpuPoints);
        }

        Profiles.Add(custom);
        _selectedProfile = Profiles.FirstOrDefault(profile => profile.Id == _settings.SelectedProfileId) ?? balanced;
        _linearInterpolation = _settings.LinearInterpolation;
        _hysteresisDegrees = Math.Clamp(_settings.HysteresisDegrees, 0, 10);
        _updateIntervalSeconds = Math.Clamp(_settings.UpdateIntervalSeconds, 1, 5);
        _forceCoolingTarget = Math.Clamp(_settings.ForceCoolingTarget, 40, 90);
        _startWithWindows = StartupService.IsEnabled();

        HardwareResult hardwareResult = HardwareFactory.Create();
        _hardwareName = hardwareResult.Hardware.Name;
        _statusMessage = hardwareResult.Warning ?? "硬件接口已就绪，当前由原厂 EC 自动控制。";
        _isSimulation = hardwareResult.Hardware.IsSimulation;
        _engine = new FanControlEngine(hardwareResult.Hardware, _selectedProfile)
        {
            LinearInterpolation = _linearInterpolation,
            HysteresisDegrees = _hysteresisDegrees,
            UpdateIntervalSeconds = _updateIntervalSeconds,
            ForceCoolingTarget = _forceCoolingTarget
        };
        _engine.SnapshotUpdated += EngineOnSnapshotUpdated;

        SaveCommand = new RelayCommand(Save);
        ResetPresetCommand = new RelayCommand(ResetPreset);
        ForceCoolingCommand = new RelayCommand(StartForcedCooling);
        RestoreAutomaticCommand = new RelayCommand(RestoreAutomatic);
    }

    public ObservableCollection<FanProfile> Profiles { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetPresetCommand { get; }
    public RelayCommand ForceCoolingCommand { get; }
    public RelayCommand RestoreAutomaticCommand { get; }

    public FanProfile SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetField(ref _selectedProfile, value) || value is null)
                return;
            _engine.SetProfile(value);
            OnPropertyChanged(nameof(ProfileDescription));
        }
    }

    public string ProfileDescription => SelectedProfile.Description;

    public bool IsTakeover
    {
        get => _isTakeover;
        set
        {
            if (!SetField(ref _isTakeover, value))
                return;
            if (value)
                _engine.IsTakeover = true;
            else
                _engine.RestoreAutomatic();
            StatusMessage = value
                ? "风扇曲线已接管。关闭开关或退出程序会恢复原厂自动控制。"
                : "已恢复原厂 EC 自动控制。";
        }
    }

    public bool IsForcedCooling
    {
        get => _isForcedCooling;
        private set
        {
            if (SetField(ref _isForcedCooling, value))
                OnPropertyChanged(nameof(ForceCoolingButtonText));
        }
    }

    public string ForceCoolingButtonText => IsForcedCooling ? "停止强制冷却" : "强制冷却";

    public bool LinearInterpolation
    {
        get => _linearInterpolation;
        set
        {
            if (SetField(ref _linearInterpolation, value))
                _engine.LinearInterpolation = value;
        }
    }

    public int HysteresisDegrees
    {
        get => _hysteresisDegrees;
        set
        {
            if (SetField(ref _hysteresisDegrees, value))
                _engine.HysteresisDegrees = value;
        }
    }

    public int UpdateIntervalSeconds
    {
        get => _updateIntervalSeconds;
        set
        {
            if (SetField(ref _updateIntervalSeconds, value))
                _engine.UpdateIntervalSeconds = value;
        }
    }

    public int ForceCoolingTarget
    {
        get => _forceCoolingTarget;
        set
        {
            if (SetField(ref _forceCoolingTarget, value))
                _engine.ForceCoolingTarget = value;
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (!SetField(ref _startWithWindows, value))
                return;

            try
            {
                StartupService.SetEnabled(value);
            }
            catch (Exception exception)
            {
                _startWithWindows = !value;
                OnPropertyChanged();
                StatusMessage = $"无法修改开机启动：{exception.Message}";
            }
        }
    }

    public int CpuTemperature { get => _cpuTemperature; private set => SetField(ref _cpuTemperature, value); }
    public int GpuTemperature { get => _gpuTemperature; private set => SetField(ref _gpuTemperature, value); }
    public int CpuDuty { get => _cpuDuty; private set => SetField(ref _cpuDuty, value); }
    public int GpuDuty { get => _gpuDuty; private set => SetField(ref _gpuDuty, value); }
    public int CpuRpm { get => _cpuRpm; private set => SetField(ref _cpuRpm, value); }
    public int GpuRpm { get => _gpuRpm; private set => SetField(ref _gpuRpm, value); }
    public string HardwareName { get => _hardwareName; private set => SetField(ref _hardwareName, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public bool IsSimulation { get => _isSimulation; private set => SetField(ref _isSimulation, value); }

    public void Start() => _engine.Start();

    private void StartForcedCooling()
    {
        if (IsForcedCooling)
        {
            _engine.StopForcedCooling();
            IsForcedCooling = false;
            StatusMessage = "强制冷却已停止；风扇将继续使用当前接管状态。";
            return;
        }

        _engine.StartForcedCooling();
        IsForcedCooling = true;
        StatusMessage = $"强制冷却已启动，CPU 与 GPU 均低于 {ForceCoolingTarget}℃ 后自动停止。";
    }

    private void RestoreAutomatic()
    {
        _engine.RestoreAutomatic();
        IsTakeover = false;
        IsForcedCooling = false;
        StatusMessage = "已恢复原厂 EC 自动控制。";
    }

    private void ResetPreset()
    {
        FanProfile? original = PresetProfiles.CreateDefaults()
            .FirstOrDefault(profile => profile.Id == SelectedProfile.Id);
        if (original is null)
            original = PresetProfiles.CreateDefaults().First(profile => profile.Id == "balanced");

        int index = Profiles.IndexOf(SelectedProfile);
        FanProfile replacement = SelectedProfile.Id == "custom"
            ? PresetProfiles.CreateCustom(original)
            : original;
        Profiles[index] = replacement;
        SelectedProfile = replacement;
        StatusMessage = "当前曲线已恢复为预设值。";
    }

    private void Save()
    {
        FanProfile custom = Profiles.First(profile => profile.Id == "custom");
        if (SelectedProfile.Id != "custom")
        {
            int customIndex = Profiles.IndexOf(custom);
            custom = SelectedProfile.Clone("custom", "自定义", false);
            Profiles[customIndex] = custom;
            SelectedProfile = custom;
        }

        _settings.SelectedProfileId = SelectedProfile.Id;
        _settings.LinearInterpolation = LinearInterpolation;
        _settings.HysteresisDegrees = HysteresisDegrees;
        _settings.UpdateIntervalSeconds = UpdateIntervalSeconds;
        _settings.ForceCoolingTarget = ForceCoolingTarget;
        _settings.StartWithWindows = StartWithWindows;
        _settings.CustomCpuPoints = SettingsService.FromPoints(custom.CpuPoints);
        _settings.CustomGpuPoints = SettingsService.FromPoints(custom.GpuPoints);
        _settingsService.Save(_settings);
        StatusMessage = "当前曲线和控制设置已保存为“自定义”。";
    }

    private void EngineOnSnapshotUpdated(object? sender, MonitoringSnapshot snapshot)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            CpuTemperature = snapshot.Cpu.Temperature;
            GpuTemperature = snapshot.Gpu.Temperature;
            CpuDuty = snapshot.Cpu.Duty;
            GpuDuty = snapshot.Gpu.Duty;
            CpuRpm = snapshot.Cpu.Rpm;
            GpuRpm = snapshot.Gpu.Rpm;
            HardwareName = snapshot.HardwareName;
            IsForcedCooling = snapshot.IsForcedCooling;
            if (snapshot.Warning is not null)
                StatusMessage = snapshot.Warning;
        });
    }

    public async ValueTask DisposeAsync()
    {
        _engine.SnapshotUpdated -= EngineOnSnapshotUpdated;
        await _engine.DisposeAsync();
    }
}
