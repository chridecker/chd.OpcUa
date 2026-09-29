using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Base.System.Attributes;
using chd.OpcUa.Server.ObjectSystem;

namespace chd.OpcUa.ServerWorker.UaServerObjects
{
    public class Calculator(string name) : BaseUaServerObject(name)
    {
        [ObjectSystemProperty("CallCounter")]
        public int Counter { get; set => SetField(ref field, value); }

        [ObjectSystemMethod]
        public async Task<double> Sum(double x, double y, CancellationToken cancellationToken)
        {
            Counter++;
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            return x * y;
        }

        [ObjectSystemMethod("ResetCounter")]
        public void Reset()
        {
            Counter = 0;
        }
    }
}
