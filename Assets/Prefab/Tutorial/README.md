教程资源使用说明

- TutorialHUD.prefab：教程 HUD，已放在第一关 Canvas 下。上方中间为提示，左下角为操作手册。可直接调整布局、字体、图片和按钮。
- TutorialLessons.asset：每一步的标题、简短提示、记录阶段提示及手册正文。运行时文案以这里的配置为准。
- TutorialObstacle.prefab / TutorialStep.prefab：白灰色练习障碍与辅助矮台。调整尺寸时同时调整 SpriteRenderer、BoxCollider2D 和四条描边；运行时自动按底部贴地放置。
- TutorialArea.prefab：按步骤开放的玩家活动范围。选中第一关中的 Tutorial Area，可在 Scene 视图查看边框；在 ranges 中修改边界。范围只约束玩家，不限制影子。
- Tutorial Practice Anchor：第一关中的安全练习起点；调整起点时同时检查教学范围。

确认“启行”会重新开始教程；取消启行不重置。续行保持进度，跳过教学立即解除范围限制。

请在编辑模式下修改并保存。Tools > Shadow Trace > Configure Editable Tutorial 可补齐丢失的资源和场景引用，已有资源不会被默认配置覆盖。
