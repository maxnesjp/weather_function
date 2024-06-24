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
using RestSharp;
using System.Collections.Generic;

namespace metrics
{
    public static class WeatherFunction
    {
        [FunctionName("weatherFunction")]
        public static async Task<IActionResult> GetWeatherByCity(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Processing a request to get weather in a provided city.");

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

        [FunctionName("RegisterCustomer")]
        public static async Task<IActionResult> RegisterCustomer(
         [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
         ILogger log)
        {
            log.LogInformation("Processing a request to register a customer.");
            if (req.Method != "POST") return null;
            string email = req.Query["email"];
            string city = req.Query["city"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            email = email ?? data?.email;
            city = city ?? data?.city;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(city))
            {
                return new BadRequestObjectResult("Email or City is empty");
            }

            try
            {
                var dbsession = new CosmosDbHandler();
                await dbsession.Initiate();
                Customer addedCustomer = await dbsession.RegisterCustomer(email, city);

                if (addedCustomer != null)
                {
                    return new OkObjectResult(addedCustomer);
                }
                else
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [FunctionName("GetCustomerEmails")]
        public static async Task<IActionResult> GetCustomerEmails(
                    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
                    ILogger log)
        {
            try
            {
                var dbsession = new CosmosDbHandler();
                await dbsession.Initiate();
                List<Customer> customers = await dbsession.GetAllCustomers();
                if (customers != null || customers.Count < 1)
                {
                    return new OkObjectResult(customers);
                }
                else
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
