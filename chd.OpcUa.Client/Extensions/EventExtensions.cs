using chd.OpcUa.Contracts;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.Subscriptions;
using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Base.Extensions;
using chd.OpcUa.Base.States;

namespace chd.OpcUa.Client.Extensions
{
    public static class EventExtensions
    {
        public static EventFieldList ToFieldList(this EventNotification notification)
        {
            return new EventFieldList
            {
                ClientHandle = notification.MonitoredItem?.ClientHandle ?? 0,
                EventFields = notification.Fields,
            };
        }
        private static NodeId FindEventType(this EventFilter filter, EventFieldList notification)
        {
            if (filter != null)
            {
                for (int ii = 0; ii < filter.SelectClauses.Count; ii++)
                {
                    SimpleAttributeOperand clause = filter.SelectClauses[ii];

                    if (clause.BrowsePath.Count == 1 && clause.BrowsePath[0] == BrowseNames.EventType)
                    {
                        return notification.EventFields[ii].TryGetValue(out NodeId nodeId) ? nodeId : NodeId.Null;
                    }
                }
            }

            return NodeId.Null;
        }
        private static async Task<BaseEventState> ConstructEventAsync(this ISession session,
            EventFilter filter,
            EventFieldList notification,
            Dictionary<NodeId, Type> knownEventTypes,
            Dictionary<NodeId, Type> registeredTypes,
            CancellationToken ct = default)
        {
            var eventTypeId = FindEventType(filter, notification);
            if (eventTypeId.IsNull)
            {
                return null;
            }

            Type knownType = null;
            NodeId knownTypeId = NodeId.Null;

            if (registeredTypes.TryGetValue(eventTypeId, out knownType))
            {
                knownTypeId = eventTypeId;
            }
            if (knownType is null
                && knownEventTypes.TryGetValue(eventTypeId, out knownType))
            {
                knownTypeId = eventTypeId;
            }

            if (knownType is null)
            {
                var supertypes = await BrowseSuperTypesAsync(session, eventTypeId, false, ct).ConfigureAwait(false);
                if (supertypes is null)
                {
                    return null;
                }

                // find the first supertype that matches a known event type.
                for (int ii = 0; ii < supertypes.Count; ii++)
                {
                    var superTypeId = (NodeId)supertypes[ii].NodeId;

                    if (knownEventTypes.TryGetValue(superTypeId, out knownType))
                    {
                        knownTypeId = superTypeId;
                    }

                    if (!knownTypeId.IsNull)
                    {
                        break;
                    }
                }

                // can't do anything with unknown types.
                if (knownTypeId.IsNull)
                {
                    return null;
                }
            }

            var constructorParam = new List<object>()
            {
                (NodeState)null
            };
            if (knownType.GetConstructors().Any(a => a.GetParameters().Length > 1))
            {
                constructorParam.Add(eventTypeId.NamespaceIndex);
            }
            var e = (BaseEventState)Activator.CreateInstance(knownType, constructorParam.ToArray());

            // initialize the event with the values in the notification.
            e.Update(session.SystemContext, filter.SelectClauses, notification);

            // save the orginal notification.
            e.Handle = notification;

            return e;
        }

        public static async Task<List<ReferenceDescription>> BrowseAsync(this ISession session, IReadOnlyList<BrowseDescription> nodesToBrowse, CancellationToken cancellationToken)
        {
            try
            {
                List<ReferenceDescription> references = new List<ReferenceDescription>();

                while (nodesToBrowse.Count > 0)
                {
                    // start the browse operation.
                    var response = await session.BrowseAsync(
                        null,
                        null,
                        0,
                        nodesToBrowse.ToArrayOf(),
                        cancellationToken).ConfigureAwait(false);

                    var results = response.Results.ToList();
                    var diagnosticInfos = response.DiagnosticInfos.ToList();

                    ClientBase.ValidateResponse(results, nodesToBrowse);
                    ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToBrowse);

                    List<ByteString> continuationPoints = new List<ByteString>();
                    List<BrowseDescription> unprocessedOperations = new List<BrowseDescription>();

                    for (int ii = 0; ii < nodesToBrowse.Count; ii++)
                    {
                        // check for error.
                        if (StatusCode.IsBad(results[ii].StatusCode))
                        {
                            // this error indicates that the server does not have enough simultaneously active
                            // continuation points. This request will need to be resent after the other operations
                            // have been completed and their continuation points released.
                            if (results[ii].StatusCode == StatusCodes.BadNoContinuationPoints)
                            {
                                unprocessedOperations.Add(nodesToBrowse[ii]);
                            }

                            continue;
                        }

                        // check if all references have been fetched.
                        if (results[ii].References.Count == 0)
                        {
                            continue;
                        }

                        // save results.
                        references.AddRange(results[ii].References);

                        // check for continuation point.
                        if (!results[ii].ContinuationPoint.IsNull)
                        {
                            continuationPoints.Add(results[ii].ContinuationPoint);
                        }
                    }

                    // process continuation points.
                    while (continuationPoints.Count > 0)
                    {
                        // continue browse operation.
                        BrowseNextResponse response2 = await session.BrowseNextAsync(
                            null,
                            false,
                            continuationPoints,
                            cancellationToken).ConfigureAwait(false);

                        results = response2.Results.ToList();
                        diagnosticInfos = response2.DiagnosticInfos.ToList();

                        ClientBase.ValidateResponse(results, continuationPoints);
                        ClientBase.ValidateDiagnosticInfos(diagnosticInfos, continuationPoints);

                        List<ByteString> revisedContinuationPoints = new List<ByteString>();
                        for (int ii = 0; ii < continuationPoints.Count; ii++)
                        {
                            // check for error.
                            if (StatusCode.IsBad(results[ii].StatusCode))
                            {
                                continue;
                            }

                            // check if all references have been fetched.
                            if (results[ii].References.Count == 0)
                            {
                                continue;
                            }

                            // save results.
                            references.AddRange(results[ii].References);

                            // check for continuation point.
                            if (!results[ii].ContinuationPoint.IsNull)
                            {
                                revisedContinuationPoints.Add(results[ii].ContinuationPoint);
                            }
                        }

                        // check if browsing must continue;
                        continuationPoints = revisedContinuationPoints;
                    }

                    // check if unprocessed results exist.
                    nodesToBrowse = unprocessedOperations;
                }

                // return complete list.
                return references;
            }
            catch (Exception exception)
            {
                throw new ServiceResultException(exception, StatusCodes.BadUnexpectedError);
            }
        }

        private static async Task<List<ReferenceDescription>> BrowseSuperTypesAsync(this ISession session, NodeId typeId, bool throwOnError, CancellationToken ct = default)
        {
            List<ReferenceDescription> supertypes = new List<ReferenceDescription>();

            try
            {
                // find all of the children of the field.
                BrowseDescription nodeToBrowse = new BrowseDescription();

                nodeToBrowse.NodeId = typeId;
                nodeToBrowse.BrowseDirection = BrowseDirection.Inverse;
                nodeToBrowse.ReferenceTypeId = ReferenceTypeIds.HasSubtype;
                nodeToBrowse.IncludeSubtypes = false; // more efficient to use IncludeSubtypes=False when possible.
                nodeToBrowse.NodeClassMask = 0; // the HasSubtype reference already restricts the targets to Types.
                nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;

                List<ReferenceDescription> references = await session.BrowseAsync(new List<BrowseDescription>() { nodeToBrowse }, ct);

                while (references != null && references.Count > 0)
                {
                    // should never be more than one supertype.
                    supertypes.Add(references[0]);

                    // only follow references within this server.
                    if (references[0].NodeId.IsAbsolute)
                    {
                        break;
                    }

                    // get the references for the next level up.
                    nodeToBrowse.NodeId = (NodeId)references[0].NodeId;
                    references = await session.BrowseAsync(new List<BrowseDescription>() { nodeToBrowse }, ct).ConfigureAwait(false);
                }

                // return complete list.
                return supertypes;
            }
            catch (Exception exception)
            {
                if (throwOnError)
                {
                    throw new ServiceResultException(exception, StatusCodes.BadUnexpectedError);
                }

                return null;
            }
        }

        private static async Task CollectFieldsAsync(this ISession session,
            NodeId eventTypeId,
            List<SimpleAttributeOperand> eventFields,
            Dictionary<NodeId, List<QualifiedName>> foundNodes,
            CancellationToken ct = default)
        {
            List<ReferenceDescription> supertypes = await session.BrowseSuperTypesAsync(eventTypeId, false, ct).ConfigureAwait(false);

            if (supertypes == null)
            {
                return;
            }

            // process the types starting from the top of the tree.
            List<QualifiedName> parentPath = new List<QualifiedName>();

            for (int ii = supertypes.Count - 1; ii >= 0; ii--)
            {
                await CollectFieldsAsync(session, (NodeId)supertypes[ii].NodeId, parentPath, eventFields, foundNodes, ct).ConfigureAwait(false);
            }

            // collect the fields for the selected type.
            await CollectFieldsAsync(session, eventTypeId, parentPath, eventFields, foundNodes, ct).ConfigureAwait(false);
        }



        private static async Task CollectFieldsAsync(this ISession session,
            NodeId nodeId,
            List<QualifiedName> parentPath,
            List<SimpleAttributeOperand> eventFields,
            Dictionary<NodeId, List<QualifiedName>> foundNodes,
            CancellationToken ct = default)
        {
            // find all of the children of the field.
            BrowseDescription nodeToBrowse = new BrowseDescription();

            nodeToBrowse.NodeId = nodeId;
            nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
            nodeToBrowse.ReferenceTypeId = ReferenceTypeIds.Aggregates;
            nodeToBrowse.IncludeSubtypes = true;
            nodeToBrowse.NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable);
            nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;

            List<ReferenceDescription> children = await session.BrowseAsync(new List<BrowseDescription>() { nodeToBrowse }, ct).ConfigureAwait(false);

            if (children == null)
            {
                return;
            }

            // process the children.
            for (int ii = 0; ii < children.Count; ii++)
            {
                ReferenceDescription child = children[ii];

                if (child.NodeId.IsAbsolute)
                {
                    continue;
                }

                // construct browse path.
                List<QualifiedName> browsePath = new List<QualifiedName>(parentPath);
                browsePath.Add(child.BrowseName);

                // check if the browse path is already in the list.
                if (!ContainsPath(eventFields, browsePath))
                {
                    SimpleAttributeOperand field = new SimpleAttributeOperand();

                    field.TypeDefinitionId = ObjectTypeIds.BaseEventType;
                    field.BrowsePath = browsePath;
                    field.AttributeId = (child.NodeClass == NodeClass.Variable) ? Attributes.Value : Attributes.NodeId;

                    eventFields.Add(field);
                }

                // recusively find all of the children.
                NodeId targetId = (NodeId)child.NodeId;

                // need to guard against loops.
                if (foundNodes.TryAdd(targetId, browsePath))
                {
                    await CollectFieldsAsync(session, (NodeId)child.NodeId, browsePath, eventFields, foundNodes, ct).ConfigureAwait(false);
                }
            }
        }

        private static bool ContainsPath(List<SimpleAttributeOperand> selectClause, List<QualifiedName> browsePath)
        {
            for (int ii = 0; ii < selectClause.Count; ii++)
            {
                SimpleAttributeOperand field = selectClause[ii];

                if (field.BrowsePath.Count != browsePath.Count)
                {
                    continue;
                }

                bool match = true;

                for (int jj = 0; jj < field.BrowsePath.Count; jj++)
                {
                    if (field.BrowsePath[jj] != browsePath[jj])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<NodeId, Type> CreateKnownTypes()
        {
            return new Dictionary<NodeId, Type>
            {
                [ObjectTypeIds.BaseEventType] = typeof(BaseEventState),
                [ObjectTypeIds.ConditionType] = typeof(ConditionState),
                [ObjectTypeIds.DialogConditionType] = typeof(DialogConditionState),
                [ObjectTypeIds.AlarmConditionType] = typeof(AlarmConditionState),
                [ObjectTypeIds.ExclusiveLimitAlarmType] = typeof(ExclusiveLimitAlarmState),
                [ObjectTypeIds.NonExclusiveLimitAlarmType] = typeof(NonExclusiveLimitAlarmState),
                [ObjectTypeIds.AuditEventType] = typeof(AuditEventState),
                [ObjectTypeIds.AuditUpdateMethodEventType] = typeof(AuditUpdateMethodEventState),
            };
        }

        public static async Task<SimpleEventArgs?> ProcessEventNotificationAsync(this ISession session, Dictionary<uint, EventFilter> filterByHandle, Dictionary<NodeId, Type> registredTypes, EventNotification notification, CancellationToken ct)
        {
            var clientHandle = notification.MonitoredItem?.ClientHandle ?? 0;

            if (!filterByHandle.TryGetValue(clientHandle, out EventFilter filter)) { return null; }

            var fields = notification.ToFieldList();

            var eventTypeId = filter.FindEventType(fields);
            if (eventTypeId.IsNull)
            {
                return null;
            }

            var baseEvent = await session.ConstructEventAsync(filter, fields, CreateKnownTypes(), registredTypes, ct).ConfigureAwait(false);

            var type = await session.NodeCache.FindAsync(baseEvent.TypeDefinitionId, ct).ConfigureAwait(false);

            return new SimpleEventArgs()
            {
                Id = baseEvent.EventId.Value.Memory.Span.ToArray(),
                Type = type?.ToString(),
                SourceName = baseEvent.SourceName?.Value,
                Time = baseEvent.Time.Value.ToDateTime(),
                Severity = baseEvent.Severity.Value,
                Message = baseEvent.Message?.Value.Text,
                Value = GetEventValue(baseEvent)
            };
        }

        private static object GetEventValue(BaseEventState evt)
        {
            if (evt is SimpleValueCustomEventState e1)
            {
                return e1.Value.Value;
            }

            return null;
        }


        public static async Task<AlarmEventArgs?> ProcessNotificationAsync(this ISession session, Dictionary<uint, ConditionState> conditionStates, Dictionary<uint, EventFilter> filterByHandle,
            Dictionary<NodeId, Type> registeredTypes, EventNotification notification, CancellationToken ct)
        {
            uint clientHandle = notification.MonitoredItem?.ClientHandle ?? 0;

            if (!filterByHandle.TryGetValue(clientHandle, out EventFilter filter))
            {
                return null;
            }

            var fields = notification.ToFieldList();

            var eventTypeId = filter.FindEventType(fields);
            if (eventTypeId.IsNull)
            {
                return null;
            }

            // a refresh starts the list over and ends without anything to show
            if (eventTypeId == ObjectTypeIds.RefreshStartEventType)
            {

                if (conditionStates.ContainsKey(clientHandle))
                {
                    conditionStates.Remove(clientHandle);
                }
                return null;
            }

            if (eventTypeId == ObjectTypeIds.RefreshEndEventType)
            {
                return null;
            }

            // construct the condition object.
            var condition = await session.ConstructEventAsync(
                filter,
                fields,
                CreateKnownTypes(), registeredTypes,
                ct).ConfigureAwait(false) as ConditionState;

            if (condition is null)
            {
                return null;
            }

            conditionStates[clientHandle] = condition;

            INode type = await session.NodeCache.FindAsync(condition.TypeDefinitionId, ct).ConfigureAwait(false);


            return new AlarmEventArgs()
            {
                Id = condition.EventId.Value.Memory,
                Handle = clientHandle,
                Type = type?.ToString(),
                SourceName = condition.SourceName?.Value,
                ConditionName = condition.ConditionName?.Value,
                Time = condition.Time.Value.ToDateTime(),
                Severity = condition.Severity.Value,
                StateText = condition.EnabledState?.EffectiveDisplayName?.Value.Text,
                Message = condition.Message?.Value.Text,
                Comment = condition.Comment?.Value.Text,
                Retain = condition.Retain.Value,
                IsDialog = condition is DialogConditionState,
                DialogText = condition is DialogConditionState dialog ? dialog.Prompt.Value.Text : string.Empty,
                IsAlarm = condition is AlarmConditionState,
                CanSilence = condition is AlarmConditionState alarm && !(alarm.SilenceState?.Id?.Value ?? false),
                DialogResponses = condition is DialogConditionState dialog1 ? dialog1.ResponseOptionSet.Value
                    .ToArray()
                    .Select(option => Utils.Format("{0}", option))
                    .ToArray() : new[] { string.Empty }
            };
        }

    }
}
