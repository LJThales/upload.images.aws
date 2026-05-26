using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;

namespace Upload.Images.AWS
{
    class Program
    {
        private class FileToUpload
        {
            public string Source { get; set; }
            public bool IsUrl { get; set; }
            public string Name { get; set; }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Upload.Images.AWS ===");
            
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            string awsKey = config["AWS:Key"];
            string awsSecret = config["AWS:Secret"];
            string bucket = config["AWS:Bucket"];
            string baseUrl = config["AWS:BaseUrl"];

            if (string.IsNullOrEmpty(awsKey))
            {
                Console.Write("AWS Key: ");
                awsKey = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(awsSecret))
            {
                Console.Write("AWS Secret: ");
                awsSecret = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(bucket))
            {
                Console.Write("AWS Bucket: ");
                bucket = Console.ReadLine();
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                Console.Write("AWS Base URL (ex: https://s3.us-east-2.amazonaws.com): ");
                baseUrl = Console.ReadLine();
            }

            List<FileToUpload> files = new List<FileToUpload>();

            while (true)
            {
                Console.WriteLine("\nDeseja adicionar um arquivo? (s/n)");
                string resp = Console.ReadLine()?.ToLower();
                if (resp != "s")
                    break;

                Console.WriteLine("Tipo de origem (1 - URL, 2 - Arquivo Local):");
                string tipo = Console.ReadLine();
                
                string source = "";
                bool isUrl = tipo == "1";
                if (isUrl)
                {
                    Console.Write("Digite a URL: ");
                    source = Console.ReadLine();
                }
                else
                {
                    Console.Write("Digite o caminho do arquivo local: ");
                    source = Console.ReadLine();
                }

                Console.Write("Digite o nome final do arquivo no S3 (ex: pasta/arquivo.ext): ");
                string name = Console.ReadLine();

                files.Add(new FileToUpload { Source = source, IsUrl = isUrl, Name = name });
            }

            Console.WriteLine($"\nIniciando upload de {files.Count} arquivos...");

            using var httpClient = new HttpClient();
            using var client = new AmazonS3Client(awsKey, awsSecret, RegionEndpoint.USEast2);
            var fileTransferUtility = new TransferUtility(client);

            foreach (var file in files)
            {
                Console.WriteLine($"Fazendo upload de: {file.Name}");
                try
                {
                    using var memoryStream = new MemoryStream();
                    
                    if (file.IsUrl)
                    {
                        using var responseStream = await httpClient.GetStreamAsync(file.Source);
                        await responseStream.CopyToAsync(memoryStream);
                    }
                    else
                    {
                        using var fileStream = File.OpenRead(file.Source);
                        await fileStream.CopyToAsync(memoryStream);
                    }

                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        InputStream = memoryStream,
                        Key = file.Name,
                        BucketName = bucket,
                        CannedACL = S3CannedACL.PublicRead,
                        ServerSideEncryptionCustomerMethod = ServerSideEncryptionCustomerMethod.None,
                        StorageClass = S3StorageClass.OneZoneInfrequentAccess
                    };

                    await fileTransferUtility.UploadAsync(uploadRequest);
                    
                    string cleanBaseUrl = baseUrl?.TrimEnd('/');
                    string fileUrl = $"{cleanBaseUrl}/{bucket}/{file.Name}";
                    Console.WriteLine($"[Sucesso] {fileUrl}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Erro] Falha ao enviar {file.Name}: {ex.Message}");
                }
            }

            Console.WriteLine("\nProcesso finalizado!");
        }
    }
}
