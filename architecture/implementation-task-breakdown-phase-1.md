# OpenClaw 一期实现任务拆解

## 1. 目标

把现有几份协议和方案文档，拆成可直接开工的一期实现任务清单。

本文档的作用不是重复架构原则，而是回答三个执行问题：

- 先做什么，后做什么
- 每个模块最小要交付什么
- 如何判断某个阶段已经可以进入下一阶段

本文档对齐以下当前参考文档：

- `architecture/mvp-implementation-plan.md`
- `validation/real-app-acceptance.md`
- `validation/real-app-backlog.md`
- `tests/test-matrix-phase-1.md`

## 2. 拆解原则

- 优先打通闭环，不先做“大而全”抽象
- 先查询后执行，先结构化后 fallback
- 先做活动窗口范围，再考虑扩桌面范围
- 每项任务尽量有明确产出物和验收条件
- 每个阶段都要能映射到测试矩阵，而不是只映射到代码目录

## 3. 建议仓库结构

如果开始进入实现，建议一期先落成以下最小目录结构：

```text
src/
  OpenClaw.Core/
  OpenClaw.Infrastructure.Windows/
tests/
  OpenClaw.Tests.Unit/
  OpenClaw.Tests.Integration/
```

建议职责分配：

- `OpenClaw.Core`: 核心模型、接口、错误码、执行策略，以及工具请求/结果与协议适配逻辑
- `OpenClaw.Infrastructure.Windows`: `FlaUI/UIA3` 适配、窗口发现、快照、动作执行
- `OpenClaw.Tests.*`: 单元、集成，以及集成项目内聚合的测试夹具

## 4. 交付阶段总览

建议按 6 个工作阶段推进：

1. 仓库初始化
2. 可见性闭环
3. 定位闭环
4. 执行闭环
5. 恢复与诊断闭环
6. 集成验证与稳定化

这些阶段和 `mvp-implementation-plan.md` 的四阶段是兼容的。

这里额外拆出“仓库初始化”和“集成验证”，是为了让实施顺序更清晰。

## 4.1 当前进度

截至 2026-04-28：

- 阶段 0 已完成
- 阶段 1 已完成
- 阶段 2 已完成
- 阶段 3 已完成
- 阶段 4 已达到 MVP 可用
- 阶段 5 已完成核心集成验证，当前进入“真实软件产品可用性验证”阶段

当前产品旗帜：

- `OpenClaw` 当前主产品明确为 `accessibility_tree` 优先的桌面自动化能力
- 当前主线目标不是开放外部接口，也不是启动视觉 fallback，而是直接用产品操作真实电脑软件
- 后续打磨应优先围绕真实任务中的可用性、恢复能力、诊断可解释性和操作成功率展开
- `OCR`、截图、视觉 fallback 仅保留为未来可能拆分的第二产品线，不作为当前阶段阻塞项

### 4.2 当前边界复盘与下一步方向

基于当前实现与真实软件验证，底层能力边界已更清晰：

- 应继续保留在底层的：结构化感知、`ref` 机制、selector 定位、动作执行、最小验证、恢复语义
- 应避免继续向底层下沉的：任务规划、app-specific 工作流、复杂用户引导、面向终端用户的交互体验

当前最像“上层逻辑泄漏”的点主要有：

1. `ExecutionPolicy` 中偏策略化的能力开始变多
2. `dry_run` 正在从执行预览走向轻量规划描述
3. 歧义恢复提示文案已经开始接近 operator workflow，而不仅是底层诊断
4. 某些真实 app 验证很容易诱导出 app-specific 流程特化

下一步实现方向应遵循以下约束：

1. 优先增强底层稳定性，而不是继续扩动作或扩协议
2. 优先做跨 app 可复用的恢复语义和诊断改进
3. 复杂 app 只作为 robustness 压力测试对象，不反向决定接口形状
4. 遇到 accessibility boundary 时应更早、更明确地失败，而不是隐式增加猜测行为

按此边界，下一阶段最值得推进的实现主题是：

- selector portability 的小幅通用增强
- ambiguity / stale-ref / delayed-surface recovery 语义继续收紧
- verification 结果可信度继续提高
- `ExecutionPolicy` 与 action diagnostics 去除偏上层的规划味道，收敛为底层执行约束
- `AutoActivateWindow` 只保留在前景敏感执行路径，避免演变成隐式全局窗口调度策略

阶段 0 已落地内容：

- 安装 `.NET SDK 8.0.420`
- 创建 `OpenClaw.sln`
- 创建 `src/OpenClaw.Core`
- 创建 `src/OpenClaw.Infrastructure.Windows`
- 创建 `tests/OpenClaw.Tests.Unit`
- 创建 `tests/OpenClaw.Tests.Integration`
- 补充项目引用与基础依赖
- 为 Windows 自动化相关项目切换到 `net8.0-windows`
- 新增 `Directory.Build.props` 与 `global.json`
- 新增最小 CI：`.github/workflows/ci.yml`
- 通过 `dotnet restore`, `dotnet build`, `dotnet test`

阶段 1 已落地内容：

- 定义核心模型：`WindowRef`, `ElementRef`, `WindowSummary`, `ElementNode`, `SnapshotResult`, `ToolError`
- 定义核心接口：`IWindowRegistry`, `IRefRegistry`, `ISnapshotBuilder`, `ISnapshotFormatter`
- 实现 `InMemoryRefRegistry`
- 实现 `UiaWindowRegistry`
- 实现 `UiaSnapshotBuilder`
- 实现 `SnapshotTextFormatter`
- 实现查询请求/结果对象与 `QueryToolService`
- 打通 `windows_list`, `windows_active`, `windows_snapshot` 的最小调用路径
- `QueryToolService` 最小查询调用链可成功运行，并发现当前桌面窗口

阶段 2 已落地内容：

- 定义 selector AST 与解析对象：`ParsedSelector`, `SelectorScope`, `SelectorSegment`, `SelectorPredicate`, `TextSelector`
- 实现 `SelectorParser`，支持一期子集：`scope:active_window`, `scope:window(name="...")`, `role[...]`, `>`, `>>`, `=`, `~=`, `text(...)`, `||`
- 实现 `SnapshotLocator`，基于快照树完成 strict / structural / descendant / text 匹配
- 实现候选排序与歧义返回：`LocateStatus`, `ElementCandidate`, `LocateResult`
- 实现 fallback chain 顺序尝试与 `attempts` 诊断记录
- 在 `QueryToolService` 中暴露 `windows_locate`
- 在 `QueryToolService` 中暴露 `windows_describe_ref`
- 新增单元测试覆盖 selector parsing、定位命中、歧义、fallback chain、describe ref
- 通过 `dotnet build OpenClaw.sln --configuration Release` 与 `dotnet test OpenClaw.sln --configuration Release`

阶段 3 已落地内容：

- 定义动作模型：`ActionRequest`, `ActionTarget`, `ExecutionPolicy`, `ExpectedOutcome`, `ActionResult`, `ActionStatus`, `TargetResolution`, `ResolvedTarget`, `ActionExecutionResult`, `VerificationResult`, `ActionTiming`
- 定义执行接口：`ITargetResolver`, `IActionExecutor`
- 实现 `TargetResolver`，支持 `ref first, selector fallback` 的最小目标解析链路
- 扩展 `IRefRegistry` 与 `InMemoryRefRegistry`，为动作执行保留 `RefEntry` 存取能力
- 实现 `UiaActionExecutor`，支持 `invoke / focus / set_value / toggle / expand / collapse / click / press_keys`
- 在核心项目中新增 `ActRequest` 与 `ActionToolService`
- 对外打通 `windows_act` 的最小协议闭环，返回标准 `ActionResult`
- 打通动作服务初始化与最小执行调用链
- 新增单元测试覆盖 `dry_run`、目标解析失败映射、成功执行结果、校验失败结果
- 通过 `dotnet build OpenClaw.sln --configuration Release`
- 通过 `dotnet test tests/OpenClaw.Tests.Unit/OpenClaw.Tests.Unit.csproj --configuration Release`
- 通过 `dotnet test OpenClaw.sln --configuration Release`
- 说明：`dry_run` 与有限 `input_simulation` fallback 已为阶段 3 最小执行闭环提前接入，但阶段 4 仍需补齐策略控制、重试与标准化诊断边界

## 5. 阶段 0：仓库初始化

### T0-001 建立 .NET 解决方案骨架

- 目标：创建最小可编译的 solution 和项目结构
- 产出物：`.sln`、核心实现项目、测试项目
- 依赖：无
- 验收：空项目可 restore 和 build
- 状态：已完成

### T0-002 引入基础依赖

- 目标：引入 `FlaUI.UIA3`、日志、JSON 序列化、测试框架
- 产出物：项目引用与版本锁定
- 依赖：`T0-001`
- 验收：依赖恢复成功，无冲突
- 状态：已完成
- 说明：`FlaUI.UIA3` 实际锁定为 `5.0.0`

### T0-003 定义基础项目规范

- 目标：统一命名空间、异常策略、日志键名、时间与 ID 生成规则
- 产出物：`docs` 或 `src` 内的开发约定文件、基础 helper
- 依赖：`T0-001`
- 验收：核心模型项目具备统一风格约束
- 状态：部分完成
- 说明：已通过 `Directory.Build.props` 统一基础编译设置；更细的命名与日志规范待阶段 1 补齐

### T0-004 建立 CI 最小检查

- 目标：至少保证 build、unit tests、格式检查可执行
- 产出物：CI 配置文件
- 依赖：`T0-001`, `T0-002`
- 验收：提交后能自动执行基础检查
- 状态：已完成
- 说明：当前 CI 覆盖 `restore / build / test`

## 6. 阶段 1：可见性闭环

目标：能看到窗口，能生成活动窗口快照，能建立 `ref`。

### T1-001 定义核心领域模型

- 目标：建立后续模块共享的核心对象
- 产出物：
  - `WindowRef`
  - `ElementRef`
  - `WindowSummary`
  - `ElementNode`
  - `SnapshotResult`
  - `ToolError`
- 依赖：`T0-001`
- 验收：核心模型可独立编译，字段覆盖 MVP 文档中的一期最小集合
- 状态：已完成

### T1-002 实现 Window Registry 接口与 UIA3 适配

- 目标：列出顶层窗口、识别活动窗口、建立窗口引用映射
- 产出物：
  - `IWindowRegistry`
  - `UiaWindowRegistry`
  - `window_ref <-> native_handle` 映射表
- 依赖：`T0-002`, `T1-001`
- 验收：能稳定返回窗口列表和活动窗口摘要
- 关联测试：`C-001`
- 状态：已完成

### T1-003 实现 snapshot 基础节点抽取

- 目标：从活动窗口抽取最小可用元素树
- 产出物：
  - `ISnapshotBuilder`
  - `UiaSnapshotBuilder`
  - 节点字段抽取逻辑：`role/name/automation_id/class_name/bounds/states/actions/children`
- 依赖：`T1-002`
- 验收：能在至少一个 Win32 和一个 WPF 应用中生成结构树
- 关联测试：`C-001`
- 状态：已完成
- 说明：当前为最小树提取实现，后续阶段会继续收紧节点筛选和诊断细节

### T1-004 实现 ref 分配与 ref 索引

- 目标：在快照构建时给窗口和节点分配会话内稳定 `ref`
- 产出物：
  - `IRefRegistry`
  - `RefEntry`
  - `snapshot_version`
  - `ref_index`
- 依赖：`T1-003`
- 验收：同一快照版本内同节点 `ref` 稳定
- 状态：已完成

### T1-005 实现 snapshot 文本摘要生成

- 目标：生成 agent 可消费的 `summary_text`
- 产出物：
  - `SnapshotTextFormatter`
  - 活动窗口摘要模板
  - 可交互元素列表摘要模板
- 依赖：`T1-003`, `T1-004`
- 验收：输出符合 `mvp-implementation-plan.md` 中的低噪声原则
- 状态：已完成

### T1-006 暴露查询工具第一批

- 目标：实现 `windows_list`、`windows_active`、`windows_snapshot`
- 产出物：
  - tool handler
  - 输入输出 DTO
  - 错误转换逻辑
- 依赖：`T1-002`, `T1-003`, `T1-004`, `T1-005`
- 验收：工具可被 MCP 层稳定调用
- 关联测试：`C-001`, `P-002`
- 状态：已完成
- 说明：当前已通过 `QueryToolService` 打通最小调用链

## 7. 阶段 2：定位闭环

目标：能解析一期 selector 子集，并稳定返回候选、歧义和诊断。

### T2-001 定义 selector AST 和 parser 边界

- 目标：把 DSL 文本转换为受控内部表达，而不是运行时字符串硬解析
- 产出物：
  - selector tokenizer
  - AST 节点类型
  - parser 错误对象
- 依赖：`T1-001`
- 验收：支持一期子集解析
- 状态：已完成

### T2-002 实现一期 selector 子集解析

- 目标：支持 MVP 明确纳入的一期 selector 能力
- 产出物：支持以下语法：
  - `scope:active_window`
  - `scope:window(name="...")`
  - `role[...]`
  - `>`
  - `>>`
  - `=`
  - `~=`
  - `text(...)`
  - `||`
- 依赖：`T2-001`
- 验收：解析结果可序列化输出，错误位置可报告
- 关联测试：`L-001`, `L-003`, `L-004`, `L-005`, `L-007`, `L-008`
- 状态：已完成
- 说明：当前 `text(...)` 已支持 `text("...")` 与 `text(contains="...")` 两种一期文本形式

### T2-003 实现快照内匹配引擎

- 目标：在快照树而不是实时 UIA 遍历上完成大部分定位逻辑
- 产出物：
  - 严格匹配器
  - 结构路径匹配器
  - 文本匹配器
  - 候选收集器
- 依赖：`T1-003`, `T2-002`
- 验收：能返回候选、置信度、使用策略
- 关联测试：`L-001` 到 `L-005`
- 状态：已完成
- 说明：当前实现默认在作用域子树中搜索首段，再按 `>` / `>>` 收紧路径，符合 agent 文本定位使用方式

### T2-004 实现 ambiguity 与候选排序

- 目标：多命中时不误选，而是显式返回歧义结果
- 产出物：
  - `LocateResult.status`
  - `candidates`
  - `best_match`
  - 排序解释字段
- 依赖：`T2-003`
- 验收：多命中场景返回 `ambiguous`
- 关联测试：`L-002`, `P-003`, `I-003`
- 状态：已完成

### T2-005 实现 fallback chain 解析与执行

- 目标：支持 `selector1 || selector2` 顺序尝试
- 产出物：
  - selector chain 执行器
  - 每段尝试结果记录
- 依赖：`T2-002`, `T2-003`
- 验收：诊断中能看见首段失败与次段成功
- 关联测试：`L-007`, `F-001`
- 状态：已完成

### T2-006 实现 windows_locate 工具

- 目标：对外暴露定位工具
- 产出物：
  - `windows_locate` handler
  - `LocateResult` DTO
  - `explain` 与 `diagnostics`
- 依赖：`T2-003`, `T2-004`, `T2-005`
- 验收：`windows_locate` 返回结构稳定
- 关联测试：`L-001` 到 `L-008`, `P-003`
- 状态：已完成
- 说明：当前通过 `QueryToolService.WindowsLocate` 暴露，并统一映射 `TargetNotFound / TargetAmbiguous / InvalidArgument`

### T2-007 实现 windows_describe_ref 工具

- 目标：支持 agent 对既有 `ref` 进行二次确认
- 产出物：`windows_describe_ref` handler
- 依赖：`T1-004`
- 验收：能返回元素状态、动作能力、父子关系
- 状态：已完成

## 8. 阶段 3：执行闭环

目标：能对典型控件完成结构化动作，并返回标准 `ActionResult`。

### T3-001 定义 ActionRequest / ActionResult 一期内部模型

- 目标：把协议文档中的字段落成核心对象
- 产出物：
  - `ActionRequest`
  - `ExecutionPolicy`
  - `ExpectedOutcome`
  - `ActionResult`
  - `TargetResolution`
  - `VerificationResult`
- 依赖：`T1-001`
- 验收：字段覆盖一期协议子集
- 关联测试：`P-001`, `P-004`, `P-005`
- 状态：已完成

### T3-002 实现目标解析器

- 目标：统一处理 `target.ref`、`target.selector`、stale ref 重定位
- 产出物：
  - `ITargetResolver`
  - `TargetResolver`
  - `ref first, selector fallback` 逻辑
- 依赖：`T1-004`, `T2-006`, `T3-001`
- 验收：可正确输出 `target_resolution`
- 状态：已完成
- 说明：当前优先使用 `target.ref`，当 `ref` 不可用且请求带 `selector` 时回退到 selector 定位

### T3-003 实现 UIA pattern 动作执行器

- 目标：优先走结构化 pattern 路径
- 产出物：
  - `invoke` 执行器
  - `focus` 执行器
  - `set_value` 执行器
  - `toggle` 执行器
  - `expand/collapse` 执行器
- 依赖：`T3-002`
- 验收：典型控件动作成功
- 关联测试：`A-001` 到 `A-005`
- 状态：已完成

### T3-004 实现 click 和 press_keys 路径

- 目标：补齐语义 click 与通用快捷键能力
- 产出物：
  - `click` 执行逻辑
  - `press_keys` 执行逻辑
  - 窗口激活前置检查
- 依赖：`T3-002`
- 验收：能完成常见按钮点击与快捷键输入
- 状态：已完成
- 说明：`click` 当前优先走语义 invoke，缺失时在策略允许下退化到中心点输入模拟；`press_keys` 当前支持常见组合键与字母数字键

### T3-005 实现 expected outcome 校验器

- 目标：执行后检查是否达到期望结果
- 产出物：支持：
  - `no_verification`
  - `focus_change`
  - `text_updated`
  - `property_change`
  - `window_opened`
  - `window_closed`
- 依赖：`T3-003`, `T3-004`
- 验收：能区分执行成功但校验失败
- 关联测试：`A-002`, `P-005`, `I-001`, `I-002`
- 状态：已完成
- 说明：当前为最小校验器，已能表达 `no_verification` 与执行后通过/未通过的标准 `VerificationResult`

### T3-006 实现 windows_act 工具

- 目标：对外暴露统一动作工具
- 产出物：
  - `windows_act` handler
  - 协议模型与内部模型转换
  - 统一 timing 和 error 输出
- 依赖：`T3-001` 到 `T3-005`
- 验收：工具层返回字段稳定
- 关联测试：`A-001` 到 `A-006`, `P-001`, `P-004`, `P-005`
- 状态：已完成

### T3-007 实现 windows_press_keys 工具

- 目标：暴露独立快捷键工具
- 产出物：`windows_press_keys` handler
- 依赖：`T3-004`
- 验收：对指定窗口执行快捷键时行为可预测
- 状态：部分完成
- 说明：能力已通过 `windows_act(action="press_keys")` 接入，独立工具入口留待阶段 4/5 再决定是否单独暴露

## 9. 阶段 4：恢复与诊断闭环

目标：失败时能解释、能重试、能有限 fallback，而不是黑盒失败。

### T4-001 实现标准错误码与错误映射

- 目标：统一内部异常与外部错误码
- 产出物：
  - 错误码枚举
  - `ToolError` builder
  - 各工具错误映射表
- 依赖：`T1-001`, `T3-001`
- 验收：不同错误场景返回一致错误结构
- 关联测试：`P-002`
- 状态：部分完成
- 说明：当前 `windows_act` 已进一步统一解析失败、执行失败、verification failed、retry budget exhausted 的标准错误结构；跨全部工具的通用 builder 仍待后续抽取

### T4-002 实现 dry_run

- 目标：让动作请求可只做解析和执行规划
- 产出物：
  - `dry_run` 分支
  - 执行计划摘要
- 依赖：`T3-006`
- 验收：不触发真实 UI 操作
- 关联测试：`P-004`
- 状态：已完成
- 说明：当前 `dry_run` 已返回结构化执行计划摘要，包括 `planned_action`、`planned_execution_path` 和策略相关诊断字段

### T4-003 实现 input_simulation fallback

- 目标：当结构化 pattern 缺失且策略允许时，有限退化到输入模拟
- 产出物：
  - 中心点点击逻辑
  - 可见性与边界校验
  - fallback 记录
- 依赖：`T3-004`, `T4-001`
- 验收：能返回 `uia_pattern` 或 `input_simulation` 的真实执行路径
- 关联测试：`A-006`, `F-003`, `F-004`
- 状态：部分完成
- 说明：阶段 3 已落地最小中心点点击 fallback；当前阶段补充了 fallback blocked / exhausted 的结果表达与尝试诊断，但更细的 fallback 记录仍可继续增强

### T4-004 实现 fallback 策略控制

- 目标：支持 `allow_fallback`、`max_retries`、`require_visibility`、`auto_activate_window`
- 产出物：执行策略解释器
- 依赖：`T3-006`, `T4-003`
- 验收：策略能约束动作执行路径
- 关联测试：`F-003`, `F-004`
- 状态：部分完成
- 说明：`allow_fallback`、`require_visibility`、`auto_activate_window` 已在执行器生效，`max_retries` 已在 `windows_act` 结果层接入并返回 attempt 诊断；后续可继续下沉到更细粒度执行计划层

### T4-005 实现 stale ref 恢复

- 目标：在 `ref` 失效时通过 selector 重定位
- 产出物：
  - stale 检测
  - `selector_hint` 或请求 selector 重定位逻辑
  - 新 `ref` 记录
- 依赖：`T3-002`, `T4-001`
- 验收：恢复成功时结果中能看到新的 `target_resolution`
- 状态：部分完成
- 说明：当前已支持 stale `ref` 时优先使用请求 selector 重定位，缺失时回退 `selector_hint`；恢复成功后 `target_resolution` 已显式标记 `selector_recovery`，并带上 `selector_source`、`requested_ref`、`resolved_ref`、`ref_changed`、`recovered_from_stale_ref` 等诊断字段。新 `ref` 持久化策略仍可继续增强

### T4-006 实现轻量缓存与局部刷新

- 目标：建立一期缓存，不做复杂事件总线
- 产出物：
  - `active_window_snapshot`
  - `focus_context_snapshot`
  - `recent_locator_results`
  - 局部刷新触发器
- 依赖：`T1-003`, `T1-004`, `T3-003`
- 验收：焦点变化与局部结构变化时不必总是全量重建
- 关联测试：`C-002`, `C-003`, `C-004`
- 状态：部分完成
- 说明：当前 `QueryToolService` 已接入最小查询侧缓存：`active_window_snapshot`、`focus_context_snapshot`、`recent_locator_results`（以最近 selector / ref 诊断形式暴露）。`windows_active` 会优先复用活动窗口缓存，并可复用最近一次焦点刷新生成的快照；复杂事件驱动失效与真正 subtree 级局部重建仍待后续阶段补强

### T4-007 实现 windows_refresh_focus 工具

- 目标：暴露焦点上下文刷新工具
- 产出物：`windows_refresh_focus` handler
- 依赖：`T4-006`
- 验收：可返回当前焦点及其邻近上下文
- 关联测试：`C-002`
- 状态：部分完成
- 说明：`windows_refresh_focus` 最小 handler 已落地，可返回 `focus_ref`、`parent_chain`、`sibling_refs`、`child_refs`、摘要文本与标准诊断字段；当前实现通过局部焦点快照重读完成，不依赖复杂事件订阅

### T4-008 实现诊断字段标准化

- 目标：统一 `backend_used`、`snapshot_version`、`execution_path_attempts` 等字段语义
- 产出物：诊断对象 builder 和填充规范
- 依赖：`T2-006`, `T3-006`, `T4-001`
- 验收：定位与动作结果中的诊断字段含义一致
- 关联测试：`P-001`, `P-003`, `P-005`
- 状态：部分完成
- 说明：当前动作结果已统一补充 `target_source`、`selector_used`、`selector_source`、`recovered_from_stale_ref`、`resolved_ref`、`attempt_count`、`retry_count`、`attempt_n_*` 等字段；查询侧也已新增 `QueryDiagnostics` 收口 `windows_list / windows_snapshot / windows_locate / windows_active / windows_refresh_focus` 的公共字段，统一输出 `query_kind`、`window_ref`、`snapshot_version`、`backend_used`、`snapshot_cache`、`recent_locator_*` 等语义。本轮又新增 `ActionDiagnostics`、`windows_describe_ref` 成功态规范化，以及若干查询失败路径的统一错误诊断 builder，使 query / act 共享 `tool_name`、`interaction_model=text_structured`、`primary_interface=accessibility_tree` 等文本结构主路径字段；`windows_act` 顶层失败现在也会把同一合同字段同步到返回的 `ToolError.Diagnostics`，减少调用方只看 error object 时的信息损失；同时 `windows_describe_ref` 的 `ref` 解析已收紧为真实会话格式（如 `w1e3`），继续把截图/OCR/坐标点击排除在一期主合同之外。跨 query / act 的最终全局规范仍待继续整理

## 10. 阶段 5：集成验证与稳定化

目标：把前面所有模块串起来，验证真实任务链路。

### T5-001 构建集成测试夹具应用

- 目标：准备最小被测应用集合
- 产出物：
  - Win32/WinForms 示例对话框
  - WPF 表单示例
  - 结构不稳定或弱可访问性样本
- 依赖：`T0-001`
- 验收：可本地启动并用于自动化测试
- 状态：部分完成
- 说明：当前已新增最小 WinForms fixture host，可启动真实窗口并承载 `代理地址` 标签、输入框、`保存` / `取消` 按钮，作为 phase 5 首批 UIA 集成测试样本

### T5-002 建立 Locator 集成测试集

- 目标：让 `L-001` 到 `L-008` 可自动跑
- 产出物：定位集成测试用例
- 依赖：`T2-006`, `T5-001`
- 验收：定位闭环主要用例稳定通过
- 状态：部分完成
- 说明：当前已落地首个真实 locator/query 链路集成测试，覆盖 `windows_list -> windows_snapshot -> windows_locate -> windows_describe_ref` 的最小闭环；更完整的 `L-001` 到 `L-008` 覆盖仍待继续扩展

### T5-003 建立 Action 集成测试集

- 目标：让 `A-001` 到 `A-006` 可自动跑
- 产出物：动作集成测试用例
- 依赖：`T3-006`, `T4-003`, `T5-001`
- 验收：核心动作闭环稳定通过
- 状态：部分完成
- 说明：当前已新增五条真实动作集成测试，基于 WinForms fixture 打通 `windows_locate -> windows_act(action="set_value") -> fixture readback`、`windows_locate -> windows_act(action="invoke") -> status label update`、`windows_locate -> windows_act(action="toggle") -> checkbox readback`、`windows_locate -> windows_act(action="focus") -> windows_refresh_focus` 与 `windows_locate -> windows_act(action="expand"/"collapse") -> combo dropdown state readback` 五条最小闭环，分别验证真实 `ValuePattern`、`InvokePattern`、`TogglePattern`、`Focus` 与 `ExpandCollapsePattern` 路径、标准 `tool_name / interaction_model / primary_interface` 诊断，以及结构化动作后的可观察 UI 结果。其中 `toggle` 场景通过 `ExpectedOutcome("property_change", { property = "toggle_state", value = "true" })` 验证属性变化，`focus` 场景新增 `focus_changed=true` 观察值并验证与 `windows_refresh_focus` 的连续性，`expand/collapse` 场景则通过 `expand_state` 与 fixture `ComboBox.DroppedDown` 状态做双重验证。为降低桌面环境抖动，真实 UIA 集成测试当前按 collection 串行执行；其中大多数动作场景关闭 `AutoActivateWindow` 以避免前台窗口抢占带来的 flaky，但 `focus` 场景显式保留自动激活，因为该路径本身依赖真实前台焦点语义，而 `expand/collapse` 路径则验证其不依赖强制前台激活。当前共享 fixture 还额外承载了最小登录式字段（`UsernameInput` / `PasswordInput` / `LoginButton`）与最小下拉选择控件（`AdvancedOptionsCombo`），但这仍只是为了验证 text-structured 场景链路，不代表产品方向扩展到图像式表单自动化

### T5-004 建立 Cache / Event 集成测试集

- 目标：验证局部刷新和缓存策略
- 产出物：`C-001` 到 `C-004` 测试用例
- 依赖：`T4-006`, `T4-007`, `T5-001`
- 验收：局部刷新行为可观测且稳定
- 状态：部分完成
- 说明：当前已补上首条真实 query/action 连续链路检查：在共享 WinForms fixture 上先执行 `windows_locate` 与 `windows_act(action="set_value")`，随后调用 `windows_refresh_focus`，验证 `recent_locator_selector`、`recent_locator_ref`、`focus_context_snapshot` 与聚焦摘要文本能够在连续操作后保持可解释的一致性。该测试不要求桌面环境中焦点必须稳定停留在输入框本体，而是验证最小缓存/刷新合同和上下文延续能力。更深的 subtree 失效与属性增量更新场景仍待继续扩展

### T5-005 建立 Protocol / Diagnostics 集成测试集

- 目标：验证工具返回字段稳定性
- 产出物：`P-001` 到 `P-005` 测试用例
- 依赖：`T4-001`, `T4-002`, `T4-008`
- 验收：结构字段与错误语义稳定
- 状态：部分完成
- 说明：当前集成测试已验证真实 WinForms 窗口上的 query、`set_value`、`invoke`、`toggle`、`focus` 与 `expand/collapse` 结果继续携带 `tool_name`、`interaction_model=text_structured`、`primary_interface=accessibility_tree` 等主合同字段；登录式 `I-002` 场景也复用了同样的标准字段约束。本轮又新增了多条更偏协议层的真实覆盖：`dry_run` 在真实目标上只返回结构化执行计划、不产生 UI 副作用；缺失 selector 稳定返回 `TargetNotFound`，并且其顶层 `ToolError.Diagnostics` 现在会以 `windows_act` 视角复用同一标准合同，同时保留 `selector_used`、`target_source` 等定位语义；真实 `VerificationFailed` 路径验证动作本身成功但后验校验失败时，结果会稳定返回 `partial_success` 与 `expected_value / observed_value`；真实 `FallbackBlocked` 路径验证当 `click` 打到非 `InvokePattern` 目标且 `AllowFallback = false` 时，系统会明确拒绝自动降级并保留 attempt 级诊断；真实 `focus` 路径验证 `focus_changed=true` 与 `windows_refresh_focus` 的连续上下文返回；真实 `expand/collapse` 路径验证 `expand_state` 观察值、`action_strategy=expand_pattern/collapse_pattern` 以及该 UIA pattern 路径在真实夹具上不依赖强制窗口激活。与此同时还暴露并修复了两个真实问题：UIA 不支持属性读取导致的 snapshot 崩溃，以及 focused-element 属性瞬时抖动带来的 `COMException`；另一个真实稳定性经验是：桌面 UIA 集成测试需要串行运行，且并非所有场景都适合关闭自动窗口激活，`focus` 路径尤其需要保留真实前台焦点语义

### T5-006 建立场景化端到端测试

- 目标：打通真实任务链路
- 产出物：
  - `I-001` 设置页填写并保存
  - `I-002` 登录表单输入与提交
  - `I-003` 对话框歧义消解
- 依赖：`T5-002`, `T5-003`, `T5-005`
- 验收：端到端链路可重复通过
- 状态：部分完成
- 说明：当前已落地三个最小场景化链路版本。`I-001` 先通过 `automation_id="ProxyAddressInput"` 定位输入框并执行 `windows_act(action="set_value")`，再通过 `automation_id="SaveButton"` 执行 `windows_act(action="invoke")`，最终以 fixture 文本状态 `已保存:<value>` 验证整个 `填写 -> 保存 -> 反馈` 闭环，并明确校验每一步都返回标准化结果与主合同诊断字段。`I-002` 则在同一 fixture 中新增 `UsernameInput`、`PasswordInput` 与 `LoginButton`，依次完成两个 `set_value` 和一个 `invoke`，最后通过 `已登录:<username>/<password>` 文本反馈验证登录式提交链路。`I-003` 通过在 fixture 中故意放置两个同名 `保存` 按钮制造歧义，先验证 `windows_locate` 返回 `TARGET_AMBIGUOUS` 与候选数量，再使用 `automation_id="SaveButton"` 重新定位并完成执行。三个场景都继续坚持文本结构主路径，不依赖截图、OCR 或坐标点击

### T5-007 性能与稳定性回归检查

- 目标：避免闭环成立但性能或稳定性不可用
- 产出物：
  - 快照耗时统计
  - locate 耗时统计
  - act 耗时统计
  - flaky 用例记录
- 依赖：`T5-002` 到 `T5-006`
- 验收：性能与稳定性指标达到一期可用水平
- 状态：部分完成
- 说明：当前已新增一组轻量真实回归测试，而不是单独引入重型 benchmark 基建：在串行 WinForms/UIA 集成环境中，低轮次重复验证 `windows_snapshot -> windows_locate -> windows_act(action="set_value")`、`windows_locate -> windows_act(action="focus") -> windows_refresh_focus`、`windows_locate -> windows_act(action="invoke")`、`windows_locate -> windows_act(action="toggle")` 与 `windows_locate -> windows_act(action="expand"/"collapse")` 五条代表性闭环，记录并约束它们在重复运行中的稳定通过；同时复用现有 `ActionTiming` 断言 `DurationMs >= 0`、`FinishedAt >= StartedAt`，把动作耗时字段先收口为“存在且单调有效”的最小基线。本轮还顺手收口了三类真实时序/可观测性问题：`set_value` 在普通文本框上等待 `ValuePattern.Value` 与目标值对齐，但在受保护或不可读回写（如密码框）上会回退到请求值并保留 readback diagnostics；`focus` 路径在报告成功前会等待可观察焦点状态落稳；`expand/collapse` 路径在返回 `expand_state` 前会等待 `ExpandCollapseState` 与期望状态对齐。当前这还不是正式性能阈值体系，但已经开始把 flaky 观察和 timing 合同纳入持续回归范围

## 10.1 当前阶段旗帜

阶段 5 的核心意义已经从“证明实现能跑”切换为“证明产品是否真的赋能真实桌面操作”。

接下来的工作原则：

- 直接把 OpenClaw 当产品使用，用它去操作真实电脑软件
- 先在真实任务中优化控件树、selector、ref、结构化动作、verification 和 diagnostics
- 不因为少数困难场景过早跳到截图、OCR 或视觉 fallback
- 只有当 accessibility tree 在真实软件上高频失效且无法通过产品打磨解决时，才考虑单独开启第二产品线

当前阶段验收问题应改写为：

- OpenClaw 是否已经真实赋能了典型 Windows 软件操作
- 哪些任务可以仅靠 `accessibility_tree` 主路径完成
- 哪些失败属于 selector/diagnostics/product loop 问题，哪些才属于未来视觉产品边界

## 11. 暂缓项

以下任务不建议插入当前主干路径：

- `MSAA` backend
- `near / right_of / below` 等 anchor 定位正式实现
- `vision:text(...)` 视觉 fallback
- `windows_screenshot` / `windows_ocr`
- 自愈学习和跨会话 selector 记忆
- 面向人类的 inspector UI

其中有些能力在测试矩阵里已有探索性描述，例如 `L-006` 和 `F-002`。

建议做法是：

- 在代码结构里预留接口边界
- 但不把它们作为当前产品验证阶段的阻塞项

## 12. 建议优先级

如果要尽快进入“能跑”的状态，建议按以下顺序拉任务：

1. `T0-001` 到 `T0-004`
2. `T1-001` 到 `T1-006`
3. `T2-001` 到 `T2-006`
4. `T3-001` 到 `T3-006`
5. `T4-001`, `T4-002`, `T4-003`, `T4-004`, `T4-008`
6. `T5-002`, `T5-003`, `T5-005`, `T5-006`

这条路径的核心原则是：

- 先让 `windows_snapshot` 可用
- 再让 `windows_locate` 可用
- 再让 `windows_act` 可用
- 最后补恢复、缓存和场景化稳定性

## 13. 最小开工包

如果下一步就要正式开始写代码，建议先把首批任务定为：

1. `T0-001` 建立 solution 和项目骨架
2. `T0-002` 引入 `FlaUI.UIA3` 和测试依赖
3. `T1-001` 定义核心模型
4. `T1-002` 实现 `Window Registry`
5. `T1-003` 实现基础 `Snapshot Builder`
6. `T1-006` 打通 `windows_list / windows_active / windows_snapshot`

只要这 6 项完成，项目就从“文档方案”进入“可见性闭环的真实实现阶段”。

## 14. 核心结论

OpenClaw 现在已经不再处于“先把能力拼出来”的阶段，而是进入“用产品验证产品”的阶段。

当前主线不是：

- 开放外部接口
- 扩视觉 fallback
- 扩大而全的能力面

当前主线是：

- 用 `accessibility_tree` 主路径去真实操作软件
- 在真实任务里打磨 `snapshot -> locate -> ref -> act -> verify -> recover`
- 明确控件树产品的能力边界
- 只有当这条主路径被真实验证为不足时，再考虑未来的视觉 fallback 产品线
