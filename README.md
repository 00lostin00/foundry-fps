# Foundry

Foundry 是一个用 Unity 6 做的多人 FPS 小项目。地图是一座工业风的铸造场，能开两把枪、用喷气背包飞，也能自己建房或加入别人的房间。

## 怎么玩

- 自由混战，先拿到 20 个击杀，或者 8 分钟结束时击杀最多的人赢。
- 死了 3 秒后重生；出生有 2 秒保护，主动开火会提前取消保护。
- 两把枪：M16A1 全自动，Desert Eagle 半自动。两把枪弹药独立，切枪会打断当前装填。
- 可以冲刺、蹲下、跳跃；按 F 打开喷气背包，燃料会消耗，也会慢慢恢复。
- 房间可以设密码；房主在暂停菜单里可以开下一回合。

| 按键 | 作用 |
|---|---|
| WASD / 鼠标 | 移动、瞄准 |
| Shift / Ctrl | 冲刺、蹲下 |
| Space / F | 跳跃、喷气飞行 |
| 鼠标左键 / 右键 | 开火、缩放瞄准 |
| 1 / 2 / 滚轮 | 切换 M16A1、Desert Eagle |
| R | 装填 |
| Tab / Esc | 战绩、菜单 |

## 启动

需要 Unity 6.3 LTS（6000.3.28f1）和 .NET 8。

1. 用 Unity Hub 打开 `UnityProject`。第一次会导入依赖并生成场景和玩家预制体，等它跑完。
2. 想用房间列表的话，先启动房间服务：

```powershell
powershell -File tools/start.ps1
```

3. 构建 Windows 客户端：

```powershell
powershell -File tools/build.ps1
```

构建产物在 `artifacts/windows/Foundry.exe`。

4. 想最快在本机开一局，用这个（独立服务端 + 两个客户端，不走房间列表）：

```powershell
powershell -File tools/play-local.ps1
```

两个窗口出来后，先点一下游戏窗口让鼠标被捕获，再按 WASD。直接打开 `Foundry.exe` 会先看到主菜单，需要先点 Create Room / Join，或者 Local Host / Direct Connect。

## 素材

武器、地图道具、粒子、准星和脚步/撞击音效来自 Kenney；HDRI 和 PBR 地面/墙面来自 Poly Haven；字体是 Rajdhani。都是 CC0 / OFL 资源，来源和许可写在 `docs/ASSETS.md`。
