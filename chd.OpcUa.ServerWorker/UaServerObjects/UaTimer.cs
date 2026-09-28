using chd.OpcUa.Server.ObjectSystem;
using System;
using System.Collections.Generic;
using System.Text;

namespace chd.OpcUa.ServerWorker.UaServerObjects
{
    public class UaTimer(string name) : BaseUaServerObject(name, "Ua Timer Desc")
    {
        [ObjectSystemEvent()]
        public event EventHandler<int> TimerFinished;

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

        private void CreateTimer(int seconds) => Task.Run(async () =>
        {
            Time = seconds;
            State = ETimerState.Running;
            while (Time > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token);
                Time--;
            }
            TimerFinished?.Invoke(this, Time);

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
