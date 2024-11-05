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
using Elastic.Clients.Elasticsearch.Nodes;


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
            
            string url = "https://192.168.2.16:9200";  // Replace with your actual Elasticsearch URL
            string apiKey = "N2ZTYjZaSUJxYXVaUHpGd1BXNDQ6TDRLUEVHdS1UX2lJdDcxaGJ0QmpFQQ==";
            //string apiKey = "Vzdyd2o1SUJTeXYxNm01SGwzV1A6b2VwZUVGeWVTNU9UeTR3R0htUjl5Zw==";
            string indexName = "um_docs";
            

            var mySettings = new ElasticsearchClientSettings(new Uri(url))
                .ServerCertificateValidationCallback(Elastic.Transport.CertificateValidations.AllowAll)
                //.CertificateFingerprint("<FINGERPRINT>")
                .Authentication(new Elastic.Transport.ApiKey(apiKey))
                .EnableTcpKeepAlive(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))
                .RequestTimeout(TimeSpan.FromSeconds(60))
                .EnableDebugMode()
                .MaximumRetries(5);

            var client = new ElasticsearchClient(mySettings);
            
            //Fetch info
            var response1 = await client.InfoAsync();
            if (!response1.IsValidResponse)
            {
                Console.WriteLine($"Error: {response1.DebugInformation}");
            }
            
            //heck for successful authentiaction
            var response2 = await client.Indices.ExistsAsync(indexName);
            Console.WriteLine(response2.ToString());
            if (response2.Exists)
            {
                Console.WriteLine("Exists");
            }
            else
            {
                Console.WriteLine("Creating Index");
                var response3 = await client.Indices.CreateAsync(indexName);
                Console.WriteLine(response1.ToString());
            }
        
            var document = new DocumentModel
            {
                Id = 3,
                Name = "Third name",
                Description = "Yet another another description."
            };
            
            //Create new index
            var responseInsert = await client.IndexAsync(document, i => i
                    .Index(indexName)
                    .Id(document.Id)
            );


            Console.WriteLine(responseInsert.ToString());
        

            for(int i = 0; i < 30; i++)
            {
                // Start the Process time measurement for current process
                var process = System. Diagnostics.Process.GetCurrentProcess();
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
