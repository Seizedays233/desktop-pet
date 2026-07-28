# 桌面宠物

一款支持多宠物切换的跨平台桌面宠物。Windows、macOS 和 Linux 版本目前都包含橘猫“李橘”和幼年比格犬“Leagle”：两只宠物拥有独立形象、八帧动画和互动台词，可在右键菜单中随时切换。程序使用透明、无边框、置顶窗口显示宠物，支持抚摸、投喂、拖动、随机待机动画、跟随鼠标和自动散步等交互。还可以把文件或文件夹“喂”给宠物，由宠物播放进食动画并将它们送入系统回收站或废纸篓。

> 项目包含 Windows WPF 原生版本，以及基于 Avalonia 的 macOS/Linux 跨平台版本。两个项目都已支持李橘、Leagle、独立台词、完整动画和文件回收功能。

## 平台支持状态

| 平台 | 项目 | 架构 | 多宠物切换 | 文件回收 |
| --- | --- | --- | --- | --- |
| Windows 10/11 | `OrangeCatPet` | x64 | 李橘 / Leagle | Windows 回收站 |
| macOS 11+ | `OrangeCatPetMac` | Intel x64、Apple Silicon ARM64 | 李橘 / Leagle | Finder 废纸篓 |
| 长城/麒麟/UOS Linux | `OrangeCatPetMac` | x64、ARM64 | 李橘 / Leagle | GIO 或 Freedesktop/XDG 回收站 |

macOS 目前提供 `osx-x64` 和 `osx-arm64` 两个独立构建目标，不是单个 Universal Binary。Intel Mac 使用 `osx-x64`；M1、M2、M3、M4 等 Apple Silicon Mac 使用 `osx-arm64`。

## 🌟 重点功能：把文件喂给宠物

桌面宠物现在可以作为一个带动画效果的跨平台回收站：

1. 从资源管理器、Finder 或系统文件管理器中选中一个或多个文件、文件夹。
2. 将它们拖到桌面上的当前宠物身上。
3. 看到提示后松开鼠标，当前宠物会播放吃东西动画，文件卡片会逐渐被“吃掉”。
4. 动画结束后，文件或文件夹会被移动到当前系统的回收站或废纸篓。

该功能支持同时处理多个文件和文件夹。被处理的内容不会被直接永久删除；如有需要，可以从系统回收站或废纸篓中恢复。

| 平台 | 实现方式 | 支持架构 |
| --- | --- | --- |
| Windows | Windows 系统回收站 | x64 |
| macOS | Foundation 原生废纸篓接口 | Intel x64、Apple Silicon ARM64 |
| 长城/麒麟/UOS Linux | GIO 桌面回收站；缺少 GIO 时使用 Freedesktop/XDG 回收站规范 | x64、ARM64 |

> **重要提示**
>
> - Windows 上请以普通用户身份启动程序，不要选择“以管理员身份运行”，否则权限隔离可能阻止从资源管理器拖放文件。
> - 磁盘根目录、共享根目录、不存在的路径及系统不允许回收的项目不会被处理。
> - Linux 上的移动硬盘、网络共享或特殊系统挂载点能否进入回收站，取决于桌面环境和挂载配置；不支持时程序会提示失败，不会改为永久删除。
> - 如果一次拖入多个项目，程序会分别处理；个别项目失败不会影响已经成功移入回收站的项目。

## 功能特性

- **跨平台多宠物切换：Windows、macOS 和 Linux 均可在橘猫“李橘”和幼年比格犬“Leagle”之间切换**
- **角色独立表现：Leagle 使用写实幼犬形象、经典斜眼坐姿、专属互动台词，以及独立的行走、投喂、睡眠、挠痒、抚摸和打滚动画**
- **跨平台动画回收站：拖入文件或文件夹，宠物播放进食动画后将其移入系统回收站或废纸篓**
- 透明背景、无边框桌面显示
- 默认始终置顶，可通过右键菜单关闭
- 单击抚摸，当前宠物会播放专属享受动画；Leagle 还会摇尾巴
- 双击打开投喂菜单
- 支持小零食、罐头和鸡肉三种食物
- 鼠标拖动宠物到屏幕任意位置
- 鼠标滚轮或右键菜单调整宠物大小
- 随机眨眼、舔毛/打滚、后腿挠痒和睡觉动画
- 可选跟随鼠标模式
- 可选自动散步模式，行走一段距离后会停下休息
- 随机招呼和互动文案
- 默认关闭跟随鼠标和自动散步

## 操作说明

| 操作 | 效果 |
| --- | --- |
| 将一个或多个文件/文件夹拖到当前宠物身上 | 播放吃东西动画并移入当前系统的回收站或废纸篓 |
| 单击宠物 | 抚摸当前宠物 |
| 双击宠物 | 打开投喂菜单 |
| 按住左键拖动 | 移动当前宠物 |
| 鼠标滚轮 | 调整宠物大小 |
| 右键宠物 | 打开功能菜单 |

右键菜单包含：

- 打个招呼（李橘和 Leagle 使用不同台词）
- 更换宠物：橘猫·李橘 / 幼年比格犬·Leagle
- 投喂
- 跟随鼠标
- 自动散步
- 始终置顶
- 调整大小
- 回到屏幕右下角
- 退出

## 项目结构

```text
work/
├─ OrangeCatPet/        Windows WPF 版本
├─ OrangeCatPetMac/     Avalonia 跨平台版本（macOS/Linux）
├─ assets/              公共图片素材
├─ imagegen/            动画原始图和精灵图
├─ cat-source.jpg       小猫原始参考照片
├─ *.py                 动画帧拆分、对齐和处理脚本
└─ README.md
```

编译产生的 `bin`、`obj`、`publish`、安装包和临时打包目录不会提交到 Git。

## 开发环境

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 版本：Windows 10/11 x64
- macOS 版本：macOS 11 或更高版本
- Linux 版本：支持 X11 或 XWayland 的桌面系统

首次还原依赖时需要连接网络。

## Windows 构建

在仓库根目录执行：

```powershell
dotnet restore ".\OrangeCatPet\OrangeCatPet.csproj"
dotnet publish ".\OrangeCatPet\OrangeCatPet.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o ".\publish\windows-x64"
```

构建完成后，程序位于：

```text
publish/windows-x64/OrangeCatPet.exe
```

该版本为自包含单文件程序，目标电脑不需要另外安装 .NET。右键宠物选择“更换宠物”，即可在李橘与 Leagle 之间切换；切换会同时替换待机、行走、投喂、睡眠、挠痒、抚摸、打滚和回收站进食动画，并启用对应角色的专属台词。

## macOS 支持说明

macOS 版本由 `OrangeCatPetMac` Avalonia 项目提供，当前支持：

- 李橘与 Leagle 切换，以及各自独立的坐姿、眨眼、行走、投喂、睡眠、挠痒、抚摸和舔毛/打滚动画
- Leagle 的多次完整挠痒、睡下后保持睡姿、抚摸摇尾巴，以及打滚第 5～6 帧延长停留
- Finder 文件和文件夹拖放；播放进食动画后调用 macOS Foundation 接口移入废纸篓
- 鼠标拖动、滚轮缩放、跟随鼠标、自动散步、始终置顶和随机台词
- Intel x64 与 Apple Silicon ARM64 两种构建目标

## macOS Apple Silicon 构建

M1、M2、M3、M4 等 Apple Silicon 芯片使用 `osx-arm64`：

```bash
cd OrangeCatPetMac
dotnet restore
dotnet publish -c Release \
  -r osx-arm64 \
  --self-contained true \
  -o publish/osx-arm64
```

生成的主程序为：

```text
OrangeCatPetMac/publish/osx-arm64/LiJuPet
```

该目录是自包含构建，目标 Mac 不需要另行安装 .NET。在源码目录中可以直接测试：

```bash
chmod +x publish/osx-arm64/LiJuPet
./publish/osx-arm64/LiJuPet
```

如果需要分发为 `.app` 或 `.dmg`，应在 macOS 上完成应用包组装、代码签名和 DMG 制作。正式对外发布建议使用 Apple Developer ID 签名并完成公证，否则 Gatekeeper 可能阻止首次启动。

仅测试自己构建或确认可信来源的应用时，可以先进行临时签名：

```bash
codesign --force --deep --sign - "LiJu.app"
```

首次打开未公证的可信应用时，优先在 Finder 中右键应用并选择“打开”；也可以在“系统设置 → 隐私与安全性”中确认“仍要打开”。不要为来源不明的应用绕过 Gatekeeper。

## macOS Intel 构建

Intel 芯片 Mac 使用 `osx-x64`：

```bash
cd OrangeCatPetMac
dotnet publish -c Release \
  -r osx-x64 \
  --self-contained true \
  -o publish/osx-x64
```

生成的主程序为：

```text
OrangeCatPetMac/publish/osx-x64/LiJuPet
```

Intel 与 Apple Silicon 构建包含相同功能和资源，但二进制不能互换使用。

## Linux 构建

ARM64 系统，例如部分长城、麒麟和统信 UOS 设备：

```bash
cd OrangeCatPetMac
dotnet publish -c Release \
  -r linux-arm64 \
  --self-contained true \
  -o publish/linux-arm64
```

x86-64/AMD64 系统：

```bash
cd OrangeCatPetMac
dotnet publish -c Release \
  -r linux-x64 \
  --self-contained true \
  -o publish/linux-x64
```

Linux 版本需要图形桌面提供 X11 或 XWayland。制作兼容旧版麒麟/UOS 的 `.deb` 时，应使用 gzip 等旧版 `dpkg` 支持的压缩格式，避免使用 `control.tar.zst`。

## 本地调试

Windows WPF 版本：

```powershell
dotnet run --project ".\OrangeCatPet\OrangeCatPet.csproj"
```

在 M3 Pro 等 Apple Silicon Mac 上运行跨平台版本：

```bash
dotnet run --project OrangeCatPetMac/OrangeCatPetMac.csproj -r osx-arm64
```

## 动画资源约定

主要动画使用 8 帧序列，文件名使用两位数字编号：

```text
cat-smooth-walk-01.png
cat-smooth-walk-02.png
...
cat-smooth-walk-08.png

dog-walk-01.png
dog-walk-02.png
...
dog-walk-08.png
```

新增或替换动画时，应保持：

- 每组动画均为 8 帧
- 所有帧使用相同画布尺寸
- 同一宠物在每一帧中的主体大小和基准位置一致
- Leagle 动画保持与默认坐姿一致的写实幼犬画风、年龄特征、毛色和身体比例
- 背景保持透明
- 朝向与实际移动方向一致

## Git 协作

获取代码：

```bash
git clone https://github.com/Seizedays233/desktop-pet.git
cd desktop-pet
```

拉取最新修改：

```bash
git pull
```

提交修改：

```bash
git add .
git commit -m "说明本次修改内容"
git push
```

建议每次修改前先执行 `git pull`，并避免把 `publish`、安装包或本地缓存提交到仓库。

## 注意事项

- Windows WPF 项目仅用于 Windows；macOS/Linux 使用 `OrangeCatPetMac` Avalonia 项目。
- 文件回收功能支持 Windows、macOS，以及运行麒麟/UOS 等 Linux 桌面系统的长城设备。
- 拖放目标只会进入系统回收站或废纸篓，不会直接永久删除，可以通过系统界面恢复。
- Windows 上请正常启动程序，不要使用管理员模式；否则系统可能阻止资源管理器向程序拖放文件。
- Linux 的 Freedesktop/XDG 兼容回收站主要保证用户主目录内文件的兼容性；其他分区和网络位置由系统能力决定。
- 不同 CPU 架构需要使用对应的运行时标识，不能混用 ARM64 和 x64 安装包。
- macOS 正式对外分发需要 Apple Developer ID 签名和公证。
- 当前仓库未附带开源许可证，默认保留所有权利；公开分发或商用前请先获得项目所有者许可。