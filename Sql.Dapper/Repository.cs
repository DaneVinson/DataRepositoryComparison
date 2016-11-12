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
        }

        #region IRepository

        public async Task<bool> CreateAsync(IEnumerable<IThing> things)
        {
            var createCount = 0;
            var skip = 0;
            while (skip <= things.Count())
            {
                var connections = new List<SqlConnection>();
                var tasks = new List<Task<int>>();
                foreach (var thing in things.Skip(skip).Take(MaxConnections))
                {
                    var connection = new SqlConnection(ConnectionString);
                    connections.Add(connection);
                    string sql = $"insert into Things (Description, Flag, Id, Stamp, Value) Values(@Description, @Flag, @Id, @Stamp, @Value)";
                    await connection.OpenAsync();
                    tasks.Add(connection.ExecuteAsync(sql, thing));
                }
                var inserts = await Task.WhenAll(tasks);
                createCount += inserts.Sum();
                connections.ForEach(c =>
                {
                    c.Close();
                    c.Dispose();
                });
                skip += MaxConnections;
            }
            return createCount == things.Count();
        }

        public async Task<bool> DeleteAsync(IEnumerable<string> ids)
        {
            var deleteCount = 0;
            var skip = 0;
            while (skip <= ids.Count())
            {
                var connections = new List<SqlConnection>();
                var tasks = new List<Task<int>>();
                foreach (var id in ids.Skip(skip).Take(MaxConnections))
                {
                    var connection = new SqlConnection(ConnectionString);
                    connections.Add(connection);
                    string sql = "delete Things where ThingId = @ThingId";
                    await connection.OpenAsync();
                    tasks.Add(connection.ExecuteAsync(sql, new { ThingId = Convert.ToInt32(id) }));
                }
                var deletes = await Task.WhenAll(tasks);
                deleteCount += deletes.Sum();
                connections.ForEach(c =>
                {
                    c.Close();
                    c.Dispose();
                });
                skip += MaxConnections;
            }
            return deleteCount == ids.Count();
        }

        public void Dispose()
        { }

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
            while (skip <= ids.Count())
            {
                var connections = new List<SqlConnection>();
                var tasks = new List<Task<Thing>>();
                foreach (var id in ids.Skip(skip).Take(MaxConnections))
                {
                    var connection = new SqlConnection(ConnectionString);
                    connections.Add(connection);
                    string sql = "select * from Things where ThingId = @ThingId";
                    await connection.OpenAsync();
                    tasks.Add(connection.QueryFirstAsync<Thing>(sql, new { ThingId = Convert.ToInt32(id) }));
                }
                things.AddRange(await Task.WhenAll(tasks));
                connections.ForEach(c =>
                {
                    c.Close();
                    c.Dispose();
                });
                skip += MaxConnections;
            }
            return things.ToArray();
        }

        #endregion

        private string ConnectionString { get; }

        private int MaxConnections { get; }
    }
}
