using System;
using System.Collections.Generic;
using System.Text;


sealed class Version1 : ChessAbstract
{
    protected override string Name { get; }
    protected override string Version { get; }
    public Version1(bool side)
    {
        this.Side = side;
        Name = "Random Valid Move";
        Version = "1";
    }
    public override int GetMove(int[] board, double timeLimit)
    {
        return 0;  // Search.GetRandomMove();
    }

    static void Main(string[] args) {
        /*
        Board b = new();
        Random random = new();
        for (int i = 0; i < 10; i++) {
            List<Move> moves = b.PseudoLegalMoves();
            Move move = moves[random.Next(moves.Count)];
            Console.WriteLine(b.ANFromMove(move));
            b.DoMove(move);
            Console.WriteLine(b);
        }
        */
        // const int depth = 4;
        // PerformanceTest perft = new(depth, new());
        // Dictionary<string, int[]> perftResults = perft.PerfTest();
        // foreach (string metric in perftResults.Keys) {
        //     Console.WriteLine(metric + ": [" +
        //         string.Join(",", perftResults[metric].Select(p=>p.ToString()).ToArray()) + "]");
        // }
    }
}