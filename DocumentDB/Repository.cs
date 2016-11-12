using Microsoft.Azure;
using Microsoft.Azure.Documents;
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

        public async Task<bool> CreateAsync(IEnumerable<IThing> things)
        {
            var tasks = new List<Task<ResourceResponse<Document>>>();
            foreach(var thing in things)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
                tasks.Add(Client.UpsertDocumentAsync(uri, thing));
            }
            var responses = await Task.WhenAll(tasks);
            return !responses.Any(r => r == null || !((int)r.StatusCode).IsHttpSuccess());
        }

        public async Task<bool> DeleteAsync(IEnumerable<string> ids)
        {
            var tasks = new List<Task<ResourceResponse<Document>>>();
            foreach (var id in ids)
            {
                var uri = UriFactory.CreateDocumentUri(DBName, CollectionName, id);
                tasks.Add(Client.DeleteDocumentAsync(uri));
            }
            var responses = await Task.WhenAll(tasks);
            return !responses.Any(r => r == null || !((int)r.StatusCode).IsHttpSuccess());
        }

        public void Dispose()
        {
            if (Client != null) { Client.Dispose(); }
        }

        public async Task<IThing[]> GetAsync()
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
            var query = Client.CreateDocumentQuery<Thing>(uri).AsDocumentQuery();
            List<IThing> things = new List<IThing>();
            while (query.HasMoreResults)
            {
                var result = await query.ExecuteNextAsync<Thing>();
                things.AddRange(result.ToArray());
            }
            return things.ToArray();
        }

        public async Task<IThing[]> GetAsync(IEnumerable<string> ids)
        {
            var tasks = new List<Task<FeedResponse<Thing>>>();
            foreach(var id in ids)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
                var query = Client.CreateDocumentQuery<Thing>(uri).Where(d => d.Id == id).AsDocumentQuery();
                tasks.Add(query.ExecuteNextAsync<Thing>());
            }
            var results = await Task.WhenAll(tasks);
            return results.Select(r => r.FirstOrDefault()).ToArray();
        }

        #endregion

        private DocumentClient Client { get; }

        private const string CollectionName = "things";

        private string DBName { get; }
    }
}
