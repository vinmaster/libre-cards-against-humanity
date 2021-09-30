using LibreCards.Core;
using LibreCards.Core.Entities;
using Moq;
using Xunit;

namespace LibreCards.Tests.Gameplay;

public class GameTests
{
    private readonly Mock<ICardState> _cardStateMock;
    private readonly Mock<IGameStatus> _gameStatusMock;
    private readonly Mock<ILobby> _lobbyMock;
    private readonly Mock<IJudgePicker> _judgePickerMock;

    private readonly IGame _game;

    private readonly Player LobbyOwner = new(Guid.NewGuid());
    private readonly Player JudgePlayer = new(Guid.NewGuid());

    public GameTests()
    {
        _cardStateMock = new Mock<ICardState>();
        _gameStatusMock = new Mock<IGameStatus>();
        _lobbyMock = new Mock<ILobby>();
        _judgePickerMock = new Mock<IJudgePicker>();

        _game = new Game(_gameStatusMock.Object, _cardStateMock.Object, _lobbyMock.Object, _judgePickerMock.Object);
    }

    [Fact]
    public void JudgePlayerId_ShouldReturnFromJudgePicker()
    {
        var expected = Guid.NewGuid();
        _judgePickerMock.Setup(j => j.CurrentJudgeId).Returns(expected);

        Assert.Equal(expected, _game.JudgePlayerId);
    }

    [Fact]
    public void TemplateCard_ShouldReturnFromCardState()
    {
        var expected = "<BLANK>";
        _cardStateMock.Setup(c => c.CurrentTemplateCard).Returns(new Template(expected));

        Assert.Equal(expected, _game.TemplateCard.Content);
    }

    [Fact]
    public void GameState_ShouldReturnFromGameStatus()
    {
        var expected = GameState.Judging;
        _gameStatusMock.Setup(s => s.CurrentState).Returns(expected);

        Assert.Equal(expected, _game.GameState);
    }

    [Fact]
    public void StartGame_NotEnoughPlayers_ShouldThrow()
    {
        ArrangeReadyToStartGame();
        _lobbyMock.Setup(l => l.HasEnoughPlayers).Returns(false);

        Assert.Throws<InvalidOperationException>(() => _game.StartGame(LobbyOwner.Id));
    }

    [Theory]
    [InlineData(GameState.Playing)]
    [InlineData(GameState.Judging)]
    public void StartGame_InProgress_ShouldThrow(GameState state)
    {
        ArrangeReadyToStartGame();
        _gameStatusMock.Setup(s => s.CurrentState).Returns(state);

        Assert.Throws<InvalidOperationException>(() => _game.StartGame(LobbyOwner.Id));
    }

    [Fact]
    public void StartGame_PlayersShouldGetCards()
    {
        ArrangeReadyToStartGame();

        _game.StartGame(LobbyOwner.Id);

        _cardStateMock.Verify(s => s.RefillPlayerCards(It.IsAny<IReadOnlyCollection<Player>>()), Times.Once());
    }

    [Fact]
    public void StartGame_ShouldPresentTemplateCard()
    {
        ArrangeReadyToStartGame();

        _game.StartGame(LobbyOwner.Id);

        _cardStateMock.Verify(s => s.DrawTemplateCard(), Times.Once());
    }

    [Fact]
    public void StartGame_ShouldSetStatusToPlaying()
    {
        ArrangeReadyToStartGame();

        _game.StartGame(LobbyOwner.Id);

        _gameStatusMock.Verify(s => s.CurrentState, Times.AtLeastOnce());
        _gameStatusMock.Verify(s => s.SwitchToPlaying(), Times.Once());
        _gameStatusMock.VerifyNoOtherCalls();
    }

    [Fact]
    public void StartGame_ShouldPickNewJudge()
    {
        ArrangeReadyToStartGame();

        _game.StartGame(LobbyOwner.Id);

        _judgePickerMock.Verify(j => j.PickNewJudge(It.IsAny<IReadOnlyCollection<Player>>()), Times.Once());
    }

    [Fact]
    public void StartGame_NotLobbyOwner_ShouldThrow()
    {
        ArrangeReadyToStartGame();

        Assert.Throws<InvalidOperationException>(() => _game.StartGame(Guid.NewGuid()));
    }

    [Fact]
    public void PlayCards_NoCardIds_ShouldThrow()
    {
        ArrangeStartedGame();

        Assert.Throws<ArgumentException>(() => _game.PlayCards(LobbyOwner.Id, Array.Empty<int>()));
    }

    [Fact]
    public void PlayCards_NotPlaying_ShouldThrow()
    {
        const int cardId = 1;
        LobbyOwner.Cards.Add(new Card { Id = cardId });
        ArrangeReadyToStartGame();

        Assert.Throws<InvalidOperationException>(() => _game.PlayCards(LobbyOwner.Id, new[] { cardId }));
    }

    [Fact]
    public void PlayCards_JudgePlay_ShouldThrow()
    {
        const int cardId = 1;
        JudgePlayer.Cards.Add(new Card { Id = cardId });
        ArrangeStartedGame();

        Assert.Throws<InvalidOperationException>(() => _game.PlayCards(JudgePlayer.Id, new[] { cardId }));
    }

    [Fact]
    public void PlayCards_UnknownCardId_ShouldThrow()
    {
        const int cardId = 3;
        ArrangeStartedGame();

        Assert.Throws<InvalidOperationException>(() => _game.PlayCards(LobbyOwner.Id, new[] { cardId }));
    }

    [Fact]
    public void PlayCards_MoreCardsThanInHand_ShouldThrow()
    {
        const int cardId = 3;
        LobbyOwner.Cards.Add(new Card { Id = cardId });
        ArrangeStartedGame();

        Assert.Throws<InvalidOperationException>(() => _game.PlayCards(LobbyOwner.Id, new[] { cardId, cardId }));
    }

    private void ArrangeStartedGame()
    {
        _gameStatusMock.Setup(s => s.CurrentState).Returns(GameState.Playing);
        _lobbyMock.Setup(l => l.Players).Returns(new[] { LobbyOwner, JudgePlayer, new Player(Guid.NewGuid()) });
        _judgePickerMock.Setup(j => j.CurrentJudgeId).Returns(JudgePlayer.Id);
    }

    private void ArrangeReadyToStartGame()
    {
        _gameStatusMock.Setup(s => s.CurrentState).Returns(GameState.Waiting);
        _lobbyMock.Setup(l => l.HasEnoughPlayers).Returns(true);
        _lobbyMock.Setup(l => l.OwnerId).Returns(LobbyOwner.Id);
        _lobbyMock.Setup(l => l.Players).Returns(new[] { new Player(LobbyOwner.Id), JudgePlayer, new Player(Guid.NewGuid()) });
    }
}
