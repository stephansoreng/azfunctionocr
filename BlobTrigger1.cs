using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Company.Function
{
    public static class BlobTrigger1
    {
        [FunctionName("BlobTrigger1")]
        public static async Task Run([BlobTrigger("container1/{name}", Connection = "simpleocrstorage_STORAGE")]Stream myBlob, string name, ILogger log,
        [CosmosDB(databaseName: "db2", collectionName: "Container2",
            ConnectionStringSetting = "CosmosDbConnectionString"
            )]IAsyncCollector<dynamic> documentsOut)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            if (!string.IsNullOrEmpty(name))
            {
                var response = await ExtractText(myBlob, log);
                Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(response);
                var myText = string.Empty;
                foreach(var reg in myDeserializedClass.regions){
                    foreach(var line in reg.lines){
                        foreach(var word in line.words){
                            myText = myText + " " + word.text;
                        }
                    }
                }
                // Add a JSON document to the output container.
                await documentsOut.AddAsync(new
                {
                    // create a random ID
                    id = System.Guid.NewGuid().ToString(),
                    date = DateTime.Now,
                    text = myText
                });
            }

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

           // return new OkObjectResult(responseMessage);
        }

        private static async Task<string> ExtractText(Stream quoteImage, ILogger log)
        {
            string APIResponse = string.Empty;
            var APIKEY = Environment.GetEnvironmentVariable("OcrApiKey");
            var endpoint = Environment.GetEnvironmentVariable("OcrEndpoint");
            //string Endpoint = "https://<serviceurl>/vision/v2.0/ocr";
        
            HttpClient client = new HttpClient();
        
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", APIKEY);
        
            endpoint = endpoint + "?language=unk&detectOrientation=true";
        
            byte[] imgArray = ConvertStreamToByteArray(quoteImage);
        
            HttpResponseMessage response;
        
            try
            {   
                using(ByteArrayContent content = new ByteArrayContent(imgArray))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        
                    response = await client.PostAsync(endpoint, content);
        
                    APIResponse = await response.Content.ReadAsStringAsync();
                    //TODO: Perform check on the response and act accordingly.
                }
            }
            catch(Exception)
            {
                log.LogError("Error occured");
            }
        
            return APIResponse;
        }

        private static byte[] ConvertStreamToByteArray(Stream input)
        {
            byte[] buffer = new byte[16*1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
        public class Word
        {
            public string boundingBox { get; set; }
            public string text { get; set; }
        }

        public class Line
        {
            public string boundingBox { get; set; }
            public List<Word> words { get; set; }
        }

        public class Region
        {
            public string boundingBox { get; set; }
            public List<Line> lines { get; set; }
        }

        public class Root
        {
            public string language { get; set; }
            public double textAngle { get; set; }
            public string orientation { get; set; }
            public List<Region> regions { get; set; }
        }
    }
}
