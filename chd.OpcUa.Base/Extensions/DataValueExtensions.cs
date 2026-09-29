using System;
using System.Collections.Generic;
using System.Text;
using Opc.Ua;

namespace chd.OpcUa.Base.Extensions
{
    public static class DataValueExtensions
    {
        public static NodeId GetDataType(this Type type) => type switch
        {
            Type x when x == typeof(bool) => DataTypeIds.Boolean,
            Type x when x == typeof(sbyte) => DataTypeIds.SByte,
            Type x when x == typeof(byte) => DataTypeIds.Byte,
            Type x when x == typeof(short) => DataTypeIds.Int16,
            Type x when x == typeof(ushort) => DataTypeIds.UInt16,
            Type x when x == typeof(int) => DataTypeIds.Int32,
            Type x when x == typeof(uint) => DataTypeIds.UInt32,
            Type x when x == typeof(long) => DataTypeIds.Int64,
            Type x when x == typeof(float) => DataTypeIds.Float,
            Type x when x == typeof(double) => DataTypeIds.Double,
            Type x when x == typeof(decimal) => DataTypeIds.Decimal,
            Type x when x == typeof(DateTime) => DataTypeIds.DateTime,
            Type x when x == typeof(string) => DataTypeIds.String,
            Type x when x == typeof(Guid) => DataTypeIds.Guid,
            Type x when x.IsEnum => DataTypeIds.String,
            _ => DataTypeIds.BaseDataType
        };

        public static object GetValue(this Variant value)
            => new DataValue(value).GetValue();

        public static object GetValue(this DataValue value)
        {
            if (!value.WrappedValue.TypeInfo.IsArray)
            {
                switch (value.WrappedValue.TypeInfo.BuiltInType)
                {
                    case BuiltInType.Boolean:
                        {
                            if (value.WrappedValue.TryGetValue(out bool val)) ;
                            {
                                return val;
                            }
                            return false;
                        }
                    case BuiltInType.SByte:

                        {
                            if (value.WrappedValue.TryGetValue(out sbyte val))
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.Byte:
                        {
                            if (value.WrappedValue.TryGetValue(out byte val)) ;
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.Int16:
                        {
                            if (value.WrappedValue.TryGetValue(out short val))
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.UInt16:
                        {
                            if (value.WrappedValue.TryGetValue(out ushort val))
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.Int32:
                        {
                            if (value.WrappedValue.TryGetValue(out int val)) ;
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.UInt32:
                        {
                            if (value.WrappedValue.TryGetValue(out uint val)) ;
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.Int64:
                        {
                            if (value.WrappedValue.TryGetValue(out long val)) ;
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.UInt64:
                        {
                            if (value.WrappedValue.TryGetValue(out ulong val))
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.Float:
                        {
                            if (value.WrappedValue.TryGetValue(out float val))
                            {
                                return val;
                            }
                            return 0;
                        }

                    case BuiltInType.Double:
                        {
                            if (value.WrappedValue.TryGetValue(out double val))
                            {
                                return val;
                            }

                            return 0;
                        }

                    default:
                        return value.WrappedValue.Value;
                }
            }
            switch (value.WrappedValue.TypeInfo.BuiltInType)
            {
                case BuiltInType.Boolean:
                    return value.WrappedValue.GetBooleanArray();
                case BuiltInType.SByte:
                    return value.WrappedValue.GetSByteArray();

                case BuiltInType.Byte:
                    return value.WrappedValue.GetByteArray();

                case BuiltInType.Int16:
                    return value.WrappedValue.GetInt16Array();

                case BuiltInType.UInt16:
                    return value.WrappedValue.GetUInt16Array();

                case BuiltInType.Int32:
                    return value.WrappedValue.GetInt32();

                case BuiltInType.UInt32:
                    return value.WrappedValue.GetUInt32Array();

                case BuiltInType.Int64:
                    return value.WrappedValue.GetInt64Array();

                case BuiltInType.UInt64:
                    return value.WrappedValue.GetUInt64Array();

                case BuiltInType.Float:
                    return value.WrappedValue.GetFloatArray();

                case BuiltInType.Double:
                    return value.WrappedValue.GetDoubleArray();

                default:
                    return value.WrappedValue.Value;
            }
        }

        public static Variant ConvertToVariant(this object? value)
        {
            if (value is null)
            {
                return Variant.Null;
            }

            if (value is Variant variant)
            {
                return variant;
            }

            return value switch
            {
                bool v => Variant.From(v),

                sbyte v => Variant.From(v),
                byte v => Variant.From(v),
                short v => Variant.From(v),
                ushort v => Variant.From(v),
                int v => Variant.From(v),
                uint v => Variant.From(v),
                long v => Variant.From(v),
                ulong v => Variant.From(v),

                float v => Variant.From(v),
                double v => Variant.From(v),

                string v => Variant.From(v),
                DateTime v => Variant.From(v),
                Guid v => Variant.From((Uuid)v),

                NodeId v => Variant.From(v),
                ExpandedNodeId v => Variant.From(v),
                StatusCode v => Variant.From(v),
                QualifiedName v => Variant.From(v),
                LocalizedText v => Variant.From(v),
                ExtensionObject v => Variant.From(v),

                bool[] v => Variant.From(v),

                sbyte[] v => Variant.From(v),
                byte[] v => Variant.From(v),
                short[] v => Variant.From(v),
                ushort[] v => Variant.From(v),
                int[] v => Variant.From(v),
                uint[] v => Variant.From(v),
                long[] v => Variant.From(v),
                ulong[] v => Variant.From(v),

                float[] v => Variant.From(v),
                double[] v => Variant.From(v),

                string[] v => Variant.From(v),

                NodeId[] v => Variant.From(v),
                ExpandedNodeId[] v => Variant.From(v),
                StatusCode[] v => Variant.From(v),
                QualifiedName[] v => Variant.From(v),
                LocalizedText[] v => Variant.From(v),
                ExtensionObject[] v => Variant.From(v),

                _ => throw new ArgumentException(
                    $"Typ '{value.GetType().FullName}' kann nicht in einen OPC UA Variant konvertiert werden.",
                    nameof(value))
            };
        }

        public static Variant ChangeType(this DataValue value, object newValue)
        {
            switch (value.WrappedValue.TypeInfo.BuiltInType)
            {
                case BuiltInType.Boolean:
                    {
                        return Variant.From(Convert.ToBoolean(newValue));
                    }

                case BuiltInType.SByte:
                    {
                        return Variant.From(Convert.ToSByte(newValue));
                    }

                case BuiltInType.Byte:
                    {
                        return Variant.From(Convert.ToByte(newValue));
                    }

                case BuiltInType.Int16:
                    {
                        return Variant.From(Convert.ToInt16(newValue));
                    }

                case BuiltInType.UInt16:
                    {
                        return Variant.From(Convert.ToUInt16(newValue));
                    }

                case BuiltInType.Int32:
                    {
                        return Variant.From(Convert.ToInt32(newValue));
                    }

                case BuiltInType.UInt32:
                    {
                        return Variant.From(Convert.ToUInt32(newValue));
                    }

                case BuiltInType.Int64:
                    {
                        return Variant.From(Convert.ToInt64(newValue));
                    }

                case BuiltInType.UInt64:
                    {
                        return Variant.From(Convert.ToUInt64(newValue));
                    }

                case BuiltInType.Float:
                    {
                        return Variant.From(Convert.ToSingle(newValue));
                    }

                case BuiltInType.Double:
                    {
                        return Variant.From(Convert.ToDouble(newValue));
                    }

                default:
                    {
                        return Variant.From(newValue.ToString());
                    }
            }
        }
    }
}
