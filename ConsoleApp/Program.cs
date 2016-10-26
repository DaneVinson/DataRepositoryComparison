using AzureStorageTable;
using GenFu;
using log4net;
using log4net.Config;
using Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                int count = 500;
                int iterations = 5;
                Log.Info($"[{count} Things, {iterations} iterations]");

                for (int i = 0; i < iterations; i++)
                {
                    IEnumerable<IThing> things = NewThings(count);
                    IRepository repository = null;

                    //Log.Info("---Local SQL Server---");
                    //repository = new Sql.Dapper.Repository("LocalSqlConnection");
                    //ExeciseRepository(repository, things);

                    Log.Info("---Azure SQL, S0---");
                    repository = new Sql.Dapper.Repository("AzureSqlConnection");
                    ExeciseRepository(repository, things);

                    //Log.Info("---Azure Storage Table---");
                    //repository = new AzureStorageTable.Repository();
                    //ExeciseRepository(repository, things);

                    //Log.Info("---Azure DocumentDB---");
                    //repository = new DocumentDB.Repository();
                    //ExeciseRepository(repository, things);
                }
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

        private static void ExeciseRepository(IRepository repository, IEnumerable<IThing> things)
        {
            // Create new things
            int successCount = CreateThings(things, repository).Result;

            // Get all things
            IEnumerable<IThing> freshThings = GetAllThings(repository).Result.ToList();

            // Get each thing
            successCount = GetThings(freshThings, repository).Result;

            // Delete each thing
            successCount = DeleteThings(freshThings, repository).Result;
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


        private static List<Thing> NewThings(int count)
        {
            Random random = new Random();
            var genFuThings = A.ListOf<Thing>(count);

            var things = new List<Thing>();
            var counter = 0;
            while (counter < count)
            {
                things.Add(new Thing()
                {
                    Description = genFuThings[counter].Description,
                    Flag = random.Next() % 2 == 0,
                    Id = Guid.NewGuid().ToString(),
                    Stamp = genFuThings[counter].Stamp,
                    ThingId = counter++,
                    Value = random.NextDouble() * random.Next(1, 3) * 10
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
