# 第三方运行组件

本页记录原作者 Release 附带文件的静态分析结果。分析过程只读取 PE 头、资源、
导入导出表和签名证书，没有执行 DLL 或安装程序。

## 原始文件

| 文件 | 大小 | SHA-256 | 架构与版本 | Authenticode 证书 |
|---|---:|---|---|---|
| `ClevoEcInfo.dll` | 68,568 B | `f1fa68742b86022ce436d9998c3a7de34d64866eefc95e40c12f6439328ba656` | PE32 x86，1.0.0.1，2015 | CLEVO CO.，带时间戳 |
| `NTPortDrvSetup.exe` | 163,096 B | `ae7c5ef7347ed7e82dade673ad1126ba7f01753e751a85d0a2052c78b2186f71` | PE32 x86，NSIS，2.8.0.0，2008 | LI Hai / Zeal SoftStudio |
| `NVGPU_DLL.dll` | 8,769,024 B | `78cca80c81d190dbaea0ed75a4ab17dc4ad86744706462b777279c758e971717` | PE32 x86，381.0.0.1，2018 | 无签名证书表 |

证书表存在不等于已经在当前 Windows 信任链和吊销状态下完成验证。正式发布前，
仍应在目标 Windows 系统上用“数字签名”属性页或 `Get-AuthenticodeSignature`
复核签名状态。

## Clevo EC 接口

`ClevoEcInfo.dll` 的产品描述为 `CLEVO EC FAN LIB. (RD)`，导出以下 11 个函数：

```text
InitIo
GetECVersion
GetCpuFanRpm
GetFanCount
GetGpuFanRpm
GetGpu1FanRpm
GetOptionModual
GetTempFanDuty
GetX72FanRpm
SetFanDuty
SetFanDutyAuto
```

现代版使用的函数均与旧源码声明一致。静态反汇编还确认：

- 调用约定是 x86 C/C++ 默认的 `cdecl`；
- `GetTempFanDuty(int fanId)` 把 Remote 温度、Local 温度和 FanDuty 三个字节
  打包在 32 位返回值中；
- 风扇 ID 支持 1–4；`SetFanDutyAuto` 还处理额外通道；
- CPU/GPU/GPU1/X72 转速分别读取不同的 EC 数据索引；
- RPM 仍是根据返回计数值换算的估算值。

`ClevoEcInfo.dll` 直接导入 `ntport.dll` 的四个序号接口，用其访问 EC 的
`0x66` 命令/状态端口和 `0x62` 数据端口。因此只复制 Clevo DLL 并不足以运行。

## NTPort 安装包

NSIS 包内包含：

| 组件 | 版本 | 作用 |
|---|---|---|
| `ntport.dll`（x86） | 2.8.1.14 | 用户态 I/O 端口 API |
| `zntport.sys`（x64） | 2.8.3.1 | 64 位 Windows 内核驱动 |
| `zntport.sys`（x86） | 2.8.3.1 | 32 位 Windows 内核驱动 |

安装逻辑会按系统架构选择驱动，创建名为 `zntport` 的服务/设备，并使
`ntport.dll` 可被 32 位 `ClevoEcInfo.dll` 加载。它是 2007–2008 年代的直接
I/O 驱动，现代 Windows 可能因驱动签名、内存完整性或易受攻击驱动阻止列表而
拒绝加载。程序不能也不应自动绕过这些系统安全策略。

## NVIDIA 模块

`NVGPU_DLL.dll` 提供 47 个导出函数，包括 GPU 信息、温度、频率、P-State、
核心/显存偏移和锁频接口。旧程序实际使用的接口名称仍然存在，但该 DLL：

- 编译于 2018 年，早于 RTX 30 系列；
- 没有 Authenticode 签名证书表；
- 可执行超频、降频和 P-State 修改；
- 没有随仓库提供源码或兼容性说明。

因此现代预览版暂不加载它。恢复 GPU 限频前，应先在隔离的可回退测试版本中
验证 RTX 3070 与当前 NVIDIA 驱动的行为，并确保退出和异常路径能够可靠还原。

## 分发策略

这些二进制文件暂不提交到仓库，也不打入现代版发布包。用户应从原作者 Release
取得文件，并用上表 SHA-256 校验。等授权情况明确后，再决定是否随安装包分发。
