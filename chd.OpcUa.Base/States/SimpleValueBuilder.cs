using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Base.Extensions;

namespace chd.OpcUa.Base.States
{
    public struct SimpleValueBuilder : IVariantBuilder<object>
    {
        public object GetValue(Variant value) => value.GetValue();

        public Variant WithValue(object value) => value.ConvertToVariant();
    }
}
