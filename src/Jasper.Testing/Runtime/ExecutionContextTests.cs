using System;
using System.Linq;
using System.Threading.Tasks;
using Baseline;
using Jasper.Runtime;
using Jasper.Testing.Messaging;
using Jasper.Transports;
using Jasper.Transports.Sending;
using Jasper.Util;
using NSubstitute;
using Shouldly;
using TestMessages;
using Xunit;

namespace Jasper.Testing.Runtime
{
    public class ExecutionContextTests
    {
        private readonly MockJasperRuntime theRuntime;
        private readonly ExecutionContext theContext;
        private readonly Envelope theEnvelope;

        public ExecutionContextTests()
        {
            theRuntime = new MockJasperRuntime();

            var original = ObjectMother.Envelope();
            original.Id = Guid.NewGuid();
            original.CorrelationId = Guid.NewGuid().ToString();

            var context = new ExecutionContext(theRuntime);
            context.ReadEnvelope(original, InvocationCallback.Instance);
            theContext = context.As<ExecutionContext>();

            theEnvelope = ObjectMother.Envelope();

        }

        [Fact]
        public async Task reschedule_without_native_scheduling()
        {
            var callback = Substitute.For<IChannelCallback>();
            var scheduledTime = DateTime.Today.AddHours(8);

            theContext.ReadEnvelope(theEnvelope, callback);

            await theContext.ReScheduleAsync(scheduledTime);

            theEnvelope.ScheduledTime.ShouldBe(scheduledTime);

            await theContext.Persistence.Received().ScheduleJobAsync(theEnvelope);
        }

        [Fact]
        public async Task reschedule_with_native_scheduling()
        {
            var callback = Substitute.For<IChannelCallback, IHasNativeScheduling>();
            var scheduledTime = DateTime.Today.AddHours(8);

            theContext.ReadEnvelope(theEnvelope, callback);

            await theContext.ReScheduleAsync(scheduledTime);

            theEnvelope.ScheduledTime.ShouldBe(scheduledTime);

            await theContext.Persistence.DidNotReceive().ScheduleJobAsync(theEnvelope);
            await callback.As<IHasNativeScheduling>().Received()
                .MoveToScheduledUntilAsync(theEnvelope, scheduledTime);
        }

        [Fact]
        public async Task move_to_dead_letter_queue_without_native_dead_letter()
        {
            var callback = Substitute.For<IChannelCallback>();

            theContext.ReadEnvelope(theEnvelope, callback);

            var exception = new Exception();

            await theContext.MoveToDeadLetterQueueAsync(exception);

            await theRuntime.Persistence.Received()
                .MoveToDeadLetterStorageAsync(theEnvelope, exception);
        }

        [Fact]
        public async Task move_to_dead_letter_queue_with_native_dead_letter()
        {
            var callback = Substitute.For<IChannelCallback, IHasDeadLetterQueue>();

            theContext.ReadEnvelope(theEnvelope, callback);

            var exception = new Exception();

            await theContext.MoveToDeadLetterQueueAsync(exception);

            await callback.As<IHasDeadLetterQueue>().Received()
                .MoveToErrorsAsync(theEnvelope, exception);

            await theRuntime.Persistence.DidNotReceive()
                .MoveToDeadLetterStorageAsync(theEnvelope, exception);
        }

        [Fact]
        public void correlation_id_should_be_same_as_original_envelope()
        {
           theContext.CorrelationId.ShouldBe(theContext.Envelope.CorrelationId);
        }

        [Fact]
        public void new_context_gets_a_non_empty_correlation_id()
        {
            theRuntime.NewContext().CorrelationId.ShouldNotBeNull();
        }

    }
}
