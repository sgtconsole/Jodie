using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using Jodie.Utility;
using Newtonsoft.Json;

namespace Jodie
{
    /// <summary>
    /// This is a simple example implementation of an event store, using a SQL database
    /// to provide the storage. Tested and known to work with SQL Server.
    /// </summary>
    public class SqlEventStore : IEventStore
    {
        private readonly string _connectionString;
        private readonly IHostNameResolver _hostNameResolver;
        private readonly IClock _clock;

        public SqlEventStore(
            string connectionString,
            IHostNameResolver hostNameResolver,
            IClock clock)
        {
            _connectionString = connectionString;
            _hostNameResolver = hostNameResolver;
            _clock = clock;
        }

        public IEnumerable LoadEvents(string aggregateId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = con;
                    cmd.CommandText = @"
                        SELECT [FullyQualifiedTypeName], [Body]
                        FROM [dbo].[Event]
                        WHERE [AggregateInstanceId] = @AggregateId
                        ORDER BY [SequenceNumber]";
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddWithValue("AggregateId", aggregateId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            yield return JsonConvert.DeserializeObject(r.GetString(1),Type.GetType(r.GetString(0)));
                        }
                    }
                }
            }
        }

        public int GetAggregateEventCount(string aggregateId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = con;
                    cmd.CommandText = @"
                        SELECT COUNT(*)
                        FROM [dbo].[Event]
                        WHERE [AggregateInstanceId] = @AggregateId";
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddWithValue("AggregateId", aggregateId);
                    var response = cmd.ExecuteScalar();
                    return response.GetType() != typeof(DBNull) ? (int) response : 0;
                }
            }
        }


        public void SaveAggregate(string aggregateId, Type aggregateType)
        {
            using (var cmd = new SqlCommand())
            {
                cmd.CommandText =
                    @"IF NOT EXISTS(SELECT Id FROM [AggregateInstance] WHERE Id = @Id)
                        BEGIN
                            INSERT INTO [AggregateInstance] (Id, FullyQualifiedTypeName) VALUES (@Id,@Type)
                        END";

                cmd.Parameters.AddWithValue("Id",aggregateId);
                cmd.Parameters.AddWithValue("Type", aggregateType.AssemblyQualifiedName);

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    cmd.Connection = con;
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
        }
        
        public void SaveEvents(string aggregateId, IEnumerable<IEvent> newEvents, Type aggregateType)
        {
            if (GetAggregateEventCount(aggregateId) == 0)
                SaveAggregate(aggregateId, aggregateType);

            Retries.Retry(() => InternalSaveEvents(aggregateId, newEvents), new TimeSpan(0, 0, 2), 5);
        }

        private void InternalSaveEvents(string aggregateId, IEnumerable<IEvent> newEvents)
        {
            var hostName = _hostNameResolver.GetHostName();
            
            using (var cmd = new SqlCommand())
            {
                var queryText = new StringBuilder(512);
                queryText.AppendLine("BEGIN TRANSACTION;");
                cmd.Parameters.AddWithValue("CommitDateTime", _clock.GetUtcNow());
                cmd.Parameters.AddWithValue("AggregateId", aggregateId);
                cmd.Parameters.AddWithValue("HostName", hostName);

                var eventsLoaded = GetAggregateEventCount(aggregateId);

                var i = 0;

                foreach (var e in newEvents)
                {
                    queryText.AppendFormat(
                        @"INSERT INTO [dbo].[Event] ([AggregateInstanceId], [SequenceNumber], [FullyQualifiedTypeName], [Body], [CommitDateTime], HostName)
                          VALUES(@AggregateId, {0}, @Type{1}, @Body{1}, @CommitDateTime, @HostName);",
                        eventsLoaded + i, i);
                    cmd.Parameters.AddWithValue("Type" + i, e.GetType().AssemblyQualifiedName);
                    cmd.Parameters.AddWithValue("Body" + i, JsonConvert.SerializeObject(e));

                    i++;
                }

                queryText.Append("COMMIT;");

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    cmd.Connection = con;
                    cmd.CommandText = queryText.ToString();
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
