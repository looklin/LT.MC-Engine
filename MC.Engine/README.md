# MC.Engine - 完整功能运动控制流程引擎

类 PLC 启动/暂停/继续/停止功能的完整实现，支持中间件管道和依赖注入

## 概述

这是一个功能完整的运动控制流程引擎，基于 .NET 8 构建，提供：

## 特性

- **高扩展性**：通过 `CommandRegistry` 运行时注册新命令
- **中间件链**：日志、安全、监控等通过中间件插入
- **易于测试**：接口抽象便于 Mock
- **多设备支持**：通过 DI 切换不同 HAL 适配器

## 核心架构

```
┌──────────────────────────────────────────────────────────────┐
│                    Demo Console App                           │
│  Program.cs - 演示启动/暂停/继续/停止流程                      │
└──────────────────────────┬───────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────┐
│              IMotionEngine (接口 + 实现)                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌────────────┐  │
│  │ State    │  │ Middleware│ │  Command │  │   HAL      │  │
│  │ Machine  │  │ Pipeline │  │ Registry │  │ Adapters   │  │
│  └──────────┘  └──────────┘  └──────────┘  └────────────┘  │
└──────────────────────────────────────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────────┐
│              IHalMotionCard (硬件抽象层)                       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                    │
│  │ LeiSai   │  │ Googol   │  │ Simulator│                    │
│  │ Adapter  │  │ Adapter  │  │ Adapter  │                    │
│  └──────────┘  └──────────┘  └──────────┘                    │
└──────────────────────────────────────────────────────────────┘
```

## 快速开始

### 1. 编译类库

```bash
cd src
dotnet build -c Release
```

生成的 DLL 位于 `src/bin/Release/net8.0/MC.Engine.dll`

### 2. 运行 Demo

```bash
cd demo
dotnet run
```

### 3. 在你的项目中使用

```csharp
// 1. 配置服务
var services = new ServiceCollection();

services.AddMotionEngine(options =>
{
    options.DefaultStopMode = StopMode.Smooth;
    options.EnableMiddleware = true;
})
.UseMiddleware<LoggingMiddleware>()
.UseMiddleware<SafetyMiddleware>();

// 注册硬件适配器
services.AddSingleton<IHalMotionCard, LeiSaiAdapter>();

var provider = services.BuildServiceProvider();
var engine = provider.GetRequiredService<IMotionEngine>();

// 2. 订阅事件
engine.StateChanged += (s, e) => 
    Console.WriteLine($"{e.PreviousState} → {e.NewState}");

// 3. 创建任务
var task = new MotionTask
{
    Name = "取料-加工-放料",
    Commands = new List<MotionCommand>
    {
        new HomeCommand(hal) { Axis = 0 },
        new MoveAbsoluteCommand(hal) { Axis = 0, Position = 100.0 },
        new DwellCommand { DurationMs = 500 },
        new MoveAbsoluteCommand(hal) { Axis = 0, Position = 0.0 },
    }
};

// 4. 启动任务
await engine.StartAsync(task);

// 暂停/继续/停止
await engine.PauseAsync();
await engine.ResumeAsync();
await engine.StopAsync(StopMode.Smooth);
```

## 中间件系统

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

## 命令系统

### 内置命令

| 命令 | 说明 |
|------|------|
| `MoveAbsoluteCommand` | 绝对位置移动 |
| `MoveRelativeCommand` | 相对位置移动 |
| `HomeCommand` | 回零操作 |
| `DwellCommand` | 延时等待 |
| `WaitInputCommand` | 等待输入信号 |

### 自定义命令

```csharp
public class MyCustomCommand : MotionCommand
{
    private readonly IHalMotionCard _hal;

    public MyCustomCommand(IHalMotionCard hal)
    {
        _hal = hal;
        Name = "MyCustomCommand";
    }

    public override Task PrepareAsync(IExecutionContext context) 
    {
        // 准备工作
        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync(IExecutionContext context)
    {
        // 核心逻辑
        await _hal.MoveAxisAsync(0, 100, 50, context.CancellationToken);
        await context.ReportProgressAsync(1.0);
    }

    public override Task CleanupAsync(IExecutionContext context) 
    {
        // 清理工作
        return Task.CompletedTask;
    }
}
```

## API 参考

### IMotionEngine 接口

| 方法 | 说明 |
|------|------|
| `StartAsync(task, ct)` | 启动引擎，执行任务 |
| `PauseAsync(ct)` | 暂停引擎 |
| `ResumeAsync(ct)` | 继续引擎 |
| `StopAsync(mode, ct)` | 停止引擎 |
| `ResetAsync(ct)` | 从错误状态复位 |

### 属性

| 属性 | 说明 |
|------|------|
| `State` | 当前引擎状态 |
| `CurrentTask` | 当前执行的任务 |
| `CurrentProgress` | 当前进度 (0.0 ~ 1.0) |

### 事件

| 事件 | 说明 |
|------|------|
| `StateChanged` | 状态变更时触发 |
| `ProgressChanged` | 命令进度变更 |
| `ErrorOccurred` | 错误发生时触发 |

## 项目结构

```
MC.Engine/
├── src/                          # 核心类库
│   ├── MC.Engine.csproj
│   ├── Core/                     # 核心组件
│   │   ├── IMotionEngine.cs      # 接口定义
│   │   ├── MotionEngine.cs       # 主引擎实现
│   │   ├── StateMachine.cs       # 状态机
│   │   └── ExecutionContext.cs   # 执行上下文
│   │
│   ├── Commands/                 # 命令系统
│   │   └── MotionCommands.cs     # 各种运动命令
│   │
│   ├── Middleware/               # 中间件
│   │   └── IMiddleware.cs        # 中间件接口和实现
│   │
│   ├── HAL/                      # 硬件抽象层
│   │   ├── IHalMotionCard.cs     # 接口定义
│   │   └── Adapters/             # 适配器实现
│   │       └── SimulatorAdapter.cs
│   │
│   ├── Common/                   # 公共组件
│   │   ├── Enums.cs
│   │   ├── EventArgs.cs
│   │   └── Exceptions.cs
│   │
│   └── ServiceCollectionExtensions.cs  # DI 扩展
│
├── demo/                         # 控制台演示程序
│   ├── MC.Engine.Demo.csproj
│   └── Program.cs
│
├── docs/                         # 文档
│   └── README.md
│
└── MC.Engine.sln                 # 解决方案
```

## 状态转换图

```
                    ┌──────────────────────────────────────┐
                    │                                      ▼
  ┌─────┐  Start   ┌────────┐  Pause   ┌────────┐  Resume  ┌─────────┐
  │ Idle├─────────►│Running ├─────────►│Paused  ├─────────►│ Running │
  └─────┘          └───┬────┘          └────┬───┘          └────┬────┘
       ▲               │                    │                   │
       │    Stop       │     Stop           │      Stop         │
       │               ▼                    ▼                   ▼
       │          ┌─────────────────────────────────────────────┐
       │          │                 Stopping                     │
       │          └────────────────────┬────────────────────────┘
       │                               │
       │         停止完成/复位         │
       └───────────────────────────────┘
```

## 技术栈

- **.NET 8.0** - 现代 C# 特性
- **Microsoft.Extensions.DependencyInjection** - 依赖注入
- **Microsoft.Extensions.Logging** - 日志抽象
- **System.Threading** - 并发控制

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！
