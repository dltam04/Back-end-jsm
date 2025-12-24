using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieApi.Models
{
    [Table("links")]
    public class MovieLink
    {
        [Key]
        [Column("movieId")]
        public int MovieId { get; set; }      // MovieLens id

        [Column("imdbId")]
        public int? ImdbId { get; set; }

        [Column("tmdbId")]
        public int? TmdbId { get; set; }
    }
}
