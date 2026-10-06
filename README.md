# AAA

基于 Unity 的 2D 游戏项目，使用 Universal Render Pipeline（URP）。

## 开发环境

- Unity Editor：`2022.3.62f3c1`（请使用匹配版本）
- 项目依赖记录在 `Packages/manifest.json` 和 `Packages/packages-lock.json`

## 打开与运行

1. 克隆仓库。
2. 在 Unity Hub 中添加仓库根目录，并使用上述 Unity 版本打开。
3. 等待依赖恢复与资源导入完成。
4. 打开 `Assets/ShadowTrace/Scenes/ShadowTraceDemo.unity`，点击 Play 运行玩家与影迹演示。

## 玩家与影迹系统

- A/D 移动，Shift 疾跑，Space 跳跃，E 开始影迹准备或结束录制。
- 按录制行为生成停留型、趋近型、回避型或跟随型影子；趋近型停止后可作为玩家平台。
- 同时最多 8 个，数字键 1–8 选择，R 删除选中影子，双击 R 清空。
- 完整操作、配置和关卡接入说明见 [影迹系统使用文档](Docs/影迹系统使用文档.md)。
- 若演示资源缺失，使用 Unity 菜单 `Tools > Shadow Trace > Create Demo Assets` 生成。

## 目录说明

- `Assets/`：场景、资源及对应的 `.meta` 文件。
- `Packages/`：包依赖及锁定文件。
- `ProjectSettings/`：Unity 项目设置。

`Library/`、`Temp/`、`Logs/`、`UserSettings/` 等生成目录由 Git 忽略，无需提交。
