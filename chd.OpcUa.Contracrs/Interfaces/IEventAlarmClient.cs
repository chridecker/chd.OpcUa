using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace chd.OpcUa.Contracts.Interfaces
{
    public interface IEventAlarmClient
    {
        event AsyncEventHandler<AlarmEventArgs> AlarmNotification;
        event AsyncEventHandler<SimpleEventArgs> EventNotification;
        Task<bool> AttachToEventsAsync(string node, Dictionary<(string,Type), List<string>> customSelects, CancellationToken cancellationToken = default);

        Task AcknowledgeAsync(uint handle, ReadOnlyMemory<byte> eventId, string comment, CancellationToken cancellationToken);

        Task AddCommentAsync(uint handle, ReadOnlyMemory<byte> eventId, string comment, CancellationToken cancellationToken);

        Task ConfirmAsync(uint handle, ReadOnlyMemory<byte> eventId, string comment, CancellationToken cancellationToken);
    }
}
