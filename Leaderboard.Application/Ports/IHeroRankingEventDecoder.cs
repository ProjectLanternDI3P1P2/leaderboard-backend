using Leaderboard.Domain.Entities;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Application.Ports;

public interface IHeroRankingEventDecoder
{
    IReadOnlyList<HeroRankingChange> Decode(RawEvent rawEvent);
}
