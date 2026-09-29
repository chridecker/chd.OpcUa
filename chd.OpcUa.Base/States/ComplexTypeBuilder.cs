using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using chd.OpcUa.Base.Extensions;

namespace chd.OpcUa.Base.States
{
    public struct ComplexTypeBuilder<T> : IVariantBuilder<T>
    {
        public T GetValue(Variant value)
        {
            var json = value.GetString();
            return JsonSerializer.Deserialize<T>(json);
        }

        public Variant WithValue(T value)
        {
            var json = JsonSerializer.Serialize(value);
            return json.ConvertToVariant();
        }
    }
}
