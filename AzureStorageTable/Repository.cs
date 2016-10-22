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

        public async Task<bool> CreateAsync(IThing thing)
        {
            TableOperation insertOperation = TableOperation.Insert(thing as ThingEntity);
            var tableResult = await Table.ExecuteAsync(insertOperation);
            return IsHttpSuccess(tableResult.HttpStatusCode);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var entity = new DynamicTableEntity(ThingsPartitionKey, id.ToString());
            entity.ETag = "*";
            var tableResult = await Table.ExecuteAsync(TableOperation.Delete(entity));
            return IsHttpSuccess(tableResult.HttpStatusCode);
        }

        public async Task<ICollection<IThing>> GetAsync()
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
            return list;
        }

        public async Task<IThing> GetAsync(string id)
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<ThingEntity>(ThingsPartitionKey, id);
            TableResult retrievedResult = await Table.ExecuteAsync(retrieveOperation);
            return retrievedResult.Result as IThing;
        }

        #endregion

        private bool IsHttpSuccess(int httpStatusCode)
        {
            return httpStatusCode > 199 && httpStatusCode < 300;
        }


        private CloudTable Table { get; }


        private static readonly string StorageAccountKey = CloudConfigurationManager.GetSetting("StorageAccountKey");
        private static readonly string StorageAccountName = CloudConfigurationManager.GetSetting("StorageAccountName");
        private static readonly string StorageTableName = CloudConfigurationManager.GetSetting("StorageTableName");
        internal const string ThingsPartitionKey = "Rock";
    }
}
