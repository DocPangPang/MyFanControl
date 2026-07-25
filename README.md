# MyFanControl

蓝天（Clevo）笔记本风扇监控与曲线控制工具。仓库当前同时保留 2018 年的
MFC 原版，以及正在重构的现代 Windows 版本。

## Modern Preview

新的 `.NET 8 + WPF` 原型位于 [`src/MyFanControl.Modern`](src/MyFanControl.Modern)：

- Windows 11 风格深色仪表盘
- 可直接拖动控制点的 CPU/GPU 风扇曲线
- 安静、均衡、性能、自定义预设
- 线性/阶梯控制、回差温度、强制冷却
- 托盘、开机启动、JSON 配置和异常安全回退
- 不引入第三方 UI 框架或 NuGet 软件包
- 缺少真实硬件接口时自动进入演示模式

详细构建方式和安全策略请阅读
[`src/MyFanControl.Modern/README.md`](src/MyFanControl.Modern/README.md)，原作者 Release
组件的静态分析与校验信息见
[`docs/third-party-runtime-components.md`](docs/third-party-runtime-components.md)。

## 必须披露的底层依赖

原仓库没有提交 EC 访问层源码。现代版要实际读写风扇，暂时仍需要 32 位
`ClevoEcInfo.dll`、`ntport.dll` 及其使用的 `zntport.sys` 驱动。这些组件均不是现代界面框架的依赖，
而是直接访问硬件所需的旧底层实现；在取得 EC 协议或相应源码前无法安全替换。

旧版 GPU 限频依赖 `NVGPU_DLL.dll`，目前没有接入现代原型。上述底层组件在本仓库中
均无源码，授权情况也有待确认，请勿从来历不明的网站下载驱动或 DLL。

## 原版说明

原版来自贴吧用户 `hqnklwsy`：
https://tieba.baidu.com/p/5971634018

原版位于 [`MyFanControl`](MyFanControl)，使用 Visual Studio 2013、MFC 和 Win32。
它支持双风扇曲线、线性控制、过渡温度、强制冷却、托盘、自启和 GPU 限频。
原版的 RPM 是根据 EC 计数值拟合的估算值，不保证适用于所有机型或改装风扇。
