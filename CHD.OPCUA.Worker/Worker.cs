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
            client.EventAlarmNotification += Client_EventAlarmNotification; ;
            await client.StartAsync(stoppingToken);

            await client.AttachToEventsAsync("Server", stoppingToken);

            await client.CallMethod("2:CHDTimer1#StartAsync", stoppingToken,10);


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

        private async ValueTask Client_EventAlarmNotification(object? sender, Contracts.EventAlarmEventArgs e, CancellationToken cancellationToken)
        {
            try
            {
                logger?.LogInformation($"Event {e.ConditionName} -> {e.Message} {e.Comment} {e.StateText}");
                //if (!e.Retain)
                //{
                //    logger?.LogInformation($"Event {e.ConditionName} -> {e.Message} {e.Comment} {e.StateText}");
                //    if (e.IsAlarm)
                //    {
                //        await client.AcknowledgeAsync(e.Handle, e.Id, "CHD ACK", cancellationToken);
                //    }
                //}
                //else
                //{
                //    logger?.LogWarning(
                //        $"Event {e.ConditionName} -> {e.Message} {e.Comment} {e.StateText} {e.IsDialog} {e.IsAlarm}");
                //    if (e.IsAlarm && string.IsNullOrWhiteSpace(e.Comment))
                //    {
                //        await client.AddCommentAsync(e.Handle, e.Id, "Test Comment", cancellationToken);
                //    }
                //}
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, ex.Message);
            }
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
