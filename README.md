
## 📖 简介 / Introduction

**中文**  
胡桃工具箱是一款以 MIT 协议开源的原神工具箱，专为现代化 Windows 平台设计，旨在改善桌面端玩家的游戏体验。

该版本注入功能暂不可用，并且由于缺失资源和开发能力，不建议长期使用

**English**  
Snap Hutao is an open-source Genshin Impact toolkit under MIT license, designed for modern Windows platform to improve the gaming experience for desktop players.

---

## 🚀 安装 / Installation

> 如果你的设备不支持ipv6，请下载末尾带有`ipv4`的压缩包，正常情况下请尽量下载普通包（服务器速度快）

目前 Sanp.Hutao.Rev 更新了打包方式，并采用了标准现代的 msi 安装，方便程序获取管理员权限和更多的功能设置，不再需要原 Depolyment

只有`.msi`安装包安装的可以和之前的版本共存，如果通过`.msix`安装包安装则可能出现`0x80073CF3`，备份旧版本数据文件夹后卸载旧版本即可继续安装，将旧版本数据文件夹里面的文件复制到该版本的数据文件夹中即可恢复数据

---

## 开发
项目启动位置已升级为 VS2026 的 slnx 格式 Snap.Hutao\src\Snap.Hutao\Snap.Hutao.slnx
> [!WARNING]
> 要使该项目可以长期运行，我们需要以下资源
> 1. `src/Snap.Hutao/Snap.Hutao/Web/Hoyolab/DataSigning/SaltConstants.cs`中的新签名值
> 2. 元数据的编写
> 3. 图片资源

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

http://serverjp.wdg.cloudns.ch:8001/
