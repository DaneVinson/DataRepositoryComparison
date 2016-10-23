using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Thing : IThing
    {
        public string Description { get; set; }
        public bool Flag { get; set; }
        [JsonProperty(PropertyName = "id")]
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
