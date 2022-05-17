using System;
using System.Threading;
using System.Threading.Tasks;
using Jasper.Testing.Transports.Tcp.Protocol;
using Jasper.Transports.Sending;
using Jasper.Transports.Tcp;
using Jasper.Util;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TestingSupport;
using Xunit;

namespace Jasper.Testing.Transports.Tcp
{
    public class ping_handling
    {
        [Fact]
        public async Task ping_happy_path_with_tcp()
        {
            using (var runtime = JasperHost.For(opts => { opts.ListenAtPort(2222); }))
            {
                var sender = new BatchedSender("tcp://localhost:2222".ToUri(), new SocketSenderProtocol(),
                    CancellationToken.None, NullLogger.Instance);

                sender.RegisterCallback(new StubSenderCallback());

                await sender.PingAsync();
            }
        }

        [Fact]
        public async Task ping_sad_path_with_tcp()
        {
            var sender = new BatchedSender("tcp://localhost:3322".ToUri(), new SocketSenderProtocol(),
                CancellationToken.None, NullLogger.Instance);

            await Should.ThrowAsync<InvalidOperationException>(async () => { await sender.PingAsync(); });
        }
    }
}