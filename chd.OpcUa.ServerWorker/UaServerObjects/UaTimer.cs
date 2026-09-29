using chd.OpcUa.Server.ObjectSystem;
using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Base.System.Attributes;
using Opc.Ua;

namespace chd.OpcUa.ServerWorker.UaServerObjects
{
    public class UaTimer(string name) : BaseUaServerObject(name, "Ua Timer Desc")
    {
        [ObjectSystemEvent()]
        public event EventHandler TimerFinished;

        [ObjectSystemEvent(SeverityMethod = nameof(Sev))]
        public event EventHandler<int> TimerTicked;

        [ObjectSystemEvent()]
        public event EventHandler<string> TimerMode;

        private CancellationTokenSource _cts;

        [ObjectSystemProperty("Time")]
        public int Time { get; set => SetField(ref field, value); }

        [ObjectSystemProperty("TimerState")]
        public ETimerState State { get; set => SetField(ref field, value); }

        [ObjectSystemMethod()]
        public bool StartAsync(int seconds, CancellationToken cancellationToken)
        {
            if (State is not ETimerState.Running)
            {
                this.TimerMode?.Invoke(this, "Timer starting");
                this._cts = new();
                CreateTimer(seconds);
                return true;
            }

            return false;
        }

        [ObjectSystemMethod()]
        public void StopAsync()
        {
            _cts.Cancel();
            Time = 0;
            State = ETimerState.Canceled;
        }

        public EventSeverity Sev(string eventName) => eventName switch
        {
            nameof(TimerTicked) => EventSeverity.High,
            nameof(TimerMode) => EventSeverity.Low,
            _ => EventSeverity.Medium,
        };

        private void CreateTimer(int seconds) => Task.Run(async () =>
        {
            Time = seconds;
            State = ETimerState.Running;
            while (Time > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token);
                this.TimerTicked?.Invoke(this, Time);
                Time--;
            }

            TimerFinished?.Invoke(this, EventArgs.Empty);

            State = ETimerState.Finished;
        }, _cts.Token);
    }

    public enum ETimerState : byte
    {
        Off,
        Running,
        Canceled,
        Finished
    }
}
