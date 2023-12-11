using System.Net;
using System.Collections.Generic;


class PerformanceTest {
    int[] nodes;
    int[] captures;
    int[] epCaptures;
    int[] castles;
    int[] promotions;
    int[] checks;
    int[] checkMates;
    int[] gameEnds;
    readonly Board b;
    int Depth {get; set;}
    public PerformanceTest(int depth, Board board) {
        b = board;
        nodes = new int[Depth];
        captures = new int[Depth];
        epCaptures = new int[Depth]; // en passant captures
        castles = new int[Depth];
        promotions = new int[Depth];
        checks = new int[Depth];
        checkMates = new int[Depth];
        gameEnds = new int[Depth];
        Depth = depth;
    }

    public PerformanceTest(int depth) : this(depth, new()) {}

    private void PerfTestRec(int i) {
        List<Move> moves = b.PseudoLegalMoves();
        if (moves.Count == 0) {
            Board.Status s = b.GameStatus();
            switch (s) {
                case Board.Status.WinByCheckmate:
                    checkMates[i-1]++;
                    gameEnds[i-1]++;
                    break;
                case Board.Status.DrawByInsufficientMaterial:
                case Board.Status.DrawByRepitition:
                case Board.Status.DrawBy50MoveRule:
                case Board.Status.DrawByStalemate:
                    gameEnds[i]++;
                    break;
                default:
                    // Console.WriteLine("Invalid Game End error");
                    break;
            }
            return;
        }
        foreach (Move m in moves) {
            nodes[i]++;
            if (b.IntBoard[m.Dest] != 0 || m.IsEnPassant) captures[i]++;
            if (m.IsEnPassant) epCaptures[i]++;
            if (m.IsCastle) castles[i]++;
            if (m.IsPromotion) promotions[i]++;

            if (i+1 < Depth) {
                b.DoMove(m);
                if(b.IsCheck()) checks[i]++;
                PerfTestRec(i+1);
                b.UndoMove();
            }
        }
    }

    public Dictionary<string, int[]> PerfTest() {
        nodes = new int[Depth];
        captures = new int[Depth];
        epCaptures = new int[Depth]; // en passant captures
        castles = new int[Depth];
        promotions = new int[Depth];
        checks = new int[Depth];
        checkMates = new int[Depth];
        gameEnds = new int[Depth];
        PerfTestRec(0);
        return new Dictionary<string, int[]>
        {
            {"Nodes", nodes},
            {"Captures", captures},
            {"En Passant Captures", epCaptures},
            {"castles", castles},
            {"promotions", promotions},
            {"checks", checks},
            {"checkMates", checkMates},
            {"gameEnds", gameEnds}
        };
    }
}
