using Microsoft.WindowsAzure.Storage.Table;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureStorageTable
{
    public class ThingEntity : TableEntity, IThing
    {
        public ThingEntity() : base()
        { }

        public ThingEntity(IThing sourceThing) : base(Repository.ThingsPartitionKey, sourceThing?.Id)
        {
            Description = sourceThing.Description;
            Flag = sourceThing.Flag;
            Id = sourceThing.Id;
            ThingId = sourceThing.ThingId;
            Stamp = sourceThing.Stamp;
            Value = sourceThing.Value;
        }

        public string Description { get; set; }
        public bool Flag { get; set; }
        public string Id { get; set; }
        public DateTime Stamp { get; set; }
        public int ThingId { get; set; }
        public double Value { get; set; }

        public override string ToString()
        {
            return $"ThingId: {ThingId}, Id: {Id}, Description: {Description}";
        }
    }
}
