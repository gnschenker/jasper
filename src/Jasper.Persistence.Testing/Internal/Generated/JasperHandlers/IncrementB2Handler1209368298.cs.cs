// <auto-generated/>
#pragma warning disable
using Jasper.Persistence.Marten.Publishing;

namespace Internal.Generated.JasperHandlers
{
    // START: IncrementB2Handler1209368298
    public class IncrementB2Handler1209368298 : Jasper.Runtime.Handlers.MessageHandler
    {
        private readonly Jasper.Persistence.Marten.Publishing.OutboxedSessionFactory _outboxedSessionFactory;

        public IncrementB2Handler1209368298(Jasper.Persistence.Marten.Publishing.OutboxedSessionFactory outboxedSessionFactory)
        {
            _outboxedSessionFactory = outboxedSessionFactory;
        }



        public override async System.Threading.Tasks.Task HandleAsync(Jasper.IExecutionContext context, System.Threading.CancellationToken cancellation)
        {
            var incrementB2 = (Jasper.Persistence.Testing.Marten.IncrementB2)context.Envelope.Message;
            await using var documentSession = _outboxedSessionFactory.OpenSession(context);
            var eventStore = documentSession.Events;
            // Loading Marten aggregate
            var eventStream = await eventStore.FetchForWriting<Jasper.Persistence.Testing.Marten.SelfLetteredAggregate>(incrementB2.SelfLetteredAggregateId, cancellation).ConfigureAwait(false);

            if (eventStream.Aggregate == null) throw new Jasper.Persistence.Marten.UnknownAggregateException(typeof(Jasper.Persistence.Testing.Marten.SelfLetteredAggregate), incrementB2.SelfLetteredAggregateId);
            var bEvent = await eventStream.Aggregate.Handle(incrementB2).ConfigureAwait(false);
            if (bEvent != null)
            {
                // Capturing any possible events returned from the command handlers
                eventStream.AppendOne(bEvent);

            }

            await documentSession.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }

    }

    // END: IncrementB2Handler1209368298
    
    
}

