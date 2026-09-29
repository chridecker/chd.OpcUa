using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Text;
using chd.OpcUa.Base.Extensions;

namespace chd.OpcUa.Base.States
{
    public struct EventValueBuilder<T> : IVariantBuilder<T>
    {
        public T GetValue(Variant value)
        {
            return (T)value.GetValue();
        }

        public Variant WithValue(T value)
        {
            return value.ConvertToVariant();
        }
    }
}
