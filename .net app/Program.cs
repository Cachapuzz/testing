using System;
using System.Diagnostics;
using Elastic.Apm;
using Elastic.Apm.Api;
using Elastic.Apm.NetCoreAll;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Security;
using Elastic.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using ApiKey = Elastic.Transport.ApiKey;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
using Elasticsearch.Net;
using Nest;

namespace ElasticAgent.NETCore
{
    public class DocumentModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
    
    public class Program
    {
        // Host builder method that sets up the Startup class
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();  // Use the Startup class to configure the application
                });
        
        //Reads the appsettings.json file and configures the connection with the elastic APM
        public class Startup
        {
            private readonly IConfiguration _configuration;
            public Startup()
            {
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("C:\\Users\\david\\RiderProjects\\ElasticAgent.NetCore\\appsettings.json")
                    .Build();
            
                Console.WriteLine("Startup done");
            }
            
            //Automatically called by the library
            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                Console.WriteLine("Configure started");
                app.UseAllElasticApm(_configuration);
            }
        }
        
        //Simulation of a working application
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            await host.StartAsync();
            
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("C:\\Users\\david\\RiderProjects\\ElasticAgent.NetCore\\appsettings.json")
                .Build();
            
            //Authentication and Instantiation of a client on Elasticsearch (Basic Authentication should be enough(?))
            var settings1 = new ElasticsearchClientSettings(new Uri("http://192.168.2.16:9200"))
                //.Authentication(new BasicAuthentication("elastic", "password"))
                //.Authentication(new ApiKey("Vzdyd2o1SUJTeXYxNm01SGwzV1A6b2VwZUVGeWVTNU9UeTR3R0htUjl5Zw=="))
                .Authentication(new ApiKey("YUFRcjg1SUJxYXVaUHpGd0tVWWs6Wndxd3JWek1SVS1YaTJzS0FCZWVDQQ=="))
                .DefaultIndex("apm_test");
            
            var client = new ElasticsearchClient(settings1);
            
            //Creating a new index
            var response = await client.Indices.CreateAsync("pleaseWorkIndex");
            
            if (response.IsValidResponse){
                Console.WriteLine("Index created with success");
            }
            else{
                Console.WriteLine("Index creation failed: "+ response.DebugInformation);
                return;
            }
            
            var document = new DocumentModel
            {
                Id = 1,
                Name = "Something something name",
                Description = "DescriptionDescriptionDescriptionDescriptionDescription."
            };
            
            // Index the document
            var indexResponse = await client.IndexAsync<DocumentModel>(document);

            if (indexResponse.IsValidResponse)
            {
                Console.WriteLine("Document indexed successfully!");
            }
            else
            {
                Console.WriteLine("Failed to index document: " + indexResponse.DebugInformation);
            }
            
            for(int i = 0; i < 30; i++)
            {
                // Start the Process time measurement for current process
                var process = Process.GetCurrentProcess();
                var startCpuUsage = process.TotalProcessorTime;
                var stopwatch = Stopwatch.StartNew();

                Transactions transactions = new Transactions();
                
                await transactions.StartTransaction().ConfigureAwait(false);
                await transactions.CaptureTransaction().ConfigureAwait(false);
                await transactions.DeleteTransaction().ConfigureAwait(false);

                stopwatch.Stop();
                var endCpuUsage = process.TotalProcessorTime;

                // Calculate CPU usage
                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = stopwatch.Elapsed.TotalMilliseconds;
                var cpuUsageTotal = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;

                Console.WriteLine($"Total CPU Usage: {cpuUsageTotal}%");

                await Task.Delay(5000);
            }
            
            Console.ReadKey();
        }

        
        public class Transactions
        {
            public async Task<Task> StartTransaction()
            {
                var startTransaction = Agent.Tracer.StartTransaction("Transaction","Insert");

                try
                {
                    Operation operation = new Operation();
                    await operation.Insert();
                }
                
                catch (Exception e)
                {
                    startTransaction.CaptureException(e);

                }
                finally
                {
                    startTransaction.End();
                }
                return Task.CompletedTask;
            }

            public Task<Task> CaptureTransaction()
            {
                var captureTransaction = Agent.Tracer.CaptureTransaction("CaptureTransaction","Update", func: async () =>
                {
                    await Operation.Update(1);
                });

                return Task.FromResult(Task.CompletedTask);
            }

            public Task<Task> DeleteTransaction()
            {
                Agent.Tracer.CaptureTransaction("DeleteTransaction", "Delete", async () =>
                {
                    await Operation.Delete(2);
                });

                return Task.FromResult(Task.CompletedTask);
            }
        }

        public class Operation
        {
            public async Task<Task> Insert()
            {
                await Task.Delay(1);

                Console.WriteLine("Inserted Operation");

                return Task.CompletedTask;
            }

            public static async Task<int> Update(int opid)
            {
                return await Agent.Tracer.CurrentTransaction.CaptureSpan("Operation", "Update", func:async () =>
                {
                    await Task.Delay(1);

                    Console.WriteLine($"Updated Operation with ID {opid}");

                    return opid;
                });
            }

            public static async Task<Task> Delete(int opid)
            {
                await Task.Delay(1);

                Console.WriteLine($"Operation {opid} deleted! (Intentional Error)");

                throw new Exception("error");
            }

            public static async Task<Task> GetOperations()
            {
                await Task.Delay(1);

                Console.WriteLine("Everythiiiiiiiing in its right place");

                return Task.CompletedTask;
            }
        }
    }
}
