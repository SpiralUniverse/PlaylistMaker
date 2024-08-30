using System.Text;
using System.Text.RegularExpressions;

namespace TxtToM3U
{
    class Program
    {
        public static int notFound = 0;
        public static int errors = 0;
        public static int improperlyFormatted = 0;
        static void Main(string[] args)
        {
            List<string> inputLines = new List<string>();
            string inputLine;

            Console.WriteLine("Enter the song details (Press Enter twice to finish):");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            while (!string.IsNullOrWhiteSpace(inputLine = Console.ReadLine()))
            {
                inputLines.Add(inputLine);
            }

            Console.Clear();
            string inputText = string.Join("\n", inputLines);

            ProcessInput(inputText);
            Console.WriteLine($"Number of warnings (per song) = {notFound} | Number of errors = {errors} | Number of Improperly formatted {improperlyFormatted}");
            Console.ReadKey();
        }

        static void ProcessInput(string inputText)
        {
            var songs = ParseInputText(inputText);

            string playlistDirectory = "GeneratedM3U";

            if (!Directory.Exists(playlistDirectory))
            {
                Directory.CreateDirectory(playlistDirectory);
            }

            // Add songs to M3U files
            AddSongsToM3UFiles(songs, playlistDirectory);
        }

        static List<Song> ParseInputText(string inputText)
        {
            var songs = new List<Song>();
            var lines = inputText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains("Name:") && line.Contains("Artist:") && line.Contains("Playlists:"))
                {
                    try
                    {

                        int nameStart = line.IndexOf("Name:") + "Name:".Length;
                        int artistStart = line.IndexOf("Artist:");
                        int playlistStart = line.IndexOf("Playlists:");

                        // Extract substrings for name, artist, and playlists
                        string name = line.Substring(nameStart, artistStart - nameStart).Trim().Trim(',');
                        string artist = line.Substring(artistStart + "Artist:".Length, playlistStart - artistStart - "Artist:".Length).Trim().Trim(',');
                        string playlistsString = line.Substring(playlistStart + "Playlists:".Length).Trim();

                        // Split playlists by comma and trim each one
                        var playlists = playlistsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(p => p.Trim())
                                                    .ToList();

                        songs.Add(new Song(name, artist, playlists));
                    }
                    catch (Exception ex)
                    {
                        PrintError($"Error parsing line: {line}. Exception: {ex.Message}");
                    }
                }
                else
                {
                    PrintWarning($"Skipping line that does not contain all required fields: {line}");
                }
            }

            return songs;
        }

        static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        static void AddSongsToM3UFiles(List<Song> songs, string songDirectory)
        {
            string originalM3UFile = "all.m3u";
            var allSongsInOriginalM3U = LoadOriginalM3UFile(originalM3UFile);

            // Use a dictionary to ensure unique playlist names
            var playlistsToSongs = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var song in songs)
            {
                string originalPath = FindSongInOriginalM3U(allSongsInOriginalM3U, song);

                if (originalPath != null)
                {
                    string entry = $"#EXTINF:0, - {song.Name}_{song.Artist}\n{originalPath}";

                    foreach (var playlist in song.Playlists)
                    {
                        if (!playlistsToSongs.ContainsKey(playlist))
                        {
                            playlistsToSongs[playlist] = new HashSet<string>();
                        }

                        playlistsToSongs[playlist].Add(entry);
                    }
                }
                else
                {
                    notFound++;
                    PrintWarning($"Warning: Song '{song.Name}_{song.Artist}' not found in the original M3U file.");
                }
            }

            foreach (var playlist in playlistsToSongs)
            {
                string sanitizedPlaylistName = SanitizeFileName(playlist.Key);
                string filePath = Path.Combine(songDirectory, $"{sanitizedPlaylistName}.m3u");

                try
                {
                    // Use StreamWriter with append mode
                    using (StreamWriter sw = new StreamWriter(filePath, append: !File.Exists(filePath)))
                    {
                        if (!File.Exists(filePath))
                        {
                            sw.WriteLine("#EXTM3U");
                        }

                        foreach (var entry in playlist.Value)
                        {
                            sw.WriteLine(entry);
                        }
                    }
                }
                catch (IOException ioEx)
                {
                    PrintError($"Error writing to file '{filePath}': {ioEx.Message}");
                }
            }
        }


        static string CleanSongName(string songName)
        {
            // Remove any leading/trailing whitespace, and normalize characters
            return songName.Trim().Normalize(NormalizationForm.FormC);
        }

        // Function to load songs and their paths from the original M3U file
        static Dictionary<string, string> LoadOriginalM3UFile(string originalM3UPath)
        {
            var songDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var lines = File.ReadAllLines(originalM3UPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("#EXTINF:"))
                    {
                        // Extract the song name after the known prefix
                        string prefix = "#EXTINF:0, - ";
                        string songName = CleanSongName(lines[i].Substring(prefix.Length).Trim());

                        if (i + 1 < lines.Length && !lines[i + 1].StartsWith("#"))
                        {
                            string filePath = lines[i + 1].Trim();
                            songDictionary[songName] = filePath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error reading original M3U file '{originalM3UPath}': {ex.Message}");
            }

            return songDictionary;
        }

        // Function to find the song in the original M3U file dictionary
        static string FindSongInOriginalM3U(Dictionary<string, string> originalSongs, Song song)
        {
            string key;
            if(song.Artist.ToLower() != "unknown")
                key = CleanSongName($"{song.Name}_{song.Artist}");
            else
            {
                key = CleanSongName(song.Name);
                PrintWarning($"Found {song.Name} has Aritist {song.Artist}");
            }
            originalSongs.TryGetValue(key, out string songPath);
            if(songPath == null)
            {
                //debuging purposes tobe removed
                return null;
            }
            return songPath;
        }

        static void PrintError(string message)
        {
            errors++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        static void PrintWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
