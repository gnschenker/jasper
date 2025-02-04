﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Baseline;
using Jasper;
using Jasper.Runtime.Routing;
using Jasper.Serialization;
using Microsoft.Extensions.Hosting;
using StoryTeller;
using StoryTeller.Grammars.Tables;

namespace StorytellerSpecs.Fixtures
{
    public class Handler<T>
    {
        public void Handle(T message)
        {
        }
    }

    public class BusRoutingFixture : BusFixture
    {
        private IHost _host;
        private JasperOptions _options;
        private IMessageRoute[] _tracks;


        public Task RemoveCapabilities(string serviceName)
        {
            throw new NotImplementedException();
        }


        public void Dispose()
        {
        }

        [FormatAs("The application has a handler for {MessageType}")]
        public void Handles([SelectionList("MessageTypes")] string MessageType)
        {
            var messageType = messageTypeFor(MessageType);
            var handlerType = typeof(Handler<>).MakeGenericType(messageType);
            _options.Handlers.IncludeType(handlerType);
        }


        [FormatAs("The application is configured to publish all messages locally")]
        public void PublishAllLocally()
        {
            _options.PublishAllMessages().Locally();
        }

        [FormatAs("The application is configured to publish the message {MessageType} locally")]
        public void PublishLocally([SelectionList("MessageTypes")] string MessageType)
        {
            var messageType = messageTypeFor(MessageType);

            _options.Publish(x =>
            {
                x.Message(messageType);
                x.Locally();
            });
        }


        [FormatAs("Sends message {messageType} to {channel}")]
        public void SendMessage([SelectionList("MessageTypes")] string messageType,
            [SelectionList("Channels")] Uri channel)
        {
            var type = messageTypeFor(messageType);

            _options.Publish(x => { x.Message(type).To(channel); });

            // Just makes the test harness listen for things
            _options.ListenForMessagesFrom(channel);
        }


        [ExposeAsTable("The available custom media writers are")]
        public void CustomWritersAre([SelectionList("MessageTypes")] string MessageType, string ContentType)
        {
            var messageType = messageTypeFor(MessageType);
            var writer = new FakeWriter(messageType, ContentType);
            _options.Services.For<IMessageSerializer>().Add(writer);
        }

        [FormatAs("For message type {MessageType}")]
        public void ForMessage([SelectionList("MessageTypes")] string MessageType)
        {
            var messageType = messageTypeFor(MessageType);

            if (_host == null)
            {
                _host = JasperHost.For(_options);
            }

            var router = _host.Get<IEnvelopeRouter>();

            _tracks = router.RouteByType(messageType).Routes.ToArray();
        }

        [FormatAs("There should be no routes")]
        public bool NoRoutesFor()
        {
            StoryTellerAssert.Fail(_tracks.Any(),
                () => { return "Found message routes:\n" + _tracks.Select(x => x.ToString()).Join("\n"); });

            return true;
        }


        public IGrammar TheRoutesShouldBe()
        {
            return VerifySetOf(() => _tracks)
                .Titled("The routes should be")
                .MatchOn(x => x.Destination, x => x.ContentType);
        }

        public override void SetUp()
        {
            _options = new JasperOptions();

            _options.Handlers.DisableConventionalDiscovery();
        }

        public override void TearDown()
        {
            _host?.Dispose();
            _host = null;
        }
    }

    internal class FakeWriter : IMessageSerializer
    {
        public FakeWriter(Type messageType, string contentType)
        {
            DotNetType = messageType;
            ContentType = contentType;
        }

        public Type DotNetType { get; }
        public string ContentType { get; }

        public byte[] Write(object model)
        {
            throw new NotImplementedException();
        }

        public object ReadFromData(Type messageType, byte[] data)
        {
            throw new NotImplementedException();
        }

        public object ReadFromData(byte[] data)
        {
            throw new NotImplementedException();
        }
    }
}
