using chd.OpcUa.Base.States;
using chd.OpcUa.Client;
using chd.OpcUa.Contracts.Interfaces;
using Opc.Ua;

namespace chd.OpcUa.Worker
{
    public class Worker(ILogger<Worker> logger, IOpcUAClient client) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            client.MonitoredItemNotification += Client_MonitoredItemNotification;
            client.EventNotification += Client_EventNotification;
            client.AlarmNotification += Client_AlarmNotification; ;
            await client.StartAsync(stoppingToken);


            var additional = new Dictionary<(string, Type), List<string>>();
            additional[($"ns=2;s=5:{nameof(SimpleValueCustomEventState).Replace("State", "Type")}", typeof(SimpleValueCustomEventState))] = ["Value"];
            additional[($"ns=2;s=5:{nameof(ComplexValueCustomEventState).Replace("State", "Type")}", typeof(ComplexValueCustomEventState))] = ["Value"];
            additional[($"ns=2;s=5:{nameof(ObjectValueCustomEventState).Replace("State", "Type")}", typeof(ObjectValueCustomEventState))] = ["Value"];

            await client.AttachToEventsAsync("1:CHDTimer1", additional, stoppingToken);
            await client.AttachToAlarmsAsync("1:CHDTimer1", new Dictionary<(string, Type), List<string>>(), stoppingToken);

            await client.CallMethod("2:CHDTimer1#StartAsync", stoppingToken, 1);


            await client.MonitorItem("1:CHDTimer1?Time", 500, stoppingToken);
            //await client.MonitorItem("1:CC1001?Input2", 5000, stoppingToken);
            //await client.MonitorItem("2:State", 500, stoppingToken);
            //var val = await client.ReadAsync<uint>("2:State", stoppingToken);

            //var input = new ProcessStartInput(val, val == 0 ? val + (uint)10 : 0);

            //var output = await client.CallMethod<ProcessStartInput, ProcessStartOutput>("2:Start", input, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {

                //var val = await client.ReadAsync<float>("1:CC1001?Input1", stoppingToken);

                //await client.WriteAsync("1:CC1001?Input1", ++val, stoppingToken);
                await Task.Delay(1000, stoppingToken);

                //client.RemoveMonitorItem("1:CC1001?Input2");
            }
        }

        private async ValueTask Client_AlarmNotification(object? sender, Contracts.AlarmEventArgs e, CancellationToken cancellationToken = default)
        {
            logger?.LogInformation($"Alarm {e.SourceName} -> {e.Message} {e.Acknowledged} {e.Confirmed} {e.Comment} {e.Type} {e.Time}");
            if (e.Acknowledged)
            {
                await client.ConfirmAsync(e.Handle, new ReadOnlyMemory<byte>(e.Id), e.Comment, cancellationToken);
            }
        }

        private async ValueTask Client_EventNotification(object? sender, Contracts.SimpleEventArgs e, CancellationToken cancellationToken = default)
        {
            logger?.LogInformation($"Event {e.SourceName} -> {e.Message} {e.Value} {e.Type} {e.Time}");
        }

        private async ValueTask Client_MonitoredItemNotification(object? sender, Contracts.MonitoredItemEventArgs e, CancellationToken cancellationToken)
        {
            logger?.LogInformation($"Item {e.Node} [{e.Value}]");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await client.StopAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
