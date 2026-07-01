# JulyGame

跨项目复用的游戏业务 System 层（`com.july.game`）。基于 JulyArch 的 Store + System 模式，提供 UI 管理、存档、网络、音频等基础设施 System，以及活动/任务/引导等业务脚手架。

> **本文档描述框架的真实行为，与 `Runtime/` 代码一一对应。**

## 架构定位

```
JulyArch (Store + System + Procedure + View)
    │
    └── JulyGame (具体 System 实现 + 业务脚手架)
            │
            └── 项目层 (继承 Scaffold、注册 Provider)
```

JulyGame 的 System 均继承 `SystemBase`，通过 `ArchContext.RegisterSystem<T>()` 注册，业务侧通过 `GetSystem<T>()` 访问。

## 模块概览

### Business Scaffolds（抽象基类，项目继承实现）

| 基类 | 职责 |
|------|------|
| `ActivitySystemBase` | 活动生命周期、状态机驱动 |
| `TaskSystemBase` | 任务条件/解锁/重置策略 |
| `GuideSystemBase` | 新手引导步骤编排 |
| `ABTestSystemBase` | AB 实验分组与策略 |
| `RedDotSystemBase` | 红点树注册与刷新 |

### UI

| 组件 | 说明 |
|------|------|
| `UISystem` | 窗口 Open/Close、层级管理、遮罩、预加载 |
| `UIView` | 面板基类（`OnBeforeOpen` / `OnOpen` / `OnClose` / `OnAfterClose`） |
| `IUIAnimationStrategy` | 动画策略：Fade / Scale / Slide / Animator / None |
| `TipManager` | Toast / 飘字提示 |

### Infrastructure

| System | 接口 | 说明 |
|--------|------|------|
| `SaveSystem` / `LocalFileSaveSystem` | `ISaveSystem` | 存档读写（抽象 + 本地文件实现） |
| `HttpSystem` | `IHttpSystem` | HTTP 请求封装 |
| `AudioSystem` | `IAudioSystem` | 音效/BGM 播放 |
| `TimeSystem` | `ITimeSystem` | 服务器时间同步 |
| `PoolSystem` | `IPoolSystem` | GameObject 对象池 |
| `ConfigSystem` | `IConfigSystem` | 配置表加载与查询 |
| `LocalizationSystem` | `ILocalizationSystem` | 多语言 |
| `JsonSerializeSystem` | `ISerializeSystem` | JSON 序列化 |
| `AesEncryptionSystem` / `NoEncryptionSystem` | `IEncryptionSystem` | 数据加解密 |
| `InputSystem` | `IInputSystem` | 输入管理 |
| `SceneSystem` | `ISceneSystem` | 场景加载 |
| `PerformanceSystem` | `IPerformanceSystem` | 性能监控 |
| `FsmSystem` | `IFsmSystem` | 有限状态机 |
| `UnityResourceSystem` | `IResourceSystem` | 资源加载（Unity 实现） |

### Platform / Analytics / Debug

| 组件 | 说明 |
|------|------|
| `IPlatformSystem` | 平台 SDK 抽象（登录、分享、支付等） |
| `IAnalyticsSystem` | 埋点接口（仅契约，项目实现） |
| GM 系统 | `Runtime/GM/`，`#if JULYGF_DEBUG` 条件编译，Release 构建自动剥离 |

## 共享模式

### ISupportMultipleSource&lt;T&gt;

支持主/备双 Provider 的 System 使用此接口（如 `ConfigSystem`、`LocalizationSystem`）：

```csharp
public interface ISupportMultipleSource<T> where T : IDataProvider
{
    T MainProvider { get; }
    T AdditionalProvider { get; }       // 热更/补丁 Provider
    void SetMainProvider(T provider);
    void SetAdditionalProvider(T provider);
    void UnsetAdditionalProvider(T provider);
}
```

### IDataProvider

空标记接口，具体 Provider（如 `IConfigProvider`、`ILocalizationDataProvider`）继承它，由项目层实现数据加载逻辑。

## 使用示例

```csharp
// 注册（IHotUpdateRegistrar.Register 中）
ctx.RegisterSystem<UISystem>();
ctx.RegisterSystem<AudioSystem>();
ctx.RegisterSystem(new ConfigSystem());

// 使用
var ui = GetSystem<UISystem>();
var window = await ui.OpenAsync<UISettingsWindow>(windowId: 1001, data: settings);

var audio = GetSystem<AudioSystem>();
audio.PlaySfx("click");

// 业务脚手架 — 项目继承
public class MyTaskSystem : TaskSystemBase
{
    protected override void OnInitialize()
    {
        // 注册任务条件、解锁规则
    }
}
```

## UIView 生命周期

```
UISystem.OpenAsync
  → InternalSetData → InternalBeforeOpen → [open 动画]
  → InternalOpen (OnOpen，推荐在此 Subscribe 事件)
  → [用户交互]
  → InternalClose (UnsubscribeAll → OnClose)
  → [close 动画] → InternalAfterClose
```

## 约定

| 约定 | 说明 |
|------|------|
| System 通过接口 + 实现注册 | 便于 Mock 和替换 |
| Scaffold 必须继承而非直接使用 | 业务逻辑在项目层 |
| UIView 不标记 ISingletonView | 由 UISystem 管理生命周期 |
| Provider 在项目层实现 | JulyGame 只提供 System 骨架 |

## 依赖

- `com.july.arch` — SystemBase、ArchContext
- `com.july.common` — FrameworkResult、JLogger
- `com.july.events` — 事件总线
- UniTask、DOTween、TextMeshPro（asmdef 引用）

程序集：`JulyGame.Runtime`。
