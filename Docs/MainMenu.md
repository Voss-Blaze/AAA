# Log 主菜单

功能脚本：`Assets/ShadowTrace/Runtime/MainMenuController.cs`，挂载在 Log 场景的 Canvas 上。组件引用已通过 Inspector 序列化，运行时注册按钮事件，无需重复添加 On Click。

- Start 打开 PopPanelStart；NO 关闭，OK 加载组件中配置的 Level1 场景。
- Quit 打开 PopPanelQuit；NO 关闭，OK 在打包游戏中退出程序，在 Unity 编辑器中停止 Play。
- SettingButton 打开 SettingPanel，每次默认显示操作教程并高亮教程分类。
- 操作教程、音乐音效两个分类按钮切换对应内容，高亮图取自 Button 已配置的 Selected Sprite，未配置时使用 Pressed Sprite，再使用 Highlighted Sprite。
- 返回按钮隐藏设置面板。弹窗、设置面板打开期间，背景菜单不可交互。

所有页面文字沿用场景中的 Text 组件；场景路径和组件引用在 MainMenuController 的 Inspector 中配置，无需修改代码。UI 布局保持现有设计。音量滑块、音乐/音效按钮、Resume 和 PopPanelResume 未添加业务逻辑。

Build Settings 已将 Log 设为第一个启动场景，Level 1 设为第二个，原有演示和 SampleScene 条目保留。

集成测试：`Assets/ShadowTrace/Tests/PlayMode/MainMenuTests.cs`，覆盖弹窗开关、背景交互、设置分类及高亮、重新打开设置时重置分类，以及确认 Start 后实际加载 Level1。
