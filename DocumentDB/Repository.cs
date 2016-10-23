using Microsoft.Azure;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentDB
{
    public class Repository : IRepository
    {
        public Repository()
        {
            Client = new DocumentClient(
                            new Uri(CloudConfigurationManager.GetSetting("DocumentDBUri")), 
                            CloudConfigurationManager.GetSetting("DocumentDBKey"));
            DBName = CloudConfigurationManager.GetSetting("DocumentDBName");
        }

        #region IRepository

        public async Task<bool> CreateAsync(IThing thing)
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
            var response = await Client.UpsertDocumentAsync(uri, thing);
            return ((int)response.StatusCode).IsHttpSuccess();
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var uri = UriFactory.CreateDocumentUri(DBName, CollectionName, id);
            var response = await Client.DeleteDocumentAsync(uri);
            return ((int)response.StatusCode).IsHttpSuccess();
        }

        public void Dispose()
        {
            if (Client != null) { Client.Dispose(); }
        }

        public async Task<ICollection<IThing>> GetAsync()
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
            var query = Client.CreateDocumentQuery<Thing>(uri).AsDocumentQuery();
            List<IThing> things = new List<IThing>();
            while (query.HasMoreResults)
            {
                var result = await query.ExecuteNextAsync<Thing>();
                things.AddRange(result.ToArray());
            }
            return things;
        }

        public async Task<IThing> GetAsync(string id)
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
            var query = Client.CreateDocumentQuery<Thing>(uri).Where(d => d.Id == id).AsDocumentQuery();
            var result = await query.ExecuteNextAsync<Thing>();
            return result.FirstOrDefault();
        }

        #endregion

        private DocumentClient Client { get; }

        private const string CollectionName = "things";

        private string DBName { get; }
    }
}
