using Dapper;
using Microsoft.Azure;
using Model;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sql.Dapper
{
    public class Repository : IRepository
    {
        public Repository(string connectionName)
        {
            ConnectionString = CloudConfigurationManager.GetSetting(connectionName);
            int maxConnections = 0;
            if(!Int32.TryParse(CloudConfigurationManager.GetSetting("MaxSqlConnections"), out maxConnections))
            {
                maxConnections = 0;
            }
            MaxConnections = Math.Max(0, maxConnections);
            Connections = new SqlConnection[MaxConnections];
        }

        #region IRepository

        public bool Create(IEnumerable<IThing> things)
        {
            int createCount = 0;
            using (var connection = new SqlConnection(ConnectionString))
            {
                foreach (var thing in things)
                {
                    connection.Open();
                    createCount += connection.Execute(InsertSqlString, thing);
                    connection.Close();
                }
            }
            return createCount == things.Count();
        }

        public async Task<bool> CreateAsync(IEnumerable<IThing> things)
        {
            var createCount = 0;
            var skip = 0;
            var count = things.Count();
            while (skip < count)
            {
                var thingsBlock = things.Skip(skip).Take(MaxConnections).ToArray();

                await OpenConnectionsAsync(thingsBlock.Length);

                var tasks = new List<Task<int>>();
                for (int i = 0; i < thingsBlock.Length; i++)
                {
                    tasks.Add(Connections[i].ExecuteAsync(InsertSqlString, thingsBlock[i]));
                }
                var inserts = await Task.WhenAll(tasks);
                createCount += inserts.Sum();

                CleanUpConnections();

                skip += MaxConnections;
            }
            return createCount == things.Count();
        }

        public bool Delete(IEnumerable<string> ids)
        {
            int deleteCount = 0;
            using (var connection = new SqlConnection(ConnectionString))
            {
                foreach (var id in ids)
                {
                    connection.Open();
                    deleteCount += connection.Execute(DeleteSqlString, new { ThingId = Convert.ToInt32(id) });
                    connection.Close();
                }
            }
            return deleteCount == ids.Count();
        }

        public async Task<bool> DeleteAsync(IEnumerable<string> ids)
        {
            var deleteCount = 0;
            var skip = 0;
            var count = ids.Count();
            while (skip < count)
            {
                var idsBlock = ids.Skip(skip).Take(MaxConnections).ToArray();

                await OpenConnectionsAsync(idsBlock.Length);

                var tasks = new List<Task<int>>();
                for (int i = 0; i < idsBlock.Length; i++)
                {
                    tasks.Add(Connections[i].ExecuteAsync(DeleteSqlString, new { ThingId = Convert.ToInt32(idsBlock[i]) }));
                }
                var inserts = await Task.WhenAll(tasks);
                deleteCount += inserts.Sum();

                CleanUpConnections();

                skip += MaxConnections;
            }
            return deleteCount == ids.Count();
        }

        public void Dispose() { }

        public IThing[] Get()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                var results = connection.Query<Thing>(GetAllSqlString);
                connection.Close();
                return results.ToArray();
            }
        }

        public IThing[] Get(IEnumerable<string> ids)
        {
            List<IThing> things = new List<IThing>();
            using (var connection = new SqlConnection(ConnectionString))
            {
                foreach (var id in ids)
                {
                    connection.Open();
                    things.Add(connection.QueryFirst<Thing>(GetSqlString, new { ThingId = Convert.ToInt32(id) }));
                    connection.Close();
                }
            }
            return things.ToArray();
        }

        public async Task<IThing[]> GetAsync()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                string sql = "select * from Things";
                await connection.OpenAsync();
                var results = await connection.QueryAsync<Thing>(sql);
                connection.Close();
                return results.ToArray();
            }
        }

        public async Task<IThing[]> GetAsync(IEnumerable<string> ids)
        {
            var things = new List<IThing>();
            var skip = 0;
            var count = ids.Count();
            while (skip < count)
            {
                var idsBlock = ids.Skip(skip).Take(MaxConnections).ToArray();

                await OpenConnectionsAsync(idsBlock.Length);

                var tasks = new List<Task<Thing>>();
                for (int i = 0; i < idsBlock.Length; i++)
                {
                    tasks.Add(Connections[i].QueryFirstAsync<Thing>(GetSqlString, new { ThingId = Convert.ToInt32(idsBlock[i]) }));
                }
                things.AddRange(await Task.WhenAll(tasks));

                CleanUpConnections();

                skip += MaxConnections;
            }
            return things.ToArray();
        }

        #endregion

        #region Private Methods

        private void CleanUpConnections()
        {
            for (int i = 0; i < Connections.Length; i++)
            {
                if (Connections[i] != null)
                {
                    Connections[i].Close();
                    Connections[i].Dispose();
                    Connections[i] = null;
                }
            }
        }

        private async Task OpenConnectionsAsync(int count)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < count; i++)
            {
                Connections[i] = new SqlConnection(ConnectionString);
                tasks.Add(Connections[i].OpenAsync());
            }
            await Task.WhenAll(tasks);
        }

        #endregion

        #region Readonly Fields

        private readonly SqlConnection[] Connections;

        private readonly string ConnectionString;

        private readonly string DeleteSqlString = "delete Things where ThingId = @ThingId";

        private readonly string GetAllSqlString = "select * from Things";

        private readonly string GetSqlString = "select * from Things where ThingId = @ThingId";

        private readonly string InsertSqlString = "insert into Things (Description, Flag, Id, Stamp, Value) Values(@Description, @Flag, @Id, @Stamp, @Value)";

        private readonly int MaxConnections;

        #endregion
    }
}
