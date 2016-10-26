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
        }

        #region IRepository

        public async Task<bool> CreateAsync(IThing thing)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                string sql = $"insert into Things (Description, Flag, Id, Stamp, Value) Values(@Description, @Flag, @Id, @Stamp, @Value)";
                await connection.OpenAsync();
                var createCount = await connection.ExecuteAsync(sql, thing);
                connection.Close();
                return createCount == 1;
            }
        }

        public async Task<bool> DeleteAsync(string id)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                string sql = "delete Things where ThingId = @ThingId";
                await connection.OpenAsync();
                var deleteCount = await connection.ExecuteAsync(sql, new { ThingId = Convert.ToInt32(id) });
                connection.Close();
                return deleteCount == 1;
            }
        }

        public void Dispose()
        { }

        public async Task<ICollection<IThing>> GetAsync()
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

        public async Task<IThing> GetAsync(string id)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                string sql = "select * from Things where ThingId = @ThingId";
                await connection.OpenAsync();
                var thing = await connection.QueryFirstAsync<Thing>(sql, new { ThingId = Convert.ToInt32(id) });
                connection.Close();
                return thing;
            }
        }

        #endregion

        private string ConnectionString { get; }
    }
}
