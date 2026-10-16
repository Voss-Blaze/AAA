# Q 退出、存档提示与游戏音频

本轮新增 GameAudioManager、AudioSettingsPanel 两个功能脚本，更新 10 个已有功能脚本。场景和 UI 原有布局保留。

## Q 与存档提示

Level1 按 Q（对应 LevelCheckpointController.ExitLevel）退出关卡返回 Log，保留最后一次路灯检查点；没有检查点时不创建新存档，不保存玩家当前行走位置。暂停期间也可以 Q 退出。死亡过渡期间不会重复退出。

路灯存档真正写盘成功后，使用现有 Tooltip / Text 显示提示。提示优先于影子倒计时，结束后恢复当前阶段倒计时。文字和时间在 Assets/ShadowTrace/Level1UISettings.asset 的 Save Succeeded、Save Notice Duration 中调整，默认“存档成功”、2 秒。暂停期间提示计时也暂停。

## 音频资源

Assets/Prefab/Game Audio.prefab 保存场景音乐和八种音效的引用。Log、Level1 各放置一个实例；运行时只保留一个跨场景管理器，防止重复音乐。

按用户指定映射：

- Log → Assets/Music/关卡界面音乐.mp3
- Level1 → Assets/Music/各界面音乐.mp3

背景音乐循环。每个音效的基础音量可以在预制体 Sounds 数组对应条目的 Volume 中调整，音效文件也可直接替换。

- HInteract：H 切换暂停／继续；点击暂停图标不会额外触发 H 音效。
- Run / Walk：玩家地面移动时使用单独脚步通道，互斥循环；停止、腾空、暂停或死亡后停止。
- DoorOpen：成功执行 F 通行操作后播放；操作失败或重复请求不播放。
- LampLit：影子首次点亮路灯播放；Resume 恢复亮灯不播放。
- Landing：玩家落地时播放一次，初始出生不播放。
- Plate：PressurePlate1 / PressurePlate2 触发时播放，持续触发不反复播放。
- Clock：E 开始准备后循环，贯穿准备和生成阶段；再次 E、取消、自动完成、死亡或退出关卡时停止。暂停时保留时钟进度，继续游戏后恢复。

## 设置面板

Log / Canvas / SettingPanel / Setting / 音乐音效面板 的 AudioSettingsPanel 已绑定：

- 音乐按钮和音效按钮，默认开启。
- 总音量、音乐音量、音效音量三个 Slider 及原有百分比文字。
- Enabled Text、Disabled Text 和 Percentage Format 均可在 Inspector 调整，默认“开启”“关闭”“{0}%”。

滑块即时生效。音乐实际音量 = 总音量 × 音乐音量；音效实际音量 = 总音量 × 音效音量 × 各音效基础音量。背景音乐与一次性音效、脚步、时钟分别管理。音频设置使用独立的 PlayerPrefs 偏好保存，重新打开后保留，Start 清除检查点时不会重置音频设置。

自动测试使用独立音频偏好前缀和临时存档目录，不修改正式游戏的设置和存档。AudioFlowTests 覆盖设置持久化、场景切曲、脚步和落地、H/时钟、机关/开门、Q/Resume 和存档提示；同时验证相关菜单、检查点、HUD 与物理回归。
