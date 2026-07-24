# 桌面宠物

一只以真实橘猫为原型制作的跨平台桌面宠物。程序使用透明、无边框、置顶窗口显示小猫，支持抚摸、投喂、拖动、随机待机动画、跟随鼠标和自动散步等交互。

> 项目目前包含 Windows 原生版本，以及基于 Avalonia 的 macOS/Linux 跨平台版本。

## 功能特性

- 透明背景、无边框桌面显示
- 默认始终置顶，可通过右键菜单关闭
- 单击抚摸，小猫会闭眼并伸头享受
- 双击打开投喂菜单
- 支持小鱼干、猫罐头和鸡肉三种食物
- 鼠标拖动小猫到屏幕任意位置
- 鼠标滚轮或右键菜单调整小猫大小
- 随机眨眼、舔毛、后腿挠痒和睡觉动画
- 可选跟随鼠标模式
- 可选自动散步模式，行走一段距离后会停下休息
- 随机招呼和互动文案
- 默认关闭跟随鼠标和自动散步

## 操作说明

| 操作 | 效果 |
| --- | --- |
| 单击小猫 | 抚摸小猫 |
| 双击小猫 | 打开投喂菜单 |
| 按住左键拖动 | 移动小猫 |
| 鼠标滚轮 | 调整小猫大小 |
| 右键小猫 | 打开功能菜单 |

右键菜单包含：

- 打个招呼
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

该版本为自包含单文件程序，目标电脑不需要另外安装 .NET。

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

如果需要分发为 `.app` 或 `.dmg`，应在 macOS 上完成应用包组装、代码签名和 DMG 制作。未经 Apple Developer ID 签名和公证的应用可能被 Gatekeeper 拦截。

本机测试自建应用包时，可以进行临时签名：

```bash
codesign --force --deep --sign - "LiJu.app"
```

## macOS Intel 构建

Intel 芯片 Mac 使用 `osx-x64`：

```bash
cd OrangeCatPetMac
dotnet publish -c Release \
  -r osx-x64 \
  --self-contained true \
  -o publish/osx-x64
```

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
```

新增或替换动画时，应保持：

- 每组动画均为 8 帧
- 所有帧使用相同画布尺寸
- 小猫在每一帧中的主体大小和基准位置一致
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

- Windows WPF 项目仅用于 Windows。
- macOS/Linux 使用 `OrangeCatPetMac` Avalonia 项目。
- 不同 CPU 架构需要使用对应的运行时标识，不能混用 ARM64 和 x64 安装包。
- macOS 正式对外分发需要 Apple Developer ID 签名和公证。
- 当前仓库未附带开源许可证，默认保留所有权利；公开分发或商用前请先获得项目所有者许可。
