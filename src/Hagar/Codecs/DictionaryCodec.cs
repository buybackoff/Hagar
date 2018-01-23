﻿using System;
using System.Collections.Generic;
using Hagar.Activators;
using Hagar.Buffers;
using Hagar.Serializers;
using Hagar.Session;
using Hagar.WireProtocol;

namespace Hagar.Codecs
{
    /// <summary>
    /// Codec for <see cref="Dictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    public class DictionaryCodec<TKey, TValue> : IFieldCodec<Dictionary<TKey, TValue>>
    {
        private readonly IFieldCodec<KeyValuePair<TKey, TValue>> pairCodec;
        private readonly IUntypedCodecProvider codecProvider;
        private readonly IFieldCodec<Type> typeCodec;
        private readonly DictionaryActivator<TKey, TValue> activator;

        public DictionaryCodec(
            IFieldCodec<KeyValuePair<TKey, TValue>> pairCodec,
            IUntypedCodecProvider codecProvider,
            IFieldCodec<Type> typeCodec,
            DictionaryActivator<TKey, TValue> activator)
        {
            this.pairCodec = pairCodec;
            this.codecProvider = codecProvider;
            this.typeCodec = typeCodec;
            this.activator = activator;
        }

        public void WriteField(Writer writer, SerializerSession session, uint fieldIdDelta, Type expectedType, Dictionary<TKey, TValue> value)
        {
            if (ReferenceCodec.TryWriteReferenceField(writer, session, fieldIdDelta, expectedType, value)) return;
            writer.WriteFieldHeader(session, fieldIdDelta, expectedType, value.GetType(), WireType.TagDelimited);
            
            if (value.Comparer != EqualityComparer<TKey>.Default)
            {
                this.typeCodec.WriteField(writer, session, 0, typeof(Type), value.Comparer?.GetType());
            }

            var first = true;
            foreach (var element in value)
            {
                this.pairCodec.WriteField(writer, session, first ? 1U : 0, typeof(KeyValuePair<TKey, TValue>), element);
                first = false;
            }

            writer.WriteEndObject();
        }

        public Dictionary<TKey, TValue> ReadValue(Reader reader, SerializerSession session, Field field)
        {
            if (field.WireType == WireType.Reference)
                return ReferenceCodec.ReadReference<Dictionary<TKey, TValue>>(reader, session, field, this.codecProvider);
            if (field.WireType != WireType.TagDelimited) ThrowUnsupportedWireTypeException(field);

            var placeholderReferenceId = ReferenceCodec.CreateRecordPlaceholder(session);
            Dictionary<TKey, TValue> result = null;
            Type comparer = null;
            uint fieldId = 0;
            while (true)
            {
                var header = reader.ReadFieldHeader(session);
                if (header.IsEndBaseOrEndObject) break;
                fieldId += header.FieldIdDelta;
                switch (fieldId)
                {
                    case 0:
                        comparer = this.typeCodec.ReadValue(reader, session, header);
                        break;
                    case 1:
                        if (result == null)
                        {
                            result = CreateInstance(comparer, session, placeholderReferenceId);
                        }

                        var pair = this.pairCodec.ReadValue(reader, session, header);
                        // ReSharper disable once PossibleNullReferenceException
                        result.Add(pair.Key, pair.Value);
                        break;
                    default:
                        reader.ConsumeUnknownField(session, header);
                        break;
                }
            }
            
            return result;
        }

        private Dictionary<TKey, TValue> CreateInstance(Type equalityComparerType, SerializerSession session, uint placeholderReferenceId)
        {
            IEqualityComparer<TKey> comparer;
            if (equalityComparerType != null)
            {
                comparer = (IEqualityComparer<TKey>)Activator.CreateInstance(equalityComparerType);
            }
            else
            {
                comparer = null;
            }

            var result = this.activator.Create(comparer);
            ReferenceCodec.RecordObject(session, result, placeholderReferenceId);
            return result;
        }

        private static void ThrowUnsupportedWireTypeException(Field field) => throw new UnsupportedWireTypeException(
            $"Only a {nameof(WireType)} value of {WireType.TagDelimited} is supported. {field}");
    }
}
