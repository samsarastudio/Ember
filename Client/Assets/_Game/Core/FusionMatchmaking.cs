using System;

namespace Emberwake.Core
{
    [Serializable] public sealed class FusionQueueReply
    {
        public string status, matchId, room, region, ticket, opponent, error;
        public int team;
        public Catalog catalog;
    }
    // Written by the allocator in its private data directory, never served over HTTP.
    [Serializable] public sealed class FusionAssignment
    {
        public string matchId, room, region;
        public string[] players, names, tickets, deckA, deckB;
        public Catalog catalog;
        public int seed;
        public bool aiOpponent;
    }
    [Serializable] public sealed class FusionOutcome
    {
        public string matchId;
        public int winner, tick, hash;
    }
    public static class TrophyMatching
    {
        public static int Range(double waitedSeconds) => Math.Min(600, 100 + 50 * (int)(Math.Max(0, waitedSeconds) / 5));
        public static bool Compatible(int a, int b, double waitA, double waitB) =>
            Math.Abs((long)a-b) <= Math.Min(Range(waitA), Range(waitB));
    }
}
