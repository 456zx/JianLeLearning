# Kongfu 项目架构分析 —— 与传统 Unity 开发的对比

> 本文从 **项目结构**、**应用技术栈**、**中间层与热更层框架**、**性能优化** 四个维度，
> 系统总结 Kongfu 项目的架构设计，并逐项对比其与"传统 Unity 开发"方式的差异。
>
> 一句话概括：Kongfu 不是一个"把 C# 逻辑编译进包体"的常规 Unity 工程，而是一个
> **面向中国小游戏平台（微信 / 抖音）、基于 ILRuntime 解释执行、逻辑可热更新的双域架构**。
> 引擎侧（AOT）只保留一层稳定、窄接口的宿主运行时；全部游戏逻辑作为可替换的解释型 DLL 运行。

---

## 0. 核心差异一览

| 维度 | 传统 Unity 开发 | Kongfu 项目 |
|------|----------------|-------------|
| 引擎 | 标准 Unity，海外 registry | **团结引擎 (Tuanjie 1.6.9)** + Unity 2022.3.62f3c1，`packages.unity.cn` 国内源 |
| 发布目标 | iOS / Android / PC 原生包 | **微信小游戏 + 抖音小游戏**（WebGL/WASM 单一构建 + 双平台适配 SDK） |
| 逻辑执行 | AOT / IL2CPP，逻辑编译进包体 | **ILRuntime 解释执行**热更 DLL，逻辑可远程更新、免重新提审 |
| 代码组织 | MonoBehaviour 直接挂场景，Inspector 连线 | **双域切分**：AOT 宿主层 + 解释型热更层，仅通过 `ScriptRuntime` 静态门面通信 |
| 逻辑与引擎耦合 | 直接持有 `GameObject`/`Transform`/`Component` | 只通过 **整数 ID 句柄** 操作场景，不跨域传递 Unity 对象 |
| 事件回调 | 引擎逐对象回调 `Update`/`OnTrigger` | **信号轮询**：宿主入队，热更侧每帧拉取分发 |
| 对象池 | 手写固定数量池 | **内存预算评分池**（按 KB 计费 + LRU/成本淘汰 + 构建期自动烘焙） |
| 资源加载 | `Resources.Load` / 直接 Addressables 字符串地址 | **引用计数句柄缓存 + 数字编码索引**，逻辑层按整数码引用资源 |

---

## 1. 项目结构

### 1.1 传统 Unity 的结构惯例
典型工程把几乎所有东西放进 `Assets/` 下：脚本、场景、预制体、美术资源混在一起，逻辑以
`MonoBehaviour` 形式直接挂到场景 GameObject 上，靠 Inspector 拖拽连线，`Packages/` 基本
只放第三方 UPM 包。逻辑、资源、场景是强绑定的整体，一起打进最终包体。

### 1.2 Kongfu 的结构：三层 + 双仓
Kongfu 把工程明确切成了**可复用中间层 / 热更业务 / 宿主资源**三部分，并用 Git submodule
把通用框架抽离出去：

```text
Kongfu/
├── Packages/                          # UPM 包（AOT 宿主中间层）
│   ├── com.jian.runtime               # 运行时核心 + ScriptRuntime API 门面（中间层主体）
│   ├── com.jian.hotfix.ilruntime      # ILRuntime 宿主、异步适配器、绑定生成器
│   ├── com.jian.hotfix.framework      # 热更侧事件分发框架（信号轮询）
│   ├── com.ourpalm.ilruntime          # 第三方 ILRuntime 解释器 (2.1.0)
│   ├── com.cysharp.unitask            # UniTask 无 GC 异步 (2.5.0)
│   ├── com.qq.weixin.minigame         # 微信小游戏团结引擎适配 SDK
│   └── jian-unity-packages/           # Git submodule：com.jian.* 的源头仓库
├── HotfixProject~/                    # 独立 .NET SDK 工程，编译出 HotFix.Kongfu.dll
│   ├── Entry.cs                       # 热更入口：Run / Tick / FixedTick / Stop
│   ├── KongFuGame.cs                  # 业务编排
│   ├── Battle/  Diagnostics/  Framework/  Sample/
│   └── Hotfix.csproj                  # netstandard2.1，dll文件输出到 Assets/StreamingAssets/
├── Assets/
│   ├── StreamingAssets/               # HotFix.dll.bytes / .pdb.bytes / resource/*.te / AssetIndex
│   ├── AddressableAssetsData/         # Addressables 配置
│   ├── Editor/AssetIndex/             # 自定义资源数字编码索引构建
│   ├── Scripts/ILRuntime/             # 生成的 CLR 绑定代码
│   ├── Scripts/Editor/Build/          # 构建期自动烘焙（对象池评分等）
│   ├── SPUM/                          # 2D 角色制作/动画资产 (1.8.6)
│   ├── Epic Toon FX/                  # 卡通特效 VFX 资产
│   ├── WX-WASM-SDK-V2/                # 微信小游戏 WASM 运行时/编辑器
│   └── link.xml                       # IL2CPP 防裁剪保护（保护 ILRuntime 反射类型）
├── LocalPackages/
│   └── com.bytedance.starksdk@6.7.4   # 抖音小游戏 TTSDK / Stark SDK
└── Docs/                              # 文档
```

**关键结构性差异：**

1. **通用框架 submodule 抽离** —— `com.jian.*` 系列不直接维护在主仓，而是通过
   `Packages/jian-unity-packages` 这个 submodule 引用（`manifest.json` 用 `file:` 相对路径）。
   框架改动提交到独立仓库后再更新指针，业务逻辑留在 `HotfixProject~` / `Assets`。
   传统工程很少这样把"引擎中间层"当独立产品维护。

2. **`HotfixProject~` 是 Unity 之外的独立 .NET 工程** —— 后缀 `~` 让 Unity 资源导入器忽略它。
   它用普通 MSBuild 编译成 `HotFix.Kongfu.dll`，把 Unity 编译产物（`Jian.Runtime`、
   `Assembly-CSharp`、`UniTask` 等）当外部 DLL 引用（`<Private>False</Private>`，只引用不拷贝）。
   这与传统工程"所有 C# 都由 Unity 编译"完全不同。

3. **产物即字节** —— 编译出的热更 DLL 以 `HotFix.dll.bytes` 形式放进 `StreamingAssets`
   （`.bytes` 后缀是为了让 Unity 当 `TextAsset` 导入），随包发布或从 CDN 拉取，运行时由
   `ILRuntimeHost` 读取解释执行。

---

## 2. 应用到的技术

### 2.1 引擎与发布目标
- **团结引擎 (Tuanjie Engine 1.6.9)** ：Unity 中国分支，核心卖点是一等公民级的微信/抖音小游戏
  构建管线。`ProjectVersion.txt` 同时记录 `2022.3.62f3c1` 与 `TuanjieEditorVersion 1.6.9`，
  包源为 `packages.unity.cn`。
- **构建目标 WebGL/WASM** ：`m_BuildTarget: WebGLSupport`，IL2CPP 后端，WebGL 模板为微信专用的
  `WXTemplate2022`，内存按小游戏受限运行时调优（初始 32MB、几何增长、上限受控）。
- **双小游戏平台**：
  - 微信小游戏：`com.qq.weixin.minigame` (0.1.31) + `Assets/WX-WASM-SDK-V2`（含 `MiniGameConfig.asset`）。
  - 抖音小游戏：`LocalPackages/com.bytedance.starksdk@6.7.4`（字节 TTSDK / Stark SDK，含 `ttsdk.dll`，
    changelog 明确适配团结引擎）。
  - 两者共用同一份团结引擎 WebGL 核心，通过两个平台适配 SDK 分别打包。

### 2.2 热更技术栈
- **ILRuntime 2.1.0 (Ourpalm)** ：纯 C# 的 CIL 解释器。这是整套架构的基石 —— 热更 DLL 的字节码被
  **解释执行而非 JIT**，天然规避了 iOS / 微信平台对运行时代码生成（JIT）的封禁。
- **`com.jian.hotfix.ilruntime`** ：自研宿主基建，含 `ILRuntimeHost`（加载 DLL、驱动 AppDomain、
  每帧 `Invoke` 热更 Tick）、`AsyncStateMachineAdapter`（把 `async`/`await`/UniTask 桥接进解释域）、
  `HotfixHooks`（扩展点）、`ILRuntimeBindingGenerator`（编辑器 CLR 绑定代码生成）。
- **`com.jian.hotfix.framework`** ：热更侧事件分发框架（详见 §3）。

### 2.3 异步：UniTask 2.5.0
全部资源/场景加载 API 返回 `UniTask<T>`，使用 `SwitchToMainThread`、`.Forget()` 等。选用 UniTask
而非 `Task` 是为了 **无 GC 分配的异步**，这在内存受限的小游戏 WASM 运行时尤为关键。
（注：项目只引入了 UniTask 的 DOTween 集成 shim，**DOTween 本身并未安装**。）

### 2.4 资源管理：Addressables + 自定义数字编码索引
- 标准 Addressables 1.22.3 负责实际加载，源资源打 `runtime_asset` 标签。
- 之上叠了一层**自定义索引**（`Assets/Editor/AssetIndex/`）：构建期扫描资源，输出
  `StreamingAssets/AssetIndex/asset_index.json`，把每个资源映射到一个 **6 位数字编码**。
- 运行时 `AssetIndexManager` 把索引读入字典，热更代码 **按稳定整数码引用资源**，而非字符串地址。
  好处：编码在内容变更间保持稳定，把解释型逻辑与 Addressables 字符串地址解耦，避免字符串抖动与分配。

### 2.5 其他关键资产
- **SPUM 1.8.6** ：2D 模块化角色制作 / 动画管线（配套精灵编辑器、图集导出、存读预制体）。
- **Epic Toon FX** ：卡通风格粒子/特效库（其 Demo 脚本已在清理，仅保留美术资源）。
- **TextMeshPro 3.0.9 + uGUI** ：文本与 UI。
- Burst / Collections / Mathematics、AI Navigation、Timeline、Newtonsoft-Json 等 registry 包。

---

## 3. 中间层与热更层框架

这是整个项目最核心、也是与传统 Unity 差异最大的部分。**架构主张是严格的双域切分：**

```text
┌─────────────────────────────────────────────────────────┐
│  HotFix.Kongfu.dll （解释执行，可热更）  ← 全部游戏逻辑      │
│  KongFuGame / KF_*UIManager / KF_EnemyAgent ...           │
└───────────────┬─────────────────────────────────────────┘
                │  只调用 ScriptRuntime.* / Jian.Hotfix.Framework.*
                │  （生成的 CLR 绑定 + 跨域适配器）
                ▼
┌─────────────────────────────────────────────────────────┐
│  com.jian.runtime  ScriptRuntime API 门面（AOT，在宿主内） │
│  ← 稳定、窄接口的引擎能力面                                 │
└───────────────┬─────────────────────────────────────────┘
                ▼
┌─────────────────────────────────────────────────────────┐
│  Unity 引擎 + GameKernel（AOT 原生）                       │
└─────────────────────────────────────────────────────────┘
```

### 3.1 中间层：`com.jian.runtime`（AOT 宿主）

**目录划分**（`Runtime/`）：
- `API/` —— 门面边界层，`ScriptRuntime` 按业务域分布在 33 个 partial 文件里。
- `Core/` —— 启动与生命周期：`FrameworkBootstrap`、`GameEntry`、`GameKernel`、`StringBuilderPool`。
- `Modules/` —— 真正面向引擎的实现（32 个 Manager）。
- `Data/` —— 数据原语与配置表加载（`GVar/GInt/GFloat/GString` 装箱值、`TeTable` 二进制表格式）。
- `Flow/` —— 舞台生命周期契约。
- `Randomness/` —— 确定性随机（`GameRandom`、`XorShiftRandom`、`PRDCalculator`、`ShuffleBag`）。
- `DevTools/`、`Templates/`、`UI/`、`ThirdParty/LitJson/`。

**`ScriptRuntime` 门面模式（关键架构点）** ——
一个 `public static partial class`（命名空间 `MiniCSharp.Runtime`），**650+ 个 `public static` 门面成员**
按域切分在 33 个 partial 文件（`API.Entity.cs`、`API.Resource.cs`、`API.UI.cs`、`API.Physics.cs`…）。
其一致贯彻的设计原则：

1. **句柄/ID 间接层 —— Unity 对象不跨域** ：门面从不返回 `GameObject`/`Transform`/`Sprite`，
   而是返回不透明整数 ID。实体是 `int entityId`，资源是 `int` 句柄，UI 窗口是 `int WindowId`。
   热更 DLL 完全通过整数令牌操作场景，ILRuntime 无需 marshal 或绑定活的 Unity 引用类型。
2. **`Try*` 命名 + 双结果约定** ：轻量版返回哨兵值（`-1`/`false`/`null`），完整版返回
   `ApiResult`/`ApiResult<T>`（携带 `Success` + `ApiErrorCode` 枚举 + 消息）。
3. **每次调用都受保护、绝不跨域抛异常** ：内部 `InternalTryGetKernel` 网关校验（kernel 缺失/
   正在关闭/非播放态即返回安全失败），方法体统一 try/catch。异常从 AOT 传回解释器代价高且脆弱，
   门面把它们全部吸收。
4. **异步基于 UniTask 且绑定生命周期** ：`CancellationToken` 与 kernel/实体销毁令牌联动，
   业务操作在框架拆卸或实体回收时自动取消。
5. **委托薄转发** ：每个门面方法解析 kernel 后转发给对应 Module，门面只加"守卫 + ID 映射 +
   取消联动 + 错误归一化"。
6. **反射重载标记过时** ：接受类型全名反射的便捷重载都带 `[Obsolete]`，引导热更作者用强类型变体。

**`GameKernel` —— 运行时心脏** ：`[DefaultExecutionOrder(-1000)]` + `[Preserve]` 的单例 MonoBehaviour。
以属性暴露各 Module（`Resource`/`Entity`/`UI`/`Stage`/`Input`/`Audio`/`AssetIndex`…），
`InitializeModules()` 做 **手写的有序依赖注入**（`Data.Initialize(Resource)` → `Entity.Initialize(Resource)`
→ `UI.Initialize(Resource,Data,Input,Layer)` → `Stage` 最后）。
它还**通过字符串类型名反射发现 `ILRuntimeHost`** —— 中间层对具体热更实现**零编译期引用**，保持可复用。

**代表性 Module**：`ResourceManager`（引用计数异步加载）、`EntityManager`（ID→实体注册 + 对象池 +
事件缓冲）、`UIManager`（栈式 UI + WindowId 句柄）、`StageManager`（**双轨状态机**：逻辑轨
Running/Paused/Ended 与渲染轨 ActiveRender/PausedRender/UnloadedView 独立，后台舞台可保持逻辑运行而
暂停渲染）、以及各种 `*Forwarder`（把引擎回调转发进 Manager 缓冲，供热更轮询）。

### 3.2 热更层框架：`com.jian.hotfix.framework`（信号轮询）

框架的核心贡献是一套 **信号/事件分发层，让处理器始终留在解释器域内**。设计原则：
不把解释器回调注册成 Unity 对象上的原生委托（跨域脆弱、慢、域重载时有问题），而是
**宿主入队事件、热更侧每帧轮询分发**。

- **`UISignalDispatcher`** —— 最典型。`RegisterButtonClick(windowId, control, handler)` 调
  `ScriptRuntime.AddUIButtonClickSignalListener(...)` 拿到整数 `listenerId`，`Tick()` 拉取
  `TryDequeueUIControlSignal(listenerId, ...)` 并在**热更域内**调用 handler。
  文件注释明言："通过信号轮询机制替代旧版跨域委托绑定，Handler 始终留在热更域内"。
- **`AnimationEventDispatcher`** —— `Tick()` 排空 `TryDequeueAnimationEvent`，分发
  `EntityAnimationEvent{EntityId,EventName}`，逆序遍历 + 逐处理器 try/catch。
- **`Trigger2DDispatcher`** —— `FixedTick()` 排空 2D 触发进/出事件，含 self/other 实体 ID、
  碰撞体路径（`$root`、`Body/HurtBox`、`HitBox[1]`）、tag、layer。

### 3.3 ILRuntime 宿主基建：`com.jian.hotfix.ilruntime`

- **`ILRuntimeHost`** ：`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 自举出持久化宿主
  GameObject；`LoadHotfixAssemblies()` 新建 `AppDomain` → 注册跨域适配器与方法委托 →
  从 StreamingAssets 读 DLL/PDB 字节 `LoadAssembly` → `CLRBindingUtils.Initialize` →
  反射式 `appDomain.Invoke("HotFix.Entry", "Run", ...)`；每帧 `Update`/`FixedUpdate` 调
  `HotFix.Entry.Tick/FixedTick`。DLL 字节可通过 `UnityWebRequest` + `hotfixBaseUrl` 指向 CDN，
  实现真正的 OTA 逻辑更新。抛异常的 Tick 会被禁用，避免逐帧刷屏。
- **`AsyncStateMachineAdapter`** ：`CrossBindingAdaptor`，适配 `IAsyncStateMachine`，让解释型
  代码里的 `async`/`await`/UniTask 能被原生运行时驱动（`MoveNext` 回调进解释器）。
- **CLR 绑定生成器** ：编辑器菜单 `ILRuntime/生成当前项目CLR绑定代码`，按白名单（Unity 值类型 +
  Jian 运行时类型 + 框架事件类型）生成绑定桩，把"解释代码调原生方法"的慢反射变成直接调用，
  并自动维护 `ILRuntime.Generated.asmdef` 的引用列表。这是热路径性能的关键。

### 3.4 热更业务工程：`HotfixProject~`

入口 `HotFix.Entry`（`Run`/`Tick`/`FixedTick`/`Stop`）严格匹配宿主的序列化契约。
`KongFuGame` 编排启动与战斗/大厅状态。业务代码（`KF_*Manager`、`KF_EnemyAgent`…）是普通 C#，
**只通过 `ScriptRuntime` / `Jian.Hotfix.Framework` 门面与引擎对话**，不直接持有 `MonoBehaviour`/
`GameObject`，一律用整数 ID 指代实体。`Hotfix.csproj` 的 `AfterBuild` 目标把产物拷成
`HotFix.dll.bytes` / `HotFix.Kongfu.dll.bytes`（含 PDB）并同步 `.te` 配置表到
`StreamingAssets/resource/`。

### 3.5 与传统 Unity 的对比小结
传统 Unity：逻辑是编进包体的 `MonoBehaviour`，直接持有引擎对象，靠 Inspector 连线与逐对象
`Update` 回调，静态链接、发布后不可更新。Kongfu 把这一切反转：业务逻辑是**解耦、解释、可热替换**
的代码，只通过 **ID 化的过程式门面** 操作场景，用**轮询而非引擎回调**接收事件，由中心化
`GameKernel` 以**手写依赖注入**编排，而非场景图连线。

---

## 4. 性能优化

对于一个小游戏而言，这个项目的优化工作异常系统化，明显是围绕小游戏受限内存与增量 GC 预算设计的。

### 4.1 内存预算评分对象池（而非计数池）
核心 `EntityManager` + ScriptableObject 配置 `EntityPoolConfig`：
- 全局预算 `MaxBudgetKB = 256000`（256MB），仅计非活跃池实例。
- 每条目携带 `ResidentScoreKB`（共享资产一次性成本，如网格/贴图）与 `InstanceScoreKB`（每个非活跃
  实例成本）；`CurrentScoreKB = ResidentScoreKB + count * InstanceScoreKB`，增量维护总分。
- 超预算时 **LRU 优先淘汰**（最旧 `LastUsedTime`），大分数为二次判据 —— 冷门重型池先被销毁。
- **安全兜底**：开启预算但预制体没有烘焙评分时，强制禁用其池化（"无法计费内存 → 不允许无界增长"），
  防止未烘焙预制体静默泄漏。
- `PrefabHandleBinder` 随实例携带资源句柄并缓存可池化组件，避免复用时反复 `GetComponentsInChildren`。
- 配置支持 **Baked（烘焙）敌人变体**：巨大 `ResidentScoreKB` 换取极小 `InstanceScoreKB`
  （如 `EliteEnemy01_Baked` 64127KB / 26KB vs 原版 898KB / 137KB），高敌人数下近乎零的单实例成本。

### 4.2 引用计数句柄资源缓存
`ResourceManager`：
- 按 `AssetKey(path,type)` 引用计数缓存；并发同 key 加载经共享 `UniTaskCompletionSource` **去重**
  （N 个并发请求只触发一次加载）。
- 对外只给不透明句柄 ID，不暴露原始 Unity 对象。
- **空闲自动释放循环**：每 10s 扫描，30s 空闲后驱逐，每帧最多卸载 4 个（把卸载成本摊到多帧）。
- `Resources.UnloadUnusedAssets()` 门控为每 60s 且确有释放才跑，避免昂贵全量扫描空转。

### 4.3 显式多批次预热（消除首波实例化尖峰）
`ScriptRuntime.TryWarmupEntityPool` 分批（默认 16）生成后全部回收，跨帧 yield 避免单帧尖峰。
战斗层 `KF_BattleState.WarmupBattleEntityPoolsAsync` 在开战前激进预热：150 普通敌人、4 精英、
16 子弹、8 伤害文字画布、金币文字、掉落、波次特效等 —— 首波永不付实例化代价。

### 4.4 构建期自动烘焙
`AutoBakePoolScoresPreprocess`（`IPreprocessBuildWithReport`，callbackOrder -1000，最先跑）在构建时
自动调 `PoolScoreBaker.BakeForBuild`，保证生产包永不带缺失/陈旧的池评分。`PoolScoreBaker` 既可
**精确测量**（预览场景实例化后累加 `Profiler.GetRuntimeMemorySizeLong`），也可**启发式估算**
（渲染器×8、碰撞体×4、蒙皮网格×16…），且保留手动 override 条目。

### 4.5 诊断 / Profiling 三层
- **`DebugPerformanceHUD`** ：F3 切换的屏幕 HUD（FPS/帧耗时、内存、VRAM、三角形+DrawCall、资源缓存、
  实体池、UI 池），F4 强制 GC；Profiler recorder 仅 `#if UNITY_EDITOR`，发布包无运行时开销。
- **`KF_PerfProbe`** ：热更侧自动压测/性能探针，定义 12 个计时区（BattleTick、EnemyFixedTick、
  NearestEnemySearch…），50→1000 敌人分阶扫描，16384 环形缓冲做百分位分析，**逐区 GC 分配字节**
  归因，检测 >50ms 帧尖峰。仅编辑器运行时启用。
- **实体池 spawn 统计** ：区分池命中 vs 新实例化，记录 pop/load/instantiate/prepare/register 各阶段
  平均/最大耗时 —— 直接看出池命中率与耗时去向。

### 4.6 运行时视觉降级（性能阀门）
`KF_ProbeEnemyVisualSimplifier`：大敌人数时禁用除 `Body`/`BakedVisual` 外的渲染器（保留 ≤1 精灵），
并对非攻击/死亡/受击状态的敌人暂停 Animator（`SetEntityAnimatorSpeed(id,0)`），拥挤时削减 DrawCall
与动画采样。重反射工作刻意下沉到 AOT 宿主层，不放进 ILRuntime 域。

### 4.7 代码裁剪 / link.xml / GC 抑制
- **`Assets/link.xml`** 保护 ILRuntime 宿主类型、`ScriptRuntime` 门面、LitJson、随机系统、以及热更
  反射用到的引擎类型 —— 因为 IL2CPP 托管裁剪会移除只被 ILRuntime 动态引用的类型。WX/小游戏 SDK
  各自也带 `link.xml`。
- **GC 最小化贯穿始终**（为小游戏增量 GC 预算）：
  - `StringBuilderPool` + `ScriptRuntime.GetStringBuilder/ReleaseStringBuilder`，字符串构建全程借还。
  - `ScriptRuntime.NewList/NewDictionary` 带容量提示，按表行数预分配避免 rehash。
  - `EntityManager` 保留可复用字段缓冲（`_poolableBehavioursBuffer`、`_overlapCollider2DBuffer`、
    8192/1024 预分配事件队列），每帧触发/重叠处理零分配。
  - 颜色/材质走共享 `MaterialPropertyBlock`（`_sharedColorBlock`），避免实例化材质与打断合批。
  - 物理/动画事件走**队列轮询**而非逐碰撞回调跨 ILRuntime 域，每帧批量分发一次。

### 4.8 与传统 Unity 的对比小结
| 优化点 | 传统做法 | Kongfu |
|--------|----------|--------|
| 对象池 | 手写固定数量池 | 内存预算评分 + LRU/成本淘汰 + 构建期自动烘焙 |
| 资源 | `Resources.Load` + 手动卸载 | 引用计数句柄缓存 + 加载去重 + 跨帧空闲卸载 |
| 首波尖峰 | 首次遇到才实例化 | 开战前显式多批次预热 |
| 高实体数 | 直接渲染全部 | 烘焙变体 + 运行时渲染器/动画降级 |
| GC | 随手分配 | 池化 StringBuilder/集合、复用缓冲、共享 MPB、轮询事件 |
| 裁剪 | 默认 | link.xml 保护反射面，保持热更 DLL 小（~408KB） |

---

## 5. 技术名词速查

> 下表把本文出现的每个关键术语，用一句大白话讲清「是什么 / 为什么用它」。按被问到时能自圆其说的顺序排列。

| 术语 | 一句话解释 | 为什么这个项目要用 |
|------|-----------|-------------------|
| **AOT（Ahead-Of-Time）** | 提前把代码编译成机器码，运行时不再生成代码。 | iOS 和微信小游戏运行时禁止 JIT，所以引擎侧宿主必须走 AOT（IL2CPP）。 |
| **JIT（Just-In-Time）** | 运行时把字节码即时编译成机器码。 | 正因为被封禁，才不能用普通 Mono/JIT 热更，只能改用「解释执行」。 |
| **IL2CPP** | Unity 把 C# → IL → 生成 C++ → 原生编译的 AOT 后端。 | WebGL/小游戏唯一可用后端；带来「托管代码裁剪」问题，所以需要 link.xml。 |
| **ILRuntime** | 一个纯 C# 写的 CIL（IL 字节码）**解释器**，逐条解释执行 DLL。 | 它不生成机器码 → 不触发 JIT 禁令 → 成为「逻辑可热更」的技术基石。 |
| **解释执行 vs 编译执行** | 解释器逐条读字节码执行，慢但灵活；编译执行快但固化进包。 | 用「慢一点的解释执行」换「逻辑能远程替换、免重新提审」。 |
| **热更（HotFix / OTA）** | 不重新发版、从 CDN 拉新逻辑 DLL 就能更新游戏。 | 小游戏审核周期长，热更让 bug 修复和活动逻辑当天上线。 |
| **双域（AOT 域 / 解释域）** | 引擎侧原生代码是一个域，热更 DLL 在 ILRuntime 里是另一个域。 | 两域内存/类型系统不互通，必须靠「门面 + 绑定 + 适配器」跨域。 |
| **跨域（Cross-domain）调用** | 解释域里的代码调 AOT 域里的原生方法，反之亦然。 | 跨域慢且脆，所以设计上「能不跨就不跨」——用 ID、用轮询、绑定生成。 |
| **CLR 绑定（Binding）** | 编辑器生成的桩代码，把「解释代码调原生方法」从慢反射变直接调用。 | 热路径（每帧调用）不能走反射，绑定是性能刚需。 |
| **CrossBindingAdaptor** | 让解释域能「实现」原生域接口/基类的适配器（如 `IAsyncStateMachine`）。 | 让热更侧的 `async/await`、UniTask 能被原生运行时驱动。 |
| **门面模式（Facade）** | 用一个统一入口类（`ScriptRuntime`）包住底层一堆复杂子系统。 | 给热更层一个稳定、窄、不易变的接口面，隔离引擎细节。 |
| **句柄 / ID 间接层** | 不传对象本体，只传一个整数编号，用编号去查真身。 | Unity 对象不能安全跨域传递，整数最轻、最稳、最好 marshal。 |
| **信号轮询（Signal Polling）** | 宿主把事件塞进队列，热更侧每帧主动来取，而非引擎回调。 | 逐对象跨域回调既慢又在域重载时出错，轮询把它变成每帧批量一次。 |
| **依赖注入（DI）** | 一个对象需要的其他对象，由外部按顺序装配好再交给它。 | `GameKernel` 手写有序装配各 Manager，替代 Inspector 拖拽连线。 |
| **对象池（Object Pool）** | 用完不销毁、回收复用，避免反复 `Instantiate`/`Destroy`。 | 小游戏内存/GC 紧张，实例化尖峰会掉帧，池化是硬需求。 |
| **LRU（Least Recently Used）** | 淘汰时优先踢掉「最久没被用过」的。 | 内存超预算时决定先销毁哪个池，冷门重型池先走。 |
| **UniTask** | 专为 Unity 优化的**无 GC 分配**异步库，替代 `Task`。 | 小游戏 GC 预算极紧，`Task` 的堆分配会拖累帧率。 |
| **Addressables** | Unity 官方的「按地址异步加载 + 依赖管理」资源系统。 | 替代 `Resources.Load`，支持远程包、按需加载、引用计数卸载。 |
| **link.xml** | 告诉 IL2CPP「这些类型别裁掉」的保护清单。 | 只被 ILRuntime 反射用到的类型，静态分析看不见，会被误删。 |
| **团结引擎（Tuanjie）** | Unity 中国版分支，内建微信/抖音小游戏构建管线。 | 国内小游戏发布的一等公民支持，普通 Unity 要自己接 SDK。 |
| **WASM / WebGL** | 浏览器里跑的字节码/图形标准，小游戏的实际运行载体。 | 微信/抖音小游戏本质是跑在 WebGL 容器里的 WASM 程序。 |

---

## 6. 技术选型问答（被追问时的答案）

> 这些是复述这套架构时最容易被追问的「为什么」，每条给一个能站住脚的回答。

**Q：为什么不用 HybridCLR / Lua / 直接 C# 热更，偏要 ILRuntime？**
A：核心约束是「iOS 和微信小游戏禁 JIT」。HybridCLR 在小游戏/iOS 上受限，Lua 要另写一套逻辑且丢掉 C# 类型安全与工具链。ILRuntime 是纯解释执行、不碰 JIT，能直接跑现有 C# 编译出的 DLL，代价是运行慢一些——项目用「绑定生成 + 把重活留在 AOT 层」把这个代价压下去了。

**Q：解释执行不是很慢吗，怎么扛住上百个敌人？**
A：三个手段。① 热路径生成 CLR 绑定，避免反射；② 把重反射/物理/动画这些重活刻意留在 AOT 宿主层，热更层只发指令；③ 事件走每帧一次的批量轮询，而不是逐对象跨域回调。再加上内存预算对象池和运行时视觉降级，实测能做分阶压测到 1000 敌人。

**Q：为什么门面全用整数 ID，不直接传 GameObject？**
A：Unity 的引用类型不能安全跨 ILRuntime 域传递（marshal 成本高、生命周期难对齐、域重载易失效）。整数是最轻的令牌，热更层拿 ID 说话，真身始终留在 AOT 域由 Manager 持有，从根上避免了跨域悬垂引用。

**Q：为什么门面「绝不跨域抛异常」？**
A：异常从 AOT 域穿回解释器域代价高且不稳定，可能直接崩掉解释器。所以门面内部统一 try/catch，对外用 `ApiResult` + 错误码返回失败，把异常吸收在原生侧。

**Q：为什么事件要轮询，不直接注册回调？**
A：把解释域的 handler 注册成 Unity 对象上的原生委托，既慢又在 AppDomain 重载时容易变野指针。改成「宿主入队、热更每帧拉取」后，handler 永远留在解释域内，且每帧批量分发一次，跨域次数从 O(事件数) 降到 O(1)。

**Q：对象池为什么按内存 KB 计费而不是按数量？**
A：小游戏真正的约束是内存上限，不是实例个数。一个重型敌人可能顶几十个轻型对象。按 KB 计费 + LRU 淘汰，才能在固定内存预算内做出「留谁、踢谁」的正确决策；未烘焙评分的预制体直接禁止池化，杜绝无法计费的静默泄漏。

**Q：热更 DLL 怎么发布、怎么更新？**
A：`HotfixProject~` 是 Unity 之外的独立 .NET 工程，用 MSBuild 编译出 `HotFix.Kongfu.dll`，产物以 `.bytes` 后缀放进 `StreamingAssets` 随包发，或由 `ILRuntimeHost` 通过 `hotfixBaseUrl` 从 CDN 拉。运行时读字节 → `LoadAssembly` → 反射 `Invoke("HotFix.Entry","Run")` 启动，每帧 `Tick`/`FixedTick` 驱动。

**Q：一句话，这套架构和传统 Unity 的根本区别？**
A：传统 Unity 把逻辑 AOT 编进包体、直接持有引擎对象、靠 Inspector 连线和逐对象回调，发布后不可改；Kongfu 把逻辑做成解释执行、可远程替换的 DLL，只通过 ID 化的过程式门面操作场景、用轮询接事件、由 GameKernel 手写装配——用一定的性能和复杂度，换来「逻辑免提审热更 + 双平台复用」。

---

## 7. 结论

Kongfu 的本质是一套 **面向中国小游戏平台的、逻辑可热更新的双域 Unity 架构**。它与传统 Unity 开发的
根本分歧在于回答了同一个问题的不同答案：**"游戏逻辑应该编进包体还是可远程替换？"**

- 传统 Unity 选择把逻辑 AOT 编进原生包，换取直接、简单、高性能，但发布后不可更新，且受
  iOS/微信 JIT 封禁约束。
- Kongfu 选择把逻辑作为 **ILRuntime 解释执行的热更 DLL**，宿主只保留一层稳定、窄接口、
  ID 化、绝不跨域抛异常的 `ScriptRuntime` 门面。代价是解释执行的性能开销和跨域约束，
  收益是**逻辑免提审远程热更**、双平台复用、以及框架层的可复用性。

围绕这个选择，项目付出了大量配套工程：CLR 绑定生成、异步跨域适配、信号轮询事件模型、
内存预算对象池、数字编码资源索引、构建期自动烘焙、无 GC 纪律 —— 它们共同构成了这套架构
"能在小游戏受限运行时里跑得动、且逻辑可持续热更"的完整解法。

---

### 附：关键文件索引

- 门面与错误模型：`Packages/com.jian.runtime/Runtime/API/API.cs`、`API/Entity/API.Entity.cs`、`API/Resource/API.Resource.cs`
- 启动/生命周期：`Runtime/Core/GameKernel.cs`、`GameEntry.cs`、`FrameworkBootstrap.cs`
- 核心 Module：`Runtime/Modules/EntityManager.cs`、`ResourceManager.cs`、`UIManager.cs`、`StageManager.cs`、`EntityPoolConfig.cs`、`PrefabHandleBinder.cs`
- ILRuntime 宿主：`Packages/com.jian.hotfix.ilruntime/Runtime/ILRuntimeHost.cs`、`AsyncStateMachineAdapter.cs`、`Editor/ILRuntimeBindingGenerator.cs`
- 热更框架：`Packages/com.jian.hotfix.framework/Runtime/Events/{UISignalDispatcher,AnimationEventDispatcher,Trigger2DDispatcher}.cs`
- 热更业务：`HotfixProject~/Entry.cs`、`KongFuGame.cs`、`Hotfix.csproj`、`Battle/KF_BattleState.cs`、`Diagnostics/Performance/KF_PerfProbe.cs`、`Diagnostics/Visual/KF_ProbeEnemyVisualSimplifier.cs`
- 优化/构建：`Assets/Scripts/Editor/PoolScoreBaker.cs`、`Assets/Scripts/Editor/Build/AutoBakePoolScoresPreprocess.cs`、`Assets/link.xml`、`Assets/Resources/Configs/EntityPoolConfig.asset`
- 资源索引：`Assets/Editor/AssetIndex/AssetIndexBuildConfig.asset`、`Assets/StreamingAssets/AssetIndex/asset_index.json`
- 平台 SDK：`Assets/WX-WASM-SDK-V2/`、`Packages/com.qq.weixin.minigame`、`LocalPackages/com.bytedance.starksdk@6.7.4`
