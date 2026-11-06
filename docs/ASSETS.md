# 素材与替代资源

课程原始资源中有一部分已经失效、下架或改为付费。本项目不复制课程商店付费资源，也不声称是原版课程美术；以下记录当前实际使用的资源与许可。

## 当前使用的资源

| 用途 | 资源 | 许可 | 项目位置 |
|---|---|---|---|
| 两把武器模型 | Kenney Weapon Pack（`machinegun`、`pistol`） | CC0 1.0 | `UnityProject/Assets/Resources/Foundry/Weapons` |
| 工业地图道具 | Kenney Space Kit（桶、控制台、围栏、管道、楼梯、金属板、机器人等） | CC0 1.0 | `UnityProject/Assets/Resources/Foundry/Props` |
| 枪口焰、火花、烟雾、弹道贴图 | Kenney Particle Pack | CC0 1.0 | `UnityProject/Assets/Resources/Foundry/Particles` |
| 准星 | Kenney Crosshair Pack | CC0 1.0 | `UnityProject/Assets/Resources/Foundry/Crosshairs/crosshair.png` |
| 脚步与撞击音效 | Kenney Impact Sounds | CC0 1.0 | `UnityProject/Assets/Resources/Foundry/Audio` |
| 天空 HDRI | Poly Haven：Industrial Sunset 02 | CC0 1.0 | `UnityProject/Assets/Resources/Foundry/Sky` |
| 地面/墙面 PBR | Poly Haven：Concrete Floor Worn 001、Concrete Wall 003 | CC0 1.0 | `UnityProject/Assets/Resources/Foundry/Textures` |
| HUD 字体 | Rajdhani（Google Fonts） | SIL Open Font License 1.1 | `UnityProject/Assets/Resources/Foundry/Fonts` |
| 后处理（暗角/色彩/曝光） | 项目自制 Shader | 项目原创 | `UnityProject/Assets/Resources/Foundry/Shaders/ArenaPostFX.shader` |
| 地图几何、角色模型、材质编排与光照 | 项目代码生成（`ArenaWorld`、`SoldierRig`） | 项目原创 | `UnityProject/Assets/Scripts` |
| 枪声音效 | 项目代码合成（`ArenaEffects.Synthesize`） | 项目原创 | `UnityProject/Assets/Scripts/ArenaEffects.cs` |

Kenney 资源本身采用 CC0；本项目通过社区镜像 `https://github.com/ETdoFresh/kenney.nl` 取得已解压文件，许可证文本已复制到 `UnityProject/Assets/ThirdParty/Kenney/`。CC0 不要求署名，但这里保留来源说明，感谢 Kenney。

Poly Haven 的 HDRI 和 PBR 纹理为 CC0（`polyhaven.com/a/industrial_sunset_02`、`concrete_floor_worn_001`、`concrete_wall_003`）。Rajdhani 字体为 SIL Open Font License 1.1，许可证文本在 `UnityProject/Assets/ThirdParty/GoogleFonts/OFL-Rajdhani.txt`。

## 课程原始资源的处理

- **3D Prototyping Pack**：课程链接已失效，Unity Asset Store 版本也已改动/收费。当前地图改用项目代码生成的基础几何 + Kenney Space Kit 道具，保持课程要求的仓库/掩体/平台/多路线结构。
- **guns（M16A1、Desert Eagle）**：原课程枪械包不可直接取得。当前使用 Kenney Weapon Pack 的 `machinegun` 与 `pistol` 作为替代，游戏内名称仍为 M16A1 / Desert Eagle，规则参数保持不变。
- **游戏地图 3.0**：课程评论区给出的网盘链接需要登录且无法核验授权，不采用；当前使用项目自定义地图。
- **角色模型（免费版/高级版）**：课程链接和网盘分享的授权无法核验，当前保留项目原创的低多边形装甲角色与程序化动作，避免引入来源不明的模型。
- **常用粒子特效**：使用 Kenney Particle Pack 的透明贴图，由运行时粒子系统组合。
- **射击音效**：不使用来源不明的采样；枪声由代码合成，脚步和撞击使用 Kenney CC0 采样。
- **装弹/血条/玩家 UI**：项目使用 IMGUI 绘制，准星使用 Kenney Crosshair Pack。

## 许可摘要

- Kenney 资源：Creative Commons Zero（CC0 1.0），可用于个人和商业项目，署名非强制。
- 项目原创代码与程序化模型/音效：随本项目源码使用。
- 未使用课程商店付费资源、未使用需要账号登录才能取得且授权不明的网盘资源。
