using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureStorageBlob
{
    public class Repository : IRepository
    {
        public Repository()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse($"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey}");
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            Container = blobClient.GetContainerReference(StorageBlobContainerName);
        }


        public bool Create(IEnumerable<IThing> things)
        {
            foreach(var thing in things)
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(thing))))
                {
                    CloudBlockBlob blockBlob = Container.GetBlockBlobReference(thing.Id);
                    blockBlob.UploadFromStream(stream);
                }
            }
            return true;
        }

        public async Task<bool> CreateAsync(IEnumerable<IThing> things)
        {
            var tasks = new List<Task>();
            var streams = new List<MemoryStream>();
            foreach(var thing in things)
            {
                CloudBlockBlob blockBlob = Container.GetBlockBlobReference(thing.Id);
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(thing)));
                tasks.Add(blockBlob.UploadFromStreamAsync(stream));
                streams.Add(stream);
            }
            await Task.WhenAll(tasks);
            streams.ForEach(s => s.Dispose());
            return true;
        }

        public bool Delete(IEnumerable<string> ids)
        {
            var tasks = new List<Task<ICloudBlob>>();
            foreach (var id in ids)
            {
                Container.GetBlobReferenceFromServer(id).Delete();
            }
            return true;
        }

        public async Task<bool> DeleteAsync(IEnumerable<string> ids)
        {
            // Get blob references
            var tasks = new List<Task<ICloudBlob>>();
            foreach (var id in ids)
            {
                tasks.Add(Container.GetBlobReferenceFromServerAsync(id));
            }
            var blobReferences = await Task.WhenAll(tasks);

            // Delete blobs
            var tasks2 = new List<Task>();
            foreach(var blobReference in blobReferences)
            {
                tasks2.Add(blobReference.DeleteAsync());
            }
            await Task.WhenAll(tasks2);

            return true;
        }

        public void Dispose() { }

        public IThing[] Get()
        {
            var streams = new List<MemoryStream>();
            foreach (var blobItem in Container.ListBlobs())
            {
                var blob = (CloudBlockBlob)blobItem;
                var stream = new MemoryStream();
                blob.DownloadToStream(stream);
                streams.Add(stream);
            }
            return GetThingsFromStreams(streams);
        }

        public IThing[] Get(IEnumerable<string> ids)
        {
            var streams = new List<MemoryStream>();
            foreach (var id in ids)
            {
                var cloubBlob = Container.GetBlobReferenceFromServer(id);
                var stream = new MemoryStream();
                cloubBlob.DownloadToStream(stream);
                streams.Add(stream);
            }
            return GetThingsFromStreams(streams);
        }

        public async Task<IThing[]> GetAsync()
        {
            var tasks = new List<Task>();
            var streams = new List<MemoryStream>();
            foreach (var blobItem in Container.ListBlobs())
            {
                var blob = (CloudBlockBlob)blobItem;
                var stream = new MemoryStream();
                tasks.Add(blob.DownloadToStreamAsync(stream));
                streams.Add(stream);
            }
            await Task.WhenAll(tasks);

            return GetThingsFromStreams(streams);
        }

        public async Task<IThing[]> GetAsync(IEnumerable<string> ids)
        {
            // Get blob references
            var tasks = new List<Task<ICloudBlob>>();
            foreach (var id in ids)
            {
                tasks.Add(Container.GetBlobReferenceFromServerAsync(id));
            }
            var blobReferences = await Task.WhenAll(tasks);

            // Download blobs
            var tasks2 = new List<Task>();
            var streams = new List<MemoryStream>();
            foreach (var blobReference in blobReferences)
            {
                var stream = new MemoryStream();
                tasks2.Add(blobReference.DownloadToStreamAsync(stream));
                streams.Add(stream);
            }
            await Task.WhenAll(tasks2);

            return GetThingsFromStreams(streams);
        }


        private IThing[] GetThingsFromStreams(List<MemoryStream> streams)
        {
            var things = new List<IThing>();
            streams.ForEach(s =>
            {
                things.Add(JsonConvert.DeserializeObject<Thing>(Encoding.UTF8.GetString(s.ToArray())));
                s.Dispose();
            });
            return things.ToArray();
        }


        private CloudBlobContainer Container { get; }

        private static readonly string StorageAccountKey = CloudConfigurationManager.GetSetting("StorageAccountKey");
        private static readonly string StorageAccountName = CloudConfigurationManager.GetSetting("StorageAccountName");
        private static readonly string StorageBlobContainerName = CloudConfigurationManager.GetSetting("StorageBlobContainerName");
    }
}
