namespace MovieApi.Models
{
    public class Person
    {
        public int PersonId { get; set; } // TMDb person id
        public string Name { get; set; } = null!;
        public string? Biography { get; set; }
        public string? ProfilePath { get; set; }
        public DateTime? Birthday { get; set; }
        public string? PlaceOfBirth { get; set; }

        public ICollection<MovieCast> MovieCredits { get; set; } = new List<MovieCast>();
    }
}
