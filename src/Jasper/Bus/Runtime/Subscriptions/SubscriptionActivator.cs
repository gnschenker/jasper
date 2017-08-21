﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Baseline;
using Jasper.Bus.Configuration;
using Jasper.Util;

namespace Jasper.Bus.Runtime.Subscriptions
{
    public interface ISubscriptionActivator
    {
        Task Activate();
    }

    public class SubscriptionActivator : ISubscriptionActivator
    {
        private readonly ISubscriptionsRepository _subscriptions;
        private readonly IEnvelopeSender _sender;
        private readonly INodeDiscovery _nodeDiscovery;
        private readonly IEnumerable<ISubscriptionRequirements> _requirements;
        private readonly ChannelGraph _channels;
        private readonly EnvironmentSettings _settings;
        private readonly UriAliasLookup _lookups;

        public SubscriptionActivator(ISubscriptionsRepository subscriptions, IEnvelopeSender sender, INodeDiscovery nodeDiscovery, IEnumerable<ISubscriptionRequirements> requirements, ChannelGraph channels, EnvironmentSettings settings, UriAliasLookup lookups)
        {
            _subscriptions = subscriptions;
            _sender = sender;
            _nodeDiscovery = nodeDiscovery;
            _requirements = requirements;
            _channels = channels;
            _settings = settings;
            _lookups = lookups;
        }

        public async Task Activate()
        {
            var local = new TransportNode(_channels, _settings.MachineName);

            await _nodeDiscovery.Register(local);
            await setupSubscriptions(_channels);
        }

        private async Task setupSubscriptions(ChannelGraph graph)
        {
            var staticSubscriptions = _requirements
                .SelectMany(x => x.DetermineRequirements())
                .Select(x =>
                {
                    x.Publisher = graph.Name;
                    x.Role = SubscriptionRole.Subscribes;
                    x.Destination = x.Destination.ToMachineUri();
                    return x;
                }).ToArray();

            await applyLookups(staticSubscriptions);

            await _subscriptions.PersistSubscriptions(staticSubscriptions);

            await sendSubscriptions();
        }

        internal async Task applyLookups(Subscription[] staticSubscriptions)
        {
            var destinations = staticSubscriptions.Select(x => x.Destination)
                .Concat(staticSubscriptions.Select(x => x.Source)).Distinct().ToArray();

            await _lookups.ReadAliases(destinations);

            foreach (var subscription in staticSubscriptions)
            {
                subscription.Destination = _lookups.Resolve(subscription.Destination);
                subscription.Source = _lookups.Resolve(subscription.Source);
            }
        }

        private async Task sendSubscriptions()
        {
            var subscriptions = await _subscriptions.LoadSubscriptions(SubscriptionRole.Subscribes);

                subscriptions.GroupBy(x => x.Source)
                .Each(group =>
                {
                    var envelope = new Envelope
                    {
                        Message = new SubscriptionRequested
                        {
                            Subscriptions = group.Each(x => x.Source = x.Source.ToMachineUri()).ToArray()
                        },
                        Destination = group.Key
                    };

                    _sender.Send(envelope);
                });
        }
    }
}
