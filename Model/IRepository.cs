using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public interface IRepository : IDisposable
    {
        Task<bool> CreateAsync(IThing thing);
        Task<bool> DeleteAsync(string id);
        Task<ICollection<IThing>> GetAsync();
        Task<IThing> GetAsync(string id);
    }
}
