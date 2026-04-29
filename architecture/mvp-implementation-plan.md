# Allyflow 一期 MVP 实施方案

## 1. 目标

在现有架构草案基础上，定义 `allyflow` 一期 `MVP` 的可实施方案，目标不是覆盖所有桌面软件，而是尽快打通一条稳定闭环：

`窗口发现 -> 结构化快照 -> 元素定位 -> 动作执行 -> 结果校验 -> 有限回退`

当前阶段补充：

- 这条闭环现在已经基本落地
- 项目当前重点不再是开放接口，而是直接把 Allyflow 当产品去操作真实桌面软件
- 当前产品旗帜明确为 `accessibility_tree` 优先
- `OCR`、截图、视觉 fallback 暂不进入当前阶段实施范围，只保留未来边界

一期要回答的不是“架构是否足够完整”，而是：

- `allyflow` 是否能稳定操作典型 Windows 桌面应用
- 文本选择器和结构化动作是否足以支撑 agent 执行常见任务
- 元素引用、动作协议和诊断输出是否足以支撑重试与恢复

## 2. 一期范围

一期只实现最小可用闭环，不追求全量能力。

### 2.1 包含内容

- `Windows 10/11`
- `UIA` 主 backend
- `Control View` 默认树
- 活动窗口范围内的快照与定位
- 焦点上下文输出
- 元素引用 `ref` 机制
- `StrictSelector + StructuralSelector + TextSelector`
- `invoke / focus / set_value / toggle / expand / collapse / click / press_keys`
- 基础结果校验
- 有限输入模拟兜底
- 标准化诊断输出

### 2.2 暂不包含

- `MSAA` 兼容 backend
- 全桌面持续建模
- 复杂 anchor/fuzzy ranking
- 大规模视觉推理
- 自愈学习系统
- 跨会话历史记忆
- 面向人类的 inspector UI

## 3. 实现栈建议

一期建议直接收敛到以下实现栈：

- 语言：`C#`
- 运行时：`.NET 8`
- 自动化库：`FlaUI.UIA3`
- 对外接口：本地 `JSON RPC` 或 `MCP server`，但当前阶段不以外部开放为主目标
- 日志：结构化文本日志 + 请求级 trace

选择理由：

- Windows 自动化在 `.NET` 生态更原生
- `FlaUI` 已封装大量 `UIA` 常见能力，适合快速建立闭环
- `UIA3` 对现代应用支持更好，覆盖 `Win32 / WinForms / WPF / UWP`
- `MCP` 或等价工具协议更适合 agent 集成

不建议一期主打 `WinAppDriver` 或 `Appium`，原因如下：

- 它们更偏测试驱动模型，不够 agent-native
- 会话和定位模型更适合测试脚本，不适合增量上下文消费
- 对本项目最重要的“快照 + ref + 动作协议 + 恢复”帮助有限

## 4. 一期系统边界

一期建议拆成五个最小模块：

1. `Window Registry`
2. `Snapshot Builder`
3. `Locator`
4. `Action Executor`
5. `Protocol Adapter`

## 4A. 底层 API / 语义清单（一页版）

当前项目应明确定位为：

- 一个服务于 Allyflow 上层的桌面操作 substrate
- 以 `accessibility_tree` 为主界面
- 负责感知、定位、执行、验证、恢复语义
- 不负责任务规划、用户意图解释、复杂工作流编排和最终用户体验

这份清单用于约束后续实现范围，避免底层继续膨胀成半个上层产品。

### 4A.1 底层必须提供的 API

最小查询面：

- `windows_list`
- `windows_active`
- `windows_snapshot`
- `windows_describe_ref`
- `windows_locate`
- `windows_refresh_focus`

最小动作面：

- `windows_act`

`windows_act` 当前承载的高价值动作原语：

- `invoke`
- `focus`
- `set_value`
- `toggle`
- `expand`
- `collapse`
- `click`
- `press_keys`

### 4A.2 底层必须提供的语义

1. 结构化感知

- 能列出窗口并识别活动窗口
- 能构建当前窗口或指定窗口的结构化树
- 能输出上层可消费的节点字段、动作能力和层级关系

2. 稳定引用

- 每个窗口和元素都应有会话内 `ref`
- `ref` 必须可用于后续查询、动作和恢复
- `windows_describe_ref` 必须能把 `ref` 重新展开为可理解的目标描述

3. 元素定位

- 支持 selector 定位
- 支持 `ref` 优先、selector 回退
- 支持歧义暴露，而不是静默猜测
- 支持 stale ref 检测和有限重定位

4. 动作执行

- 优先使用结构化 UIA pattern
- 只在显式允许时使用有限 fallback
- 动作结果必须返回执行路径，而不是只返回布尔成功

5. 最小验证

- 区分“动作未执行”、“动作执行但未验证”、“动作已验证成功”
- 尽量返回观察到的 effect，而不是只返回调用成功
- 成功路径属性读取必须容错，不能因为附加属性不可读而把成功动作误判为失败

6. 恢复语义

- 失败必须返回稳定错误码
- 失败必须包含足够 diagnostics
- 失败必须尽量附带上层可直接消费的 next step

### 4A.3 底层必须稳定输出的错误语义

至少包括：

- `NO_ACTIVE_WINDOW`
- `WINDOW_NOT_FOUND`
- `REF_NOT_FOUND`
- `REF_STALE`
- `TARGET_STALE`
- `TARGET_NOT_FOUND`
- `TARGET_AMBIGUOUS`
- `ACTION_NOT_SUPPORTED`
- `VERIFICATION_FAILED`
- `FALLBACK_BLOCKED`
- `FALLBACK_EXHAUSTED`
- `BACKEND_ERROR`
- `INVALID_ARGUMENT`

要求：

- 错误码比错误文案更稳定
- diagnostics 比自由文本更重要
- `suggested_next_step` 应服务上层恢复，而不是服务终端用户教学

### 4A.4 明确不属于底层的能力

以下能力默认属于 Allyflow 上层，不应继续下沉到底层：

- 用户自然语言意图解释
- 多步任务规划
- 条件分支和事务式工作流编排
- app-specific operator playbook
- 面向终端用户的交互体验
- 视觉兜底、多模态理解和截图/OCR 主线

判断标准：

- 如果问题是在回答“当前桌面上有什么、能操作什么、操作后发生了什么、失败后如何恢复”，它属于底层
- 如果问题是在回答“为了完成用户任务，下一步应该做什么”，它属于上层

### 4A.5 当前阶段的底层质量目标

当前阶段不再以“新增多少接口”衡量进展，而以这四项为主：

- 定位稳定性
- 执行一致性
- 验证可信度
- 失败可恢复性

### 4.1 Window Registry

职责：

- 列出顶层窗口
- 获取活动窗口
- 维护窗口句柄与内部窗口引用映射

建议输出字段：

- `window_ref`
- `native_handle`
- `title`
- `process_id`
- `framework_id`
- `is_active`

### 4.2 Snapshot Builder

职责：

- 从活动窗口构建精简结构化树
- 为节点分配稳定的会话内 `ref`
- 输出 agent 可消费的文本摘要

一期快照只保留必要字段：

- `ref`
- `role`
- `name`
- `automation_id`
- `class_name`
- `bounds`
- `states`
- `actions`
- `children`

### 4.3 Locator

职责：

- 解析 selector
- 在当前窗口快照中定位候选元素
- 给出最佳候选与诊断信息

一期只支持：

- `scope:active_window`
- `scope:window(name="...")`
- `role[...]`
- `>`
- `>>`
- `=`
- `~=`
- `text(...)`
- `||`

当前进度：已完成阶段 2 最小定位闭环。

已实现内容：

- `SelectorParser` 已可解析一期 selector 子集
- `SnapshotLocator` 已在结构化快照树上执行匹配，而不是直接依赖实时 UIA 遍历
- 已支持 strict、structural、descendant、text 和 fallback chain 这几类一期定位策略
- 已输出候选、最佳命中、歧义状态和诊断信息，面向 `allyflow` 的文本视觉消费

### 4.4 Action Executor

职责：

- 基于 `ref` 或 selector 找到目标元素
- 优先走结构化 pattern
- 失败后按策略有限回退
- 返回标准 `ActionResult`

一期执行路径顺序：

1. `uia_pattern`
2. `input_simulation`

不在一期实现：

- `msaa_default_action`
- `native_message`
- `vision_guided_input`

当前进度：已完成阶段 3 最小执行闭环，并开始阶段 4 的第一轮恢复与诊断加固。

已实现内容：

- `TargetResolver` 已支持 `ref` 优先、selector 回退的最小目标解析链路
- `UiaActionExecutor` 已支持 `invoke / focus / set_value / toggle / expand / collapse / click / press_keys`
- 动作结果已标准化为 `ActionResult`，包含 `target_resolution`, `execution_path`, `verification`, `timing`
- 为满足 `allyflow` 的文本操作闭环，最小 `dry_run` 与有限 `input_simulation` fallback 已提前接入
- `windows_act` 现已补充执行计划型 `dry_run` 输出、`max_retries` 重试预算处理、attempt 级诊断字段和更真实的 expected outcome 比对

### 4.5 Protocol Adapter

职责：

- 暴露工具调用接口
- 在文本请求与内部对象之间做适配
- 统一返回标准结果

建议一期对外只暴露少量高价值工具。

## 5. 一期核心接口

建议一期先固定 8 个接口。

### 5.1 `windows_list`

返回当前顶层窗口列表。

### 5.2 `windows_active`

返回当前活动窗口摘要。

### 5.3 `windows_snapshot`

输入：窗口 `ref` 或默认活动窗口。

输出：结构化文本树和节点 `ref`。

### 5.4 `windows_describe_ref`

输入：元素 `ref`。

输出：元素详细描述与可用动作。

当前进度：已实现最小版本，可返回元素角色、名称、状态、动作、父子关系。

### 5.5 `windows_locate`

输入：selector。

输出：`ElementMatch` 或歧义/失败结果。

当前进度：已实现最小版本，可返回 `found / ambiguous / not_found / invalid_selector`，并附带候选和诊断。

### 5.6 `windows_act`

输入：标准化 `ActionRequest`。

输出：标准化 `ActionResult`。

当前进度：已实现最小版本，可返回结构化 `ActionResult`，并通过 `windows_locate` / `ref` 解析目标后执行一期动作子集；同时已补充 `dry_run` 计划摘要、重试预算、attempt 诊断和更细的 verification 结果。

### 5.7 `windows_press_keys`

用于无结构目标或通用快捷键路径。

当前进度：能力已先并入 `windows_act(action="press_keys")`，独立工具是否单独暴露留待后续阶段决定。

### 5.8 `windows_refresh_focus`

刷新当前焦点上下文，减少整窗重复快照。

当前进度：最小版本已实现，`QueryToolService` 可返回当前焦点元素、父链、同级和子级 `ref`，并把最近一次 locate 的 selector / ref 作为辅助诊断回传。

## 6. 元素引用模型

一期必须把 `ref` 机制做成一等公民，而不是每一步都重新依赖 selector。

### 6.1 设计目标

- 先快照，后基于 `ref` 交互
- `ref` 在单次会话中稳定
- `ref` 失效时允许按 selector 重定位
- 对 agent 暴露短引用，而不是底层对象细节

### 6.2 建议规则

- 窗口引用：`w1`, `w2`
- 元素引用：`w1e1`, `w1e2`
- 每次窗口重建快照时允许重新编号
- 但在同一快照版本内 `ref` 必须稳定

### 6.3 内部存储

建议维护：

```text
RefEntry {
  ref,
  window_ref,
  backend_source,
  automation_element,
  selector_hint,
  snapshot_version,
  created_at,
  last_validated_at
}
```

### 6.4 失效处理

当 `ref` 失效时：

1. 检查元素是否已 stale
2. 若请求中带 `selector_hint`，尝试重定位
3. 若重定位成功，返回新 `ref`
4. 若失败，返回 `TARGET_STALE`

当前进度：最小 stale 恢复路径已落地。`TargetResolver` 现在会在 `ref` stale 时优先使用请求 selector，若请求未提供 selector，则回退到历史 `selector_hint`；恢复成功后会把结果标记为 `selector_recovery`，并输出 `requested_ref / resolved_ref / ref_changed / recovered_from_stale_ref / selector_source` 等诊断字段。

## 7. 动作协议子集

一期不需要把完整协议全部实现，但要把闭环字段做全。

### 7.1 ActionRequest 一期必需字段

```text
ActionRequest {
  request_id,
  target,
  action,
  arguments,
  execution_policy,
  expected_outcome,
  timeout_ms
}
```

### 7.2 target 一期支持两种写法

- `ref`
- `selector`

优先级建议：

- 有 `ref` 时优先使用 `ref`
- `ref` 失效时可使用 `selector` 重定位

### 7.3 ExecutionPolicy 一期支持字段

- `allow_fallback`
- `max_retries`
- `require_visibility`
- `auto_activate_window`
- `verify_after_action`
- `dry_run`

### 7.4 ExpectedOutcome 一期支持类型

- `no_verification`
- `focus_change`
- `text_updated`
- `property_change`
- `window_opened`
- `window_closed`

当前进度：最小执行链路已支持把这些期望类型映射为标准 `VerificationResult`，其中 `text_updated`、`property_change`、`focus_change`、`window_opened`、`window_closed` 已具备更具体的 observed/expected 比对；更细的真实事件驱动校验仍留在后续阶段增强。

## 8. 结构化动作策略

一期建议只支持最关键的 8 类动作：

- `invoke`
- `focus`
- `set_value`
- `toggle`
- `expand`
- `collapse`
- `click`
- `press_keys`

### 8.1 动作映射建议

`invoke`

- 优先 `InvokePattern`
- 否则退化到 `click`

`set_value`

- 优先 `ValuePattern.SetValue`
- 否则退化到 `focus + ctrl+a + type`

`toggle`

- 优先 `TogglePattern`
- 若不支持则失败，不强行猜测

`expand / collapse`

- 优先 `ExpandCollapsePattern`
- 若不支持则失败

`focus`

- 使用结构化聚焦接口
- 成功后刷新焦点上下文

`click`

- 若元素支持可识别点击语义则走语义路径
- 否则走输入模拟点击元素边界中心点

当前进度：该策略已在阶段 3 最小实现中落地，并在阶段 4 首轮加固中进一步补上 retry budget、attempt diagnostics 与 fallback exhausted 结果表达。

## 9. 输入模拟兜底边界

一期可以做输入模拟，但必须收紧边界，不能把它当万能解。

### 9.1 允许触发的场景

- 元素存在但无对应 pattern
- 元素可见且边界可信
- 执行策略允许 fallback

### 9.2 不建议触发的场景

- 元素不可见或被遮挡
- 目标边界置信度低
- 当前前台窗口不匹配目标窗口
- 动作属于高风险系统操作

### 9.3 一期高风险动作限制

以下动作建议默认禁止自动 fallback：

- 安装器类确认
- 系统设置类开关
- 删除、卸载、格式化类按钮
- 涉及管理员权限切换的对话框

这些场景至少应返回明确风险提示，而不是静默点击。

## 10. 快照输出约束

为了让 agent 可消费，快照必须控制噪声。

### 10.1 一期默认输出原则

- 默认只输出活动窗口
- 默认只输出可交互元素和关键结构节点
- 文本长度受控
- 同类重复节点可截断

### 10.2 建议文本格式

```text
Window: 设置 [ref=w1]
Focused: 编辑框 "代理地址" [ref=w1e7]

Actionable elements:
- button "保存" [ref=w1e12] actions=[invoke]
- button "取消" [ref=w1e13] actions=[invoke]
- edit "代理地址" [ref=w1e7] actions=[set_value, focus]
- checkbox "自动检测设置" [ref=w1e9] actions=[toggle]
```

### 10.3 一期不要默认输出

- 完整原始属性字典
- 全量 descendants
- 大段不可交互文本
- 不稳定运行时 ID

## 11. 事件与缓存策略

一期不需要复杂事件总线，但至少要有轻量缓存和局部刷新。

### 11.1 一期缓存建议

- `active_window_snapshot`
- `focus_context_snapshot`
- `ref_index`
- `recent_locator_results`

### 11.2 一期刷新触发

- 调用 `windows_snapshot`
- 动作执行成功后目标状态可能变化
- 焦点发生变化
- 定位结果显示快照疑似过期

### 11.3 一期可暂时不做

- 全量 UIA 事件订阅编排
- 复杂 subtree diff
- 长生命周期跨窗口缓存

如果实现复杂度过高，一期可先采用“动作后按需局部重读”的简化策略。

当前进度：该简化策略已经开始落地。当前查询层已维护 `active_window_snapshot`、`focus_context_snapshot` 和最近 locate 结果提示；`windows_refresh_focus` 通过局部重读生成焦点上下文，`windows_active` 则会尽量复用已有活动窗口或焦点快照，避免每次都重新走全量查询路径。

当前进度补充：查询侧诊断字段也已开始统一收口。`windows_list / windows_snapshot / windows_locate / windows_active / windows_refresh_focus` 现在会尽量复用同一组基础字段语义，例如 `query_kind`、`window_ref`、`snapshot_version`、`backend_used`、`snapshot_cache` 与最近 locate 提示，减少各工具各自拼装诊断对象的漂移。

当前进度补充：诊断标准化也开始跨到 query / act 共用层。当前工具结果会显式强调产品的文本操作定位，统一补充 `tool_name`、`interaction_model=text_structured`、`primary_interface=accessibility_tree` 等字段；这意味着一期主路径仍然是文本快照、`ref`、selector 和结构化动作，而不是截图理解、OCR 或坐标驱动点击。

当前进度补充：这组统一字段已开始覆盖部分查询失败路径，而不仅是成功结果。例如 `windows_locate` 的 selector 语法错误、`windows_refresh_focus` 的无活动窗口 / 无焦点错误、`windows_describe_ref` 的空 ref 或非法 ref 都会回到同一文本结构诊断合同；同时 `ref` 解析规则已收紧为真实会话引用格式，减少把任意字符串误判为可描述目标的漂移。

当前进度补充：phase 5 现在也开始验证恢复与缓存边界在真实 fixture 上的表现。一方面，`click` 打到不支持 `InvokePattern` 的输入框、且策略显式禁用 fallback 时，会稳定返回 `FallbackBlocked` 与 attempt 级诊断，而不是静默退化到坐标输入；另一方面，`windows_locate -> windows_act(action="set_value") -> windows_refresh_focus` 的连续链路已经在真实 WinForms 场景中验证了最近 locate 提示与焦点上下文刷新结果仍然可解释，继续坚持文本结构化主路径，而不是依赖截图、OCR 或坐标补偿。

## 12. 交付阶段建议

建议按四个阶段推进，而不是一次做完所有抽象。

### 阶段 1：可见性闭环

目标：能看见窗口和元素。

交付：

- `windows_list`
- `windows_active`
- `windows_snapshot`
- 元素 `ref` 生成

验收：

- 能稳定输出活动窗口摘要
- 能在记事本、设置、计算器等应用中生成可用快照

当前进度：已完成。

已实现内容：

- `Allyflow.sln` 和基础项目结构已建立
- `UiaWindowRegistry` 已可列出顶层窗口并识别活动窗口
- `UiaSnapshotBuilder` 已可生成活动窗口结构化快照
- `InMemoryRefRegistry` 已可分配窗口和元素 `ref`
- `QueryToolService` 已打通 `windows_list / windows_active / windows_snapshot` 的最小调用路径
- 最小查询调用链已可直接通过 `QueryToolService` 驱动，并发现当前桌面窗口

### 阶段 2：定位闭环

目标：能稳定从文本约束找到目标元素。

交付：

- selector parser
- `windows_locate`
- 严格/结构/文本三类定位
- 基础歧义报告

验收：

- `L-001` 到 `L-008` 主要用例可通过

### 阶段 3：执行闭环

目标：能对典型控件完成动作。

交付：

- `windows_act`
- `invoke / focus / set_value / toggle / expand / collapse`
- 标准 `ActionResult`

验收：

- `A-001` 到 `A-006` 主要用例可通过

### 阶段 4：恢复闭环

目标：失败后能返回可恢复结果，而不是直接崩掉。

交付：

- 有限 fallback
- `ref stale -> relocate`
- `expected_outcome` 基础校验
- 标准错误码与诊断

验收：

- `F-001` 到 `F-004`
- `P-001` 到 `P-005`
- 至少两个集成场景通过

当前进度补充：phase 5 已形成一套真实 WinForms fixture + 串行 UIA 集成测试基线，并且始终坚持文本结构主路径，而不是转向截图、OCR 或坐标驱动。当前覆盖可压缩概括为：

- 查询主链路：`windows_list -> windows_snapshot -> windows_locate -> windows_describe_ref` 已在真实 UIA 场景中打通；`windows_refresh_focus` 也已补上 `locate -> set_value -> refresh_focus` 的连续性检查，用于验证最近 locate 提示、焦点上下文和轻量缓存合同仍然可解释。
- 真实动作主路径：`set_value`、`invoke`、`toggle` 都已有真实 fixture 覆盖，分别验证 `ValuePattern`、`InvokePattern`、`TogglePattern` 以及标准 `tool_name / interaction_model / primary_interface` 诊断字段。为降低测试环境焦点竞争，真实动作场景按 collection 串行执行，并显式关闭 `AutoActivateWindow`。
- 最小场景链路：`I-001`、`I-002`、`I-003` 都已有真实缩小版验证，分别覆盖设置填写并保存、登录表单输入与提交、以及先暴露歧义再用更精确 selector 消解的路径。
- 协议与失败边界：真实 `dry_run`、缺失 selector 的 `TargetNotFound`、真实 `VerificationFailed`、以及禁用 fallback 时的 `FallbackBlocked` 都已纳入集成验证，强调一期合同必须返回结构化计划、结构化失败解释和可观测诊断，而不是退化成不透明异常或坐标式补偿。
- 集成验证还反向推动了两个真实稳健性修复：`UiaSnapshotBuilder` 现在容忍部分控件不支持 `Name / AutomationId / ClassName` 读取；`windows_describe_ref` 现在优先复用最近同窗快照，降低 `snapshot -> locate -> describe` 连续调用中的 `ref` 重编号问题。

## 13. 一期基准任务

建议把以下任务作为 MVP 基准任务，而不是只看单点接口。

当前阶段补充：这些任务接下来应优先在真实软件上执行，而不再只停留在 fixture 或协议层验证。

### 13.1 真实文本输入任务

- 在真实可编辑窗口中定位输入区
- 聚焦或直接写入
- 输入文本
- 校验文本变化

### 13.2 设置页填写并保存

- 找到代理地址输入框
- 写入地址
- 点击保存
- 校验状态变化或窗口反馈

### 13.3 复选框切换

- 找到指定 checkbox
- 切换到目标状态
- 校验属性变化

### 13.4 展开折叠菜单或树节点

- 定位支持 `expand/collapse` 的节点
- 执行动作
- 校验展开状态

### 13.5 结构失败后的有限回退

- 让主 selector 故意失效
- 使用回退 selector 或输入模拟恢复
- 返回明确执行路径

### 13.6 真实软件可用性判断

- 记录哪些真实软件可以仅靠 `accessibility_tree` 主路径完成任务
- 记录失败是产品主路径问题还是软件本身可访问性问题
- 只有在高频真实阻塞无法通过产品打磨解决时，才为未来视觉 fallback 产品线立项

## 14. 风险清单

一期最可能遇到的风险如下。

### 14.1 现代应用与旧应用混杂

表现：不同应用的 `UIA` 质量差异很大。

应对：

- 一期先以 `UIA3` 为中心
- 测试样本覆盖 `Win32 / WPF / WinForms / UWP`

### 14.2 元素名称本地化

表现：`name="保存"` 在多语言环境下不稳定。

应对：

- 优先 `automation_id`
- 文本 selector 仅作为次级约束
- 测试矩阵覆盖中英文环境

### 14.3 自绘或 Electron 类应用可访问性差

表现：树里节点不足，pattern 缺失。

应对：

- 一期只声明部分支持
- 先不承诺复杂视觉兜底

### 14.4 输入模拟误触发

表现：窗口切换后点错位置。

应对：

- fallback 前校验前台窗口
- 默认限制高风险操作
- 结果中明确记录 `execution_path`

## 15. 一期验收标准

满足以下条件，可认为一期 `MVP` 成立：

- 能稳定输出活动窗口快照
- 能基于 `ref` 对典型控件进行交互
- `StrictSelector + StructuralSelector + TextSelector` 能支撑主要用例
- `invoke / set_value / toggle / focus` 闭环可用
- 失败时返回标准错误和诊断，而不是无解释失败
- 输入模拟只在受控条件下触发
- `tests/test-matrix-phase-1.md` 中核心用例大部分可通过

当前阶段再增加一条产品验收标准：

- 使用 Allyflow 主路径对若干真实桌面软件执行任务时，能够明确区分“产品可用”、“产品待打磨”和“需要未来视觉产品线”的边界

## 16. 与当前文档集的关系

当前参考文档已经收敛为“实现计划 + 任务拆解 + 真实验证”三类：

- `implementation-task-breakdown-phase-1.md` 负责回答“按什么顺序做、当前做到哪里”
- `validation/real-app-acceptance.md` 负责约束真实软件产品迭代方式
- `validation/real-app-backlog.md` 负责记录下一批真实任务
- `validation/real-app-validation-log.md` 负责沉淀已经验证过的结论与边界
- 本文档负责回答“一期先做什么、怎么做、做到什么算过关”

## 17. 核心结论

`allyflow` 一期最重要的不是做成“大而全的桌面自动化框架”，而是先做成一个对 agent 真正可用、能操作真实软件的最小执行内核：

- 能看见
- 能定位
- 能操作
- 能校验
- 能解释失败

当前阶段要做的不是优先开放接口，也不是提前进入 OCR/截图/视觉 fallback，而是先验证这条 `accessibility_tree` 主路径在真实软件上的产品价值。

只有当这条主路径在真实软件上被充分验证为不足时，后续再扩 `MSAA`、anchor selector、视觉兜底和任务规划才有工程价值。
