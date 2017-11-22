﻿using Hagar.Buffers;
using Hagar.Codecs;
using Hagar.Session;
using Hagar.TypeSystem;
using Xunit;

namespace Hagar.TestKit
{
    public abstract class FieldCodecTester<TField, TCodec> where TCodec : IFieldCodec<TField>
    {
        protected abstract SerializerSession CreateSession();
        protected abstract TCodec CreateCodec();
        protected abstract TField CreateValue();

        [Fact]
        public void CorrectlyAdvancesReferenceCounter()
        {
            var writer = new Writer();
            var writerSession = CreateSession();
            var writerCodec = this.CreateCodec();
            var beforeReference = writerSession.ReferencedObjects.CurrentReferenceId;

            // Write the field. This should involve marking at least one reference in the session.
            writerCodec.WriteField(writer, writerSession, 0, typeof(TField), this.CreateValue());
            var afterReference = writerSession.ReferencedObjects.CurrentReferenceId;
            Assert.True(beforeReference < afterReference, "Writing a field should result in at least one reference being marked in the session.");

            var reader = new Reader(writer.ToBytes());
            var readerSession = CreateSession();
            var readerCodec = this.CreateCodec();
            var readField = reader.ReadFieldHeader(readerSession);
            beforeReference = readerSession.ReferencedObjects.CurrentReferenceId;
            readerCodec.ReadValue(reader, readerSession, readField);
            afterReference = readerSession.ReferencedObjects.CurrentReferenceId;
            Assert.True(beforeReference < afterReference, "Reading a field should result in at least one reference being marked in the session.");
        }
    }
}