﻿using System;
using System.Threading.Tasks;
using Jasper.Persistence.Durability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jasper.Persistence
{
    public static class HostPersistenceExtensions
    {

        /// <summary>
        ///     Drops and recreates the Sql Server backed persistence database objects
        /// </summary>
        /// <param name="host"></param>
        [Obsolete("Replace with Oakton equivalents")]
        public static Task RebuildMessageStorage(this IHost host)
        {
            return host.Services.GetRequiredService<IEnvelopePersistence>().Admin.RebuildStorageAsync();
        }

        /// <summary>
        /// Remove any persisted incoming, scheduled, or outgoing message
        /// envelopes from your underlying database
        /// </summary>
        /// <param name="host"></param>
        [Obsolete("Replace with Oakton equivalents")]
        public static Task ClearAllPersistedMessages(this IHost host)
        {
            return host.Services.GetRequiredService<IEnvelopePersistence>().Admin.ClearAllPersistedEnvelopes();
        }

    }
}
