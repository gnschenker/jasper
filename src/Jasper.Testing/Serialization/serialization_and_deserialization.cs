﻿using System;
using System.Reflection;
using Baseline;
using Baseline.ImTools;
using Jasper.Serialization;
using Jasper.Util;
using Shouldly;
using TestingSupport;
using Xunit;

namespace Jasper.Testing.Serialization
{
    public class serialization_and_deserialization_of_single_message
    {
        public serialization_and_deserialization_of_single_message()
        {
            outgoing = new Envelope
            {
                SentAt = DateTime.Today.ToUniversalTime(),
                Data = new byte[] {1, 5, 6, 11, 2, 3},
                Destination = "durable://localhost:2222/incoming".ToUri(),
                DeliverBy = DateTime.Today.ToUniversalTime(),
                ReplyUri = "durable://localhost:2221/replies".ToUri(),
                SagaId = Guid.NewGuid().ToString()
            };

            outgoing.Headers.Add("name", "Jeremy");
            outgoing.Headers.Add("state", "Texas");
        }

        private readonly Envelope outgoing;
        private Envelope _incoming;

        private Envelope incoming
        {
            get
            {
                if (_incoming == null)
                {
                    var messageBytes = EnvelopeSerializer.Serialize(outgoing);
                    _incoming = EnvelopeSerializer.Deserialize(messageBytes);
                }

                return _incoming;
            }
        }

        [Fact]
        public void accepted_content_types_positive()
        {
            outgoing.AcceptedContentTypes = new[] {"a", "b"};
            incoming.AcceptedContentTypes.ShouldHaveTheSameElementsAs("a", "b");
        }

        [Fact]
        public void ack_requested_negative()
        {
            outgoing.AckRequested = false;
            incoming.AckRequested.ShouldBeFalse();
        }

        [Fact]
        public void ack_requested_positive()
        {
            outgoing.AckRequested = true;
            incoming.AckRequested.ShouldBeTrue();
        }

        [Fact]
        public void all_the_headers()
        {
            incoming.Headers["name"].ShouldBe("Jeremy");
            incoming.Headers["state"].ShouldBe("Texas");
        }

        [Fact]
        public void brings_over_the_correlation_id()
        {
            incoming.Id.ShouldBe(outgoing.Id);
        }

        [Fact]
        public void brings_over_the_saga_id()
        {
            incoming.SagaId.ShouldBe(outgoing.SagaId);
        }

        [Fact]
        public void content_type()
        {
            outgoing.ContentType = EnvelopeConstants.JsonContentType;
            incoming.ContentType.ShouldBe(outgoing.ContentType);
        }

        [Fact]
        public void data_comes_over()
        {
            incoming.Data.ShouldHaveTheSameElementsAs(outgoing.Data);
        }


        [Fact]
        public void deliver_by_with_value()
        {
            incoming.DeliverBy.Value.ShouldBe(outgoing.DeliverBy.Value);
        }

        [Fact]
        public void deliver_by_without_value()
        {
            outgoing.DeliverBy = null;
            incoming.DeliverBy.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void destination()
        {
            incoming.Destination.ShouldBe(outgoing.Destination);
        }

        [Fact]
        public void execution_time_not_null()
        {
            outgoing.ScheduledTime = DateTime.Today;
            incoming.ScheduledTime.ShouldBe(DateTime.Today.ToUniversalTime());
        }

        [Fact]
        public void execution_time_null()
        {
            outgoing.ScheduledTime = null;
            incoming.ScheduledTime.HasValue.ShouldBeFalse();
        }

        [Fact]
        public void message_type()
        {
            outgoing.MessageType = "some.model.object";

            incoming.MessageType.ShouldBe(outgoing.MessageType);
        }

        [Fact]
        public void original_id()
        {
            outgoing.CorrelationId = Guid.NewGuid().ToString();
            incoming.CorrelationId.ShouldBe(outgoing.CorrelationId);
        }

        [Fact]
        public void parent_id()
        {
            outgoing.CausationId = Guid.NewGuid().ToString();
            incoming.CausationId.ShouldBe(outgoing.CausationId);
        }

        [Fact]
        public void reply_requested()
        {
            outgoing.ReplyRequested = Guid.NewGuid().ToString();
            incoming.ReplyRequested.ShouldBe(outgoing.ReplyRequested);
        }

        [Fact]
        public void reply_uri()
        {
            incoming.ReplyUri.ShouldBe(outgoing.ReplyUri);
        }

        [Fact]
        public void sent_at()
        {
            incoming.SentAt.ShouldBe(outgoing.SentAt);
        }


        [Fact]
        public void source()
        {
            outgoing.Source = "something";
            incoming.Source.ShouldBe(outgoing.Source);
        }
    }
}
