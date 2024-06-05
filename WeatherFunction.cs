using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace metrics
{
    public static class WeatherFunction
    {
        [FunctionName("weatherFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string city = req.Query["city"];
            string API_KEY = Environment.GetEnvironmentVariable("API_KEY");
            if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(API_KEY))
            {
                return new BadRequestObjectResult("City or API key is invalid");
            }

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.weatherapi.com/v1/current.json?key={API_KEY}&q={city}&aqi=no");
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                log.LogError($"Weather API request failed with status code: {response.StatusCode}");
                return new StatusCodeResult((int)response.StatusCode);
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseBody);

            return new JsonResult(json);
        }
    }
}
