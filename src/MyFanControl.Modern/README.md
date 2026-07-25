# MyFanControl Modern

这是 MyFanControl 的现代化 Windows 桌面重构原型。它使用 .NET 8 和 WPF，
不依赖任何第三方 UI 框架或 NuGet 软件包。

## 当前功能

- 现代深色界面与实时 CPU/GPU 温度、占空比和估算 RPM
- 可直接拖动控制点的 CPU/GPU 风扇曲线
- 安静、均衡、性能和自定义四套方案
- 线性插值或阶梯控制
- 0–10℃ 回差温度
- 强制冷却，并在达到目标温度后自动结束
- 配置以版本化 JSON 保存到 `%LocalAppData%\MyFanControlModern`
- 托盘运行、当前用户开机启动
- 退出、关闭接管或连续读取失败时恢复 EC 自动控制
- 找不到硬件组件时自动进入界面演示模式

## 底层组件与限制

旧仓库没有包含 EC 访问层的源代码。真实硬件控制目前仍需：

| 组件 | 用途 | 当前仓库是否有源码 |
|---|---|---|
| `ClevoEcInfo.dll` 1.0.0.1（32 位） | 读取温度/转速并设置 Clevo EC 风扇占空比 | 否 |
| `ntport.dll` 2.8.1.14（32 位） | 为 Clevo DLL 提供 I/O 端口调用 | 否 |
| `zntport.sys` 2.8.3.1（x86/x64） | NTPort 内核驱动与 `zntport` 设备服务 | 否 |
| `NVGPU_DLL.dll` 381.0.0.1（32 位） | 旧版 NVIDIA GPU 限频 | 否 |

现代版自身没有第三方界面依赖，但在获得 EC 协议或现有 DLL 源码之前，真实风扇控制
仍不能彻底移除 `ClevoEcInfo.dll` 和 NTPort。原版 `NTPortDrvSetup.exe` 会把
`ntport.dll` 与对应架构的 `zntport.sys` 安装到 Windows 系统目录。

`NVGPU_DLL.dll` 未接入此原型：
它属于与风扇控制无关、兼容性和授权情况均不明确的可选功能。

将兼容的 `ClevoEcInfo.dll` 放到程序目录或 `native\ClevoEcInfo.dll`。如果 DLL
不存在、位数不匹配、导出函数缺失或 `InitIo` 失败，程序会显示“演示模式”，
不会写入 EC。

> 不要从来历不明的网站下载内核驱动或 DLL。底层组件拥有直接访问硬件的权限。
> Release 组件的哈希、签名与静态分析结果见
> [`../../docs/third-party-runtime-components.md`](../../docs/third-party-runtime-components.md)。

## 构建

需要 Windows 10/11、Visual Studio 2022 或 .NET 8 SDK：

```powershell
dotnet build .\src\MyFanControl.Modern\MyFanControl.Modern.csproj -c Release -p:Platform=x86
```

发布为自包含 x86 程序：

```powershell
dotnet publish .\src\MyFanControl.Modern\MyFanControl.Modern.csproj `
  -c Release -r win-x86 --self-contained true `
  -p:PublishSingleFile=true
```

必须使用 x86，因为旧版 `ClevoEcInfo.dll` 是为 32 位程序设计的。

## 安全策略

- 默认不接管风扇，启动后保持原厂 EC 自动控制。
- 用户必须主动开启“接管风扇”。
- 关闭接管、选择托盘“恢复原厂自动控制”或正常退出时，CPU/GPU 风扇均恢复自动。
- 连续三次读取硬件失败后立即放弃接管并恢复自动。
- 演示模式下的温度、转速均为模拟数据。

这仍是第一版原型。正式用于硬件前，应在目标机型上验证 DLL ABI、风扇通道编号、
最低可靠启动占空比、休眠恢复以及异常退出行为。
