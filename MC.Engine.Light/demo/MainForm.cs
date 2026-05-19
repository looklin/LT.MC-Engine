using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using MC.Engine.Light;

namespace MC.Engine.Light.Demo
{
    public partial class MainForm : Form
    {
        private readonly MotionEngine _engine;
        private StepA _stepA = StepA.None;
        private StepB _stepB = StepB.None;

        public MainForm()
        {
            InitializeComponent();
            _engine = new MotionEngine();

            // 订阅事件
            _engine.StateChanged += Engine_StateChanged;
            _engine.StepChanged += Engine_StepChanged;
            _engine.ErrorOccurred += Engine_ErrorOccurred;
        }

        #region 按钮事件

        private void BtnStart_Click(object sender, EventArgs e)
        {
            try
            {
                Log(">>> 启动流程");

                // 初始化步骤
                if (_stepA == StepA.None) _stepA = StepA.StepA0;
                if (_stepB == StepB.None) _stepB = StepB.StepB0;

                // 启动引擎
                _ = Task.Run(() => _engine.StartAsync(async ctx =>
                {
                    await Task.WhenAll(RunTaskA(ctx), RunTaskB(ctx));
                }));
            }
            catch (Exception ex)
            {
                Log($"启动失败: {ex.Message}");
            }
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            try
            {
                _engine.Pause();
                Log(">>> 暂停流程");
            }
            catch (Exception ex)
            {
                Log($"暂停失败: {ex.Message}");
            }
        }

        private void BtnResume_Click(object sender, EventArgs e)
        {
            try
            {
                _engine.Resume();
                Log(">>> 继续流程");
            }
            catch (Exception ex)
            {
                Log($"继续失败: {ex.Message}");
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            try
            {
                _engine.Stop();
                Log(">>> 停止流程");
            }
            catch (Exception ex)
            {
                Log($"停止失败: {ex.Message}");
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            try
            {
                _engine.Reset();
                Log(">>> 复位引擎");
            }
            catch (Exception ex)
            {
                Log($"复位失败: {ex.Message}");
            }
        }

        #endregion

        #region 任务A执行逻辑

        private async Task RunTaskA(MC.Engine.Light.ExecutionContext ctx)
        {
            string previousStep = "None";

            while (_stepA != StepA.None && !ctx.CancellationToken.IsCancellationRequested)
            {
                ctx.WaitIfPaused();

                switch (_stepA)
                {
                    case StepA.StepA0:
                        Log("TaskA: 初始化，移动到原点");
                        await SimulateMove(0, 0);
                        previousStep = _stepA.ToString();
                        _stepA = StepA.StepA1;
                        break;

                    case StepA.StepA1:
                        Log("TaskA: 等待 TaskB 完成准备...");
                        await ctx.WaitUntilAsync(() => ctx.GetCondition());
                        previousStep = _stepA.ToString();
                        _stepA = StepA.StepA2;
                        break;

                    case StepA.StepA2:
                        Log("TaskA: 移动到工作位置 (100mm)");
                        await SimulateMove(0, 100);
                        previousStep = _stepA.ToString();
                        _stepA = StepA.StepA3;
                        break;

                    case StepA.StepA3:
                        Log("TaskA: 执行加工操作");
                        await SimulateWork(1000);
                        previousStep = _stepA.ToString();
                        _stepA = StepA.StepA4;
                        break;

                    case StepA.StepA4:
                        Log("TaskA: 返回原点");
                        await SimulateMove(0, 0);
                        previousStep = _stepA.ToString();
                        _stepA = StepA.None;
                        break;
                }

                if (_stepA != StepA.None)
                {
                    _engine.ReportStepChanged("TaskA", previousStep, _stepA.ToString());
                    await Task.Delay(100, ctx.CancellationToken);
                }
            }
        }

        #endregion

        #region 任务B执行逻辑

        private async Task RunTaskB(MC.Engine.Light.ExecutionContext ctx)
        {
            string previousStep = "None";

            while (_stepB != StepB.None && !ctx.CancellationToken.IsCancellationRequested)
            {
                ctx.WaitIfPaused();

                switch (_stepB)
                {
                    case StepB.StepB0:
                        Log("TaskB: 初始化夹具");
                        await SimulateMove(1, 0);
                        previousStep = _stepB.ToString();
                        _stepB = StepB.StepB1;
                        break;

                    case StepB.StepB1:
                        Log("TaskB: 设置准备完成标志，通知 TaskA");
                        ctx.SetCondition(true);
                        previousStep = _stepB.ToString();
                        _stepB = StepB.StepB2;
                        break;

                    case StepB.StepB2:
                        Log("TaskB: 移动到辅助位置 (50mm)");
                        await SimulateMove(1, 50);
                        previousStep = _stepB.ToString();
                        _stepB = StepB.StepB3;
                        break;

                    case StepB.StepB3:
                        Log("TaskB: 等待 TaskA 完成加工");
                        await Task.Delay(2000, ctx.CancellationToken);
                        previousStep = _stepB.ToString();
                        _stepB = StepB.None;
                        break;
                }

                if (_stepB != StepB.None)
                {
                    _engine.ReportStepChanged("TaskB", previousStep, _stepB.ToString());
                    await Task.Delay(100, ctx.CancellationToken);
                }
            }
        }

        #endregion

        #region 事件处理

        private void Engine_StateChanged(object? sender, StateChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStateLabel(e.NewState)));
            }
            else
            {
                UpdateStateLabel(e.NewState);
            }

            Log($"状态变更: {e.PreviousState} → {e.NewState}");
        }

        private void Engine_StepChanged(object? sender, StepChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    if (e.TaskName == "TaskA")
                        LblStateA.Text = e.CurrentStep;
                    else
                        LblStateB.Text = e.CurrentStep;
                }));
            }
            else
            {
                if (e.TaskName == "TaskA")
                    LblStateA.Text = e.CurrentStep;
                else
                    LblStateB.Text = e.CurrentStep;
            }
        }

        private void Engine_ErrorOccurred(object? sender, ErrorEventArgs e)
        {
            Log($"错误: {e.ErrorMessage} [{e.Severity}]");
        }

        #endregion

        #region 辅助方法

        private void UpdateStateLabel(EngineState state)
        {
            LblState.Text = state.ToString();
            LblState.ForeColor = state switch
            {
                EngineState.Idle => System.Drawing.Color.Gray,
                EngineState.Running => System.Drawing.Color.Green,
                EngineState.Paused => System.Drawing.Color.Orange,
                EngineState.Stopping => System.Drawing.Color.Red,
                EngineState.Error => System.Drawing.Color.DarkRed,
                _ => System.Drawing.Color.Black
            };
        }

        private void Log(string message)
        {
            if (TxtLog.InvokeRequired)
            {
                TxtLog.Invoke(new Action(() =>
                {
                    TxtLog.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
                }));
            }
            else
            {
                TxtLog.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }

        private async Task SimulateMove(int axis, double position)
        {
            // 模拟运动延迟
            await Task.Delay(500);
            Log($"  [轴{axis}] 移动到 {position}mm");
        }

        private async Task SimulateWork(int durationMs)
        {
            await Task.Delay(durationMs);
            Log("  加工完成");
        }

        #endregion

        #region 窗体设计器生成代码（省略，实际项目中由设计器生成）

        private System.Windows.Forms.Button BtnStart;
        private System.Windows.Forms.Button BtnPause;
        private System.Windows.Forms.Button BtnResume;
        private System.Windows.Forms.Button BtnStop;
        private System.Windows.Forms.Button BtnReset;
        private System.Windows.Forms.TextBox TxtLog;
        private System.Windows.Forms.Label LblState;
        private System.Windows.Forms.Label LblStateA;
        private System.Windows.Forms.Label LblStateB;
        private System.Windows.Forms.GroupBox GrpTaskA;
        private System.Windows.Forms.GroupBox GrpTaskB;
        private System.Windows.Forms.Label LblTitleA;
        private System.Windows.Forms.Label LblTitleB;

        private void InitializeComponent()
        {
            this.BtnStart = new System.Windows.Forms.Button();
            this.BtnPause = new System.Windows.Forms.Button();
            this.BtnResume = new System.Windows.Forms.Button();
            this.BtnStop = new System.Windows.Forms.Button();
            this.BtnReset = new System.Windows.Forms.Button();
            this.TxtLog = new System.Windows.Forms.TextBox();
            this.LblState = new System.Windows.Forms.Label();
            this.LblStateA = new System.Windows.Forms.Label();
            this.LblStateB = new System.Windows.Forms.Label();
            this.GrpTaskA = new System.Windows.Forms.GroupBox();
            this.LblTitleA = new System.Windows.Forms.Label();
            this.GrpTaskB = new System.Windows.Forms.GroupBox();
            this.LblTitleB = new System.Windows.Forms.Label();
            this.GrpTaskA.SuspendLayout();
            this.GrpTaskB.SuspendLayout();
            this.SuspendLayout();
            // 
            // BtnStart
            // 
            this.BtnStart.Location = new System.Drawing.Point(12, 12);
            this.BtnStart.Name = "BtnStart";
            this.BtnStart.Size = new System.Drawing.Size(75, 30);
            this.BtnStart.TabIndex = 0;
            this.BtnStart.Text = "Start";
            this.BtnStart.UseVisualStyleBackColor = true;
            this.BtnStart.Click += new System.EventHandler(this.BtnStart_Click);
            // 
            // BtnPause
            // 
            this.BtnPause.Location = new System.Drawing.Point(93, 12);
            this.BtnPause.Name = "BtnPause";
            this.BtnPause.Size = new System.Drawing.Size(75, 30);
            this.BtnPause.TabIndex = 1;
            this.BtnPause.Text = "Pause";
            this.BtnPause.UseVisualStyleBackColor = true;
            this.BtnPause.Click += new System.EventHandler(this.BtnPause_Click);
            // 
            // BtnResume
            // 
            this.BtnResume.Location = new System.Drawing.Point(174, 12);
            this.BtnResume.Name = "BtnResume";
            this.BtnResume.Size = new System.Drawing.Size(75, 30);
            this.BtnResume.TabIndex = 2;
            this.BtnResume.Text = "Resume";
            this.BtnResume.UseVisualStyleBackColor = true;
            this.BtnResume.Click += new System.EventHandler(this.BtnResume_Click);
            // 
            // BtnStop
            // 
            this.BtnStop.Location = new System.Drawing.Point(255, 12);
            this.BtnStop.Name = "BtnStop";
            this.BtnStop.Size = new System.Drawing.Size(75, 30);
            this.BtnStop.TabIndex = 3;
            this.BtnStop.Text = "Stop";
            this.BtnStop.UseVisualStyleBackColor = true;
            this.BtnStop.Click += new System.EventHandler(this.BtnStop_Click);
            // 
            // BtnReset
            // 
            this.BtnReset.Location = new System.Drawing.Point(336, 12);
            this.BtnReset.Name = "BtnReset";
            this.BtnReset.Size = new System.Drawing.Size(75, 30);
            this.BtnReset.TabIndex = 4;
            this.BtnReset.Text = "Reset";
            this.BtnReset.UseVisualStyleBackColor = true;
            this.BtnReset.Click += new System.EventHandler(this.BtnReset_Click);
            // 
            // TxtLog
            // 
            this.TxtLog.Location = new System.Drawing.Point(12, 60);
            this.TxtLog.Multiline = true;
            this.TxtLog.Name = "TxtLog";
            this.TxtLog.ReadOnly = true;
            this.TxtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.TxtLog.Size = new System.Drawing.Size(600, 300);
            this.TxtLog.TabIndex = 5;
            // 
            // LblState
            // 
            this.LblState.AutoSize = true;
            this.LblState.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold);
            this.LblState.Location = new System.Drawing.Point(12, 370);
            this.LblState.Name = "LblState";
            this.LblState.Size = new System.Drawing.Size(46, 20);
            this.LblState.TabIndex = 6;
            this.LblState.Text = "Idle";
            // 
            // GrpTaskA
            // 
            this.GrpTaskA.Controls.Add(this.LblTitleA);
            this.GrpTaskA.Controls.Add(this.LblStateA);
            this.GrpTaskA.Location = new System.Drawing.Point(160, 360);
            this.GrpTaskA.Name = "GrpTaskA";
            this.GrpTaskA.Size = new System.Drawing.Size(200, 40);
            this.GrpTaskA.TabIndex = 7;
            this.GrpTaskA.TabStop = false;
            this.GrpTaskA.Text = "TaskA";
            // 
            // LblTitleA
            // 
            this.LblTitleA.AutoSize = true;
            this.LblTitleA.Location = new System.Drawing.Point(6, 16);
            this.LblTitleA.Name = "LblTitleA";
            this.LblTitleA.Size = new System.Drawing.Size(47, 13);
            this.LblTitleA.TabIndex = 0;
            this.LblTitleA.Text = "Current:";
            // 
            // LblStateA
            // 
            this.LblStateA.AutoSize = true;
            this.LblStateA.Location = new System.Drawing.Point(60, 16);
            this.LblStateA.Name = "LblStateA";
            this.LblStateA.Size = new System.Drawing.Size(35, 13);
            this.LblStateA.TabIndex = 1;
            this.LblStateA.Text = "None";
            // 
            // GrpTaskB
            // 
            this.GrpTaskB.Controls.Add(this.LblTitleB);
            this.GrpTaskB.Controls.Add(this.LblStateB);
            this.GrpTaskB.Location = new System.Drawing.Point(370, 360);
            this.GrpTaskB.Name = "GrpTaskB";
            this.GrpTaskB.Size = new System.Drawing.Size(200, 40);
            this.GrpTaskB.TabIndex = 8;
            this.GrpTaskB.TabStop = false;
            this.GrpTaskB.Text = "TaskB";
            // 
            // LblTitleB
            // 
            this.LblTitleB.AutoSize = true;
            this.LblTitleB.Location = new System.Drawing.Point(6, 16);
            this.LblTitleB.Name = "LblTitleB";
            this.LblTitleB.Size = new System.Drawing.Size(47, 13);
            this.LblTitleB.TabIndex = 0;
            this.LblTitleB.Text = "Current:";
            // 
            // LblStateB
            // 
            this.LblStateB.AutoSize = true;
            this.LblStateB.Location = new System.Drawing.Point(60, 16);
            this.LblStateB.Name = "LblStateB";
            this.LblStateB.Size = new System.Drawing.Size(35, 13);
            this.LblStateB.TabIndex = 1;
            this.LblStateB.Text = "None";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 411);
            this.Controls.Add(this.GrpTaskB);
            this.Controls.Add(this.GrpTaskA);
            this.Controls.Add(this.LblState);
            this.Controls.Add(this.TxtLog);
            this.Controls.Add(this.BtnReset);
            this.Controls.Add(this.BtnStop);
            this.Controls.Add(this.BtnResume);
            this.Controls.Add(this.BtnPause);
            this.Controls.Add(this.BtnStart);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "MC Engine Light Demo";
            this.GrpTaskA.ResumeLayout(false);
            this.GrpTaskA.PerformLayout();
            this.GrpTaskB.ResumeLayout(false);
            this.GrpTaskB.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }

    #region 步骤枚举定义

    public enum StepA
    {
        None = 0,
        StepA0,
        StepA1,
        StepA2,
        StepA3,
        StepA4
    }

    public enum StepB
    {
        None = 0,
        StepB0,
        StepB1,
        StepB2,
        StepB3
    }

    #endregion
}
