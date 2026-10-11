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
    }
}
