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
            Console.WriteLine($"Customer found: {existingCustomer.Email}, updating city to: {city}");
            existingCustomer.City = city;
            try
            {
                var updatedCustomerResponse = await this.container.ReplaceItemAsync(existingCustomer, existingCustomer.Id);
                Console.WriteLine($"Updated Customer: [{existingCustomer.Email}, {existingCustomer.City}]. Body is now: {updatedCustomerResponse.Resource}");
                return updatedCustomerResponse.Resource;
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"Failed to update customer. CosmosException: {ex}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error occurred while updating customer: {ex}");
                throw;
            }
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
            try
            {
                ItemResponse<Customer> newCustomerResponse = await this.container.CreateItemAsync(newCustomer);
                Console.WriteLine($"Created item in database with id: {newCustomerResponse.Resource.Id}. Operation consumed {newCustomerResponse.RequestCharge} RUs.\n");
                return newCustomerResponse.Resource;
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"Failed to create new customer. CosmosException: {ex}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error occurred while creating new customer: {ex}");
                throw;
            }
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
        try
        {
            IOrderedQueryable<Customer> queryable = container.GetItemLinqQueryable<Customer>();
            var matches = queryable.Where(p => p.Email == email);
            using FeedIterator<Customer> linqFeed = matches.ToFeedIterator();

            while (linqFeed.HasMoreResults)
            {
                FeedResponse<Customer> response = await linqFeed.ReadNextAsync();
                Customer firstCustomer = response.FirstOrDefault();
                if (firstCustomer != null)
                {
                    return firstCustomer;
                }
            }
            return null; // No matching customer found
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            Console.WriteLine($"Customer with email {email} not found. CosmosException: {ex}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while fetching the customer: {ex}");
            throw;
        }
    }

}