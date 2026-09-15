# PrismDemo — WPF + Prism 模块化工业监控上位机（Demo）

基于 .NET 8 / WPF / Prism 8 的模块化上位机示例：主程序不引用任何业务模块，
业务模块以 DLL 形式放在 `modules/` 目录下，运行时扫描、加载、启停与热重载。
当前包含两个业务模块：**A 模块**（`PrismDemo.A`）与 **B 模块**（`PrismDemo.B`）。

> 本仓库为演示版本：业务算法细节、现场点位命名与真实连接凭据均已做通用化处理，
> 仅保留可运行的框架与界面骨架。

---

## 1. 解决方案结构

| 工程 | 输出 | 职责 |
| --- | --- | --- |
| `PrismDemo.APP` | WinExe（主程序） | 外壳窗口、导航、首页看板、报表、报警、设置；模块扫描/部署/热重载；OPC 采集主循环 |
| `PrismDemo.Core` | 类库 | 共享内核：配置读取、数据库服务、OPC 服务、报警服务、日志、通用控件与转换器、共享数据模型 |
| `PrismDemo.A` | 模块 DLL | 业务模块 A：药剂 A 投加控制（采集 → 计算 → 回写 → 入库主循环，四个控制点 + 历史曲线）；**算法细节已移出本示例** |
| `PrismDemo.B` | 模块 DLL | 业务模块 B：药剂 B 投加控制（采集 → 计算 → 回写 → 入库主循环，四个控制点 + 历史曲线 + 实时表）；**算法细节已移出本示例** |

依赖方向：`APP → Core`，`A → Core`，`B → Core`；**APP 不引用 A/B**，
两者仅通过 `PrismDemo.Core` 中的接口（`IModuleSwitch`、`IWriteableService`、`ServiceProxy<T>` 等）解耦。

```
PrismDemo.sln
├── PrismDemo.APP/            # 主程序
├── PrismDemo.Core/           # 共享内核
├── PrismDemo.A/              # 业务模块 A（a.config.json）
└── PrismDemo.B/              # 业务模块 B（b.config.json）
```

## 2. 运行时架构要点

**模块动态加载**
- `App.ConfigureModuleCatalog()` 不静态注册模块，由 `DynamicModuleManager` 用自定义
  `AssemblyLoadContext`（`ModuleLoadContext`）在运行时加载 `modules/` 下的 DLL。
- 模块命名约定：`PrismDemo.<ModuleName>.yyyyMMddHHmm.dll`，同名模块保留最新版本，
  `OnInitialized` 时清理多余的历史版本。

**热重载接缝**
- 容器中注册的是 `ServiceProxy<T>`（`Singleton`，生命周期与进程相同），内部持有当前服务实例。
- 模块被卸载/重载时只替换 `Proxy.Target`，宿主侧已解析的引用无需重建。
- 模块 `RegisterTypes` 使用 `IfAlreadyRegistered.Replace` 语义，保证二次加载幂等。

**模块启停**
- `IModuleSwitch` 维护模块开关状态；模块主服务订阅 `SwitchChanged`，据此启动/停止后台循环。
- 模块被禁用时触发 `ModuleDisabled`，由页面清理该模块的视图与订阅，避免孤儿视图。

**数据链路**
- `PrismDemo.APP/Services/APPMainService` 负责 OPC UA 采集主循环，写入 `SharedDataModel` 与数据库。
- 各模块的 `*DataService` 从采集数据中抽取本模块关注的字段，落库并触发更新事件；
  模块的 `*MainService` 负责投加控制循环（采集 → 算法 → 回写 → 入库），本示例中算法步骤为占位。
- 报警：`AlarmService` 统一读取报警配置、判级、去抖、弹窗与入库。

## 3. 目录速览

```
PrismDemo.A/
├── AModule.cs                    # 模块入口（RegisterTypes / OnInitialized / Dispose）
├── a.config.json                 # 模块配置（上下限等）
├── Configuration/Config.cs       # 配置加载
├── Interfaces/IAService.cs       # 模块服务接口
├── Models/                       # AFieldConfig、HistoryPoint
├── Services/
│   ├── ADataService.cs           # 数据抽取 / 入库 / 表结构维护
│   └── AMainService.cs           # 控制主循环（采集 → [算法区] → 入库）
├── ViewModels/                   # 总览 + 4 个控制点 + 4 个历史曲线
└── Views/                        # 对应 XAML
```

`PrismDemo.B` 与 `PrismDemo.A` 结构同构（命名前缀 `B*`），并额外包含 `Models/RealtimeRow.cs`（控制点实时表使用）。

### 关于 A / B 模块的算法区

两个模块的 `*MainService.cs` 控制循环中都保留了算法区的 `#region` 结构，但**算法实现已移除**，
只留占位注释；采集、算法区之后的落库、界面、历史曲线、报表等功能不受影响。

`PrismDemo.A/Services/AMainService.cs`：

```
#region 算法逻辑实现
    #region 数据预处理（数据检查、过滤）      ← 占位，实现已移除
    #region 沉后水浊度反馈                     ← 占位，实现已移除
    #region 计算加药量                          ← 占位，实现已移除
    #region 加药联动控制                        ← 占位，实现已移除
    #region 保存数据库                         ← 保留（数据落库，非算法）
#endregion
```

`PrismDemo.B/Services/BMainService.cs`：

```
#region 算法逻辑实现
    #region 赋值初始投加率                     ← 占位，实现已移除
    #region 数据预处理（数据检查、过滤）      ← 占位，实现已移除
    #region 反馈更新                            ← 占位，实现已移除
    #region 计算加药量                          ← 占位，实现已移除
    #region 加药联动控制                        ← 占位，实现已移除
    #region 保存数据库                         ← 保留（数据落库，非算法）
#endregion
```

随之删除的实现文件（两个模块各自一份，共 10 个）：`Services/ProcessModel.cs`、`Services/Feedback.cs`、
`Services/UnitRateConvert.cs`、`Services/LinearInterpolation.cs`、`Models/FeedbackCount.cs`。

> 数据滤波工具 `DataFilter`（`PrismDemo.Core/Services/DataFilter.cs`）同样已删除；
> 仅保留配置契约 `PrismDemo.Core/Interfaces/IFilterConfig.cs`（由 `AFieldConfig` / `BFieldConfig` 实现，
> 对应配置表的 `IsFilter` / `FilterLength` 列）。两模块的算法区内不再有任何算法实现。

`PrismDemo.Core` 主要目录：`Configuration`、`Interfaces`、`Models`、`Services`、`Controls`、`Converters`、`Helpers`、`Views`。

## 4. 编译与运行

环境要求：Windows、.NET SDK 8.0 及以上（已用 9.0.302 验证）、Visual Studio 2022 或 `dotnet` CLI。

```powershell
dotnet restore PrismDemo.sln
dotnet build   PrismDemo.sln -c Debug
```

运行主程序：`PrismDemo.APP\bin\Debug\net8.0-windows\PrismDemo.APP.exe`

**首次运行需要准备的外部依赖**

| 依赖 | 说明 | 配置位置 |
| --- | --- | --- |
| SQL Server | 采集数据、报警、参数表（`PrismDemoDB`） | `PrismDemo.Core/core.config.json` → `Database:ConnectionString` |
| OPC UA 服务 | 现场数据源 | `PrismDemo.Core/core.config.json` → `Opc:Endpoint` |
| 设置页口令 | 进入"设置"页的校验口令 | `PrismDemo.Core/core.config.json` → `SettingPassword:Password` |

缺少上述外部服务时程序仍可启动，界面可浏览，但采集/入库相关功能会报连接失败。

**模块部署与热重载（演示流程）**
1. 编译 `PrismDemo.A` / `PrismDemo.B`，各自构建后会把 `PrismDemo.X.dll` 与 `x.config.json`
   复制到 `PrismDemo.APP\bin\<Configuration>\Modules\`。
2. 启动主程序 → 设置页 → 选择模块源目录（例如模块的 `bin\Debug\net8.0-windows`）→ 扫描。
3. 部署：DLL 以 `PrismDemo.<模块名>.<时间戳>.dll` 形式复制到 `PrismDemo.APP\bin\...\net8.0-windows\modules\`。
4. 重新加载：卸载旧上下文 → 加载新 DLL → 复用 `ServiceProxy` 接缝，界面无需重启。

## 5. 演示版命名约定

- 两个业务模块统一以 **A / B** 标识：模块键（`IModuleSwitch`、导航键、`modules` 目录扫描）均为 `A`、`B`。
- 配置文件：`a.config.json`、`b.config.json`、`core.config.json`。
- 点位 / 数据字段名做了通用化：`A*` 表示 A 模块相关量，`B*` 表示 B 模块相关量，
  `Turbidity`（浊度）、`Outlet`（出厂）、`Effluent`（出水）、`Settled`（沉后）、
  `Front`（沉前）、`Filtered`（滤后）、`UnitRate`（投加率）、`Main` / `Standby`（主/备）等为通用过程词。
- `core.config.json` 中的连接串、口令、OPC 端点为占位值，请按现场实际情况替换。

## 6. 已知事项

- 编译会产生一批 `CS8632`（可空引用类型注释缺少 `#nullable` 上下文）警告，属原始工程遗留，可按需统一开启
  `<Nullable>annotations</Nullable>` 处理。
- `PrismDemo.APP` 构建期创建的是 `bin\<Configuration>\Modules` 目录，
  而运行时扫描的是程序基目录下的 `modules`（即 `bin\...\net8.0-windows\modules`）。
  正式环境请以"设置页部署"流程为准，或自行对齐这两个路径。
