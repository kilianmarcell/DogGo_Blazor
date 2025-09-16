using System.Text.Json.Serialization;

namespace doggo.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
        
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        
        [JsonPropertyName("password_confirmation")]
        public string PasswordConfirmation { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public User User { get; set; } = new User();
    }

    public class Rating
    {
        public int Id { get; set; }
        public int Stars { get; set; }
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("location_id")]
        public int LocationId { get; set; }
        
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }
    }

    public class Location
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("lat")]
        public double Latitude { get; set; }
        
        [JsonPropertyName("lng")]
        public double Longitude { get; set; }
        
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("average_rating")]
        public double? AverageRating { get; set; }
        
        // Count of ratings for this location (calculated client-side)
        public int RatingCount { get; set; }
        
        [JsonPropertyName("allowed")]
        public bool IsAllowed { get; set; } = true;
        
        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }
    }

    public class LocationRating
    {
        public int Id { get; set; }
        
        [JsonPropertyName("location_id")]
        public int LocationId { get; set; }
        
        public string Username { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}