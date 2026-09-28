using Microsoft.Extensions.Logging;
using Opc.Ua;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using chd.OpcUa.Base.Extensions;
using chd.OpcUa.Contracts;

namespace chd.OpcUa.Server.UnderlyingSystem
{
    public class UnderlyingSystemBlock : UnderlyingSystemBase
    {
        private readonly ConcurrentBag<UnderlyingSystemTag> _tags = [];
        private readonly ConcurrentBag<UnderlyingSystemMethod> _methods = [];
        private readonly ConcurrentBag<UnderlyingSystemEvent> _events = [];

        private event EventHandler<UnderlyingSystemTag> OnTagsChanged;
        private Func<UnderlyingSystemEvent, CancellationToken, ValueTask> OnEventTriggered;

        public string BlockType { get; set; }
        public DateTime Timestamp { get; set; }


        public UnderlyingSystemBlock(string name, string description, string blockType) : base(name)
        {
            BlockType = blockType;
            Description = description;
        }

        public void AddEvent(string name, string description)
        {
            var evt = new UnderlyingSystemEvent(name, description);
            _events.Add(evt);
        }

        public void AddMethod(string name, string description, bool canExecute, Action<UnderlyingSystemMethod> handleMethod = null)
        {
            var method = new UnderlyingSystemMethod(name, description, canExecute, this, this.MethodExecuted);
            handleMethod?.Invoke(method);
            _methods.Add(method);
        }


        public async ValueTask CreateTag(Type type, string tagName, string description, bool writeable, string[] labels = null,
            Func<CancellationToken, ValueTask<Variant>> readValue = null,
            Func<Variant, CancellationToken, ValueTask> writeValue = null,
            CancellationToken cancellationToken = default)
        {
            var tag = new UnderlyingSystemTag(tagName, writeable)
            {
                Block = this,
                Description = description,
                Type = type,
                Labels = labels,
                ReadFunc = readValue,
                WriteFunc = writeValue
            };
            if (readValue is null)
            {
                tag.Value = tag.Type.IsEnum ? 0 : tag.Type.IsValueType ? new Variant(Activator.CreateInstance(tag.Type)) : null;
            }
            else
            {
                tag.Value = await readValue.Invoke(cancellationToken);
            }


            _tags.Add(tag);
            Timestamp = DateTime.UtcNow;
        }

        public void CreateTag<T>(string tagName, string description, bool writeable, string[] labels = null)
            => CreateTag(typeof(T), tagName, description, writeable, labels);


        public IList<UnderlyingSystemTag> GetTags() => _tags.Select(s => s.CreateSnapshot()).ToList();

        public IList<UnderlyingSystemMethod> GetMethods() => _methods.Select(s => s.CreateSnapshot()).ToList();

        public IList<UnderlyingSystemEvent> GetEvents() => _events.Select(s => s.CreateSnapshot()).ToList();

        public async ValueTask<StatusCode> WriteTagValueAsync(string tagName, Variant value, CancellationToken cancellationToken)
        {
            var tag = _tags.FirstOrDefault(x => x.Name == tagName);

            if (tag is null)
            {
                return StatusCodes.BadNodeIdUnknown;
            }

            var val = value.GetValue();
            if (val.GetType() != tag.Type)
            {
                return StatusCodes.BadTypeMismatch;
            }

            tag.Value = new Variant(val);

            tag.Timestamp = DateTime.UtcNow;

            if (tag.WriteFunc is not null)
            {
                await tag.WriteFunc.Invoke(tag.Value, cancellationToken);
            }

            OnTagsChanged?.Invoke(this, tag);

            return StatusCodes.Good;
        }

        public async ValueTask<(StatusCode, Variant)> ReadTagValueAsync(string tagName, CancellationToken cancellationToken)
        {
            var tag = _tags.FirstOrDefault(x => x.Name == tagName);

            if (tag is null)
            {
                return (StatusCodes.BadNodeIdUnknown, Variant.Null);
            }

            if (tag.ReadFunc is not null)
            {
                tag.Value = await tag.ReadFunc.Invoke(cancellationToken);
            }

            return (StatusCodes.Good, tag.Value);
        }


        public void StartMonitoring(EventHandler<UnderlyingSystemTag> callback)
        {
            OnTagsChanged = callback;
        }
        public void StopMonitoring()
        {
            OnTagsChanged = null;
        }

        public void SubscribeEvents(Func<UnderlyingSystemEvent, CancellationToken, ValueTask> callback)
        {
            OnEventTriggered = callback;
        }

        public void UnSubscribeEvents()
        {
            OnEventTriggered = null;
        }

        public void TagChanged(string tagName)
        {
            var tag = _tags.FirstOrDefault(x => x.Name == tagName);
            if (tag is not null)
            {
                OnTagsChanged?.Invoke(this, tag);
            }
        }

        public ValueTask TriggerEvent(string eventIdentifier, object value, CancellationToken cancellationToken)
        {
            var evt = _events.FirstOrDefault(x => x.Identifier == eventIdentifier);
            if (evt is not null
                && OnEventTriggered is not null)
            {
                evt.Message = value is string message ? message : $"{eventIdentifier} fired";
                evt.Value = value;
                return OnEventTriggered(evt, cancellationToken);
            }

            return ValueTask.CompletedTask;
        }

        public event UnderlyingSystemMethodExcutionHandler MethodExecution;


        private ValueTask<object[]> MethodExecuted(UnderlyingSystemMethod method, object[] inputs, CancellationToken cancellationToken)
        {
            if (MethodExecution is not null)
            {
                return MethodExecution.Invoke(method, inputs, cancellationToken);
            }

            return ValueTask.FromResult(Array.Empty<object>());
        }
    }

    public delegate ValueTask<object[]> UnderlyingSystemMethodExcutionHandler(UnderlyingSystemMethod method,
        object[] inputs, CancellationToken cancellationToken);
}
