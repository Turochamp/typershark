using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Threading.Tasks;
using Xunit;
using TypeShark2.Server.Services;
using TypeShark2.Shared.Services;
using TypeShark2.Server.Hubs;
using Moq;
using TypeShark2.Shared.Dtos;
using System.Collections.Generic;
using System.Linq;

namespace TyperShark2.IntegrationTests
{
    // Based upon https://lurumad.github.io/integration-tests-in-aspnet-core-signalr
    public class GameHubTest
    {
        [Fact]
        public async Task player_added_after_join_game()
        {
            // Assemble
            var gameEngineMock = new Mock<IGameEngine>();
            gameEngineMock.Setup(f => f.CreateGame()).Returns(new GameState());
            var gamesService = new GamesService(gameEngineMock.Object);

            HubConnection connection = 
                CreateTestGameHubConnection(gameEngineMock.Object, gamesService);

            // Act
            var testGameId = 1;
            var testGame = new GameDto()
            {
                Id = testGameId,
                Players = new List<PlayerDto>(),
                Sharks = new List<SharkDto>()
            };
            var testPlayerName = "test";
            var testPlayer = new PlayerDto() { Name = testPlayerName };

            List<PlayerDto> actualPlayers = new List<PlayerDto>();
            connection.On<List<PlayerDto>>("PlayerAdded", players =>
            {
                actualPlayers = players;
            });

            gamesService.Create(testGame);
            await connection.StartAsync();
            await connection.InvokeAsync("JoinGame", testGameId, testPlayer);

            // Assert
            actualPlayers.Any(p => p.Name == testPlayerName).ShouldBeTrue();
        }

        private static HubConnection CreateTestGameHubConnection(IGameEngine gameEngine, GamesService gamesService)
        {
            TestServer server = null;
            var webHostBuilder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSignalR(o =>
                    {
                        o.EnableDetailedErrors = true;
                    });
                    services.AddSingleton<IGamesService, GamesService>(
                        f => gamesService);
                    services.AddSingleton(
                        f => gameEngine);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHub<GameHub>("/gamehub");
                    });
                });

            server = new TestServer(webHostBuilder);
            var connection = new HubConnectionBuilder()
                .WithUrl(
                    "http://localhost/gamehub",
                    o => o.HttpMessageHandlerFactory = _ => server.CreateHandler())
                .Build();
            return connection;
        }
    }
}