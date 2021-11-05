using System.Buffers;
using DotPulsar;
using DotPulsar.Abstractions;

namespace Jasper.Pulsar
{
    public interface IPulsarProtocol
    {
        /// <summary>
        /// Transfer information from Jasper's Envelope to the Rabbit MQ properties
        /// </summary>
        /// <param name="env"></param>
        /// <param name="message"></param>
        /// <param name="properties"></param>
        void MapEnvelopeToOutgoing(Envelope env, MessageMetadata message);

        /// <summary>
        /// Create an Envelope object from the raw message data and the header
        /// values in the properties
        /// </summary>
        /// <param name="envelope"></param>
        /// <param name="properties"></param>
        /// <param name="body"></param>
        /// <returns></returns>
        void MapIncomingToEnvelope(Envelope envelope, IMessage<ReadOnlySequence<byte>> message);
    }
}
