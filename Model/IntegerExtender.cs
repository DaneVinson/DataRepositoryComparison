using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public static class IntegerExtender
    {
        public static bool IsHttpSuccess(this int httpStatusCode)
        {
            return httpStatusCode > 199 && httpStatusCode < 300;
        }
    }
}
