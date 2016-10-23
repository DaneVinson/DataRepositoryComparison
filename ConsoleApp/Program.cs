using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Config;
using Model;
using AzureStorageTable;
using System.Diagnostics;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                IRepository repository = null;
                //repository = new AzureStorageTable.Repository();
                //repository = new Sql.Dapper.Repository();
                repository = new DocumentDB.Repository();

                int count = 1;
                var things = NewThings(count);
                Log.Info($"{count} Things");

                // Create new things
                int successCount = CreateThings(things, repository).Result;

                // Get all things
                things = GetAllThings(repository).Result.ToList();

                // Get each thing
                successCount = GetThings(things, repository).Result;

                // Delete each thing
                successCount = DeleteThings(things, repository).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0} - {1}", ex.GetType(), ex.Message);
                Console.WriteLine(ex.StackTrace ?? String.Empty);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("...");
                Console.ReadKey();
            }
        }

        private static async Task<int> CreateThings(IEnumerable<IThing> things, IRepository repository)
        {
            var stopWatch = new Stopwatch();
            List<IThing> createThings = things.ToList();

            // Azure Storage Table requires its own IThing implementation.
            if (repository.GetType() == typeof(AzureStorageTable.Repository))
            {
                createThings.Clear();
                foreach(var thing in things)
                {
                    createThings.Add(new ThingEntity(thing));
                }
            }

            List<Task<bool>> tasks = new List<Task<bool>>();
            stopWatch.Start();
            createThings.ToList().ForEach(t => tasks.Add(repository.CreateAsync(t)));
            var results = await Task.WhenAll(tasks);
            stopWatch.Stop();
            var successCount = results.Where(r => r).Count();
            Log.Info($"{repository.GetType()} - Create: {successCount}, ms: {stopWatch.ElapsedMilliseconds}");
            return successCount;
        }

        private static async Task<int> DeleteThings(IEnumerable<IThing> things, IRepository repository)
        {
            var stopWatch = new Stopwatch();
            List<Task<bool>> tasks = new List<Task<bool>>();

            stopWatch.Start();

            // For SQL repository use ThingId.ToString() instead of Id.
            if (repository.GetType() == typeof(Sql.Dapper.Repository))
            {
                things.ToList().ForEach(t => tasks.Add(repository.DeleteAsync(t.ThingId.ToString())));
            }
            else
            {
                things.ToList().ForEach(t => tasks.Add(repository.DeleteAsync(t.Id)));
            }

            var results = await Task.WhenAll(tasks);
            stopWatch.Stop();
            var successCount = results.Where(r => r).Count();
            Log.Info($"{repository.GetType()} - Deleted: {successCount}, ms: {stopWatch.ElapsedMilliseconds}");
            return successCount;
        }

        private static async Task<ICollection<IThing>> GetAllThings(IRepository repository)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var returnedThings = await repository.GetAsync();
            stopWatch.Stop();

            var successCount = returnedThings.Count;
            Log.Info($"{repository.GetType()} - Get All: {successCount}, ms: {stopWatch.ElapsedMilliseconds}");
            return returnedThings;
        }

        private static async Task<int> GetThings(IEnumerable<IThing> things, IRepository repository)
        {
            var stopWatch = new Stopwatch();
            var tasks = new List<Task<IThing>>();

            stopWatch.Start();

            // For SQL repository use ThingId.ToString() instead of Id.
            if (repository.GetType() == typeof(Sql.Dapper.Repository))
            {
                things.ToList().ForEach(t => tasks.Add(repository.GetAsync(t.ThingId.ToString())));
            }
            else
            {
                things.ToList().ForEach(t => tasks.Add(repository.GetAsync(t.Id)));
            }

            var results = await Task.WhenAll(tasks);
            stopWatch.Stop();
            var successCount = results.Where(r => r != null).Count();
            Log.Info($"{repository.GetType()} - Get by Id: {successCount} / {things.Count()}, {stopWatch.ElapsedMilliseconds} ms");
            return successCount;
        }


        private static List<IThing> NewThings(int count)
        {
            var things = new List<IThing>();
            var counter = 0;
            while (counter < count)
            {
                things.Add(new Thing()
                {
                    Description = "randomize with GenFu",
                    Flag = true || false,
                    Id = Guid.NewGuid().ToString(),
                    Stamp = DateTime.UtcNow,
                    ThingId = counter++,
                    Value = 23.5
                });
            }
            return things;
        }


        private static readonly ILog Log = log4net.LogManager.GetLogger(typeof(Program));

        static Program()
        {
            XmlConfigurator.Configure();
        }
    }
}
