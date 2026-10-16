# 单存档与路灯检查点

新增功能脚本：SaveGameService、LevelCheckpointController。修改现有脚本：MainMenuController、ShadowLamp、PlayerLife。

## 游戏流程

- 影子点亮 Level1 路灯后立即保存。只有一份存档，后点亮的路灯覆盖前一个检查点。
- Resume 打开 PopPanelResume：NO 关闭；OK 读取有效存档并进入 Level1，在保存位置重生，同时恢复对应路灯点亮。影子、移动平台等运行状态重新初始化。
- 没有存档、存档损坏或对应路灯已被移除时，返回／留在 Log 并显示“请开始新游戏”，3 秒后隐藏。
- Start 的 OK 清除旧存档，从关卡初始位置开始；仅打开弹窗或点击 NO 不清档。
- 玩家死亡后渐变黑屏，返回 Log。死亡保留检查点，进入菜单后可以 Resume。

## Inspector 配置

- Log / Canvas / MainMenuController：Resume 按钮、弹窗、OK/NO、No Save Hint 和 No Save Hint Duration 已绑定。提示文字直接编辑 Canvas / No Save Hint 的 Text 组件。
- Level1 / Level Checkpoints：绑定玩家、生命组件、3 盏路灯、黑屏 Image 和 Log 场景路径。Death Fade Duration、Death Black Duration 分别调整渐黑时间与黑屏停留时间，使用不受暂停影响的计时。
- 每盏路灯的 ShadowLamp：Checkpoint Id 是独立且稳定的编号；Respawn Point 指向子对象 Respawn Point。可移动该子对象调整重生位置，程序会依据玩家真实碰撞框向下寻地，并排除墙体和地刺重叠。
- 新增路灯时，填写不重复的 Checkpoint Id，配置安全 Respawn Point，并加入 Level Checkpoints 的 Lamps 数组。
- 保持 Checkpoint Id 稳定，否则旧存档无法找到对应路灯，将提示开始新游戏。

## 存储与验证

正式存档位于 Unity 的 Application.persistentDataPath 下，文件名 checkpoint.json。保存使用临时文件加替换方式，避免直接覆写过程留下半份 JSON。场景路径、路灯编号与重生位置写入磁盘，不依赖运行时内存缓存。

自动测试使用独立 Temp 子目录，不读写正式存档。CheckpointSaveTests 验证持久化、覆盖、清档及异常数据；CheckpointFlowTests 验证菜单到关卡的完整流程；原菜单、HUD 和暂停测试同步回归。
