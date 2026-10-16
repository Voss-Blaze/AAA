# 影迹 Shadow Trace

基于 Unity + Universal Render Pipeline（URP）的 2D 平台解谜游戏。核心玩法是「录制」玩家短短数秒的移动意图，生成不同行为模式的**影子**，再利用影子去按压机关、点亮路灯、推动箱子和搭出平台，玩家自己则与影子配合闯关。

游戏已具备完整流程：主菜单（新游戏 / 继续 / 教程 / 音频设置）→ Level 1 关卡（开场剧情、路灯存档、门与压板、影子驱动的移动平台、终点）→ 两段终局剧情 → 黑屏丝滑切回菜单。

## 开发环境

- Unity Editor：**2022.3.62f3c1**（请用 Hub 安装匹配小版本）
- Universal RP：14.0.12
- Unity Test Framework：1.1.33
- 输入：传统 Input Manager（Active Input Handling 设为 Input Manager 或 Both）
- 依赖见 `Packages/manifest.json`

## 快速开始

1. 克隆仓库，用 Unity Hub 以 **2022.3.62f3c1** 打开仓库根目录。
2. 等待包恢复与资源导入。
3. Build Settings 中的场景顺序（首场景为菜单）：

| # | 场景 | 用途 |
| --- | --- | --- |
| 0 | `Assets/Scenes/Log.unity` | 主菜单（游戏入口） |
| 1 | `Assets/Scenes/Level 1.unity` | 正式关卡，完整玩法与剧情流程 |
| 2 | `Assets/ShadowTrace/Scenes/ShadowTraceDemo.unity` | 运行时生成的核心玩法演示沙盒 |
| 3 | `Assets/Scenes/SampleScene.unity` | 机制试验场（移动平台机关在此接线） |

体验完整游戏：打开 **Log** 场景 Play，点击「开始」→ 确认新游戏即可进入 Level 1。
体验单项机制：打开 SampleScene 或用 **Tools → Shadow Trace → Open Demo Scene**。

## 操作说明

| 按键 | 功能 |
| --- | --- |
| A / D | 左右移动（同时按视为无输入） |
| 左 / 右 Shift | 疾跑 |
| Space | 跳跃（无二段跳）；跟随型影子各自判定起跳 |
| E | 开始影迹准备 / 准备中再按取消 / 录制中再按提前结束 |
| F | 推进剧情对白；接触已解锁大门时通行（清空影子并取消录制） |
| 鼠标左键 | 推进剧情对白 |
| H 或右上角按钮 | 暂停 / 继续（剧情与死亡动画期间禁用） |
| Q | 放弃当前关卡，返回主菜单 |
| 主键盘 1–5 | 选择对应编号影子；选空编号取消选择 |
| Tab | 影子上限超过 5 个时循环切换选择   |
| R | 0.3 秒窗口后删除选中的影子 |
| 连按两次 R | 清空全部影子 |

数字键只影响高亮和删除目标，不影响跟随型影子是否响应输入。

## 影迹系统

### 录制与分类

按 `E` 开始一轮影迹：**准备 3 秒 → 录制最多 6 秒 → 生成影子**。准备 / 录制中可正常移动、疾跑、跳跃；生成起点是准备结束瞬间玩家所在位置。系统只按左右输入**意图**分类，不回放完整轨迹、不重播疾跑与跳跃；原地跳跃不改变类型，撞墙时按住方向仍算移动。

| 影子 | 生成条件 | 行为 |
| --- | --- | --- |
| 停留 Stay（蓝） | 全程无有效左右输入 | Kinematic、无重力，在起点正下方找地面后**原地不动**，可用于踩机关、压压板 |
| 趋近 Approach（橙） | 单向移动，结束前停顿不足 0.3 秒 | 沿首向匀速前进，可推可推动箱子；撞墙或推不动持续 0.4 秒后永久停止，停止后成为玩家可站立的平台 |
| 回避 Avoid（紫） | 录制中发生过左右反向（最高优先级） | 沿首向前进，遇障碍周期性反向往返 |
| 跟随 Follow（绿） | 单向移动且结束前停顿至少 0.3 秒 | 镜像玩家的左右与跳跃输入，与玩家各自独立受重力、地形和楼层约束 |

其他规则：

- 默认最多并存 5** 个**影子，可在 Shadow Manager 的 Max Shadows 调整（运行前生效），满员不能开始新一轮。
- 影子之间互不碰撞；除停止的趋近型外，影子不阻挡玩家。趋近型停止时若与玩家重叠，会等玩家离开后再开启实体碰撞。
- 生成前做空间占用检测，停留型还要求下方有地面；失败不消耗编号。
- 影子持续存在直到被删除、随场景卸载或被大门通行等逻辑清空。
- 动态影子没有悬崖保护、寻路和自动跳跃。

### 核心组件

- `PlayerController2D`：移动 / 疾跑 / 跳跃、地面检测（`TracePhysics`）。
- `ShadowTraceAbility`：E 键录制状态机（Idle → Preparing → Recording）。
- `TraceClassification`：按输入意图分类（Stay / Approach / Avoid / Follow）。
- `ShadowManager`：影子槽位、编号复用、选择、删除、`TrySpawn`。
- `ShadowActor`：四类影子的运行时行为、类型配色、头顶编号。

## 关卡机关与交互

| 对象 | 脚本 | 行为 |
| --- | --- | --- |
| 地刺 `spike1` | `SpikeHazard` + `PlayerLife` | 仅玩家触碰死亡：红色闪烁淡出 0.6 秒（时间暂停）后返回菜单；影子免疫 |
| 路灯 `light2` | `ShadowLamp` | 仅影子触碰点亮并播放音效，同时作为**检查点**写入存档；复活点做地面与地刺安全校验 |
| 持续型压板 | `ShadowPressurePlate`（Sustained） | 只认影子的顶部踩踏；最后一个影子离开 / 被删后释放 |
| 短暂型压板 | `ShadowPressurePlate`（Latched） | 影子踩一次即永久保持，直到场景重置；玩家与侧面擦碰不触发 |
| 大门 `door1` | `InteractiveDoor` | 所有绑定压板满足 All（默认）/ Any 条件后解锁，开门图但仍保持实体阻挡 |
| F 通行 | `InteractiveDoor.TryPass` | 玩家在交互范围内按 F 才获得本次通行权限，同时清空影子、取消录制；附近显示 `[F]` 提示 |
| 终点 `End` | `EndGameTrigger` | 只接受玩家（影子无效），触发两段终局剧情 |
| 移动平台 | `MovingPlatform` + `ShadowMoverPlate` | 见下节 |

### 影子驱动的移动平台（SampleScene）

- `ShadowMoverPlate`（压板）：**默认只认影子，玩家踩压无效**（Inspector 的 Player Can Press 可手动放开）。影子进入 Press Area 即触发，压板切换按压精灵。
  - **Patrol（默认）**：踩着平台朝终点走，松开沿原路返回起点停住。
  - **Toggle**：按一下走向另一端；移动途中再按会等走到端点后立刻反向；必须离开后再踩才算新的一次。
- `MovingPlatform`（平台）：Kinematic 刚体 + `MovePosition`，在原点与「原点 + Offset」之间按 Speed 往返；自动驮运站在顶面的角色（玩家与影子都可乘坐）；Scene 视图有蓝色 gizmo 显示轨迹两端。
- 一键接线：选中 SampleScene 后执行 **Tools → Shadow Trace → Setup Moving Platform (SampleScene)**，工具会给 `moveplatform` 补 Kinematic 刚体与按精灵轮廓生成的碰撞体、给 `PressurePlate1` 挂机关脚本并关闭旧的压板脚本。执行后 Ctrl+S 保存场景即可 Play。

## 游戏流程、剧情与存档

- **开场剧情**：进入 Level 1 约 2 秒后播放开场对白；已有存档时跳过。经过大门还会触发对应的门剧情。对白期间 `Time.timeScale = 0`，按 F 或鼠标左键逐句推进。
- **暂停**：H 键或右上角暂停按钮，遮罩盖住画面，音频切换到暂停状态。
- **检查点**：影子点亮路灯即存档（HUD 提示保存成功）。存档为 `Application.persistentDataPath/checkpoint.json`，采用临时文件 + 原子替换写入，内容含场景路径、路灯 ID 与坐标。
- **死亡**：地刺击杀后播放红色闪烁淡出，随后回到主菜单；可在菜单选「继续」回到最近一次点亮的路灯。存档损坏或场景不匹配时菜单会弹出无存档提示。
- **返回菜单**：Q 键随时返回（剧情 / 死亡中禁用）。
- **终局**：碰到 End → 第一段普通对白 → 第二段黑底白字（EndWord）。最后一句确认后白字淡出，**黑底保持全黑不退场**，由跨场景常驻的 `SceneFadeOverlay` 接管：纯黑状态下异步加载 Log 场景，激活后黑幕用 0.6 秒缓缓淡开（`Ending Reveal Duration` 可调），全程不会闪出 Level 1 画面；通关同时清除存档。

## UI 与音频

- HUD（`ShadowTraceHUD`）：影子槽位图标、操作提示、准备 / 录制倒计时与预计影子类型、保存成功 / 数量上限等优先提示。
- 主菜单（`MainMenuController`，Log 场景）：开始（二次确认新游戏，会清空旧档）、继续（无有效存档时弹窗提示）、教程与音频设置页签、退出（二次确认）。
- 音频（`GameAudioManager`，`Game Audio.prefab`，DontDestroyOnLoad 单例）：按场景播放背景音乐；脚步声随移动 / 疾跑切换；落地、开门、点灯、压板、H 交互、时钟等音效；主 / 音乐 / 音效三路音量与开关持久化到 PlayerPrefs，菜单内音频面板实时调节。

## 动画

玩家与四类影子共用 `Assets/Animation/Player.controller` 与同一组片段，由 `ActorAnimationDriver` 驱动整数参数 `MotionState`：

| 值 | 状态 | 条件 |
| --- | --- | --- |
| 0 | Idle | 落地且水平速度近零 |
| 1 | Walk | 落地移动 |
| 2 | Run | 玩家落地移动且按住 Shift（影子不疾跑） |
| 3 | Jump | 离地（含走出平台下落），落地退出 |
| 4 | Shadowtrace | 成功生成影子且地面静止，播 0.5 秒，移动 / 离地立即打断 |

Animator 与精灵位于 `Visual` 子物体，碰撞体留在根节点，动画不影响物理尺寸；影子的类型配色、半透明与选中黄高亮持续生效。可用 **Tools → Shadow Trace → Configure Shared Actor Animation** 重建接线。

## 编辑器工具（Tools 菜单）

- **Shadow Trace → Open Demo Scene / Create Demo Assets**：打开演示场景或重建演示资源。
- **Shadow Trace → Configure Shared Actor Animation**：配置共用动画器、预制体与 SampleScene。
- **Shadow Trace → Configure Scene Interactions**：重建交互预制体与场景绑定（保留已有模式与目标）。
- **Shadow Trace → Setup Moving Platform (SampleScene)**：一键接线移动平台机关。
- 顶层工具菜单中的 **Square 障碍碰撞体批量工具**（`Assets/Editor/SquareObstacleColliderTool.cs`）：按精灵可见轮廓为 Square 前缀物体批量生成 / 校准 BoxCollider2D，幂等可重复执行。

## 目录结构

```text
Assets/
├── Animation/               共用 Animator Controller 与动画片段
├── Art/                     角色与影子序列帧、环境、背景、UI 美术
├── Music/                   音乐与音效（mp3）
├── Prefab/                  Level 1 用预制体（Player、End、灯、门、压板、地刺、平台等）
├── MapPrefabs/              地图拼装用 Square 预制体
├── Scenes/                  Log（菜单）、Level 1、SampleScene
├── Settings/                URP 资源与 2D Renderer
├── Editor/                  工程级编辑器工具
└── ShadowTrace/
    ├── Runtime/             全部玩法脚本（asmdef: ShadowTrace.Runtime）
    ├── Editor/              Shadow Trace 编辑器工具（asmdef: ShadowTrace.Editor）
    ├── Prefabs/             Player、Shadow、PushableBox 等系统预制体
    ├── Scenes/              ShadowTraceDemo 演示场景
    ├── Level1UISettings.asset  Level 1 文案与 HUD 配置
    └── Tests/               EditMode / PlayMode 测试
Docs/                        分系统文档（见下方索引）
Packages/                    包依赖与锁文件
ProjectSettings/             项目设置
```

资源需连同 `.meta` 一起保留；`Library/`、`Temp/`、`Logs/`、`UserSettings/` 为生成目录，不入库。

## 文档索引（Docs/）

- [影迹系统使用文档](Docs/影迹系统使用文档.md)：影子分类、物理与接入细节
- [场景交互使用文档](Docs/场景交互使用文档.md)：压板、大门、地刺、路灯配置
- [MainMenu.md](Docs/MainMenu.md)：主菜单结构与按钮逻辑
- [CheckpointSave.md](Docs/CheckpointSave.md)：检查点存档格式与复活流程
- [Level1UI.md](Docs/Level1UI.md)：Level 1 HUD、暂停与剧情 UI
- [GameAudio.md](Docs/GameAudio.md)：音频单例、音量与场景音乐
- [ShadowInteractionFixes.md](Docs/ShadowInteractionFixes.md)：交互问题修复记录

## 测试

通过 **Window → General → Test Runner** 运行：

- **EditMode**：影子分类、停止阈值、反向优先级、槽位编号复用等纯逻辑测试。
- **PlayMode**：推动箱子、受阻停止变平台、回避往返、跟随型移动 / 独立跳跃 / 跨楼层、压板权限与多板组合、F 通行、路灯存档、暂停 / 死亡、主菜单流程、Level 1 完整剧情与终局转场（含黑幕全程不穿帮、转场后自毁）。

最近一次终局改动（黑屏异步转场）在 Unity `2022.3.62f3c1` 隔离副本中验证：`LevelStoryTests` 5 项全部通过。键盘手感、动画与画面效果仍需在 Game 窗口人工验收。

## 常见问题

- **画面发紫 / 材质 Missing**：确认 URP 14.0.12 已安装且 `manifest.json` 保留 universal RP 依赖，等待导入完成。
- **按键无响应**：先点一下 Game 窗口获取焦点，并确认 Active Input Handling 含 Input Manager。
- **生成不了影子**：看 HUD 提示——影子槽满、起点被占或停留型脚下找不到地面都会失败。
- **压板不触发**：所有压板只认影子，玩家站上去无效；影子需进入触发区域（移动平台压板）或从板面上方踩住（大门压板）。
- **门显示开了还挡路**：开门只是解锁状态，需要玩家走到门前按 F 才能穿过。
- **趋近型停下后站不上去**：先离开影子让它安全开启实体碰撞，再站上其顶部。
- **「继续」提示无存档**：存档已被新游戏 / 通关清除，或存档对应的不是该关卡；点亮路灯后才会产生检查点。
- **移动平台不动**：在 SampleScene 执行一次 Tools → Shadow Trace → Setup Moving Platform (SampleScene) 并保存场景，检查平台 Offset 不为零、压板 Target Platform 已关联。
