# MC.Engine.Light - 轻量级运动控制流程引擎

类 PLC 启动/暂停/继续/停止功能的轻量实现

## 概述

这是一个基于 C# 的轻量级运动控制流程引擎，模拟 PLC 的执行逻辑，通过状态机驱动多任务协同，支持用户随时启动或暂停整个流程。

适用于与运动控制卡配合使用的非标自动化设备，无需依赖外部硬件 PLC。

## 特性

- **极简设计**：一个 `ManualResetEventSlim` 搞定暂停/继续，`CancellationTokenSource` 搞定停止
- **零依赖**：仅使用 .NET 原生类型，.NET Standard 2.0 起即可用
- **直观易懂**：`switch (step)` 一眼看懂流程走向，工业现场调试方便
- **快速交付**：适合非标设备快速交付，改改步骤就能用
- **DLL 导出**：可编译为类库供其他项目引用

## 核心架构

```
┌─────────────────────────────────────────┐
│           WinForms Demo (UI)             │
│   Start / Pause / Resume / Stop 按钮     │
└──────────────────┬──────────────────────┘
                   │
┌──────────────────▼──────────────────────┐
│         MotionEngine (引擎核心)          │
│  ┌──────────┐  ┌──────────────────────┐ │
│  │ StateMachine │ ExecutionContext   │ │
│  │ 状态机     │ │ ManualResetEvent   │ │
│  │ 转换矩阵   │ │ 闸门控制           │ │
│  └──────────┘  └──────────────────────┘ │
└─────────────────────────────────────────┘
```

## 快速开始

### 1. 编译类库

```bash
cd src
dotnet build -c Release
```

生成的 DLL 位于 `src/bin/Release/netstandard2.0/MC.Engine.Light.dll`

### 2. 运行 Demo

```bash
cd demo
dotnet run
```

### 3. 在你的项目中使用

```csharp
// 引用 DLL
using MC.Engine.Light;

// 创建引擎
var engine = new MotionEngine();

// 订阅事件
engine.StateChanged += (s, e) => 
    Console.WriteLine($"{e.PreviousState} → {e.NewState}");

// 启动任务
await engine.StartAsync(async ctx =>
{
    // 任务A
    await RunTaskA(ctx);
    
    // 任务B
    await RunTaskB(ctx);
});

// 暂停
engine.Pause();

// 继续
engine.Resume();

// 停止
engine.Stop();
```

## 任务编写示例

### 定义步骤枚举

```csharp
public enum MyStep
{
    None = 0,
    Init,
    MoveToPick,
    Pick,
    MoveToPlace,
    Place,
    Done
}
```

### 实现任务逻辑

```csharp
private async Task RunMyTask(ExecutionContext ctx)
{
    var step = MyStep.Init;
    
    while (step != MyStep.Done && !ctx.CancellationToken.IsCancellationRequested)
    {
        ctx.WaitIfPaused();  // 暂停检查点
        
        switch (step)
        {
            case MyStep.Init:
                Console.WriteLine("初始化");
                step = MyStep.MoveToPick;
                break;
                
            case MyStep.MoveToPick:
                await MoveAxisAsync(0, 100);
                step = MyStep.Pick;
                break;
                
            case MyStep.Pick:
                await PickAsync();
                step = MyStep.MoveToPlace;
                break;
                
            case MyStep.MoveToPlace:
                await MoveAxisAsync(0, 200);
                step = MyStep.Place;
                break;
                
            case MyStep.Place:
                await PlaceAsync();
                step = MyStep.Done;
                break;
        }
        
        await Task.Delay(100);
    }
}
```

## 任务间通信

使用 `ExecutionContext` 的条件标志实现任务间同步：

```csharp
// TaskA: 等待 TaskB 完成准备
await ctx.WaitUntilAsync(() => ctx.GetCondition());

// TaskB: 设置准备完成标志
ctx.SetCondition(true);
```

## API 参考

### MotionEngine 类

| 方法 | 说明 |
|------|------|
| `StartAsync(taskRunner, ct)` | 启动引擎，执行任务 |
| `Pause()` | 暂停引擎（关闭闸门） |
| `Resume()` | 继续引擎（打开闸门） |
| `Stop(mode)` | 停止引擎 |
| `Reset()` | 从错误状态复位 |

### ExecutionContext 类

| 方法 | 说明 |
|------|------|
| `WaitIfPaused()` | 暂停检查点，如暂停则阻塞 |
| `WaitUntilAsync(condition, timeout)` | 等待条件满足 |
| `RequestPause()` | 请求暂停 |
| `RequestResume()` | 请求继续 |
| `RequestStop()` | 请求停止 |
| `SetCondition(value)` | 设置条件标志 |
| `GetCondition()` | 获取条件标志 |

### 事件

| 事件 | 说明 |
|------|------|
| `StateChanged` | 状态变更时触发 |
| `StepChanged` | 步骤变更时触发 |
| `ErrorOccurred` | 错误发生时触发 |

## 项目结构

```
MC.Engine.Light/
├── src/                          # 核心类库
│   ├── MC.Engine.Light.csproj    # 项目文件
│   ├── MotionEngine.cs           # 主引擎
│   ├── StateMachine.cs           # 状态机
│   ├── ExecutionContext.cs       # 执行上下文
│   ├── Enums.cs                  # 枚举定义
│   ├── EventArgs.cs              # 事件参数
│   └── Exceptions.cs             # 自定义异常
│
├── demo/                         # WinForms 演示程序
│   ├── MC.Engine.Light.Demo.csproj
│   ├── MainForm.cs               # 主窗体
│   └── Program.cs                # 入口点
│
├── docs/                         # 文档
│   └── README.md
│
└── MC.Engine.Light.sln           # 解决方案
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

- **.NET Standard 2.0** - 最大兼容性
- **System.Threading** - ManualResetEventSlim / CancellationToken
- **WinForms** - Demo UI（.NET 8.0）

## 许可证

MIT License

## 贡献

欢迎提交 Issue 和 Pull Request！
