using System;
using System.Collections.Generic;
using Hagar.Buffers;
using Hagar.Codecs;
using Hagar.WireProtocol;

namespace Hagar.Serializers
{
    /// <summary>
    /// Serializer for types which are abstract and therefore cannot be instantiated themselves, such as abstract classes and interface types.
    /// </summary>
    /// <typeparam name="TField"></typeparam>
    public sealed class AbstractTypeSerializer<TField> : IFieldCodec<TField> where TField : class
    {
        void IFieldCodec<TField>.WriteField<TBufferWriter>(ref Writer<TBufferWriter> writer, uint fieldIdDelta, Type expectedType, TField value)
        {
            // If the value is null then we will not be able to get its type in order to get a concrete codec for it.
            // Therefore write the null reference and exit.
            if (value is null)
            {
                ReferenceCodec.TryWriteReferenceField(ref writer, fieldIdDelta, expectedType, null);
                return;
            }

            var fieldType = value.GetType();
            var specificSerializer = writer.Session.CodecProvider.GetCodec(fieldType);
            if (specificSerializer != null)
            {
                specificSerializer.WriteField(ref writer, fieldIdDelta, expectedType, value);
            }
            else
            {
                ThrowSerializerNotFound(fieldType);
            }
        }

        public TField ReadValue(ref Reader reader, Field field)
        {
            if (field.WireType == WireType.Reference) return ReferenceCodec.ReadReference<TField>(ref reader, field);
            var fieldType = field.FieldType;
            if (fieldType == null) ThrowMissingFieldType();

            var specificSerializer = reader.Session.CodecProvider.GetCodec(fieldType);
            if (specificSerializer != null)
            {
                return (TField)specificSerializer.ReadValue(ref reader, field);
            }

            return ThrowSerializerNotFound(fieldType);
        }

        private static TField ThrowSerializerNotFound(Type type)
        {
            throw new KeyNotFoundException($"Could not find a serializer of type {type}.");
        }
        
        private static void ThrowMissingFieldType()
        {
            throw new FieldTypeMissingException(typeof(TField));
        }
    }
}