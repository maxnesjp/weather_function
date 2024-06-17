using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
class CosmosDbHandler
{
    private static readonly string EndpointUri = Environment.GetEnvironmentVariable("COSMOSD_DB_URI");
    private static readonly string PrimaryKey = Environment.GetEnvironmentVariable("COSMOS_DB_PR_KEY");

    // The Cosmos client instance
    private CosmosClient cosmosClient;
    // The database we will create
    private Database database;
    // The container we will create.
    private Container container;

    // The name of the database and container we will create
    private string databaseId = "weatherdb";
    private string containerId = "customer";

    private static Customer updatedCustomer;
    public CosmosDbHandler()
    {
        Console.WriteLine("Initializing the session...\n");
        this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = "WeatherFunction" });
        updatedCustomer = new Customer();
    }

    public async Task Initiate()
    {
        await this.CreateDatabaseAsync();
        await this.CreateContainerAsync();
    }

    public async Task<List<Customer>> GetAllCustomers()
    {
        return await this.GetCustomers();
    }


    private async Task CreateDatabaseAsync()
    {
        // Create a new database
        this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
        Console.WriteLine("Created Database: {0}\n", this.database.Id);
    }

    private async Task CreateContainerAsync()
    {
        // Create a new container
        this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, "/City");
        Console.WriteLine("Created Container: {0}\n", this.container.Id);
    }



    public async Task<Customer> RegisterCustomer(string email, string city)
    {
        Customer existingCustomer = await this.GetCustomerByEmail(email);

        if (existingCustomer != null)
        {
            existingCustomer.City = city;
            var updatedCustomerResponse = await this.container.ReplaceItemAsync(existingCustomer, existingCustomer.Id);
            Console.WriteLine("Updated Customer: [{0},{1}].\n \tBody is now: {2}\n", existingCustomer.Email, existingCustomer.City, updatedCustomerResponse.Resource);
            return updatedCustomerResponse.Resource;
        }
        else
        {
            // If customer does not exist, create a new one
            var newCustomer = new Customer
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                City = city
            };

            ItemResponse<Customer> newCustomerResponse = await this.container.CreateItemAsync(newCustomer);
            Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", newCustomerResponse.Resource.Id, newCustomerResponse.RequestCharge);
            return newCustomerResponse.Resource;
        }
    }


    private async Task<List<Customer>> GetCustomers()
    {
        var query = new QueryDefinition(query: "SELECT * FROM customer p");

        using FeedIterator<Customer> feed = container.GetItemQueryIterator<Customer>(
            queryDefinition: query
        );
        List<Customer> customersOfCity = new List<Customer>();
        double requestCharge = 0d;
        while (feed.HasMoreResults)
        {
            FeedResponse<Customer> response = await feed.ReadNextAsync();
            foreach (Customer c in response)
            {
                customersOfCity.Add(c);
            }
            requestCharge += response.RequestCharge;
        }
        return customersOfCity;
    }

    private async Task<Customer> GetCustomerByEmail(string email)
    {
        IOrderedQueryable<Customer> queryable = container.GetItemLinqQueryable<Customer>();
        var matches = queryable.Where(p => p.Email == email);
        using FeedIterator<Customer> linqFeed = matches.ToFeedIterator();
        if (linqFeed.HasMoreResults)
        {
            FeedResponse<Customer> response = await linqFeed.ReadNextAsync();
            Customer firstCustomer = response.FirstOrDefault();
            return firstCustomer;
        }
        return null;
    }
}