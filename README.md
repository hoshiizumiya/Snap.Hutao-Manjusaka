
## 📖 简介 / Introduction

**中文**  
胡桃工具箱是一款以 MIT 协议开源的原神工具箱，专为现代化 Windows 平台设计，旨在改善桌面端玩家的游戏体验。

该版本注入功能暂不可用，并且由于缺失资源和开发能力，可能会在下次游戏更新后立即失效（除非有人愿意继续维护），不建议长期使用

**English**  
Snap Hutao is an open-source Genshin Impact toolkit under MIT license, designed for modern Windows platform to improve the gaming experience for desktop players.

---

## 🚀 安装 / Installation

> 如果你的设备不支持ipv6，请下载末尾带有`ipv4`的压缩包，正常情况下请尽量下载普通包（服务器速度快）

目前 Sanp.Hutao.Rev 更新了打包方式，并采用了标准现代的 msi 安装，方便程序获取管理员权限和更多的功能设置，不再需要原 Depolyment
可以和之前的版本共存，将之前版本的数据文件夹里面的文件复制到该版本的数据文件夹中即可恢复数据

---

## 开发
项目启动位置已升级为 VS2026 的 slnx 格式 Snap.Hutao\src\Snap.Hutao\Snap.Hutao.slnx
> [!WARNING]
> 要使该项目可以长期运行，我们需要以下资源
> 1. `src/Snap.Hutao/Snap.Hutao/Web/Hoyolab/DataSigning/SaltConstants.cs`中的签名值
> 2. 元数据的编写
> 3. 图片资源
>
> 这些东西必须要专人维护，我对游戏内数据一窍不通

**若需编译项目，请使用[Visual Studio 2026](https://visualstudio.microsoft.com/zh-hans/)**  
调试选项请选择unpackaged（不打包）
**原开发文档现在还可使用（其中的AI功能很好用），以下是开发文档链接：**  

https://deepwiki.com/DGP-Studio/Snap.Hutao

https://deepwiki.com/DGP-Studio/Snap.Hutao.Server
## 打包测试

由于采用了 wix 进行打包程序，VS 需要安装 **HeatWave for VS2022**（2026兼容）。需要 msi 安装包时，右键选中 Snap.Hutao.Installer 生成后即可在目标目录找到。默认目录：Snap.Hutao.Installer\bin\x64\Release\en-US\Snap.Hutao.Installer.msi

### 资源

> 注意：普通包的资源服务器只能使用ipv6连接，也就是说，你的电脑必须有ipv6，并且建议你手动配置DNS为`223.5.5.5`  
> 如果你的设备不支持ipv6，请下载末尾带有`ipv4`的压缩包  
> 由于数据文件夹中有元数据的仓库和图片缓存，才得以恢复资源文件  
> 如果你发现之前版本可以显示的图片不能显示了，请查找旧数据文件夹  
> `C:\Users\<用户名>\AppData\Local\Packages\xxxDGPStudio.SnapHutao_xxx\LocalCache\ImageCache`  
> 并将`ImageCache`文件夹提供给我，我会尽力恢复资源

**元数据仓库：**  
https://github.com/wangdage12/Snap.Metadata

镜像：  
http://server.wdg.cloudns.ch:3000/wdg1122/Snap.Metadata

http://serverjp.wdg.cloudns.ch:3000/wdg1122/Snap.Metadata

---

**临时API：**  
http://server.wdg.cloudns.ch:5222/

http://serverjp.wdg.cloudns.ch:5222/

---

**临时资源站：**  
http://server.wdg.cloudns.ch:8007/

http://serverjp.wdg.cloudns.ch:8001/
