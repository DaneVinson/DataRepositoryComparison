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
                //CleanDocumentDB();
                //return;

                int count = 100;
                int iterations = 10;
                Log.Info($"[{count} Things, {iterations} iterations]");

                for (int i = 0; i < iterations; i++)
                {
                    IEnumerable<IThing> things = NewThings(count);
                    IRepository repository = null;

                    Log.Info("---Local SQL Server---");
                    repository = new Sql.Dapper.Repository("LocalSqlConnection");
                    ExeciseRepository(repository, things);

                    Log.Info("---Azure SQL, S0---");
                    repository = new Sql.Dapper.Repository("AzureSqlConnection");
                    ExeciseRepository(repository, things);

                    Log.Info("---Azure Storage Table---");
                    repository = new AzureStorageTable.Repository();
                    ExeciseRepository(repository, things);

                    Log.Info("---Azure DocumentDB---");
                    repository = new DocumentDB.Repository();
                    ExeciseRepository(repository, things);
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

        private static void CleanDocumentDB()
        {
            IRepository repository = new DocumentDB.Repository();
            var things = repository.GetAsync().Result;
            var success = DeleteThings(things, repository).Result;
        }


        private static async Task<bool> CreateThings(IEnumerable<IThing> things, IRepository repository)
        {
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

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var success = await repository.CreateAsync(createThings);
            stopWatch.Stop();
            Log.Info($"{repository.GetType()} - Created {things.Count()} Things: {success}, ms: {stopWatch.ElapsedMilliseconds}");
            return success;
        }

        private static async Task<bool> DeleteThings(IEnumerable<IThing> things, IRepository repository)
        {
            var success = false;
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            // For SQL repository use ThingId.ToString() instead of Id.
            if (repository.GetType() == typeof(Sql.Dapper.Repository))
            {
                success = await repository.DeleteAsync(things.Select(t => t.ThingId.ToString()));
            }
            else
            {
                success = await repository.DeleteAsync(things.Select(t => t.Id));
            }

            stopWatch.Stop();

            Log.Info($"{repository.GetType()} - Delete {things.Count()}: {success}, {stopWatch.ElapsedMilliseconds} ms");
            return success;
        }

        private static void ExeciseRepository(IRepository repository, IEnumerable<IThing> things)
        {
            // Create new things
            bool success = CreateThings(things, repository).Result;

            // Get all things
            IEnumerable<IThing> freshThings = GetAllThings(repository).Result.ToList();

            // Get each thing
            success = GetThings(freshThings, repository).Result;

            // Delete each thing
            success = DeleteThings(freshThings, repository).Result;
        }

        private static async Task<ICollection<IThing>> GetAllThings(IRepository repository)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var returnedThings = await repository.GetAsync();
            stopWatch.Stop();

            var successCount = returnedThings.Count();
            Log.Info($"{repository.GetType()} - Get All: {successCount}, ms: {stopWatch.ElapsedMilliseconds}");
            return returnedThings;
        }

        private static async Task<bool> GetThings(IEnumerable<IThing> things, IRepository repository)
        {
            IThing[] foundThings = new IThing[0];
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            // For SQL repository use ThingId.ToString() instead of Id.
            if (repository.GetType() == typeof(Sql.Dapper.Repository))
            {
                foundThings = await repository.GetAsync(things.Select(t => t.ThingId.ToString()));
            }
            else
            {
                foundThings = await repository.GetAsync(things.Select(t => t.Id));
            }

            stopWatch.Stop();
            Log.Info($"{repository.GetType()} - Get by Id: {foundThings.Length} / {things.Count()}, {stopWatch.ElapsedMilliseconds} ms");
            return foundThings.Length == things.Count();
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
