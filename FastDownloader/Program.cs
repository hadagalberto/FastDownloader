using Newtonsoft.Json;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Search;
using static SpotifyAPI.Web.Scopes;

namespace FastDownloader
{
    internal class Program
    {
        private static YoutubeClient youtube = new YoutubeClient();
        private static EmbedIOAuthServer _server;
        private const string _clientId = "31407b8b381c4e0ebf7b8fafb03e2615";
        private const string CredentialsPath = "credentials.json";
        private static SpotifyClient spotify;
        private static bool _spotifyEnabled = false;

        private static void Exiting() => Console.CursorVisible = true;

        static async Task Main(string[] args)
        {
            Console.Title = "Fast Downloader";

            if (File.Exists(CredentialsPath))
            {
                await Start();
            }
            else
            {
                AppDomain.CurrentDomain.ProcessExit += (sender, e) => Exiting();
                Console.WriteLine("Deseja logar-se no spotify? (S/N)");
                var option = Console.ReadLine();
                if (option != null)
                {
                    {
                        if (option.ToLower() == "s")
                        {
                            await SpotifyAuthentication();
                        }
                        else
                        {
                            Console.WriteLine("O Spotify será desativado...");
                            Console.WriteLine("--------------------------------");
                            _spotifyEnabled = true;
                        }
                    }
                }
            }

            while (!_spotifyEnabled){}
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => Exiting();
            Console.WriteLine("Coloque o caminho que deseja salvar as musicas");
            string path = Console.ReadLine()!;

            while (true)
            {
                Console.WriteLine("Coloque o link da playlist ou da música");
                string url = Console.ReadLine()!;

                switch (ParseCommand(url))
                {
                    case Tipo.Playlist:
                        await ProcessPlaylist(url, path);
                        break;

                    case Tipo.Musica:
                        await ProcessMusic(url, path);
                        break;

                    case Tipo.Spotify:
                        await ProcessSpotify(url, path);
                        break;

                    case Tipo.Busca:
                        await ProcessSearch(url, path);
                        break;

                    default:
                        Console.WriteLine("Link inválido");
                        break;
                }
            }

        }

        private static async Task BaixarMusica(string title, string path, string url)
        {
            try
            {
                Console.WriteLine($"Baixando música {title}");

                path = path.Replace("|", "");
                path = path.Replace("?", "");
                path = path.Replace("\"", "");
                path = path.Replace("<", "");
                path = path.Replace(">", "");
                path = path.Replace("*", "");

                await youtube.Videos.DownloadAsync(url, $"{path}", down => down
                    .SetContainer("mp3")
                .SetPreset(ConversionPreset.UltraFast));

                Console.WriteLine($"Baixado música {title}");

                Console.WriteLine("--------------------------------");

            }
            catch(YoutubeExplode.Exceptions.VideoUnavailableException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Música protegida por direitos autorais... Não é possível baixar!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Erro ao baixar música {title}");
                Console.Write(ex.ToString());
                Console.ResetColor();
            }
        }

        private static string GetTitle(string title, string author)
        {
            if (title.Contains("-") || title.ToLower().Contains("mashup") || string.IsNullOrEmpty(author))
                return title;

            return $"{author} - {title}";
        }

        private static async Task ProcessPlaylist(string url, string path)
        {
            var playlist = await youtube.Playlists.GetAsync(url!);

            var playlistTitle = playlist.Title;

            Console.WriteLine($"Buscando vídeos da playlist {playlistTitle}");
            Console.WriteLine("-----------------------");

            Directory.CreateDirectory(Path.Combine(path, playlistTitle));

            var videos = await youtube.Playlists.GetVideosAsync(url!);

            foreach (var video in videos)
            {
                var downloadPath = Path.Combine(path!, playlistTitle, $"{GetTitle(video.Title, video.Author.ChannelTitle ?? string.Empty)}.mp3");
                await BaixarMusica(video.Title.Replace("\\", "").Replace("/", ""), downloadPath, video.Url);
            }
        }

        private static async Task ProcessMusic(string url, string path)
        {
            var video = await youtube.Videos.GetAsync(url);

            Directory.CreateDirectory(Path.Combine(path, "Variadas"));

            var downloadPath = Path.Combine(path!, "Variadas", $"{GetTitle(video.Title, video.Author.ChannelTitle ?? string.Empty)}.mp3");

            await BaixarMusica(video.Title.Replace("\\", "").Replace("/", ""), downloadPath, url);
        }

        private static async Task ProcessSpotify(string url, string path)
        {
            if (spotify is null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("O Spotify está desativado, para ativá-lo, reinicie o app e faça login...");
                Console.ResetColor();
                return;
            }

            var id = GetSpotifyId(url);

            var playlist = await spotify.Playlists.Get(id);

            if (playlist is null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Playlist não encontrada");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"Buscando músicas da playlist {playlist.Name}");
            Console.WriteLine("-----------------------");

            Directory.CreateDirectory(Path.Combine(path, playlist.Name));

            foreach (var item in playlist!.Tracks!.Items!)
            {
                if (item.Track is FullTrack track)
                {
                    var result = await youtube.Search.GetVideosAsync(track.Artists.FirstOrDefault()!.Name + " - " + track.Name).CollectAsync(1);
                    var video = result.FirstOrDefault();

                    if (video is null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Música {track.Name} não encontrada");
                        Console.ResetColor();
                        continue;
                    }

                    var downloadPath = Path.Combine(path, playlist.Name, $"{GetTitle(track.Name, video.Author.ChannelTitle ?? string.Empty)}.mp3");

                    await BaixarMusica(track.Name, downloadPath, video.Url);
                }
            }
        }

        private static async Task ProcessSearch(string search, string path)
        {
            try
            {
                Console.Clear();
                Console.WriteLine(" - O que gostaria de buscar? - ");
                Console.WriteLine("M - Músicas");
                Console.WriteLine("P - Playlists");
                Console.WriteLine("Outro rsultado - Qualquer tipo");

                var option = Console.ReadLine();

                int value = -1;

                IEnumerable<ISearchResult> results;

                Console.Clear();

                Console.WriteLine("Buscando resultados...");

                switch (option)
                {
                    case "m":
                    case "M":
                        results = await youtube.Search.GetVideosAsync(search).CollectAsync(10);
                        break;
                    case "p":
                    case "P":
                        results = await youtube.Search.GetPlaylistsAsync(search).CollectAsync(10);
                        break;
                    default:
                        results = await youtube.Search.GetResultsAsync(search).CollectAsync(10);
                        break;
                }

                Console.Clear();

                Console.WriteLine("Resultados encontrados:");
                Console.WriteLine("--------------------------------");
                foreach (var result in results.Select((value, index) => new { value, index }))
                {
                    switch (result.value)
                    {
                        case VideoSearchResult video:
                            Console.WriteLine($"{result.index + 1} - Música encontrada: {video.Title} - {video.Duration}");
                            break;
                        case PlaylistSearchResult playlist:
                            Console.WriteLine($"{result.index + 1} - Playlist encontrada: {playlist.Title}");
                            break;
                    }
                }

                Console.WriteLine("--------------------------------");
                Console.WriteLine("Escolha uma opção:");
                option = Console.ReadLine();

                value = -1;
                if (!int.TryParse(option, out value))
                {
                    Console.WriteLine("Opção inválida");
                    return;
                }

                var element = results.ElementAtOrDefault(value + 1);

                if (element == null)
                {
                    Console.WriteLine("Opção inexistente");
                    return;
                }

                switch (element)
                {
                    case VideoSearchResult video:
                        await ProcessMusic(video.Url, path);
                        break;
                    case PlaylistSearchResult playlist:
                        await ProcessPlaylist(playlist.Url, path);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

        }

        private static Tipo ParseCommand(string command)
        {
            if (command.Contains("?list") || command.Contains("&list"))
                return Tipo.Playlist;

            if (command.Contains("spotify"))
                return Tipo.Spotify;

            if (!command.Contains("http"))
                return Tipo.Busca;

            return Tipo.Musica;
        }

        private static string GetSpotifyId(string url)
        {
            var startIndex = url.LastIndexOf("/");
            int endIndex = url.LastIndexOf("?") - 1 - url.LastIndexOf("/");

            return url.Substring(startIndex + 1, endIndex);
        }

        private static async Task Start()
        {
            var json = await File.ReadAllTextAsync(CredentialsPath);
            var token = JsonConvert.DeserializeObject<PKCETokenResponse>(json);

            var authenticator = new PKCEAuthenticator(_clientId, token!);
            authenticator.TokenRefreshed += (sender, token) => File.WriteAllText(CredentialsPath, JsonConvert.SerializeObject(token));

            var config = SpotifyClientConfig.CreateDefault()
              .WithAuthenticator(authenticator);

            spotify = new SpotifyClient(config);

            var me = await spotify.UserProfile.Current();
            Console.WriteLine($"Bem vindo {me.DisplayName} ({me.Id}), você foi autenticado no Spotify!");

            var playlists = await spotify.PaginateAll(await spotify.Playlists.CurrentUsers().ConfigureAwait(false));
            Console.WriteLine($"Playlists na sua conta: {playlists.Count}");

            _spotifyEnabled = true;
        }

        private static async Task SpotifyAuthentication()
        {
            _server ??= new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);

            var (verifier, challenge) = PKCEUtil.GenerateCodes();

            await _server.Start();
            _server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await _server.Stop();
                PKCETokenResponse token = await new OAuthClient().RequestToken(
                  new PKCETokenRequest(_clientId, response.Code, _server.BaseUri, verifier)
                );

                await File.WriteAllTextAsync(CredentialsPath, JsonConvert.SerializeObject(token));
                await Start();
            };

            var request = new LoginRequest(_server.BaseUri, _clientId, LoginRequest.ResponseType.Code)
            {
                CodeChallenge = challenge,
                CodeChallengeMethod = "S256",
                Scope = new List<string> { UserReadEmail, UserReadPrivate, PlaylistReadPrivate, PlaylistReadCollaborative }
            };

            Uri uri = request.ToUri();
            try
            {
                BrowserUtil.Open(uri);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to open URL, manually open: {0}", uri);
            }
        }
    }
}