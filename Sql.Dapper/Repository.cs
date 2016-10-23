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
        public Repository()
        {
            Connection = new SqlConnection(CloudConfigurationManager.GetSetting("SqlConnectionString"));
        }

        #region IRepository

        public async Task<bool> CreateAsync(IThing thing)
        {
            string sql = $"insert into Things (Description, Flag, Id, Stamp, Value) Values(@Description, @Flag, @Id, @Stamp, @Value)";
            var createCount = await Connection.ExecuteAsync(sql, thing);
            return createCount == 1;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            string sql = "delete Things where ThingId = @ThingId";
            var deleteCount = await Connection.ExecuteAsync(sql, new { ThingId = Convert.ToInt32(id) });
            return deleteCount == 1;
        }

        public void Dispose()
        {
            if (Connection != null) { Connection.Dispose(); }
        }

        public async Task<ICollection<IThing>> GetAsync()
        {
            string sql = "select * from Things";
            var results = await Connection.QueryAsync<Thing>(sql);
            return results.ToArray();
        }

        public async Task<IThing> GetAsync(string id)
        {
            string sql = "select * from Things where ThingId = @ThingId";
            return await Connection.QueryFirstAsync<Thing>(sql, new { ThingId = Convert.ToInt32(id) });
        }

        #endregion

        private SqlConnection Connection { get; }
    }
}
