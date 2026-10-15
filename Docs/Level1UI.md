# Level1 影子 UI

本轮功能脚本：新增 `GamePauseController`、`ShadowUISettings`，修改 `ShadowTraceHUD`、`ShadowManager`、`ShadowTraceAbility`、`PlayerController2D`。

## 编辑提示文字

在 Project 中选择 `Assets/ShadowTrace/Level1UISettings.asset`，在 Inspector 修改：

- Preparation Format：准备倒计时格式，`{0}` 表示剩余秒数。
- Recording Format：生成倒计时格式，同样使用 `{0}`。
- Spawned、Limit Reached、Preparation Cancelled、Cleared：成功、上限、取消、清空提示。
- Missing References、Spawn Blocked：生成失败提示。
- Notice Duration：短提示停留秒数，暂停期间停止计时。

修改文字不需要修改脚本。准备和生成时长仍在 Player 的 ShadowTraceAbility 组件中设置，Level1 默认为 3 秒和 6 秒。

## 场景绑定

`Assets/Scenes/Level 1.unity` 中：

- Shadow Manager 的 ShadowTraceHUD 引用现有 Tooltip 和 Text (Legacy)，Occupied Icons 按 1～5 号绑定“有影子”对象。
- Shadow Manager 的 UI Settings 引用上述配置资产；Max Shadows 为 5。
- Canvas 的 GamePauseController 引用暂停按钮、两种图片、玩家生命组件和 Pause Blackout 遮罩。
- 暂停按钮的图片由暂停状态控制，Transition 为 None。按钮事件由脚本注册，无需重复添加 On Click。
- Canvas 使用 1920×1080 参考分辨率缩放，保留原有组件的布局。

空槽保持显示；删除影子不会改变其他影子的编号。多个编号空缺时，优先复用较小编号。

## 操作与验收

1. 初始五个“有影子”和 Tooltip 隐藏；按 E 显示 3、2、1，再显示“正在生成”的倒计时。
2. 生成阶段按 E 提前结束，或等待倒计时自动结束，成功后显示“影子出现”和对应图标。
3. 准备阶段再次按 E 沿用原有取消准备行为。
4. 生成五个后再次按 E 显示上限提示。用数字键选择影子、R 删除；删除 1、3 后只隐藏这两个图标，重新生成依次补回 1、3。
5. 点击暂停，画面黑屏、按钮变为播放；倒计时和游戏操作停止。再次点击继续。
6. 修改配置资产中的提示文字，重新运行确认生效。

新增 PlayMode 集成测试：`Assets/ShadowTrace/Tests/PlayMode/Level1UITests.cs`。

## 影子编号与暂停快捷键

每个影子头顶显示两位编号 01～05，删除及补位时沿用固定编号。字体引用 Assets/Art/UI/ArtFont/font.otf。编号格式、字体、字号、颜色、字形尺寸和头顶偏移均可在 Level1UISettings.asset 中调整。

空格只负责跳跃。点击暂停图标或按 H 切换暂停／继续；暂停按钮已关闭键盘导航并在点击后清除选中状态，避免空格触发 UI Submit。
