using Debug = UnityEngine.Debug;

namespace V6
{
    public static class PrecomputeMoves
    {
        public static ulong[] KnightMoves;
        public static ulong[] KingMoves;
        public static void ComputeMoveTables() 
        {
            ComputeKingTable();
            ComputeKnightTable();
        } 

        static void ComputeKnightTable() 
        {
            KnightMoves = new ulong[64];
            for (int pos = 0; pos < 64; pos++) {
                ulong board = 0UL;
                int x = pos & 0b111;
                int y = pos >> 3;
                if (x > 0)
                {
                    if (y > 1) board |= 1UL << pos - 17;
                    if (y < 6) board |= 1UL << pos + 15;
                    if (x > 1) 
                    { 
                        if (y > 0) board |= 1UL << pos - 10;
                        if (y < 7) board |= 1UL << pos + 6;
                    }
                }
                if (x < 7)
                {
                    if (y > 1) board |= 1UL << pos - 15;
                    if (y < 6) board |= 1UL << pos + 17;
                    if (x < 6)
                    {
                        if (y > 0) board |= 1UL << pos - 6;
                        if (y < 7) board |= 1UL << pos + 10;
                    }
                }
                KnightMoves[pos] = board;
            }
        }

        static void ComputeKingTable()
        {
            KingMoves = new ulong[64];
            for (int pos = 0; pos < 64; pos++) {
                KingMoves[pos] = (1UL << pos - 9) |
                                 (1UL << pos - 8) |
                                 (1UL << pos - 7) |
                                 (1UL << pos - 1) |
                                 (1UL << pos + 1) |
                                 (1UL << pos + 7) |
                                 (1UL << pos + 8) |
                                 (1UL << pos + 9);
            }
        }
    }
}