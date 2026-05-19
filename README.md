# MC.Engine

运动控制流程引擎类库 - **完整功能与轻量级实现**

[![NuGet](https://img.shields.io/badge/nuget-v1.0.0-blue.svg)](https://www.nuget.org/)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

## 特点

- **双版本** - 完整功能版 (MC.Engine) 和轻量级版 (MC.Engine.Light)
- **类PLC控制** - 启动/暂停/继续/停止功能
- **中间件管道** - 可扩展的拦截器链（日志、安全、性能监控）
- **依赖注入** - Microsoft.Extensions.DependencyInjection 集成
- **硬件抽象** - 支持不同品牌运动控制卡
- **事件驱动** - 完整的事件系统用于监控和调试

---

## 快速开始

### 安装

```bash
# 完整功能版 (.NET 8.0)
dotnet add package LT.MC.Engine

# 轻量级版 (.NET Standard 2.0)
dotnet add package LT.MC.Engine.Light
```

### 基础用法（完整功能版）

```csharp
using MC.Engine;

// 1. 配置服务
var services = new ServiceCollection();

services.AddMotionEngine(options =>
{
    options.DefaultStopMode = StopMode.Smooth;
    options.EnableMiddleware = true;
})
.UseMiddleware<LoggingMiddleware>()
.UseMiddleware<SafetyMiddleware>();

// 2. 注册硬件适配器
services.AddSingleton<IHalMotionCard, SimulatorAdapter>();

var provider = services.BuildServiceProvider();
var engine = provider.GetRequiredService<IMotionEngine>();

// 3. 订阅事件
engine.StateChanged += (s, e) => 
    Console.WriteLine($"{e.PreviousState} → {e.NewState}");

// 4. 启动任务
var task = new MotionTask
{
    Name = "取料-加工-放料",
    Commands = new List<MotionCommand>
    {
        new HomeCommand(hal) { Axis = 0 },
        new MoveAbsoluteCommand(hal) { Axis = 0, Position = 100.0 },
        new DwellCommand { DurationMs = 500 },
    }
};

await engine.StartAsync(task);

// 5. 控制执行
await engine.PauseAsync();
await engine.ResumeAsync();
await engine.StopAsync(StopMode.Smooth);
```

### 轻量级版用法

```csharp
using MC.Engine.Light;

// 1. 创建引擎
var engine = new MotionEngine();

// 2. 订阅状态变化
engine.StateChanged += (sender, args) =>
{
    Console.WriteLine($"状态: {args.NewState}");
};

// 3. 启动
engine.Start(() =>
{
    // 你的运动控制逻辑
    Console.WriteLine("执行中...");
});

// 4. 暂停/继续/停止
engine.Pause();
engine.Resume();
engine.Stop();
```

---

## 模块详解

### 一、LT.MC.Engine（完整功能版）

基于 .NET 8.0 构建，提供完整的运动控制功能：

- **中间件管道**：类似 ASP.NET Core 的中间件模式，灵活扩展
- **依赖注入**：通过 DI 容器管理所有依赖
- **硬件抽象层**：兼容不同品牌运动控制卡
- **事件驱动**：完整的事件系统用于监控和调试
- **命令系统**：可扩展的命令注册和执行机制

**适用场景**：
- ASP.NET Core 应用
- 需要复杂运动控制的应用
- 需要中间件和 DI 的项目

### 二、LT.MC.Engine.Light（轻量级版）

基于 .NET Standard 2.0 构建，提供核心的运动控制功能：

- **零依赖**：不依赖任何第三方库
- **跨平台**：支持 .NET Framework 4.6.1+ 和 .NET Core/5/6/7/8
- **简单API**：易于使用，快速集成
- **闸门机制**：基于 ManualResetEvent 实现精确控制

**适用场景**：
- WinForms/WPF 桌面应用
- .NET Framework 遗留项目
- 简单运动控制需求

---

## API 参考

### IMotionEngine 接口（完整功能版）

| 方法 | 说明 |
|------|------|
| `StartAsync(task, ct)` | 启动引擎，执行任务 |
| `PauseAsync(ct)` | 暂停引擎 |
| `ResumeAsync(ct)` | 继续引擎 |
| `StopAsync(mode, ct)` | 停止引擎 |
| `ResetAsync(ct)` | 从错误状态复位 |

### MotionEngine 类（轻量级版）

| 方法 | 说明 |
|------|------|
| `Start(action)` | 启动引擎 |
| `Pause()` | 暂停引擎 |
| `Resume()` | 继续引擎 |
| `Stop()` | 停止引擎 |
| `Reset()` | 复位引擎 |

### 状态枚举

| 状态 | 说明 |
|------|------|
| `Idle` | 空闲状态 |
| `Running` | 运行中 |
| `Paused` | 已暂停 |
| `Stopping` | 停止中 |
| `Error` | 错误状态 |

---

## 中间件系统（完整功能版）

中间件按顺序执行，形成管道：

```
请求 → Middleware1 → Middleware2 → Middleware3 → 命令执行
         ↓              ↓              ↓
       日志           安全           性能监控
```

### 内置中间件

| 中间件 | 说明 |
|--------|------|
| `LoggingMiddleware` | 记录命令执行开始/完成/异常 |
| `SafetyMiddleware` | 异常时触发急停 |
| `PerformanceMonitorMiddleware` | 监控命令执行耗时 |

### 自定义中间件

```csharp
public class MyMiddleware : IMiddleware
{
    public async Task InvokeAsync(IExecutionContext context, Func<Task> next)
    {
        // 执行前逻辑
        Console.WriteLine($"执行前: {context.CurrentCommand?.Name}");
        
        await next();  // 执行下一个中间件
        
        // 执行后逻辑
        Console.WriteLine($"执行后: {context.CurrentCommand?.Name}");
    }
}

// 注册
services.AddMiddleware<MyMiddleware>();
```

---

## 硬件抽象层

### 接口定义

```csharp
public interface IHalMotionCard : IDisposable
{
    Task MoveAxisAsync(int axisIndex, double targetPosition, double velocity, CancellationToken ct);
    Task StopAxisAsync(int axisIndex, StopMode mode, CancellationToken ct);
    Task EmergencyStopAllAsync(CancellationToken ct);
    Task<AxisStatus> GetAxisStatusAsync(int axisIndex, CancellationToken ct);
    Task<bool> ReadInputAsync(int inputIndex, CancellationToken ct);
    Task WriteOutputAsync(int outputIndex, bool value, CancellationToken ct);
}
```

### 实现适配器

项目中已包含 `SimulatorAdapter`（仿真器），用于开发测试。

实现你自己的适配器：

```csharp
public class LeiSaiAdapter : IHalMotionCard
{
    public async Task MoveAxisAsync(int axisIndex, double targetPosition, double velocity, CancellationToken ct)
    {
        // 调用雷赛控制卡 API
        // GTN.mcProfileStart(...)
    }
    
    // ... 其他实现
}

// 注册
services.AddSingleton<IHalMotionCard, LeiSaiAdapter>();
```

---

## 项目结构

```
MC-Engine/
├── MC.Engine/                      # 完整功能版
│   ├── src/                        # 核心类库
│   │   ├── Core/                   # 核心组件
│   │   ├── Commands/               # 命令系统
│   │   ├── Middleware/             # 中间件
│   │   ├── HAL/                    # 硬件抽象层
│   │   └── Common/                 # 公共组件
│   └── demo/                       # 控制台演示程序
│
├── MC.Engine.Light/                # 轻量级版
│   ├── src/                        # 核心类库
│   └── demo/                       # WinForms 演示程序
│
├── docs/                           # 文档
│   └── icon.png
│
└── nupkgs/                         # NuGet 包输出目录
```

---

## 技术栈

### LT.MC.Engine
- **.NET 8.0** - 现代 C# 特性
- **Microsoft.Extensions.DependencyInjection** - 依赖注入
- **Microsoft.Extensions.Logging** - 日志抽象
- **System.Threading** - 并发控制

### LT.MC.Engine.Light
- **.NET Standard 2.0** - 最大兼容性
- **零依赖** - 不依赖任何第三方库
- **ManualResetEvent** - 闸门机制

---

## 编译和打包

### 编译

```bash
# 编译完整功能版
cd MC.Engine/src
dotnet build -c Release

# 编译轻量级版
cd MC.Engine.Light/src
dotnet build -c Release
```

### 打包 NuGet

```bash
# 打包完整功能版
cd MC.Engine/src
dotnet pack -c Release

# 打包轻量级版
cd MC.Engine.Light/src
dotnet pack -c Release

# NuGet 包将输出到根目录的 nupkgs/ 文件夹
```

---

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！
