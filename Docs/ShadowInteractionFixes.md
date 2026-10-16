# 影子生成与机关触发修复

PressurePlate1 的根碰撞框和 Press Area 均为 Trigger，不再阻挡角色。ShadowMoverPlate 按区域重叠检测，而非要求脚底对齐旧板面；Player Can Press、Shadow Can Press 和平台巡逻／交替模式继续生效。触发范围仍通过 Press Area 的 BoxCollider2D Size、Offset 在 Inspector 中调整。取消实体支撑后，机关下方需有场景地面。

四种影子接触 SpikeHazard 的触发区域后，统一通过 ShadowActor.Despawn → ShadowManager.Remove 删除。当帧移出编号槽位、关闭对象并销毁，图标随现有 HUD 刷新隐藏。后续生成复用空缺编号；玩家碰刺仍执行原有死亡流程。

停留影子生成时，使用实际碰撞框尺寸、缩放和 Offset 计算落点与重叠检查区域，保证碰撞框底部落在地面上方的接触间隙内。横向 Offset 和镜像缩放也纳入计算。原有向下寻地规则保留，不需要调整图片、动画或把影子强制设置到玩家的 Transform 坐标。

本轮修改现有功能脚本 6 个：ShadowMoverPlate、SpikeHazard、ShadowManager、ShadowActor、TracePhysics，以及编辑器工具 MovingPlatformSetup。未新增功能脚本。PressurePlate1 预制体已更新；场景中的实例通过预制体继承更新，原有平台引用和位置参数保留。

测试覆盖：Collider Offset 与正负缩放、实际碰撞框重叠检查、四种影子碰刺销毁及编号图标释放、玩家碰刺死亡、多个影子共同触发、触发对象过滤、巡逻返回、交替模式离开后重新触发，以及实际 SampleShadow 预制体贴地并激活 PressurePlate1。
