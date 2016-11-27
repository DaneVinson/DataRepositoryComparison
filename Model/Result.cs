using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public class Result
    {
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string Action { get; set; }
        public long Milliseconds { get; set; }
        public string RepositoryType { get; set; }
        public int ThingCount { get; set; }
    }
}
