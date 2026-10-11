namespace Afterpelago.Domain
{
    public class Game
    {
        /// <summary>
        /// Primary Key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the Game
        /// </summary>
        public string Name { get; set; } = string.Empty;
        public int Revision { get; set; }
        public string? CoverHash { get; set; }
        public int? CoverWidth { get; set; }
        public int? CoverHeight { get; set; }
    }
}
