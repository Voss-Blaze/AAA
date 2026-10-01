# AAA

基于 Unity 的 2D 游戏项目，使用 Universal Render Pipeline（URP）。

## 开发环境

- Unity Editor：`2022.3.62f3c1`（请使用匹配版本）
- 项目依赖记录在 `Packages/manifest.json` 和 `Packages/packages-lock.json`

## 打开与运行

1. 克隆仓库。
2. 在 Unity Hub 中添加仓库根目录，并使用上述 Unity 版本打开。
3. 等待依赖恢复与资源导入完成。
4. 打开 `Assets/Scenes/SampleScene.unity`，点击 Play 运行。

## 目录说明

- `Assets/`：场景、资源及对应的 `.meta` 文件。
- `Packages/`：包依赖及锁定文件。
- `ProjectSettings/`：Unity 项目设置。

`Library/`、`Temp/`、`Logs/`、`UserSettings/` 等生成目录由 Git 忽略，无需提交。
