using AzureStorageTable;
using AzureStorageBlob;
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
using Newtonsoft.Json;
using System.IO;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                ExerciseRepositories(1000, 2);

                //var filePath = @"C:\Users\dvinson\Documents\GitHub\DataRepositoryComparison\ConsoleApp\bin\Debug\DataRepositoryComparison.log";
                //var results = ReadData(filePath);

                //CleanDocumentDB();
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"{ex.GetType().Name} - First of {ex.InnerExceptions.Count} inner exceptions");
                var firstEx = ex.InnerExceptions.First();
                Console.WriteLine("{0} - {1}", firstEx.GetType(), firstEx.Message);
                Console.WriteLine(firstEx.StackTrace ?? String.Empty);
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

        #region Data Generation

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
            var result = new Result()
            {
                Action = TestAction.Create.ToString(),
                Milliseconds = stopWatch.ElapsedMilliseconds,
                RepositoryType = repository.GetType().ToString(),
                ThingCount = things.Count()
            };
            Log.Info(result.ToString());
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

            var result = new Result()
            {
                Action = TestAction.Delete.ToString(),
                Milliseconds = stopWatch.ElapsedMilliseconds,
                RepositoryType = repository.GetType().ToString(),
                ThingCount = things.Count()
            };
            Log.Info(result.ToString());

            return success;
        }

        private static void ExerciseRepositories(int thingCount, int iterations)
        {
            Log.Info($"[ThingCount: {thingCount}, Iterations: {iterations}]");
            for (int i = 0; i < iterations; i++)
            {
                IEnumerable<IThing> things = NewThings(thingCount);

                //Log.Info("[Local SQL Server]");
                //ExeciseRepository(typeof(Sql.Dapper.Repository), things, "LocalSqlConnection");

                //Log.Info("[Azure SQL, Basic]");
                //ExeciseRepository(typeof(Sql.Dapper.Repository), things, "AzureSqlConnection");

                //Log.Info("[Azure Storage BLOB]");
                //ExeciseRepository(typeof(AzureStorageBlob.Repository), things);

                //Log.Info("[Azure Storage Table]");
                //ExeciseRepository(typeof(AzureStorageTable.Repository), things);

                Log.Info("[Azure DocumentDB]");
                ExeciseRepository(typeof(DocumentDB.Repository), things);
            }
        }

        private static void ExeciseRepository(Type repositoryType, IEnumerable<IThing> things, string connectionName = null)
        {
            bool success;

            // Create new things
            using (var repository = GetRepository(repositoryType, connectionName))
            {
                success = CreateThings(things, repository).Result;
            }

            // Get all things
            IEnumerable<IThing> freshThings;
            using (var repository = GetRepository(repositoryType, connectionName))
            {
                freshThings = GetAllThings(repository).Result.ToList();
            }

            // Get each thing
            using (var repository = GetRepository(repositoryType, connectionName))
            {
                success = GetThings(freshThings, repository).Result;
            }

            // Delete each thing
            using (var repository = GetRepository(repositoryType, connectionName))
            {
                success = DeleteThings(freshThings, repository).Result;
            }
        }

        private static async Task<ICollection<IThing>> GetAllThings(IRepository repository)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var returnedThings = await repository.GetAsync();
            stopWatch.Stop();

            var result = new Result()
            {
                Action = TestAction.GetAll.ToString(),
                Milliseconds = stopWatch.ElapsedMilliseconds,
                RepositoryType = repository.GetType().ToString(),
                ThingCount = returnedThings.Count()
            };
            Log.Info(result.ToString());

            return returnedThings;
        }

        private static IRepository GetRepository(Type repositoryType, string connectionName = null)
        {
            if (repositoryType == typeof(AzureStorageBlob.Repository))
            {
                return new AzureStorageBlob.Repository();
            }
            else if (repositoryType == typeof(AzureStorageTable.Repository))
            {
                return new AzureStorageTable.Repository();
            }
            else if (repositoryType == typeof(DocumentDB.Repository))
            {
                return new DocumentDB.Repository();
            }
            else if (repositoryType == typeof(Sql.Dapper.Repository))
            {
                return new Sql.Dapper.Repository(connectionName);
            }
            else { throw new ArgumentException($"{repositoryType} is an unexpected implementation of {nameof(IRepository)}"); }
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

            var result = new Result()
            {
                Action = TestAction.Get.ToString(),
                Milliseconds = stopWatch.ElapsedMilliseconds,
                RepositoryType = repository.GetType().ToString(),
                ThingCount = things.Count()
            };
            Log.Info(result.ToString());

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

        #endregion

        #region Reporting

        private static IEnumerable<Result> ReadData(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("{")) { continue; }
                    yield return JsonConvert.DeserializeObject<Result>(line);
                }
            }
        }

        #endregion

        #region Clean-up

        private static void CleanDocumentDB()
        {
            IRepository repository = new DocumentDB.Repository();
            var things = repository.GetAsync().Result;
            var success = DeleteThings(things, repository).Result;
        }

        #endregion

        private static readonly ILog Log = log4net.LogManager.GetLogger(typeof(Program));

        static Program()
        {
            XmlConfigurator.Configure();
        }
    }
}
