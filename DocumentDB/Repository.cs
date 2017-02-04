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
        public Repository(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) { name = String.Empty; }
            var dbName = $"DocumentDB{name}";
            Client = new DocumentClient(
                            new Uri(CloudConfigurationManager.GetSetting($"{dbName}Uri")), 
                            CloudConfigurationManager.GetSetting($"{dbName}Key"));
            DBName = CloudConfigurationManager.GetSetting($"{dbName}Name");
            int maxConnections = 0;
            if (!Int32.TryParse(CloudConfigurationManager.GetSetting($"Max{dbName}Connections"), out maxConnections))
            {
                maxConnections = 0;
            }
            MaxConnections = Math.Max(0, maxConnections);
        }

        #region IRepository

        public bool Create(IEnumerable<IThing> things)
        {
            var responses = new List<ResourceResponse<Document>>();
            foreach (var thing in things)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
                responses.Add(Client.UpsertDocumentAsync(uri, thing).Result);
            }
            int createCount = responses.Where(r => r != null && ((int)r.StatusCode).IsHttpSuccess()).Count();
            return createCount == things.Count();
        }

        public async Task<bool> CreateAsync(IEnumerable<IThing> things)
        {
            var createCount = 0;
            var skip = 0;
            var count = things.Count();
            while (skip < count)
            {
                var tasks = new List<Task<ResourceResponse<Document>>>();
                foreach (var thing in things.Skip(skip).Take(MaxConnections))
                {
                    var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
                    tasks.Add(Client.UpsertDocumentAsync(uri, thing));
                }
                var responses = await Task.WhenAll(tasks);
                createCount += responses.Where(r => r != null && ((int)r.StatusCode).IsHttpSuccess()).Count();
                skip += MaxConnections;
            }
            return createCount == things.Count();
        }

        public bool Delete(IEnumerable<string> ids)
        {
            var responses = new List<ResourceResponse<Document>>();
            foreach (var id in ids)
            {
                var uri = UriFactory.CreateDocumentUri(DBName, CollectionName, id);
                responses.Add(Client.DeleteDocumentAsync(uri).Result);
            }
            int deleteCount = responses.Where(r => r != null && ((int)r.StatusCode).IsHttpSuccess()).Count();
            return deleteCount == ids.Count();
        }

        public async Task<bool> DeleteAsync(IEnumerable<string> ids)
        {
            var deleteCount = 0;
            var skip = 0;
            while (skip <= ids.Count())
            {
                var tasks = new List<Task<ResourceResponse<Document>>>();
                foreach (var id in ids.Skip(skip).Take(MaxConnections))
                {
                    var uri = UriFactory.CreateDocumentUri(DBName, CollectionName, id);
                    tasks.Add(Client.DeleteDocumentAsync(uri));
                }
                var responses = await Task.WhenAll(tasks);
                deleteCount += responses.Where(r => r != null && ((int)r.StatusCode).IsHttpSuccess()).Count();
                skip += MaxConnections;
            }
            return deleteCount == ids.Count();
        }

        public void Dispose()
        {
            if (Client != null) { Client.Dispose(); }
        }

        public IThing[] Get()
        {
            var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
            var query = Client.CreateDocumentQuery<Thing>(uri).AsDocumentQuery();
            List<IThing> things = new List<IThing>();
            while (query.HasMoreResults)
            {
                things.AddRange(query.ExecuteNextAsync<Thing>().Result.ToArray());
            }
            return things.ToArray();
        }

        public IThing[] Get(IEnumerable<string> ids)
        {
            var responses = new List<FeedResponse<Thing>>();
            foreach (var id in ids)
            {
                var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
                var query = Client.CreateDocumentQuery<Thing>(uri).Where(d => d.Id == id).AsDocumentQuery();
                responses.Add(query.ExecuteNextAsync<Thing>().Result);
            }
            return responses.Select(r => r.FirstOrDefault()).ToArray();
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
            var things = new List<IThing>();
            var skip = 0;
            while (skip <= ids.Count())
            {
                var tasks = new List<Task<FeedResponse<Thing>>>();
                foreach (var id in ids.Skip(skip).Take(MaxConnections))
                {
                    var uri = UriFactory.CreateDocumentCollectionUri(DBName, CollectionName);
                    var query = Client.CreateDocumentQuery<Thing>(uri).Where(d => d.Id == id).AsDocumentQuery();
                    tasks.Add(query.ExecuteNextAsync<Thing>());
                }
                var results = await Task.WhenAll(tasks);
                things.AddRange(results.Select(r => r.FirstOrDefault()).ToArray());
                skip += MaxConnections;
            }
            return things.ToArray();
        }

        #endregion

        private DocumentClient Client { get; }

        private const string CollectionName = "things";

        private string DBName { get; }

        private int MaxConnections { get; }

    }
}
