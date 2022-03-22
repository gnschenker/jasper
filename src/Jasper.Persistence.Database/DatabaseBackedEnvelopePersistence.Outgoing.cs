﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Jasper.Transports;
using Jasper.Util;
using Weasel.Core;
using DbCommandBuilder = Weasel.Core.DbCommandBuilder;

namespace Jasper.Persistence.Database
{
    public partial class DatabaseBackedEnvelopePersistence
    {
        public static async Task<Envelope> ReadOutgoing(DbDataReader reader, CancellationToken cancellation = default)
        {
            var envelope = new Envelope
            {
                Data = await reader.GetFieldValueAsync<byte[]>(0, cancellation),
                Id = await reader.GetFieldValueAsync<Guid>(1, cancellation),
                OwnerId = await reader.GetFieldValueAsync<int>(2, cancellation),
                Destination = (await reader.GetFieldValueAsync<string>(3, cancellation)).ToUri()
            };

            // TODO -- eliminate the Uri parsing?

            if (!(await reader.IsDBNullAsync(4, cancellation)))
            {
                envelope.DeliverBy = await reader.GetFieldValueAsync<DateTimeOffset>(4, cancellation);
            }

            envelope.Attempts = await reader.GetFieldValueAsync<int>(5, cancellation);
            envelope.CausationId = await reader.MaybeRead<string>(6, cancellation);
            envelope.CorrelationId = await reader.MaybeRead<string>(7, cancellation);
            envelope.SagaId = await reader.MaybeRead<string>(8, cancellation);
            envelope.MessageType = await reader.GetFieldValueAsync<string>(9, cancellation);
            envelope.ContentType = await reader.GetFieldValueAsync<string>(10, cancellation);
            envelope.ReplyRequested = await reader.MaybeRead<string>(11, cancellation);
            envelope.AckRequested = await reader.GetFieldValueAsync<bool>(12, cancellation);
            envelope.ReplyUri = await reader.ReadUri(13, cancellation);

            return envelope;
        }



        public abstract Task DiscardAndReassignOutgoingAsync(Envelope?[] discards, Envelope?[] reassigned, int nodeId);
        public abstract Task DeleteOutgoingAsync(Envelope?[] envelopes);

        protected abstract string determineOutgoingEnvelopeSql(DatabaseSettings databaseSettings, AdvancedSettings settings);

        public Task<IReadOnlyList<Envelope?>> LoadOutgoingAsync(Uri? destination)
        {
            return Session.Transaction.CreateCommand(_outgoingEnvelopeSql)
                .With("destination", destination.ToString())
                .FetchList(r => ReadOutgoing(r, _cancellation), cancellation: _cancellation);
        }

        public abstract Task ReassignOutgoingAsync(int ownerId, Envelope?[] outgoing);

        public Task DeleteByDestinationAsync(Uri? destination)
        {
            return Session.Transaction.CreateCommand($"delete from {DatabaseSettings.SchemaName}.{DatabaseConstants.OutgoingTable} where owner_id = :owner and destination = @destination")
                .With("destination", destination.ToString())
                .With("owner", TransportConstants.AnyNode)
                .ExecuteNonQueryAsync(_cancellation);
        }

        public Task DeleteOutgoingAsync(Envelope? envelope)
        {
            return DatabaseSettings
                .CreateCommand($"delete from {DatabaseSettings.SchemaName}.{DatabaseConstants.OutgoingTable} where id = @id")
                .With("id", envelope.Id)
                .ExecuteOnce(_cancellation);
        }

        public async Task<Uri?[]> FindAllDestinationsAsync()
        {
            var cmd = Session.CreateCommand($"select distinct destination from {DatabaseSettings.SchemaName}.{DatabaseConstants.OutgoingTable}");
            var uris = await cmd.FetchList<string>(cancellation: _cancellation);
            return uris.Select(x => x.ToUri()).ToArray();
        }

        public Task StoreOutgoingAsync(Envelope? envelope, int ownerId)
        {
            return BuildOutgoingStorageCommand(envelope, ownerId, DatabaseSettings)
                .ExecuteOnce(_cancellation);
        }


        public Task StoreOutgoingAsync(Envelope[] envelopes, int ownerId)
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

        public static DbCommand BuildOutgoingStorageCommand(Envelope envelope, int ownerId,
            DatabaseSettings settings)
        {
            var builder = settings.ToCommandBuilder();

            var owner = builder.AddNamedParameter("owner", ownerId);
            ConfigureOutgoingCommand(settings, builder, envelope, owner);
            return builder.Compile();
        }

        public static DbCommand BuildOutgoingStorageCommand(Envelope[] envelopes, int ownerId,
            DatabaseSettings settings)
        {
            var builder = settings.ToCommandBuilder();

            var owner = builder.AddNamedParameter("owner", ownerId);

            foreach (var envelope in envelopes)
            {
                ConfigureOutgoingCommand(settings, builder, envelope, owner);
            }

            return builder.Compile();
        }

        private static void ConfigureOutgoingCommand(DatabaseSettings settings, DbCommandBuilder builder, Envelope envelope,
            DbParameter owner)
        {
            var list = new List<DbParameter>();

            list.Add(builder.AddParameter(envelope.Data));
            list.Add(builder.AddParameter(envelope.Id));
            list.Add(owner);
            list.Add(builder.AddParameter(envelope.Destination.ToString()));
            list.Add(builder.AddParameter(envelope.DeliverBy));

            list.Add(builder.AddParameter(envelope.Attempts));
            list.Add(builder.AddParameter(envelope.CausationId));
            list.Add(builder.AddParameter(envelope.CorrelationId));
            list.Add(builder.AddParameter(envelope.SagaId));
            list.Add(builder.AddParameter(envelope.MessageType));
            list.Add(builder.AddParameter(envelope.ContentType));
            list.Add(builder.AddParameter(envelope.ReplyRequested));
            list.Add(builder.AddParameter(envelope.AckRequested));
            list.Add(builder.AddParameter(envelope.ReplyUri?.ToString()));

            var parameterList = list.Select(x => $"@{x.ParameterName}").Join(", ");

            builder.Append(
                $"insert into {settings.SchemaName}.{DatabaseConstants.OutgoingTable} ({DatabaseConstants.OutgoingFields}) values ({parameterList});");
        }
    }
}
