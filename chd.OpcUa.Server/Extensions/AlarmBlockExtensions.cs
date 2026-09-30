using chd.OpcUa.Server.UnderlyingSystem;
using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Server.Model;
using chd.OpcUa.ServerWorker;

namespace chd.OpcUa.Server.Extensions
{
    public static class AlarmBlockExtensions
    {
        public static AlarmConditionState CreateAlarm(this BlockState block, UnderlyingSystemAlarm e, NodeManager nodeManager)
        {
            var condition = new AlarmConditionState(block);
            condition.Initialize(nodeManager.SystemContext, block, e.Severity, LocalizedText.From(e.Message));



            condition.Acknowledge = new AddCommentMethodState(condition);

            condition.EnabledState = new TwoStateVariableState(condition);
            condition.EnabledState.TransitionTime = PropertyState<DateTimeUtc>.With<VariantBuilder>(condition.EnabledState);
            condition.EnabledState.EffectiveDisplayName = PropertyState<LocalizedText>.With<VariantBuilder>(condition.EnabledState);
            condition.EnabledState.Create(nodeManager.SystemContext, NodeId.Null, new QualifiedName(BrowseNames.EnabledState), LocalizedText.Null, false);


            // same procedure add optional components to the ActiveState component.
            condition.ActiveState = new TwoStateVariableState(condition);
            condition.ActiveState.TransitionTime = PropertyState<DateTimeUtc>.With<VariantBuilder>(condition.ActiveState);
            condition.ActiveState.EffectiveDisplayName = PropertyState<LocalizedText>.With<VariantBuilder>(condition.ActiveState);
            condition.ActiveState.Create(nodeManager.SystemContext, NodeId.Null, new QualifiedName(BrowseNames.ActiveState), LocalizedText.Null, false);


            // same procedure add optional components to the ActiveState component.
            condition.ConfirmedState = new TwoStateVariableState(condition);
            condition.ConfirmedState.TransitionTime = PropertyState<DateTimeUtc>.With<VariantBuilder>(condition.ConfirmedState);
            condition.ConfirmedState.EffectiveDisplayName = PropertyState<LocalizedText>.With<VariantBuilder>(condition.ConfirmedState);
            condition.ConfirmedState.Create(nodeManager.SystemContext, NodeId.Null, new QualifiedName(BrowseNames.ConfirmedState), LocalizedText.Null, false);
            condition.Confirm = new AddCommentMethodState(condition);

            condition.Comment = ConditionVariableState<LocalizedText>.With<VariantBuilder>(condition);
            condition.Comment.Create(nodeManager.SystemContext, NodeId.Null, new QualifiedName(BrowseNames.Comment), LocalizedText.Null, false);

            condition.AddComment = new AddCommentMethodState(condition);

            condition.SymbolicName = e.Name;
            condition.SupportsFilteredRetain = PropertyState<bool>.With<VariantBuilder>(condition, true);

            condition.Create(nodeManager.SystemContext, NodeId.Null, new QualifiedName(e.Name, nodeManager.NamespaceIndex), LocalizedText.From(e.Name), true);

            condition.EnabledState.Id.Value = e.Enabled;
            condition.EnabledState.Value = LocalizedText.From("Enabled");
            condition.ActiveState.Id.Value = e.Active;
            condition.AckedState.Id.Value = e.Acknowledged;
            condition.ConfirmedState.Id.Value = e.Confirmed;

            condition.Comment.Value = LocalizedText.From(e.Comment);

            condition.EventId.Value = e.Id.Value.ToByteArray().ToByteString();
            condition.EventType.Value = condition.TypeDefinitionId;
            condition.ConditionName = PropertyState<string>.With<VariantBuilder>(condition);
            condition.ConditionName.Value = condition.SymbolicName;
            condition.Time.Value = DateTime.UtcNow;

            condition.Retain.Value = true;

            condition.SourceName.Value = block.DisplayName.Text;

            condition.Message.Value = LocalizedText.From(e.Message);

            //condition.AddReAlarmTime(_nodeManager.SystemContext).AddReAlarmRepeatCount(_nodeManager.SystemContext);
            //condition.ReAlarmTime.Value = TimeSpan.FromSeconds(2).TotalMilliseconds;
            //condition.ReAlarmRepeatCount.Value = 0;

            return condition;
        }
    }
}
