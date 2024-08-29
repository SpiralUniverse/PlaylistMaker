public class Song
{
    public string Name { get; set; }
    public string Artist { get; set; }
    public List<string> Playlists { get; set; }

    public Song(string name, string artist, List<string> playlists)
    {
        Name = name;
        Artist = artist;
        Playlists = playlists;
    }
}
