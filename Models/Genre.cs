namespace MovieApi.Models
{
    public class Genre
    {
        public int GenreId { get; set; } // TMDb genre id
        public string Name { get; set; } = null!;
        public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    }
}
