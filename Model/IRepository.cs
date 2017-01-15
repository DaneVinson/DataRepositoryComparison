using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
    public interface IRepository : IDisposable
    {
        bool Create(IEnumerable<IThing> things);
        Task<bool> CreateAsync(IEnumerable<IThing> things);
        bool Delete(IEnumerable<string> ids);
        Task<bool> DeleteAsync(IEnumerable<string> ids);
        IThing[] Get();
        IThing[] Get(IEnumerable<string> ids);
        Task<IThing[]> GetAsync();
        Task<IThing[]> GetAsync(IEnumerable<string> ids);
    }
}
