using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jasper.Messaging.Durability;
using Jasper.Messaging.Runtime;
using Jasper.Messaging.Transports;
using Jasper.Persistence.Database;
using Jasper.Persistence.Postgresql.Util;
using NpgsqlTypes;

namespace Jasper.Persistence.Postgresql
{
    public class PostgresqlDurableIncoming : DataAccessor,IDurableIncoming
    {
        private readonly PostgresqlDurableStorageSession _session;
        private readonly PostgresqlSettings _settings;
        private readonly string _findAtLargeEnvelopesSql;
        private readonly string _reassignSql;
        private CancellationToken _cancellation;

        public PostgresqlDurableIncoming(PostgresqlDurableStorageSession session, PostgresqlSettings settings, JasperOptions options)
        {
            _session = session;
            _settings = settings;
            _findAtLargeEnvelopesSql =
                $"select body from {settings.SchemaName}.{IncomingTable} where owner_id = {TransportConstants.AnyNode} and status = '{TransportConstants.Incoming}' limit {options.Retries.RecoveryBatchSize}";

            _reassignSql =
                $"UPDATE {_settings.SchemaName}.{IncomingTable} SET owner_id = :owner, status = '{TransportConstants.Incoming}' WHERE id = ANY(:ids)";

            _cancellation = options.Cancellation;
        }

        public Task<Envelope[]> LoadPageOfLocallyOwned()
        {
            return _session
                .CreateCommand(_findAtLargeEnvelopesSql)
                .ExecuteToEnvelopes(_cancellation);
        }

        public Task Reassign(int ownerId, Envelope[] incoming)
        {
            return _session.CreateCommand(_reassignSql)
                .With("owner", ownerId, NpgsqlDbType.Integer)
                .With("ids", incoming.Select(x => x.Id).ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Uuid)
                .ExecuteNonQueryAsync(_cancellation);

        }
    }
}