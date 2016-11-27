using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Queryable;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureStorageTable
{
    public class Repository : IRepository
    {
        public Repository()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse($"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey}");
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            Table = tableClient.GetTableReference(StorageTableName);
        }

        #region IRepository

        public async Task<bool> CreateAsync(IEnumerable<IThing> things)
        {
            var tasks = new List<Task<TableResult>>();
            foreach(var thing in things)
            {
                TableOperation insertOperation = TableOperation.Insert(thing as ThingEntity);
                tasks.Add(Table.ExecuteAsync(insertOperation));
            }
            var tableResults = await Task.WhenAll(tasks);
            return !tableResults.Any(r => r == null || !r.HttpStatusCode.IsHttpSuccess());
        }

        public async Task<bool> DeleteAsync(IEnumerable<string> ids)
        {
            var tasks = new List<Task<TableResult>>();
            foreach (var id in ids)
            {
                var entity = new DynamicTableEntity(ThingsPartitionKey, id);
                entity.ETag = "*";
                tasks.Add(Table.ExecuteAsync(TableOperation.Delete(entity)));
            }
            var tableResults = await Task.WhenAll(tasks);
            return !tableResults.Any(r => r == null || !r.HttpStatusCode.IsHttpSuccess());
        }

        public void Dispose() { }

        public async Task<IThing[]> GetAsync()
        {
            var tableQuery = new TableQuery<ThingEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, ThingsPartitionKey));

            // ExecuteQuerySegmentedAsync executes async operations in blocks of 1000. 
            // Currently the only available async method for querying multiple table rows (10/2016).
            TableContinuationToken continuationToken = null;
            var list = new List<IThing>();
            do
            {
                var tableQueryResult = await Table.ExecuteQuerySegmentedAsync(tableQuery, continuationToken);
                continuationToken = tableQueryResult.ContinuationToken;
                list.AddRange(tableQueryResult.Results);
            } while (continuationToken != null);
            return list.ToArray();
        }

        public async Task<IThing[]> GetAsync(IEnumerable<string> ids)
        {
            var tasks = new List<Task<TableResult>>();
            foreach (var id in ids)
            {
                TableOperation retrieveOperation = TableOperation.Retrieve<ThingEntity>(ThingsPartitionKey, id);
                tasks.Add(Table.ExecuteAsync(retrieveOperation));
            }
            var tableResults = await Task.WhenAll(tasks);
            return tableResults.Select(r => r.Result as IThing).ToArray();
        }

        #endregion

        private CloudTable Table { get; }


        private static readonly string StorageAccountKey = CloudConfigurationManager.GetSetting("StorageAccountKey");
        private static readonly string StorageAccountName = CloudConfigurationManager.GetSetting("StorageAccountName");
        private static readonly string StorageTableName = CloudConfigurationManager.GetSetting("StorageTableName");
        internal const string ThingsPartitionKey = "Rock";
    }
}
