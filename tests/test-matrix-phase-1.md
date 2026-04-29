# A11yFlow 一期测试矩阵

## 1. 目标

定义 `a11yflow` 一期最小可用闭环的测试范围，覆盖：

- 元素定位
- 动作执行
- 事件更新
- fallback 逻辑
- 协议结果一致性

本矩阵偏架构验证，不依赖具体实现语言。

## 2. 一期范围

一期能力假设：

- `UIA` 主 backend
- 默认 `Control View`
- 活动窗口快照
- 焦点上下文输出
- `StrictSelector + StructuralSelector + TextSelector + near + fallback chain`
- `invoke / focus / set_value / toggle / expand / collapse`
- 基础输入模拟兜底

## 3. 测试维度

测试分为五类：

1. `Locator`
2. `Action`
3. `Cache and Event`
4. `Fallback`
5. `Protocol and Diagnostics`

## 4. Locator 测试

### L-001 严格 selector 命中单元素

- 前置条件：活动窗口中存在唯一 `button[name="保存"]`
- 输入：`scope:active_window button[name="保存"]`
- 预期：
  - 返回单一元素
  - `confidence` 高
  - `ambiguity_count = 0`

### L-002 严格 selector 多命中

- 前置条件：窗口中有多个名称相同按钮
- 输入：`scope:active_window button[name="确定"]`
- 预期：
  - 返回 `ambiguous`
  - 给出候选数量与排序信息

### L-003 结构 selector 命中

- 前置条件：元素位于稳定层级结构中
- 输入：`scope:window(name="设置") > group[name="代理"] > button[name="保存"]`
- 预期：
  - 成功命中目标
  - 诊断中显示使用结构路径

### L-004 后代路径在结构轻微变动后仍命中

- 前置条件：目标前插入一层容器
- 输入：`scope:window(name="设置") >> button[name="保存"]`
- 预期：
  - 仍然命中
  - `strategy_used` 标记为 descendant search

### L-005 文本 selector 命中

- 前置条件：元素文本稳定但 `automation_id` 缺失
- 输入：`button:text("保存")`
- 预期：
  - 命中正确元素
  - 记录文本策略

### L-006 near selector 命中相邻输入框

- 前置条件：表单中有 `text("用户名")` 及其右侧输入框
- 输入：`edit right_of text("用户名")`
- 预期：
  - 命中正确输入框
  - 记录 anchor/relative strategy

### L-007 selector 回退链生效

- 前置条件：主 selector 失效，次级 selector 有效
- 输入：`button[automation_id="saveBtn"] || button[name="保存"]`
- 预期：
  - 第二段成功
  - 诊断显示第一段失败、第二段成功

### L-008 活动窗口作用域隔离

- 前置条件：多个窗口均存在 `button[name="确定"]`
- 输入：`scope:active_window button[name="确定"]`
- 预期：
  - 仅在活动窗口中搜索
  - 不命中后台窗口元素

## 5. Action 测试

### A-001 invoke 动作走 UIA pattern

- 前置条件：按钮支持 `invoke`
- 动作：`invoke`
- 预期：
  - `execution_path = uia_pattern`
  - 动作成功

### A-002 set_value 成功写入输入框

- 前置条件：输入框支持 value pattern
- 动作：`set_value(text="127.0.0.1:7890")`
- 预期：
  - 文本成功写入
  - 校验通过

### A-003 toggle 成功变更复选框状态

- 前置条件：复选框支持 toggle
- 动作：`toggle(target_state=true)`
- 预期：
  - 状态从未选中变成已选中
  - 事件或属性变化可观察

### A-004 expand / collapse 成功

- 前置条件：树节点支持展开折叠
- 动作：先 `expand` 再 `collapse`
- 预期：
  - 两次动作都成功
  - 展开状态正确变化

### A-005 focus 动作更新焦点

- 前置条件：目标可聚焦
- 动作：`focus`
- 预期：
  - `FocusChanged` 事件出现
  - 目标成为焦点元素

### A-006 不支持 pattern 时回退输入模拟

- 前置条件：目标可见但缺少结构化动作 pattern
- 动作：`click`
- 预期：
  - 回退到 `input_simulation`
  - 结果成功或明确失败原因

## 6. Cache and Event 测试

### C-001 活动窗口快照生成

- 前置条件：有稳定活动窗口
- 操作：请求活动窗口摘要
- 预期：
  - 仅生成当前窗口摘要
  - 不无谓扫描全桌面

### C-002 焦点变化后局部刷新

- 前置条件：窗口中有多个可聚焦元素
- 操作：切换焦点
- 预期：
  - 只刷新焦点附近上下文
  - 不全量重建树

### C-003 结构变化后局部子树失效

- 前置条件：展开或关闭某个容器导致子元素变化
- 操作：触发结构变化
- 预期：
  - 仅受影响子树失效并重建

### C-004 属性变化增量更新

- 前置条件：按钮 enabled 状态可切换
- 操作：触发按钮状态变化
- 预期：
  - 仅更新对应字段
  - 快照中的其他节点不受影响

## 7. Fallback 测试

### F-001 主 selector 失败后文本 selector 成功

- 前置条件：`automation_id` 已变化，但文本仍稳定
- 输入：`button[automation_id="saveBtn"] || button:text("保存")`
- 预期：
  - 第一段失败
  - 第二段成功

### F-002 结构层无元素时进入视觉层

- 前置条件：自绘控件无法暴露有效 UIA 节点
- 输入：`button[name="开始"] || vision:text("开始")`
- 预期：
  - 结构层失败
  - 视觉层接管
  - 返回视觉执行路径

### F-003 fallback 禁用时不允许自动降级

- 前置条件：结构动作失败
- 策略：`allow_fallback = false`
- 预期：
  - 直接失败
  - 明确说明 fallback 被策略禁止

### F-004 超过最大重试次数后终止

- 前置条件：元素持续不可达
- 策略：`max_retries = 2`
- 预期：
  - 最多尝试指定次数
  - 返回 `FALLBACK_EXHAUSTED` 或等效错误

## 8. Protocol and Diagnostics 测试

### P-001 ActionResult 包含标准字段

- 前置条件：任意成功请求
- 预期：
  - 包含 `request_id`, `success`, `status`, `execution_path`, `timing`

### P-002 失败结果包含标准错误码

- 前置条件：目标元素不存在
- 预期：
  - 错误码为 `TARGET_NOT_FOUND`
  - `retryable` 字段合理

### P-003 ambiguity 结果包含候选信息

- 前置条件：多命中场景
- 预期：
  - 返回 `status=ambiguous`
  - 包含候选数量和排序摘要

### P-004 dry_run 不实际执行动作

- 前置条件：目标存在
- 策略：`dry_run = true`
- 预期：
  - 只完成定位和执行计划解析
  - 不产生真实点击或输入

### P-005 校验失败时返回 verification 信息

- 前置条件：动作执行了，但未达到期望结果
- 预期：
  - `verification.passed = false`
  - 包含观察到的实际状态

## 9. 场景化集成测试

### I-001 设置页填写并保存

- 步骤：
  1. 定位代理地址输入框
  2. 写入地址
  3. 定位保存按钮
  4. 点击保存
- 预期：
  - 整个链路成功
  - 每步都有标准结果

### I-002 登录表单输入与提交

- 步骤：
  1. 通过锚点找到用户名输入框
  2. 输入用户名
  3. 通过锚点找到密码框
  4. 输入密码
  5. 点击登录
- 预期：
  - 锚点定位稳定
  - 最终出现窗口切换或页面变化

### I-003 对话框歧义消解

- 步骤：
  1. 出现多个“确定”按钮
  2. 主定位产生歧义
  3. 使用结构路径或 anchor 重新定位
- 预期：
  - 系统能报告歧义并通过新 selector 消解

## 10. 测试数据和环境建议

建议至少准备三类被测应用：

- 标准 Win32 / WinForms 对话框
- WPF 设置页或表单
- 一个结构不稳定或自绘程度较高的样本程序

建议环境变量覆盖：

- 单显示器 / 多显示器
- 不同 DPI 缩放
- 中英文界面
- 前台 / 后台窗口切换

## 11. 一期验收门槛建议

可作为一期最小验收标准：

- `Locator` 核心用例通过率高
- `invoke / set_value / toggle / focus` 闭环可用
- 回退链按策略生效
- `ActionResult` 字段稳定
- 局部事件刷新逻辑成立
- 在若干真实桌面软件上，A11yFlow 能沿 `accessibility_tree` 主路径完成基本任务并给出可解释结果

当前阶段说明：

- 本矩阵仍然服务于 A11yFlow 一期主产品
- 当前主产品是控件树优先的结构化桌面自动化
- OCR、截图、视觉 fallback 不作为当前验收前提
- 若真实软件验证显示控件树长期不可用，再单独开启未来产品线

## 12. 核心结论

一期测试的重点不是覆盖所有桌面软件，也不是提前证明视觉 fallback，而是验证 `a11yflow` 这条核心路径是否已经具备真实产品价值：

`文本 selector -> 结构定位 -> 动作执行 -> 结果校验 -> fallback 恢复`

只要这条 `accessibility_tree` 闭环在真实软件上稳定成立，后续再扩 backend、扩 selector、扩视觉能力才有价值。
