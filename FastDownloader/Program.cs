using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;
using System.IO;
using YoutubeExplode.Videos;
using YoutubeExplode.Playlists;

namespace FastDownloader
{
    internal class Program
    {
        private static YoutubeClient youtube = new YoutubeClient();

        static async Task Main(string[] args)
        {
            Console.Title = "Fast Downloader";

            Console.WriteLine("Coloque o caminho que deseja salvar as musicas");
            string path = Console.ReadLine()!;

            while (true)
            {
                Console.WriteLine("Coloque o link da playlist ou da música");
                string url = Console.ReadLine()!;

                if (!url.Contains("?list") && !url.Contains("&list"))
                {
                    var video = await youtube.Videos.GetAsync(url);

                    Directory.CreateDirectory(Path.Combine(path, "Variadas"));

                    var downloadPath = Path.Combine(path!, "Variadas", $"{GetTitle(video.Title, video.Author.ChannelTitle ?? string.Empty)}.mp3");

                    await BaixarMusica(video.Title, downloadPath, url);

                    continue;
                }
                    
                var playlist = await youtube.Playlists.GetAsync(url!);

                var playlistTitle = playlist.Title;

                Console.WriteLine($"Buscando vídeos da playlist {playlistTitle}");
                Console.WriteLine("-----------------------");

                Directory.CreateDirectory(Path.Combine(path, playlistTitle));

                var videos = await youtube.Playlists.GetVideosAsync(url!);

                foreach(var video in videos)
                {
                    var downloadPath = Path.Combine(path!, playlistTitle, $"{GetTitle(video.Title, video.Author.ChannelTitle ?? string.Empty)}.mp3");
                    await BaixarMusica(video.Title, downloadPath, video.Url);
                }
            }
        }

        private static async Task BaixarMusica(string title, string path, string url)
        {
            try
            {
                Console.WriteLine($"Baixando música {title}");
                

                await youtube.Videos.DownloadAsync(url, $"{path}", down => down
                    .SetContainer("mp3")
                .SetPreset(ConversionPreset.UltraFast));

                Console.WriteLine($"Baixado música {title}");

                Console.WriteLine("-----------------------");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao baixar música {title}");
            }
        }

        private static string GetTitle(string title, string author)
        {
            if (title.Contains("-") || title.ToLower().Contains("mashup") || string.IsNullOrEmpty(author))
                return title;

            return $"{author} - {title}";
        }
    }
}