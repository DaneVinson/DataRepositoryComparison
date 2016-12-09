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
                //ExerciseRepositories(1000, 11);

                var directoryPath = @"C:\temp\RepositoryCompare\";
                var fileName = "Compare_100x10.log";
                var results = ReadData(Path.Combine(directoryPath, fileName));
                OutputProcessedData(results, directoryPath);

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

        private static async Task<bool> CreateThings(IEnumerable<IThing> things, IRepository repository, string typeNameAppend)
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

            LogResult(TestAction.Create, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), typeNameAppend);

            return success;
        }

        private static async Task<bool> DeleteThings(IEnumerable<IThing> things, IRepository repository, string typeNameAppend)
        {
            var success = false;
            var stopWatch = new Stopwatch();

            // For SQL repository use ThingId.ToString() instead of Id.
            if (repository.GetType() == typeof(Sql.Dapper.Repository))
            {
                stopWatch.Start();
                success = await repository.DeleteAsync(things.Select(t => t.ThingId.ToString()));
            }
            else
            {
                stopWatch.Start();
                success = await repository.DeleteAsync(things.Select(t => t.Id));
            }

            stopWatch.Stop();

            LogResult(TestAction.Delete, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), typeNameAppend);

            return success;
        }

        private static void ExerciseRepositories(int thingCount, int iterations)
        {
            Log.Info($"[ThingCount: {thingCount}, Iterations: {iterations}]");
            for (int i = 0; i < iterations; i++)
            {
                IEnumerable<IThing> things = NewThings(thingCount);

                //Log.Info("[Local SQL Server]");
                //ExeciseRepository(typeof(Sql.Dapper.Repository), things, "LocalSqlConnection", "Local");

                Log.Info("[Azure SQL, P1");
                ExeciseRepository(typeof(Sql.Dapper.Repository), things, "AzureSqlConnection", "Azure_SQL_P1");

                //Log.Info("[Azure Storage BLOB]");
                //ExeciseRepository(typeof(AzureStorageBlob.Repository), things);

                //Log.Info("[Azure Storage Table]");
                //ExeciseRepository(typeof(AzureStorageTable.Repository), things);

                Log.Info("[Azure DocumentDB, 10k RU]");
                ExeciseRepository(typeof(DocumentDB.Repository), things, null, "10kRU");
            }
        }

        private static void ExeciseRepository(Type repositoryType, IEnumerable<IThing> things, string connectionName = null, string typeNameAppend = null)
        {
            bool success;

            // Create new things
            using (var repository = GetRepository(repositoryType, connectionName))
            {
                success = CreateThings(things, repository, typeNameAppend).Result;
            }

            // Get all things
            IEnumerable<IThing> freshThings;
            using (var repository = GetRepository(repositoryType, connectionName))
            {
                freshThings = GetAllThings(repository, typeNameAppend).Result.ToList();
            }

            // Get each thing
            using (var repository = GetRepository(repositoryType, connectionName))
            {
                success = GetThings(freshThings, repository, typeNameAppend).Result;
            }

            // Delete each thing
            using (var repository = GetRepository(repositoryType, connectionName))
            {
                success = DeleteThings(freshThings, repository, typeNameAppend).Result;
            }
        }

        private static async Task<ICollection<IThing>> GetAllThings(IRepository repository, string typeNameAppend)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var returnedThings = await repository.GetAsync();
            stopWatch.Stop();

            LogResult(TestAction.GetAll, stopWatch.ElapsedMilliseconds, repository.GetType(), returnedThings.Count(), typeNameAppend);

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

        private static async Task<bool> GetThings(IEnumerable<IThing> things, IRepository repository, string typeNameAppend)
        {
            IThing[] foundThings = new IThing[0];
            var stopWatch = new Stopwatch();

            // For SQL repository use ThingId.ToString() instead of Id.
            if (repository.GetType() == typeof(Sql.Dapper.Repository))
            {
                stopWatch.Start();
                foundThings = await repository.GetAsync(things.Select(t => t.ThingId.ToString()));
            }
            else
            {
                stopWatch.Start();
                foundThings = await repository.GetAsync(things.Select(t => t.Id));
            }

            stopWatch.Stop();

            LogResult(TestAction.Get, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), typeNameAppend);

            return foundThings.Length == things.Count();
        }

        private static void LogResult(TestAction action, long milliseconds, Type repositoryType, int thingCount, string typeNameAppend = null)
        {
            var typeName = repositoryType.ToString();
            if (!String.IsNullOrEmpty(typeNameAppend)) { typeName = $"{typeName}.{typeNameAppend}"; }
            var result = new Result()
            {
                Action = action.ToString(),
                Milliseconds = milliseconds,
                RepositoryType = typeName,
                ThingCount = thingCount
            };
            Log.Info(result.ToString());
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

        private static double GetStandardDeviation(List<long> values)
        {
            var mean = values.Average();
            List<double> deviations = new List<double>();
            values.ForEach(v =>
            {
                deviations.Add(Math.Pow((v - mean), Convert.ToDouble(2)));
            });
            return Math.Sqrt(deviations.Average());
        }

        private static List<Result> ReadData(string filePath)
        {
            var list = new List<Result>();
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("{")) { continue; }
                    list.Add(JsonConvert.DeserializeObject<Result>(line));
                }
            }
            return list;
        }

        private static void OutputProcessedData(IEnumerable<Result> results, string directoryPath)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Repository,Create,Create SD,GetAll,GetAll SD,Get,Get SD,Delete,Delete SD");
            var groups = results.GroupBy(r => r.RepositoryType).ToArray();
            var iterations = groups.First().Count() / 4;
            foreach(var group in groups)
            {
                List<long> createTimes = new List<long>();
                List<long> deleteTimes = new List<long>();
                List<long> getAllTimes = new List<long>();
                List<long> getTimes = new List<long>();
                foreach(var result in group)
                {
                    if (result.Action == TestAction.Create.ToString())
                    {
                        createTimes.Add(result.Milliseconds);
                    }
                    else if (result.Action == TestAction.Delete.ToString())
                    {
                        deleteTimes.Add(result.Milliseconds);
                    }
                    else if (result.Action == TestAction.Get.ToString())
                    {
                        getTimes.Add(result.Milliseconds);
                    }
                    else if (result.Action == TestAction.GetAll.ToString())
                    {
                        getAllTimes.Add(result.Milliseconds);
                    }
                }

                var createSD = GetStandardDeviation(createTimes);
                var deleteSD = GetStandardDeviation(deleteTimes);
                var getAllSD = GetStandardDeviation(getAllTimes);
                var getSD = GetStandardDeviation(getTimes);

                stringBuilder.AppendFormat(
                                "{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                                group.Key,
                                createTimes.Average(),
                                createSD,
                                getAllTimes.Average(),
                                getAllSD,
                                getTimes.Average(),
                                getSD,
                                deleteTimes.Average(),
                                deleteSD)
                            .AppendLine();
            }

            using (var writer = new StreamWriter(Path.Combine(directoryPath, $"Repository_Compare_{results.First().ThingCount}_objects_{iterations}_iterations.csv")))
            {
                writer.WriteLine(stringBuilder.ToString());
            }
        }

        #endregion

        #region Clean-up

        private static void CleanDocumentDB()
        {
            IRepository repository = new DocumentDB.Repository();
            var things = repository.GetAsync().Result;
            var success = DeleteThings(things, repository, null).Result;
        }

        #endregion

        #region Plumbing

        private static readonly ILog Log = log4net.LogManager.GetLogger(typeof(Program));

        static Program()
        {
            XmlConfigurator.Configure();
        }

        #endregion
    }
}
