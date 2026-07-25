using System.IO;
using System.Runtime.InteropServices;
using MyFanControl.Modern.Models;

namespace MyFanControl.Modern.Hardware;

public sealed class ClevoEcHardware : IFanHardware
{
    private readonly nint _library;
    private readonly SetFanDutyDelegate _setFanDuty;
    private readonly SetFanDutyAutoDelegate _setFanDutyAuto;
    private readonly GetTempFanDutyDelegate _getTempFanDuty;
    private readonly GetFanRpmDelegate _getCpuFanRpm;
    private readonly GetFanRpmDelegate _getGpuFanRpm;
    private readonly int _fanCount;
    private readonly string _ecVersion;
    private bool _disposed;

    public ClevoEcHardware()
    {
        string nativeDirectory = Path.Combine(AppContext.BaseDirectory, "native");
        string dllPath = File.Exists(Path.Combine(nativeDirectory, "ClevoEcInfo.dll"))
            ? Path.Combine(nativeDirectory, "ClevoEcInfo.dll")
            : Path.Combine(AppContext.BaseDirectory, "ClevoEcInfo.dll");

        if (!File.Exists(dllPath))
            throw new FileNotFoundException(
                "未找到 ClevoEcInfo.dll。请先阅读“第三方运行组件”文档。",
                dllPath);

        _library = NativeLibrary.Load(dllPath);
        try
        {
            var initIo = Load<InitIoDelegate>("InitIo");
            var getEcVersion = Load<GetEcVersionDelegate>("GetECVersion");
            _setFanDuty = Load<SetFanDutyDelegate>("SetFanDuty");
            _setFanDutyAuto = Load<SetFanDutyAutoDelegate>("SetFanDutyAuto");
            _getTempFanDuty = Load<GetTempFanDutyDelegate>("GetTempFanDuty");
            _getCpuFanRpm = Load<GetFanRpmDelegate>("GetCpuFanRpm");
            _getGpuFanRpm = Load<GetFanRpmDelegate>("GetGpuFanRpm");
            var getFanCount = Load<GetFanCountDelegate>("GetFanCount");

            if (initIo() != 1)
                throw new InvalidOperationException(
                    "Clevo EC 接口初始化失败。请确认 NTPortDrv 已安装且未被 Windows 安全策略拦截。");

            _fanCount = Math.Max(2, getFanCount());
            _ecVersion = Marshal.PtrToStringAnsi(getEcVersion()) ?? "未知版本";
        }
        catch
        {
            NativeLibrary.Free(_library);
            throw;
        }
    }

    public string Name => $"Clevo EC {_ecVersion} · {_fanCount} 个风扇通道";
    public bool IsSimulation => false;

    public FanTelemetry Read(int channel)
    {
        ThrowIfDisposed();
        ValidateChannel(channel);
        uint raw = _getTempFanDuty(channel);
        int temperature = (byte)(raw & 0xff);
        int dutyRaw = (byte)((raw >> 16) & 0xff);
        int counter = channel == 1 ? _getCpuFanRpm() : _getGpuFanRpm();
        int rpm = counter is > 300 and < 5000 ? 2_100_000 / counter : 0;
        int duty = (int)Math.Round(dutyRaw * 100d / 255d);
        return new FanTelemetry(temperature, duty, rpm);
    }

    public void SetDuty(int channel, int dutyPercent)
    {
        ThrowIfDisposed();
        ValidateChannel(channel);
        int rawDuty = (int)Math.Round(Math.Clamp(dutyPercent, 0, 100) * 255d / 100d);
        _setFanDuty(channel, rawDuty);

        // The legacy implementation mirrors GPU duty to the optional third channel.
        if (channel == 2 && _fanCount >= 3)
            _setFanDuty(3, rawDuty);
    }

    public void SetAutomatic(int channel)
    {
        if (_disposed)
            return;

        ValidateChannel(channel);
        _setFanDutyAuto(channel);
        if (channel == 2 && _fanCount >= 3)
            _setFanDutyAuto(3);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            SetAutomatic(1);
            SetAutomatic(2);
        }
        finally
        {
            _disposed = true;
            NativeLibrary.Free(_library);
        }
    }

    private T Load<T>(string export) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, export));

    private static void ValidateChannel(int channel)
    {
        if (channel is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(channel));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitIoDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetEcVersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetFanDutyDelegate(int fanId, int duty);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetFanDutyAutoDelegate(int fanId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint GetTempFanDutyDelegate(int fanId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetFanCountDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetFanRpmDelegate();
}
