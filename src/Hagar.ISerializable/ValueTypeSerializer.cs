using System;
using System.Runtime.Serialization;
using System.Security;
using Hagar.Buffers;
using Hagar.Codecs;

namespace Hagar.ISerializable
{
    /// <summary>
    /// Serializer for ISerializable value types.
    /// </summary>
    /// <typeparam name="T">The type which this serializer can serialize.</typeparam>
    internal class ValueTypeSerializer<T> : ISerializableSerializer where T : struct
    {
        public delegate void ValueConstructor(ref T value, SerializationInfo info, StreamingContext context);

        public delegate void SerializationCallback(ref T value, StreamingContext context);

        private static readonly Type Type = typeof(T);

        private readonly ValueConstructor constructor;
        private readonly SerializationCallbacksFactory.SerializationCallbacks<SerializationCallback> callbacks;

        private readonly IFormatterConverter formatterConverter;
        private readonly StreamingContext streamingContext;
        private readonly SerializationEntryCodec entrySerializer;

        [SecurityCritical]
        public ValueTypeSerializer(
            ValueConstructor constructor,
            SerializationCallbacksFactory.SerializationCallbacks<SerializationCallback> callbacks,
            SerializationEntryCodec entrySerializer,
            StreamingContext streamingContext,
            IFormatterConverter formatterConverter)
        {
            this.constructor = constructor;
            this.callbacks = callbacks;
            this.entrySerializer = entrySerializer;
            this.streamingContext = streamingContext;
            this.formatterConverter = formatterConverter;
        }

        [SecurityCritical]
        void ISerializableSerializer.WriteValue<TBufferWriter>(ref Writer<TBufferWriter> writer, object value)
        {
            var item = (T) value;
            this.callbacks.OnSerializing?.Invoke(ref item, this.streamingContext);

            var info = new SerializationInfo(Type, this.formatterConverter);
            ((System.Runtime.Serialization.ISerializable)value).GetObjectData(info, this.streamingContext);

            var first = true;
            foreach (var field in info)
            {
                var surrogate = new SerializationEntrySurrogate(field);
                this.entrySerializer.WriteField(ref writer, first ? 1 : (uint)0, SerializationEntryCodec.SerializationEntryType, surrogate);
                if (first) first = false;
            }
            
            this.callbacks.OnSerialized?.Invoke(ref item, this.streamingContext);
        }

        [SecurityCritical]
        object ISerializableSerializer.ReadValue(ref Reader reader, Type type, uint placeholderReferenceId)
        {
            var info = new SerializationInfo(Type, this.formatterConverter);
            T result = default;

            this.callbacks.OnDeserializing?.Invoke(ref result, this.streamingContext);

            uint fieldId = 0;
            while (true)
            {
                var header = reader.ReadFieldHeader();
                if (header.IsEndBaseOrEndObject) break;
                fieldId += header.FieldIdDelta;
                if (fieldId == 1)
                {
                    var entry = this.entrySerializer.ReadValue(ref reader, header);
                    info.AddValue(entry.Name, entry.Value);
                }
            }

            this.constructor(ref result, info, this.streamingContext);
            this.callbacks.OnDeserialized?.Invoke(ref result, this.streamingContext);
            if (result is IDeserializationCallback callback)
            {
                callback.OnDeserialization(this.streamingContext.Context);
            }

            return result;
        }
    }
}