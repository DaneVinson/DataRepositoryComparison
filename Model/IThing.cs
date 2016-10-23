using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public interface IThing
    {
        string Description { get; set; }
        bool Flag { get; set; }
        [JsonProperty(PropertyName = "id")]
        string Id { get; set; }
        DateTime Stamp { get; set; }
        int ThingId { get; set; }
        double Value { get; set; }
    }
}
