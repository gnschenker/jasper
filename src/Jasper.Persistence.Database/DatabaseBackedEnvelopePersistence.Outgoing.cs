﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jasper.Transports;
using Jasper.Util;
using Weasel.Core;

namespace Jasper.Persistence.Database
{
    public partial class DatabaseBackedEnvelopePersistence
    {
        public abstract Task DiscardAndReassignOutgoing(Envelope[] discards, Envelope[] reassigned, int nodeId);
        public abstract Task DeleteOutgoing(Envelope[] envelopes);

        public Envelope[] AllOutgoingEnvelopes()
        {
            using var conn = DatabaseSettings.CreateConnection();
            conn.Open();

            return conn
                .CreateCommand(
                    $"select body, '{EnvelopeStatus.Outgoing}', owner_id, NULL from {DatabaseSettings.SchemaName}.{DatabaseConstants.OutgoingTable}")
                .LoadEnvelopes();
        }

        protected abstract string determineOutgoingEnvelopeSql(DatabaseSettings databaseSettings, AdvancedSettings settings);

        public Task<Envelope[]> LoadOutgoing(Uri destination)
        {
            return Session.Transaction.CreateCommand(determineOutgoingEnvelopeSql(DatabaseSettings, Settings))
                .With("destination", destination.ToString())
                .ExecuteToEnvelopes(_cancellation);
        }

        public abstract Task ReassignOutgoing(int ownerId, Envelope[] outgoing);

        public Task DeleteByDestination(Uri destination)
        {
            return Session.Transaction.CreateCommand($"delete from {DatabaseSettings.SchemaName}.{DatabaseConstants.OutgoingTable} where owner_id = :owner and destination = @destination")
                .With("destination", destination.ToString())
                .With("owner", TransportConstants.AnyNode)
                .ExecuteNonQueryAsync(_cancellation);
        }

        public Task DeleteOutgoing(Envelope envelope)
        {
            return DatabaseSettings
                .CreateCommand($"delete from {DatabaseSettings.SchemaName}.{DatabaseConstants.OutgoingTable} where id = @id")
                .With("id", envelope.Id)
                .ExecuteOnce(_cancellation);
        }

        public async Task<Uri[]> FindAllDestinations()
        {
            var cmd = Session.Transaction.CreateCommand($"select distinct destination from {DatabaseSettings.SchemaName}.{DatabaseConstants.OutgoingTable}");
            var uris = await cmd.FetchList<string>(cancellation: _cancellation);
            return uris.Select(x => x.ToUri()).ToArray();
        }

        public Task StoreOutgoing(Envelope envelope, int ownerId)
        {
            return DatabaseSettings.CreateCommand($"insert into {DatabaseSettings.SchemaName}.{DatabaseConstants.OutgoingTable} (id, owner_id, destination, deliver_by, body) values (@id, @owner, @destination, @deliverBy, @body)")
                .With("id", envelope.Id)
                .With("owner", ownerId)
                .With("destination", envelope.Destination.ToString())
                .With("deliverBy", envelope.DeliverBy)
                .With("body", envelope.Serialize())
                .ExecuteOnce(_cancellation);
        }


        public Task StoreOutgoing(Envelope[] envelopes, int ownerId)
        {
            var cmd = BuildOutgoingStorageCommand(envelopes, ownerId, DatabaseSettings);
            return cmd.ExecuteOnce(CancellationToken.None);
        }

        public Task StoreOutgoing(DbTransaction tx, Envelope[] envelopes)
        {
            var cmd = BuildOutgoingStorageCommand(envelopes, Settings.UniqueNodeId, DatabaseSettings);
            cmd.Connection = tx.Connection;
            cmd.Transaction = tx;

            return cmd.ExecuteNonQueryAsync(_cancellation);
        }

        public static DbCommand BuildOutgoingStorageCommand(Envelope[] envelopes, int ownerId,
            DatabaseSettings settings)
        {
            var builder = settings.ToCommandBuilder();

            builder.AddNamedParameter("owner", ownerId).DbType = DbType.Int32;

            foreach (var envelope in envelopes)
            {
                var id = builder.AddParameter(envelope.Id);
                var destination = builder.AddParameter(envelope.Destination.ToString());
                var deliverBy = builder.AddParameter(envelope.DeliverBy);
                var body = builder.AddParameter(envelope.Serialize());

                builder.Append(
                    $"insert into {settings.SchemaName}.{DatabaseConstants.OutgoingTable} (id, owner_id, destination, deliver_by, body) values (@{id.ParameterName}, @owner, @{destination.ParameterName}, @{deliverBy.ParameterName}, @{body.ParameterName});");
            }

            return builder.Compile();
        }

    }
}
