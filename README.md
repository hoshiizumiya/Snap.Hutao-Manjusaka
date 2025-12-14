<p align="center">
  <img src="https://github.com/hoshiizumiya/Snap.Hutao-Manjusaka/blob/main/src/Snap.Hutao/Snap.Hutao/Assets/InAppLogo.png" alt="Snap Hutao Banner" width="600"/>
</p>
<h1 align="center">Snap Hutao-Manjusaka</h1>
<p align="center">
  🎮 胡桃工具箱-彼岸 是一款以 MIT 协议开源的原神工具箱，基于胡桃工具箱二次开发，专为现代化 Windows 平台设计，旨在改善桌面端玩家的游戏体验。  
  <br/>
  🎮 An open-source Genshin Impact toolkit for Windows, designed to improve the desktop gaming experience
</p>
## 📖 简介 / Introduction

**中文**  
胡桃工具箱-彼岸 是一款以 MIT 协议开源的原神工具箱，基于[胡桃工具箱](https://github.com/sigewinnefish/Snap.Hutao)二次开发，专为现代化 Windows 平台设计，旨在改善桌面端玩家的游戏体验。

目前程序稳定性正在逐步提升

**English**  
Snap Hutao-Manjusaka is an open-source Genshin Impact toolkit under MIT license, designed for modern Windows platform to improve the gaming experience for desktop players.

---

## 🚀 安装 / Installation

目前 Sanp.Hutao-Manjusaka 更新了打包方式，并采用了标准现代的 msi 安装，方便程序获取管理员权限和更多的功能设置，不再需要原 Depolyment

如果通过`.msix`安装包安装则可能出现`0x80073CF3`，备份旧版本数据文件夹后卸载旧版本即可继续安装，将旧版本数据文件夹里面的文件复制到该版本的数据文件夹中即可恢复数据

---

## 开发
项目启动位置已升级为 VS2026 的 slnx 格式 Snap.Hutao\src\Snap.Hutao\Snap.Hutao.slnx  
针对当程序设置为默认以管理员模式运行而无法使用 VS 启动调试的情况，请提升 VS 的运行权限（右键VS以管理员身份运行）
> [!WARNING]
> 要使该项目可以长期运行，我们需要以下资源
> 元数据的编写
> 图片资源
[开发指南](https://github.com/hoshiizumiya/Snap.Hutao-Manjusaka/tree/main?tab=contributing-ov-file)
V6.2的元数据已在编写中  
测试仓库位置：http://server.wdg.cloudns.ch:3000/wdg1122/Snap.Metadata.Test  
**目前元数据的编写进度：**

| 项目（V6.2） | 是否完成     |
| ----------- | ----------- |
| 新角色的基本数据 | ✔️ |
| 新版本角色/怪物基础数值 | ❔ |
| 新角色的详细资料、名片等 | ❌ |
| 新武器 | ✔️ |
| 新材料 | ❇️ |
| 新怪物 | ❇️ |
| 新圣遗物 | / |
| 新卡池 | ❇️ |
| 新成就 | ✔️ |
| 深境螺旋 | 💠 |
| 幻想真境剧诗 | 💠 |
| 幽境危战 | ✔️ |

✔️：已完成  
❌：未编写  
❇️：编写中  
❔：数据暂时无法得到  
 / ：似乎不需要变动  
💠：低优先级，以后编写  

**若需编译项目，请使用[Visual Studio 2026](https://visualstudio.microsoft.com/zh-hans/)**  
调试选项请选择unpackaged（不打包）
**原开发文档现在还可使用（其中的AI功能很好用），以下是开发文档链接：**  

https://deepwiki.com/hoshiizumiya/Snap.Hutao-Manjusaka/

https://deepwiki.com/DGP-Studio/Snap.Hutao.Server
## 打包测试

由于采用了 wix 进行打包程序，VS 需要安装 **HeatWave for VS2022**（2026兼容）。需要 msi 安装包时，右键选中 Snap.Hutao.Installer 生成后即可在目标目录找到。默认目录：Snap.Hutao.Installer\bin\x64\Release\en-US\Snap.Hutao.Installer.msi

### 资源

> 由于数据文件夹中有元数据的仓库和图片缓存，才得以恢复资源文件  
> 如果你发现之前版本可以显示的图片不能显示了，请查找旧数据文件夹  
> `C:\Users\<用户名>\AppData\Local\Packages\xxxDGPStudio.SnapHutao_xxx\LocalCache\ImageCache`  
> 并将`ImageCache`文件夹提供给我，我会尽力恢复资源

[服务器状态页面](http://serverjp.wdg.cloudns.ch:3001/status/hts)

**元数据仓库：**  
https://github.com/wangdage12/Snap.Metadata

镜像：  
![http://serverjp.wdg.cloudns.ch:3001/api/badge/6/status?style=flat-square](http://serverjp.wdg.cloudns.ch:3001/api/badge/6/status?style=flat-square)

http://server.wdg.cloudns.ch:3000/wdg1122/Snap.Metadata

![http://serverjp.wdg.cloudns.ch:3001/api/badge/7/status?style=flat-square](http://serverjp.wdg.cloudns.ch:3001/api/badge/7/status?style=flat-square)

http://serverjp.wdg.cloudns.ch:3000/wdg1122/Snap.Metadata

---

**临时API：**  

![http://serverjp.wdg.cloudns.ch:3001/api/badge/8/status?style=flat-square](http://serverjp.wdg.cloudns.ch:3001/api/badge/8/status?style=flat-square)

http://server.wdg.cloudns.ch:5222/


![http://serverjp.wdg.cloudns.ch:3001/api/badge/9/status?style=flat-square](http://serverjp.wdg.cloudns.ch:3001/api/badge/9/status?style=flat-square)

http://serverjp.wdg.cloudns.ch:5222/

---


**临时资源站：**  
http://server.wdg.cloudns.ch:8007/
---

## 如何卸载

卸载当前版本请前往*设置->应用->安装的应用*->找到 Snap.Hutao Manjusaka，选择卸载即可。（快捷操作：Win+X 打开安装的应用）  
注意，应用缓存数据在卸载后不会自动删除，卸载前请确认你的数据保存目录，未上传云服务的本地用户数据仅一份请注意保存或处理删除。不同的目录下包含了图片、元数据、用户设置等数据。

## 🙏 特别感谢 / Special Thanks

- [HolographicHat](https://github.com/HolographicHat)  
- [UIGF organization](https://uigf.org)  

**特定的原神项目 / Specific Genshin-related Projects**  
- [Scighost/Starward](https://github.com/Scighost/Starward)  

---

## ⚙️ 使用的技术栈 / Tech Stack

- [CommunityToolkit/dotnet](https://github.com/CommunityToolkit/dotnet)  
- [CommunityToolkit/Labs-Windows](https://github.com/CommunityToolkit/Labs-Windows)  
- [CommunityToolkit/Windows](https://github.com/CommunityToolkit/Windows)  
- [dotnet/efcore](https://github.com/dotnet/efcore)  
- [dotnet/runtime](https://github.com/dotnet/runtime)  
- [microsoft/vs-validation](https://github.com/microsoft/vs-validation)  
- [microsoft/WindowsAppSDK](https://github.com/microsoft/WindowsAppSDK)  
- [microsoft/microsoft-ui-xaml](https://github.com/microsoft/microsoft-ui-xaml)  
- [quartznet/quartznet](https://github.com/quartznet/quartznet)  

---
![Alt](https://repobeats.axiom.co/api/embed/e5d56703de1101fdf4b7034dfb78038fdc14754a.svg "Repobeats analytics image")

[![Star History Chart](https://api.star-history.com/svg?repos=hoshiizumiya/Snap.Hutao-Manjusaka&type=Date)](https://star-history.com/#hoshiizumiya/Snap.Hutao-Manjusaka&Date)  

http://serverjp.wdg.cloudns.ch:8001/
