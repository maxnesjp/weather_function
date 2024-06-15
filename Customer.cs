using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

class Customer
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }
    [Required]
    public string Email { get; set; }
    [Required]
    public string City { get; set; }
    public DateTime ModifiedDate { get; set; } = DateTime.Now;

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }

}