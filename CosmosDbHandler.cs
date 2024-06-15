using System;
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

    private static string CustomerEmail;
    private static string CustomerCity;

    public static async Task CosmodDbIns(string email, string city)
    {
        try
        {
            Console.WriteLine("Beginning operations...\n");
            CustomerEmail = email;
            CustomerCity = city;
            CosmosDbHandler dbHandler = new CosmosDbHandler();
            await dbHandler.RegisterCustomer();
        }
        catch (CosmosException de)
        {
            Exception baseException = de.GetBaseException();
            Console.WriteLine("{0} error occurred: {1}", de.StatusCode, de);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error: {0}", e);
        }
    }

    private async Task RegisterCustomer()
    {
        // Create a new instance of the Cosmos Client
        this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = "WeatherFunction" });
        await this.CreateDatabaseAsync();
        await this.CreateContainerAsync();
        await this.AddUpdateItemToContainerAsync();

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
        this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, "/partitionKey");
        Console.WriteLine("Created Container: {0}\n", this.container.Id);
    }

    private async Task AddUpdateItemToContainerAsync()
    {
        Customer customerToInsert = new Customer
        {
            Email = CustomerEmail,
            City = CustomerCity
        };
        string existingCustomerId = this.GetCustomerIdByEmail().ToString();
        if (existingCustomerId != "")
        {
            ItemResponse<Customer> customerResponse = await this.container.ReplaceItemAsync<Customer>(customerToInsert, existingCustomerId);
            Console.WriteLine("Updated Customer: [{0},{1}].\n \tBody is now: {2}\n", customerToInsert.Email, customerToInsert.City, customerResponse.Resource);
        }
        else
        {
            ItemResponse<Customer> customerResponse = await this.container.CreateItemAsync<Customer>(customerToInsert);
            Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", customerResponse.Resource.Id, customerResponse.RequestCharge);
        }
    }

    private async Task<string> GetCustomerIdByEmail()
    {
        IOrderedQueryable<Customer> queryable = container.GetItemLinqQueryable<Customer>();
        var matches = queryable.Where(p => p.Email == CustomerEmail);
        using FeedIterator<Customer> linqFeed = matches.ToFeedIterator();
        if (linqFeed.HasMoreResults)
        {
            FeedResponse<Customer> response = await linqFeed.ReadNextAsync();
            return response.FirstOrDefault().Id;
        }
        return "";
    }

    private async Task DeleteFamilyItemAsync()
    {
    }
}