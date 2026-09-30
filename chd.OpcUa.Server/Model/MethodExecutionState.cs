using chd.OpcUa.ServerWorker;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Base.Extensions;
using chd.OpcUa.Server.UnderlyingSystem;

namespace chd.OpcUa.Server.Model
{
    public class MethodExecutionState : MethodState
    {
        private readonly NodeManager _nodeManager;
        private readonly UnderlyingSystemMethod _method;

        public MethodExecutionState(NodeManager nodeManager, NodeId nodeId, UnderlyingSystemMethod method, NodeState? parent) : base(parent)
        {
            _nodeManager = nodeManager;
            _method = method;

            NodeId = nodeId;
            BrowseName = new QualifiedName(method.Name, _nodeManager.NamespaceIndex);
            SymbolicName = method.Name;
            DisplayName = LocalizedText.From(method.Name);
            Description = LocalizedText.Null;
            ReferenceTypeId = ReferenceTypeIds.HasComponent;
            UserExecutable = true;
            Executable = _method.CanExecute;
            

            if (_method.InputArguments.Any())
            {
                this.InputArguments = new PropertyInputArgumentState(method,
                    ModelUtils.ConstructIdForInputArguments(_method.Identifier, _nodeManager.NamespaceIndex), this);
            }

            if (_method.OutputArguments.Any())
            {
                this.OutputArguments = new PropertyOutputArgumentState(method, ModelUtils.ConstructIdForOutputArguments(_method.Identifier, _nodeManager.NamespaceIndex), this);
            }

            OnCallMethod2Async = OnExecuteAsync;
        }

        private async ValueTask<ServiceResult> OnExecuteAsync(
            ISystemContext context,
            MethodState method,
            NodeId objectId,
            ArrayOf<Variant> inputArguments,
            List<Variant> outputArguments,
            CancellationToken cancellationToken = default)
        {
            // all arguments must be provided.
            if (inputArguments.Count != _method.InputArguments.Count || outputArguments.Count != _method.OutputArguments.Count)
            {
                return StatusCodes.BadArgumentsMissing;
            }

            for (int i = 0; i < inputArguments.Count; i++)
            {
                var val = inputArguments[0].GetValue();

                if (val.GetType() != _method.InputArguments.ElementAt(i).Type)
                {
                    return StatusCodes.BadTypeMismatch;
                }
            }
            for (int i = 0; i < outputArguments.Count; i++)
            {
                var val = outputArguments[0].GetValue();

                if (val.GetType() != _method.OutputArguments.ElementAt(i).Type)
                {
                    return StatusCodes.BadTypeMismatch;
                }
            }

            return await _method.ExecuteAsync(inputArguments.ToList().Select(s => s.GetValue()).ToArray(), outputArguments, cancellationToken);
        }

    }
}
