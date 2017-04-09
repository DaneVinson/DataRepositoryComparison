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
using Microsoft.Azure;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                //ExerciseRepositories();
                OutputProcessedData(ReadData(@"C:\temp\temp\DataRepositoryComparison"), @"C:\temp\temp");
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

        #region Data Gathering

        private static bool CreateThings(IEnumerable<IThing> things, IRepository repository, string name)
        {
            List<IThing> createThings = things.ToList();

            // Azure Storage Table requires its own IThing implementation.
            if (repository.GetType() == typeof(AzureStorageTable.Repository))
            {
                createThings.Clear();
                foreach (var thing in things)
                {
                    createThings.Add(new ThingEntity(thing));
                }
            }

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var success = repository.Create(createThings);
            stopWatch.Stop();

            LogResult(TestAction.Create, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), name);

            return success;
        }

        private static async Task<bool> CreateThingsAsync(IEnumerable<IThing> things, IRepository repository, string name)
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

            LogResult(TestAction.CreateAsync, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), name);

            return success;
        }

        private static bool DeleteThings(IEnumerable<IThing> things, IRepository repository, string name)
        {
            var success = false;
            var stopWatch = new Stopwatch();

            // For SQL repository use ThingId.ToString() instead of Id.
            if (repository.GetType() == typeof(Sql.Dapper.Repository))
            {
                stopWatch.Start();
                success = repository.Delete(things.Select(t => t.ThingId.ToString()));
            }
            else
            {
                stopWatch.Start();
                success = repository.Delete(things.Select(t => t.Id));
            }

            stopWatch.Stop();

            LogResult(TestAction.Delete, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), name);

            return success;
        }

        private static async Task<bool> DeleteThingsAsync(IEnumerable<IThing> things, IRepository repository, string name)
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

            LogResult(TestAction.DeleteAsync, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), name);

            return success;
        }

        private static void ExerciseRepositories()
        {
            int thingCount = Convert.ToInt32(CloudConfigurationManager.GetSetting("ThingCount"));
            int iterations = Convert.ToInt32(CloudConfigurationManager.GetSetting("Iterations"));
            Log.Info($"[ThingCount: {thingCount}, Iterations: {iterations}]");

            // Evaluate the execution plan.
            Dictionary<string, string[]> instanceNamesByType = new Dictionary<string, string[]>();
            string executionPlan = CloudConfigurationManager.GetSetting("ExecutionPlan");
            foreach (var repositoryPlan in executionPlan.Split('|'))
            {
                var data = repositoryPlan.Split(':');
                instanceNamesByType.Add(data[0], data[1].Split(','));
            }

            // Execute the plan.
            for (int i = 0; i < iterations; i++)
            {
                IEnumerable<IThing> asyncThings = NewThings(thingCount);
                IEnumerable<IThing> things = NewThings(thingCount);

                foreach (var repositoryType in instanceNamesByType.Keys)
                {
                    foreach (var repositoryInstanceName in instanceNamesByType[repositoryType])
                    {
                        var logMessage = new StringBuilder(repositoryType);
                        if (!String.IsNullOrWhiteSpace(repositoryInstanceName)) { logMessage.Append($", {repositoryInstanceName}"); }
                        Log.Info($"[{logMessage.ToString()}]");
                        ExeciseRepository(repositoryType, things, repositoryInstanceName);
                        Log.Info($"[{logMessage.ToString()} async]");
                        ExeciseRepositoryAsync(repositoryType, asyncThings, repositoryInstanceName).Wait();
                    }
                }
            }
        }

        private static void ExeciseRepository(
            string repositoryType,
            IEnumerable<IThing> things,
            string repositoryInstanceName = null)
        {
            bool success;

            // Create new things
            using (var repository = GetRepository(repositoryType, repositoryInstanceName))
            {
                success = CreateThings(things, repository, repositoryInstanceName);
            }

            // Get all things
            IEnumerable<IThing> freshThings;
            using (var repository = GetRepository(repositoryType, repositoryInstanceName))
            {
                freshThings = GetAllThings(repository, repositoryInstanceName);
            }

            // Get each thing
            using (var repository = GetRepository(repositoryType, repositoryInstanceName))
            {
                success = GetThings(freshThings, repository, repositoryInstanceName);
            }

            // Delete each thing
            using (var repository = GetRepository(repositoryType, repositoryInstanceName))
            {
                success = DeleteThings(freshThings, repository, repositoryInstanceName);
            }
        }

        private static async Task ExeciseRepositoryAsync(
            string repositoryType, 
            IEnumerable<IThing> things, 
            string repositoryInstanceName = null)
        {
            bool success;

            // Create new things
            using (var repository = GetRepository(repositoryType, repositoryInstanceName))
            {
                success = await CreateThingsAsync(things, repository, repositoryInstanceName);
            }

            // Get all things
            IEnumerable<IThing> freshThings;
            using (var repository = GetRepository(repositoryType, repositoryInstanceName))
            {
                freshThings = (await GetAllThingsAsync(repository, repositoryInstanceName)).ToList();
            }

            // Get each thing
            using (var repository = GetRepository(repositoryType, repositoryInstanceName))
            {
                success = await GetThingsAsync(freshThings, repository, repositoryInstanceName);
            }

            // Delete each thing
            using (var repository = GetRepository(repositoryType, repositoryInstanceName))
            {
                success = await DeleteThingsAsync(freshThings, repository, repositoryInstanceName);
            }
        }

        private static ICollection<IThing> GetAllThings(IRepository repository, string name)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var returnedThings = repository.Get();
            stopWatch.Stop();

            LogResult(TestAction.GetAll, stopWatch.ElapsedMilliseconds, repository.GetType(), returnedThings.Count(), name);

            return returnedThings;
        }

        private static async Task<ICollection<IThing>> GetAllThingsAsync(IRepository repository, string name)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var returnedThings = await repository.GetAsync();
            stopWatch.Stop();

            LogResult(TestAction.GetAllAsync, stopWatch.ElapsedMilliseconds, repository.GetType(), returnedThings.Count(), name);

            return returnedThings;
        }

        private static IRepository GetRepository(string repositoryType, string name = null)
        {
            if (repositoryType == typeof(AzureStorageBlob.Repository).ToString())
            {
                return new AzureStorageBlob.Repository();
            }
            else if (repositoryType == typeof(AzureStorageTable.Repository).ToString())
            {
                return new AzureStorageTable.Repository();
            }
            else if (repositoryType == typeof(DocumentDB.Repository).ToString())
            {
                return new DocumentDB.Repository(name);
            }
            else if (repositoryType == typeof(Sql.Dapper.Repository).ToString())
            {
                return new Sql.Dapper.Repository(name);
            }
            else { throw new ArgumentException($"{repositoryType} is an unexpected implementation of {nameof(IRepository)}"); }
        }

        private static bool GetThings(IEnumerable<IThing> things, IRepository repository, string name)
        {
            IThing[] foundThings = new IThing[0];
            var stopWatch = new Stopwatch();

            // For SQL repository use ThingId.ToString() instead of Id.
            if (repository.GetType() == typeof(Sql.Dapper.Repository))
            {
                stopWatch.Start();
                foundThings = repository.Get(things.Select(t => t.ThingId.ToString()));
            }
            else
            {
                stopWatch.Start();
                foundThings = repository.Get(things.Select(t => t.Id));
            }

            stopWatch.Stop();

            LogResult(TestAction.Get, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), name);

            return foundThings.Length == things.Count();
        }

        private static async Task<bool> GetThingsAsync(IEnumerable<IThing> things, IRepository repository, string name)
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

            LogResult(TestAction.GetAsync, stopWatch.ElapsedMilliseconds, repository.GetType(), things.Count(), name);

            return foundThings.Length == things.Count();
        }

        private static void LogResult(
            TestAction action, 
            long milliseconds, 
            Type repositoryType, 
            int thingCount, 
            string name = null)
        {
            var typeName = repositoryType.ToString();
            if (!String.IsNullOrEmpty(name)) { typeName = $"{typeName}.{name}"; }
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

        private static List<Result> ReadData(string fileOrDirectoryPath)
        {
            List<string> filePaths = new List<string>();
            if (File.Exists(fileOrDirectoryPath)) { filePaths.Add(fileOrDirectoryPath); }
            else { filePaths.AddRange(Directory.GetFiles(fileOrDirectoryPath, "*.log")); }

            var list = new List<Result>();
            foreach (var filePath in filePaths)
            {
                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!line.StartsWith("{")) { continue; }
                        list.Add(JsonConvert.DeserializeObject<Result>(line));
                    }
                }
            }
            return list;
        }

        private static void OutputProcessedData(IEnumerable<Result> results, string directoryPath)
        {
            // Create the header line.
            var lineData = new List<string>() { "Repository" };
            foreach (TestAction action in Enum.GetValues(typeof(TestAction)))
            {
                lineData.Add(action.ToString());
                lineData.Add($"{action.ToString()}-SD");
            }
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(String.Join(",", lineData));

            // Group Results by repository type.
            var repositoryGrouping = results.GroupBy(r => r.RepositoryType).ToArray();
            var iterations = repositoryGrouping.First().Count() / Enum.GetNames(typeof(TestAction)).Count();

            foreach (var repository in repositoryGrouping)
            {
                // Initialize counters for the current repository type.
                var timesByAction = new Dictionary<TestAction, List<long>>();
                var sdByAction = new Dictionary<TestAction, double>();
                foreach (TestAction action in Enum.GetValues(typeof(TestAction)))
                {
                    timesByAction.Add(action, new List<long>());
                    sdByAction.Add(action, 0);
                }

                // Accumulate times for all TestActions for the current repository type.
                foreach (var result in repository)
                {
                    TestAction action = (TestAction)Enum.Parse(typeof(TestAction), result.Action);
                    timesByAction[action].Add(result.Milliseconds);
                }

                // Calculate standard deviations for each action.
                foreach (var action in timesByAction.Keys)
                {
                    sdByAction[action] = GetStandardDeviation(timesByAction[action]);
                }

                // Create the line of text for the current repositories data.
                lineData = new List<string>() { repository.Key };
                foreach (var action in timesByAction.Keys)
                {
                    lineData.Add(timesByAction[action].Average().ToString());
                    lineData.Add(sdByAction[action].ToString());
                }
                stringBuilder.AppendLine(String.Join(",", lineData));
            }

            // Write to the output file.
            using (var writer = new StreamWriter(Path.Combine(directoryPath, $"Repository_Compare_{results.First().ThingCount}_IThing_{iterations}_iterations.csv"), false))
            {
                writer.WriteLine(stringBuilder.ToString());
            }
        }

        #endregion

        #region Clean-up

        private static void CleanDocumentDB(string name)
        {
            IRepository repository = new DocumentDB.Repository(name);
            var things = repository.GetAsync().Result;
            var success = DeleteThingsAsync(things, repository, null).Result;
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
