namespace Billing.Models
{
    public class User
    {
        public string Name { get; set; }
        public long Rating { get; set; }
        public UserProfile Profile { get; set; }

        public User(string name, long rating, UserProfile profile)
        {
            Name = name;
            Rating = rating;
            Profile = profile;
        }
    }
}
